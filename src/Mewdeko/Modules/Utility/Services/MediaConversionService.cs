using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service for managing media conversion queue with concurrency control
/// </summary>
public class MediaConversionService : INService
{
    private const int MaxConcurrentConversions = 8;
    private const int AverageConversionTimeSeconds = 30; // Estimate for wait time calculation
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, ConversionRequest> activeConversions;
    private readonly ConcurrentQueue<ConversionRequest> conversionQueue;
    private readonly SemaphoreSlim conversionSemaphore;
    private readonly Timer queueProcessor;

    /// <summary>
    ///     Service for managing media conversion queue with concurrency control
    /// </summary>
    public MediaConversionService()
    {
        conversionSemaphore = new SemaphoreSlim(MaxConcurrentConversions, MaxConcurrentConversions);
        conversionQueue = new ConcurrentQueue<ConversionRequest>();
        activeConversions = new System.Collections.Concurrent.ConcurrentDictionary<Guid, ConversionRequest>();

        // Process queue every 2 seconds
        queueProcessor = new Timer(ProcessQueue, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    /// <summary>
    ///     Enqueues a conversion request and returns queue information
    /// </summary>
    /// <param name="request">The conversion request</param>
    /// <returns>Queue position and estimated wait time</returns>
    public (int queuePosition, TimeSpan estimatedWait) EnqueueConversion(ConversionRequest request)
    {
        conversionQueue.Enqueue(request);

        var queuePosition = conversionQueue.Count;
        var activeCount = activeConversions.Count;

        // Calculate estimated wait time
        var estimatedWait = TimeSpan.FromSeconds(
            Math.Max(0, (queuePosition - 1) * AverageConversionTimeSeconds / MaxConcurrentConversions));

        return (queuePosition, estimatedWait);
    }

    /// <summary>
    ///     Gets current queue statistics
    /// </summary>
    /// <returns>Queue statistics</returns>
    public (int queueLength, int activeConversions) GetQueueStats()
    {
        return (conversionQueue.Count, activeConversions.Count);
    }

    private void ProcessQueue(object state)
    {
        while (conversionQueue.TryDequeue(out var request) && conversionSemaphore.CurrentCount > 0)
        {
            _ = Task.Run(async () => await ProcessConversionAsync(request));
        }
    }

    private async Task ProcessConversionAsync(ConversionRequest request)
    {
        await conversionSemaphore.WaitAsync();

        try
        {
            activeConversions.TryAdd(request.Id, request);

            // Signal that we're starting processing
            request.StartProcessing();

            // Process the conversion
            await ExecuteConversionAsync(request, request.GuildId);
        }
        finally
        {
            activeConversions.TryRemove(request.Id, out _);
            conversionSemaphore.Release();
        }
    }

    private async Task ExecuteConversionAsync(ConversionRequest request, ulong guildId)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "mewdeko_convert", request.Id.ToString());
            Directory.CreateDirectory(tempDir);

            // Generate safe filenames
            var safeInputFilename = $"input_{request.Id:N}.{request.InputExtension}";
            var safeOutputFilename = $"output_{request.Id:N}.{request.OutputExtension}";
            var inputPath = Path.Combine(tempDir, safeInputFilename);
            var outputPath = Path.Combine(tempDir, safeOutputFilename);

            // Download the file
            using var httpClient = new HttpClient();
            var fileData = await httpClient.GetByteArrayAsync(request.FileUrl);
            await File.WriteAllBytesAsync(inputPath, fileData);

            // Run FFmpeg conversion
            var ffmpegArgs = GetFFmpegArgs(inputPath, outputPath, request.OutputExtension);

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();

            var timeout = Task.Delay(TimeSpan.FromMinutes(5));
            var processTask = process.WaitForExitAsync();

            var completedTask = await Task.WhenAny(processTask, timeout);

            if (completedTask == timeout)
            {
                process.Kill();
                request.SetError("CONVERSION_TIMEOUT");
                return;
            }

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                request.SetError($"CONVERSION_FAILED|{error}");
                return;
            }

            if (!File.Exists(outputPath))
            {
                request.SetError("NO_OUTPUT_FILE");
                return;
            }

            var outputInfo = new FileInfo(outputPath);
            if (outputInfo.Length > 25 * 1024 * 1024) // Discord 25MB limit
            {
                request.SetError("FILE_TOO_LARGE");
                return;
            }

            // Read the converted file
            var convertedData = await File.ReadAllBytesAsync(outputPath);
            request.SetSuccess(convertedData);

            // Cleanup
            Directory.Delete(tempDir, true);
        }
        catch (Exception ex)
        {
            request.SetError($"GENERAL_ERROR|{ex.Message}");
        }
    }

    private static string GetFFmpegArgs(string inputPath, string outputPath, string outputExt)
    {
        var baseArgs = $"-i \"{inputPath}\" -y";

        return outputExt switch
        {
            "gif" => $"{baseArgs} -vf \"fps=15,scale=480:-1:flags=lanczos\" -c:v gif \"{outputPath}\"",
            "mp4" => $"{baseArgs} -c:v libx264 -crf 23 -preset medium -c:a aac -b:a 128k \"{outputPath}\"",
            "webm" => $"{baseArgs} -c:v libvpx-vp9 -crf 30 -b:v 0 -c:a libopus \"{outputPath}\"",
            "mp3" => $"{baseArgs} -c:a libmp3lame -b:a 192k \"{outputPath}\"",
            "wav" => $"{baseArgs} -c:a pcm_s16le \"{outputPath}\"",
            "jpg" or "jpeg" =>
                $"{baseArgs} -vf \"scale=1920:-1:force_original_aspect_ratio=decrease\" -q:v 2 \"{outputPath}\"",
            "png" => $"{baseArgs} -vf \"scale=1920:-1:force_original_aspect_ratio=decrease\" \"{outputPath}\"",
            "webp" => $"{baseArgs} -vf \"scale=1920:-1:force_original_aspect_ratio=decrease\" -q:v 80 \"{outputPath}\"",
            _ => $"{baseArgs} \"{outputPath}\""
        };
    }
}

/// <summary>
///     Represents a media conversion request
/// </summary>
public class ConversionRequest
{
    /// <summary>
    ///     Unique identifier for this conversion request
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    ///     URL of the file to be converted
    /// </summary>
    public string FileUrl { get; set; }

    /// <summary>
    ///     File extension of the input file
    /// </summary>
    public string InputExtension { get; set; }

    /// <summary>
    ///     Desired file extension for the output
    /// </summary>
    public string OutputExtension { get; set; }

    /// <summary>
    ///     Original filename from the Discord attachment
    /// </summary>
    public string OriginalFilename { get; set; }

    /// <summary>
    ///     Guild ID for localization purposes
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Task completion source for async result handling
    /// </summary>
    public TaskCompletionSource<ConversionResult> CompletionSource { get; } = new();

    /// <summary>
    ///     Signals that processing has started for this request
    /// </summary>
    public void StartProcessing()
    {
        // This can be used for status updates if needed
    }

    /// <summary>
    ///     Sets the conversion as successful with the converted data
    /// </summary>
    /// <param name="data">The converted file data</param>
    public void SetSuccess(byte[] data)
    {
        CompletionSource.SetResult(new ConversionResult
        {
            Success = true, Data = data
        });
    }

    /// <summary>
    ///     Sets the conversion as failed with an error message
    /// </summary>
    /// <param name="error">The error message</param>
    public void SetError(string error)
    {
        CompletionSource.SetResult(new ConversionResult
        {
            Success = false, Error = error
        });
    }
}

/// <summary>
///     Result of a media conversion operation
/// </summary>
public class ConversionResult
{
    /// <summary>
    ///     Whether the conversion was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     The converted file data (if successful)
    /// </summary>
    public byte[] Data { get; set; }

    /// <summary>
    ///     Error message (if conversion failed)
    /// </summary>
    public string Error { get; set; }
}