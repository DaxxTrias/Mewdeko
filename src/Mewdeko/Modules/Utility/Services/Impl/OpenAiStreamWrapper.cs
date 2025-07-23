using System.ClientModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using OpenAI.Chat;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     Wraps OpenAI's streaming response in a Stream interface.
/// </summary>
public class OpenAiStreamWrapper : Stream
{
    private readonly MemoryStream buffer;
    private readonly AsyncCollectionResult<StreamingChatCompletionUpdate> stream;
    private bool endOfStream;
    private IAsyncEnumerator<StreamingChatCompletionUpdate>? enumerator;
    private readonly StringBuilder streamedContent = new();
    private int? totalTokens = null;
    private bool finalUsageEmitted = false;

    /// <summary>
    ///     Initializes a new instance of the OpenAiStreamWrapper class.
    /// </summary>
    /// <param name="stream">The streaming updates from OpenAI.</param>
    public OpenAiStreamWrapper(AsyncCollectionResult<StreamingChatCompletionUpdate> stream)
    {
        this.stream = stream;
        this.buffer = new MemoryStream();
        this.endOfStream = false;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (endOfStream && this.buffer.Length == 0 && finalUsageEmitted)
            return 0;

        while (this.buffer.Length < count && !endOfStream)
        {
            enumerator ??= stream.GetAsyncEnumerator(cancellationToken);

            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    endOfStream = true;
                    break;
                }

                var update = enumerator.Current;
                if (update.ContentUpdate.Count > 0)
                {
                    var text = update.ContentUpdate[0].Text;
                    streamedContent.Append(text);
                    var data = new
                    {
                        delta = new
                        {
                            text = text
                        },
                        usage = new
                        {
                            total_tokens = 0 // Will be emitted at the end
                        }
                    };

                    var json = JsonSerializer.Serialize(data);
                    var line = $"data: {json}\n\n";
                    var bytes = Encoding.UTF8.GetBytes(line);
                    await this.buffer.WriteAsync(bytes, cancellationToken);
                }

                // Try to get token usage if available (SDK may provide it in update.Usage)
                if (update.Usage is not null)
                {
                    // Use reflection to support any property names (Prompt, Completion, Total)
                    var usageType = update.Usage.GetType();
                    var prompt = (int?)usageType.GetProperty("Prompt")?.GetValue(update.Usage) ?? 0;
                    var completion = (int?)usageType.GetProperty("Completion")?.GetValue(update.Usage) ?? 0;
                    var total = (int?)usageType.GetProperty("Total")?.GetValue(update.Usage);

                    if (total.HasValue && total.Value > 0)
                        totalTokens = total.Value;
                    else if (prompt > 0 || completion > 0)
                        totalTokens = prompt + completion;
                }
            }
            catch
            {
                endOfStream = true;
                break;
            }
        }

        // After the stream ends, emit a final usage chunk if not already emitted
        if (endOfStream && !finalUsageEmitted)
        {
            finalUsageEmitted = true;
            var usageData = new
            {
                delta = new { text = string.Empty },
                usage = new { total_tokens = totalTokens ?? 0 }
            };
            var json = JsonSerializer.Serialize(usageData);
            var line = $"data: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(line);
            await this.buffer.WriteAsync(bytes, cancellationToken);
        }

        this.buffer.Position = 0;
        var bytesRead = await this.buffer.ReadAsync(buffer, offset, count, cancellationToken);

        if (bytesRead < this.buffer.Length)
        {
            var remaining = new byte[this.buffer.Length - bytesRead];
            await this.buffer.ReadAsync(remaining, 0, remaining.Length, cancellationToken);
            this.buffer.SetLength(0);
            await this.buffer.WriteAsync(remaining, 0, remaining.Length, cancellationToken);
        }
        else
        {
            this.buffer.SetLength(0);
        }

        return bytesRead;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Use ReadAsync instead");

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            buffer.Dispose();
            if (enumerator is not null)
            {
                enumerator.DisposeAsync().AsTask().Wait();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
}