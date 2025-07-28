#nullable enable
using System.Net.Http;
using System.Text.Json;
using Mewdeko.Database.Common;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications;

/// <summary>
///     Checks for online and offline stream notifications across multiple streaming platforms.
/// </summary>
public class NotifChecker
{
    // Cached JsonSerializerOptions for performance - this is critical for Redis operations
    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string key;
    private readonly ILogger<NotifChecker> logger;
    private readonly HashSet<(FType, string)> offlineBuffer;
    private readonly ConcurrentDictionary<string, StreamData?> streamCache = new();
    private readonly Dictionary<FType, Provider> streamProviders;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotifChecker" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="credsProvider">The credentials provider.</param>
    /// <param name="uniqueCacheKey">The unique cache key for storing data.</param>
    /// <param name="isMaster">if set to <c>true</c> clears all data at start.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public NotifChecker(
        IHttpClientFactory httpClientFactory,
        IBotCredentials credsProvider,
        string uniqueCacheKey,
        bool isMaster, ILogger<NotifChecker> logger)
    {
        this.logger = logger;
        key = $"{uniqueCacheKey}_followed_streams_data";
        streamProviders = new Dictionary<FType, Provider>
        {
            {
                FType.Twitch, new TwitchHelixProvider(httpClientFactory, credsProvider)
            },
            {
                FType.Picarto, new PicartoProvider(httpClientFactory)
            },
            {
                FType.Trovo, new TrovoProvider(httpClientFactory, credsProvider)
            },
            {
                FType.Youtube, new YoutubeScrapingProvider()
            }
        };
        offlineBuffer = [];
        if (isMaster)
            CacheClearAllData();
    }

    /// <summary>
    ///     Occurs when streams become offline.
    /// </summary>
    public event Func<List<StreamData>, Task> OnStreamsOffline = _ => Task.CompletedTask;

    /// <summary>
    ///     Occurs when streams become online.
    /// </summary>
    public event Func<List<StreamData>, Task> OnStreamsOnline = _ => Task.CompletedTask;

    /// <summary>
    ///     Gets all streams that have been failing for more than the provided timespan.
    /// </summary>
    /// <param name="duration">The duration to check for failing streams.</param>
    /// <param name="remove">if set to <c>true</c> removes the failing streams from tracking.</param>
    /// <returns>A collection of stream data keys representing failing streams.</returns>
    public IEnumerable<StreamDataKey> GetFailingStreams(TimeSpan duration, bool remove = false)
    {
        var toReturn = streamProviders
            .SelectMany(prov => prov.Value
                .FailingStreamsDictionary
                .Where(fs => DateTime.UtcNow - fs.Value > duration)
                .Select(fs => new StreamDataKey(prov.Value.Platform, fs.Key)))
            .ToList();

        if (!remove) return toReturn;
        foreach (var toBeRemoved in toReturn)
            streamProviders[toBeRemoved.Type].ClearErrorsFor(toBeRemoved.Name);

        return toReturn;
    }

    /// <summary>
    ///     Runs the notification checker loop asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public Task RunAsync()
    {
#if DEBUG
        logger.LogInformation("[NotifChecker] Starting notification checker loop...");
#endif
        return Task.Run(async () =>
        {
            var loopIteration = 0;
            while (true)
            {
                try
                {
                    loopIteration++;
#if DEBUG
                    logger.LogInformation("[NotifChecker] Starting check iteration #{LoopIteration}", loopIteration);
#endif

                    var allStreamData = await CacheGetAllData();
#if DEBUG
                    logger.LogInformation("[NotifChecker] Retrieved {CachedStreamCount} streams from cache",
                        allStreamData.Count);
#endif

                    var oldStreamDataDict = allStreamData
                        // group by type
                        .GroupBy(entry => entry.Key.Type)
                        .ToDictionary(entry => entry.Key,
                            entry => entry.AsEnumerable()
                                .ToDictionary(x => x.Key.Name ?? string.Empty, x => x.Value));

                    foreach (var typeGroup in oldStreamDataDict)
                    {
#if DEBUG
                        logger.LogInformation("[NotifChecker] Found {StreamCount} {Platform} streams in cache",
                            typeGroup.Value.Count, typeGroup.Key);
#endif
                    }

                    var newStreamData = await oldStreamDataDict
                        .Select(x =>
                        {
                            // get all stream data for the streams of this type
                            if (streamProviders.TryGetValue(x.Key, out var provider))
                            {
                                var streamNames = x.Value
                                    .Where(entry => !string.IsNullOrEmpty(entry.Key))
                                    .Select(entry => entry.Key)
                                    .ToList();
#if DEBUG
                                logger.LogInformation(
                                    "[NotifChecker] Checking {StreamCount} {Platform} streams: {StreamNames}",
                                    streamNames.Count, x.Key, string.Join(", ", streamNames));
#endif
                                return provider.GetStreamDataAsync(streamNames);
                            }

                            // this means there's no provider for this stream data, (and there was before?)
#if DEBUG
                            logger.LogWarning("[NotifChecker] No provider found for platform {Platform}", x.Key);
#endif
                            return Task.FromResult<IReadOnlyCollection<StreamData>>(
                                new List<StreamData>());
                        })
                        .WhenAll().ConfigureAwait(false);

                    var totalNewStreams = newStreamData.SelectMany(x => x).Count();
#if DEBUG
                    logger.LogInformation(
                        "[NotifChecker] Received {TotalNewStreams} stream data responses from providers",
                        totalNewStreams);
#endif

                    var newlyOnline = new List<StreamData>();
                    var newlyOffline = new List<StreamData>();
                    // go through all new stream data, compare them with the old ones
                    foreach (var newData in newStreamData.SelectMany(x => x))
                    {
                        // update cached data
                        var cachekey = newData.CreateKey();
#if DEBUG
                        logger.LogTrace(
                            "[NotifChecker] Processing stream data for {Platform}/{Username}, IsLive: {IsLive}",
                            cachekey.Type, cachekey.Name, newData.IsLive);
#endif

                        // compare old data with new data
                        if (!oldStreamDataDict.TryGetValue(cachekey.Type, out var typeDict)
                            || !typeDict.TryGetValue(cachekey.Name ?? string.Empty, out var oldData)
                            || oldData is null)
                        {
#if DEBUG
                            logger.LogInformation(
                                "[NotifChecker] First time seeing stream {Platform}/{Username}, adding to cache",
                                cachekey.Type, cachekey.Name);
#endif
                            await CacheAddData(cachekey, newData, true);
                            continue;
                        }

                        // fill with last known game in case it's empty
                        if (string.IsNullOrWhiteSpace(newData.Game))
                            newData.Game = oldData.Game;

                        await CacheAddData(cachekey, newData, true);

                        // Log status changes
                        if (oldData.IsLive != newData.IsLive)
                        {
#if DEBUG
                            logger.LogInformation(
                                "[NotifChecker] Stream status changed for {Platform}/{Username}: {OldStatus} -> {NewStatus}",
                                cachekey.Type, cachekey.Name, oldData.IsLive ? "Online" : "Offline",
                                newData.IsLive ? "Online" : "Offline");
#endif
                        }

                        // if the stream is offline, we need to check if it was
                        // marked as offline once previously
                        // if it was, that means this is second time we're getting offline
                        // status for that stream -> notify subscribers
                        // Note: This is done because twitch api will sometimes return an offline status
                        //       shortly after the stream is already online, which causes duplicate notifications.
                        //       (stream is online -> stream is offline -> stream is online again (and stays online))
                        //       This offlineBuffer will make it so that the stream has to be marked as offline TWICE
                        //       before it sends an offline notification to the subscribers.
                        var streamId = (cachekey.Type, cachekey.Name ?? string.Empty);
                        if (!newData.IsLive && offlineBuffer.Remove(streamId))
                        {
#if DEBUG
                            logger.LogInformation(
                                "[NotifChecker] Stream {Platform}/{Username} confirmed offline (second check), adding to offline notifications",
                                cachekey.Type, cachekey.Name);
#endif
                            newlyOffline.Add(newData);
                        }
                        else if (newData.IsLive != oldData.IsLive)
                        {
                            if (newData.IsLive)
                            {
                                offlineBuffer.Remove(streamId);
#if DEBUG
                                logger.LogInformation(
                                    "[NotifChecker] Stream {Platform}/{Username} came online, adding to online notifications",
                                    cachekey.Type, cachekey.Name);
#endif
                                newlyOnline.Add(newData);
                            }
                            else
                            {
                                offlineBuffer.Add(streamId);
#if DEBUG
                                logger.LogInformation(
                                    "[NotifChecker] Stream {Platform}/{Username} went offline (first check), adding to buffer",
                                    cachekey.Type, cachekey.Name);
#endif
                                // newlyOffline.Add(newData);
                            }
                        }
                    }

                    var tasks = new List<Task>();

                    if (newlyOnline.Count > 0)
                    {
#if DEBUG
                        logger.LogInformation("[NotifChecker] Firing OnStreamsOnline event for {OnlineCount} streams",
                            newlyOnline.Count);
#endif
                        tasks.Add(OnStreamsOnline(newlyOnline));
                    }

                    if (newlyOffline.Count > 0)
                    {
#if DEBUG
                        logger.LogInformation("[NotifChecker] Firing OnStreamsOffline event for {OfflineCount} streams",
                            newlyOffline.Count);
#endif
                        tasks.Add(OnStreamsOffline(newlyOffline));
                    }

                    if (tasks.Count == 0)
                    {
#if DEBUG
                        logger.LogInformation("[NotifChecker] No status changes detected in iteration #{LoopIteration}",
                            loopIteration);
#endif
                    }

                    await Task.WhenAll(tasks).ConfigureAwait(false);

#if DEBUG
                    logger.LogInformation("[NotifChecker] Completed iteration #{LoopIteration}, waiting 3 seconds...",
                        loopIteration);
#endif
                    await Task.Delay(3000).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
#if DEBUG
                    logger.LogError(ex, "[NotifChecker] Error in iteration #{LoopIteration}: {ErrorMessage}",
                        loopIteration, ex.Message);
#endif
                    await Task.Delay(10000).ConfigureAwait(false); // Wait 10 seconds before retrying
                }
            }
        });
    }

    /// <summary>
    ///     Adds or updates stream data in the cache.
    /// </summary>
    /// <param name="streamDataKey">The stream data key.</param>
    /// <param name="data">The stream data.</param>
    /// <param name="replace">if set to <c>true</c> replaces existing data.</param>
    /// <returns><c>true</c> if data was successfully added or updated; otherwise, <c>false</c>.</returns>
    public Task<bool> CacheAddData(StreamDataKey streamDataKey, StreamData? data, bool replace)
    {
        var serializedKey = JsonSerializer.Serialize(streamDataKey, CachedJsonOptions);
#if DEBUG
        logger.LogInformation(
            "[NotifChecker] Cache: CacheAddData called - Platform: {Platform}, Name: {Name}, SerializedKey: {SerializedKey}, Data: {Data}, Replace: {Replace}",
            streamDataKey.Type, streamDataKey.Name, serializedKey, data != null ? "NotNull" : "Null", replace);
#endif

        if (replace)
        {
            streamCache[serializedKey] = data;
#if DEBUG
            logger.LogInformation("[NotifChecker] Cache: Updated data for {Platform}/{Username}, IsLive: {IsLive}",
                streamDataKey.Type, streamDataKey.Name, data?.IsLive);
#endif
            return Task.FromResult(true);
        }

        var added = streamCache.TryAdd(serializedKey, data);
        if (added)
        {
#if DEBUG
            logger.LogInformation(
                "[NotifChecker] Cache: Added new entry for {Platform}/{Username}, total cache size now: {CacheSize}",
                streamDataKey.Type, streamDataKey.Name, streamCache.Count);
#endif
        }
        else
        {
#if DEBUG
            logger.LogInformation("[NotifChecker] Cache: Entry already exists for {Platform}/{Username}, not added",
                streamDataKey.Type, streamDataKey.Name);
#endif
        }

        return Task.FromResult(added);
    }

    /// <summary>
    ///     Deletes stream data from the cache.
    /// </summary>
    /// <param name="streamdataKey">The stream data key.</param>
    private Task CacheDeleteData(StreamDataKey streamdataKey)
    {
        var serializedKey = JsonSerializer.Serialize(streamdataKey, CachedJsonOptions);
        var removed = streamCache.TryRemove(serializedKey, out _);

        if (removed)
        {
#if DEBUG
            logger.LogInformation("[NotifChecker] Cache: Removed entry for {Platform}/{Username}", streamdataKey.Type,
                streamdataKey.Name);
#endif
        }
        else
        {
#if DEBUG
            logger.LogWarning("[NotifChecker] Cache: Attempted to remove non-existent entry for {Platform}/{Username}",
                streamdataKey.Type, streamdataKey.Name);
#endif
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Clears all stream data from the cache.
    /// </summary>
    private void CacheClearAllData()
    {
        var count = streamCache.Count;
        streamCache.Clear();
#if DEBUG
        logger.LogInformation("[NotifChecker] Cache: Cleared all data ({ClearedCount} entries)", count);
#endif
    }

    /// <summary>
    ///     Gets all stream data from the cache.
    /// </summary>
    /// <returns>A dictionary containing all cached stream data.</returns>
    private Task<Dictionary<StreamDataKey, StreamData?>> CacheGetAllData()
    {
#if DEBUG
        logger.LogInformation("[NotifChecker] Cache: Raw cache contains {RawCacheCount} entries", streamCache.Count);
#endif

        var allPairs = streamCache
            .Select(pair => (
                RawKey: pair.Key,
                Key: JsonSerializer.Deserialize<StreamDataKey>(pair.Key, CachedJsonOptions),
                pair.Value))
            .ToList();

#if DEBUG
        logger.LogInformation("[NotifChecker] Cache: After deserialization, found {DeserializedCount} entries",
            allPairs.Count);
#endif

        foreach (var pair in allPairs)
        {
#if DEBUG
            logger.LogInformation(
                "[NotifChecker] Cache: Entry - RawKey: {RawKey}, Platform: {Platform}, Name: {Name}, IsNameNull: {IsNameNull}",
                pair.RawKey, pair.Key.Type, pair.Key.Name ?? "(null)", pair.Key.Name is null);
#endif
        }

        var filtered = allPairs.Where(item => item.Key.Name is not null).ToList();
#if DEBUG
        logger.LogInformation("[NotifChecker] Cache: After filtering null names, {FilteredCount} entries remain",
            filtered.Count);
#endif

        var result = filtered.ToDictionary(item => item.Key, item => item.Value);

        return Task.FromResult(result);
    }

    /// <summary>
    ///     Retrieves stream data by its URL asynchronously.
    /// </summary>
    /// <param name="url">The URL of the stream.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the stream data.</returns>
    public async Task<StreamData?> GetStreamDataByUrlAsync(string url)
    {
        // loop through all providers and see which regex matches
        foreach (var (_, provider) in streamProviders)
        {
            var isValid = await provider.IsValidUrl(url).ConfigureAwait(false);
            if (!isValid)
                continue;
            // if it's not a valid url, try another provider
            return await provider.GetStreamDataByUrlAsync(url).ConfigureAwait(false);
        }

        // if no provider found, return null
        return null;
    }

    /// <summary>
    ///     Ensures a stream is being tracked and returns its current data.
    /// </summary>
    /// <param name="url">The URL of the stream to track.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the stream data.</returns>
    public async Task<StreamData?> TrackStreamByUrlAsync(string url)
    {
        var data = await GetStreamDataByUrlAsync(url).ConfigureAwait(false);
        await EnsureTracked(data);
        return data;
    }

    private async Task EnsureTracked(StreamData? data)
    {
        // something failed, don't add anything to cache
        if (data is null) return;

        // if stream is found, add it to the cache for tracking only if it doesn't already exist
        // because stream will be checked and events will fire in a loop. We don't want to override old state
        await CacheAddData(data.CreateKey(), data, false);
    }

    /// <summary>
    ///     Removes a stream from tracking.
    /// </summary>
    /// <param name="streamDataKey">The stream data key.</param>
    public async Task UntrackStreamByKey(StreamDataKey streamDataKey)
    {
#if DEBUG
        logger.LogInformation("[NotifChecker] Untracking stream: {Platform}/{Username}", streamDataKey.Type,
            streamDataKey.Name);
#endif
        await CacheDeleteData(streamDataKey);
    }
}