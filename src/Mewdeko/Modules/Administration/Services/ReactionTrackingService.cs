using System.Threading;
using Mewdeko.Common.ModuleBehaviors;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Tracks reactions added to recent guild messages so that downstream services
///     (notably <see cref="ProtectionService" />) can identify accounts that participated
///     in coordinated upvote-style behaviour even after the original message has been deleted.
/// </summary>
/// <remarks>
///     Discord does not expose reactions on deleted messages through the gateway or the
///     REST API. The <c>MESSAGE_DELETE</c> payload only includes ids, and the reactions
///     endpoint returns 404 once the parent message is gone. This service therefore keeps
///     a bounded in-memory snapshot of who reacted to whom while messages are still alive,
///     so that suspicious-deletion handling can resolve the reactor list after the fact.
/// </remarks>
public class ReactionTrackingService : INService, IUnloadableService
{
    /// <summary>Hard cap on tracked messages across all guilds.</summary>
    private const int MaxTrackedMessages = 50_000;

    /// <summary>Number of entries dropped in a single eviction pass when the cap is exceeded.</summary>
    private const int EvictBatchSize = 5_000;

    /// <summary>How long a tracked message is retained after it was first seen.</summary>
    private static readonly TimeSpan EntryTtl = TimeSpan.FromHours(2);

    /// <summary>Interval between background sweeps that evict aged-out entries.</summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly EventHandler eventHandler;
    private readonly ILogger<ReactionTrackingService> logger;
    private readonly CancellationTokenSource sweepCts = new();
    private readonly ConcurrentDictionary<ulong, TrackedReactionMessage> tracked = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReactionTrackingService" /> class
    ///     and starts the background eviction loop.
    /// </summary>
    /// <param name="eventHandler">The shared event dispatcher used to subscribe to reaction events.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public ReactionTrackingService(EventHandler eventHandler, ILogger<ReactionTrackingService> logger)
    {
        this.eventHandler = eventHandler;
        this.logger = logger;

        eventHandler.Subscribe("ReactionAdded", "ReactionTrackingService", OnReactionAdded);
        _ = Task.Run(SweepLoopAsync);
    }

    /// <inheritdoc />
    public Task Unload()
    {
        eventHandler.Unsubscribe("ReactionAdded", "ReactionTrackingService", OnReactionAdded);
        sweepCts.Cancel();
        tracked.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Attempts to retrieve the reaction snapshot for the given message id.
    /// </summary>
    /// <param name="messageId">The Discord message id to look up.</param>
    /// <param name="entry">The tracked entry, when found.</param>
    /// <returns><c>true</c> if a snapshot exists; otherwise <c>false</c>.</returns>
    public bool TryGetReactors(ulong messageId, out TrackedReactionMessage? entry)
    {
        return tracked.TryGetValue(messageId, out entry);
    }

    /// <summary>
    ///     Removes the snapshot for the given message id, if present. Call this once a
    ///     consumer has finished acting on a snapshot to release the memory eagerly.
    /// </summary>
    /// <param name="messageId">The Discord message id to forget.</param>
    public void Remove(ulong messageId)
    {
        tracked.TryRemove(messageId, out _);
    }

    /// <summary>
    ///     Returns the number of messages currently being tracked. Intended for diagnostics.
    /// </summary>
    public int TrackedCount
    {
        get
        {
            return tracked.Count;
        }
    }

    private Task OnReactionAdded(Cacheable<IUserMessage, ulong> msgCache,
        Cacheable<IMessageChannel, ulong> chCache, SocketReaction reaction)
    {
        if (reaction.UserId == 0)
            return Task.CompletedTask;

        if (!chCache.HasValue || chCache.Value is not IGuildChannel guildChannel)
            return Task.CompletedTask;

        if (reaction.User.IsSpecified && reaction.User.Value.IsBot)
            return Task.CompletedTask;

        var guildId = guildChannel.GuildId;
        var channelId = guildChannel.Id;
        var messageId = reaction.MessageId;

        var entry = tracked.GetOrAdd(messageId, _ =>
        {
            // Snowflake-derived timestamps are exact for messages we never had cached.
            var msgTimestamp = msgCache.HasValue
                ? msgCache.Value.Timestamp
                : SnowflakeUtils.FromSnowflake(messageId);

            var authorId = msgCache.HasValue ? msgCache.Value.Author?.Id ?? 0UL : 0UL;

            return new TrackedReactionMessage
            {
                MessageId = messageId,
                ChannelId = channelId,
                GuildId = guildId,
                AuthorId = authorId,
                MessageTimestamp = msgTimestamp,
                FirstSeen = DateTime.UtcNow
            };
        });

        // First reactor may have triggered creation before the message was cached; backfill if we can now.
        if (entry.AuthorId == 0 && msgCache.HasValue)
        {
            var aid = msgCache.Value.Author?.Id ?? 0UL;
            if (aid != 0)
                entry.AuthorId = aid;
        }

        entry.Reactors.TryAdd(reaction.UserId, DateTime.UtcNow);

        // Cheap fast-path; the periodic sweep handles the heavy lifting.
        if (tracked.Count > MaxTrackedMessages)
            EvictOldest();

        return Task.CompletedTask;
    }

    private async Task SweepLoopAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(SweepInterval);
            while (await timer.WaitForNextTickAsync(sweepCts.Token).ConfigureAwait(false))
            {
                try
                {
                    Sweep();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Reaction tracker sweep failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Service shutting down; expected.
        }
    }

    private void Sweep()
    {
        var cutoff = DateTime.UtcNow - EntryTtl;
        foreach (var kvp in tracked)
        {
            if (kvp.Value.FirstSeen < cutoff)
                tracked.TryRemove(kvp.Key, out _);
        }

        if (tracked.Count > MaxTrackedMessages)
            EvictOldest();
    }

    private void EvictOldest()
    {
        // Snapshot keys/values once; ConcurrentDictionary enumeration is weakly consistent.
        var toRemove = tracked
            .OrderBy(kvp => kvp.Value.FirstSeen)
            .Take(EvictBatchSize)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
            tracked.TryRemove(key, out _);
    }
}

/// <summary>
///     A bounded snapshot of reactions observed against a single guild message.
/// </summary>
public class TrackedReactionMessage
{
    /// <summary>Discord message id this snapshot belongs to.</summary>
    public ulong MessageId { get; set; }

    /// <summary>Channel that the tracked message was posted in.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>Guild that the tracked message was posted in.</summary>
    public ulong GuildId { get; set; }

    /// <summary>Author of the message, when known. Zero if not yet resolved.</summary>
    public ulong AuthorId { get; set; }

    /// <summary>Original timestamp of the tracked message (derived from the snowflake when needed).</summary>
    public DateTimeOffset MessageTimestamp { get; set; }

    /// <summary>UTC time the snapshot was first created in this process.</summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>Map of reactor user id to the UTC time we first observed them reacting.</summary>
    public ConcurrentDictionary<ulong, DateTime> Reactors { get; } = new();
}
