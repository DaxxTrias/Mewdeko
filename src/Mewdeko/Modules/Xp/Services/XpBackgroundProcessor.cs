using System.Collections.Concurrent;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Data;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Xp.Models;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Handles background processing for XP-related tasks.
/// </summary>
public class XpBackgroundProcessor : INService, IDisposable
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly XpCacheManager cacheManager;
    private readonly XpRewardManager rewardManager;
    private readonly XpCompetitionManager competitionManager;

    // Multi-threaded queue for XP updates with bounded capacity
    private readonly ConcurrentQueue<XpGainItem> xpUpdateQueue = new();
    private readonly NonBlocking.ConcurrentDictionary<ulong, byte> activeUserIds = new(); // Set to track active users

    // Constants for processing limits and throttling
    private const int MaxQueueSize = 50000;
    private const int BatchSize = 200;
    private const int MaxConcurrentDbOps = 3;

    // Thread-safe processing flags
    private int processingInProgress;
    private long totalProcessed;
    private readonly SemaphoreSlim dbThrottle;

    // Timers for background processing
    private readonly Timer processingTimer;
    private readonly Timer decayTimer;
    private readonly Timer cleanupTimer;

    // Adaptive timing parameters
    private TimeSpan processingInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpBackgroundProcessor" /> class.
    /// </summary>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="cacheManager">The cache manager.</param>
    /// <param name="rewardManager">The reward manager.</param>
    /// <param name="competitionManager">The competition manager.</param>
    public XpBackgroundProcessor(
        IDataConnectionFactory dbFactory,
        XpCacheManager cacheManager,
        XpRewardManager rewardManager,
        XpCompetitionManager competitionManager)
    {
        this.dbFactory = dbFactory;
        this.cacheManager = cacheManager;
        this.rewardManager = rewardManager;
        this.competitionManager = competitionManager;

        // Initialize throttling with optimal concurrency
        dbThrottle = new SemaphoreSlim(MaxConcurrentDbOps, MaxConcurrentDbOps);

        // Initialize background processing timers with adaptive intervals
        processingTimer = new Timer(ProcessXpBatch, null, TimeSpan.FromSeconds(5), processingInterval);
        decayTimer = new Timer(ProcessXpDecay, null, TimeSpan.FromHours(12), TimeSpan.FromHours(12));
        cleanupTimer = new Timer(CleanupCaches, null, TimeSpan.FromHours(2), TimeSpan.FromHours(2));

        Log.Information("XP Background Processor initialized with batch size {BatchSize}, " +
                        "max queue size {MaxQueueSize}, and {Concurrency} concurrent operations",
            BatchSize, MaxQueueSize, MaxConcurrentDbOps);
    }

    /// <summary>
    ///     Queues an XP gain for processing with overflow protection.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="amount">The amount of XP to add.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="source">The source of the XP gain.</param>
    public void QueueXpGain(ulong guildId, ulong userId, int amount, ulong channelId, XpSource source)
    {
        if (amount <= 0)
            return;

        // Check queue size to prevent memory issues
        if (xpUpdateQueue.Count >= MaxQueueSize)
        {
            // Log warning if this is the first overflow
            if (xpUpdateQueue.Count == MaxQueueSize)
            {
                Log.Warning("XP update queue overflow! Queue size has reached {Size} items. " +
                            "Some XP updates will be dropped until the queue is processed.",
                    MaxQueueSize);
            }

            return;
        }

        // Add to processing queue
        xpUpdateQueue.Enqueue(new XpGainItem
        {
            GuildId = guildId,
            UserId = userId,
            Amount = amount,
            ChannelId = channelId,
            Source = source,
            Timestamp = DateTime.UtcNow
        });

        // Track active user
        activeUserIds[userId] = 0; // Value doesn't matter, using as a set
    }

    /// <summary>
    ///     Processes the first message of the day for a user.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ProcessFirstMessageOfDay(SocketGuildUser user, ulong channelId)
    {
        // Quick check for server exclusion
        if (await cacheManager.IsServerExcludedAsync(user.Guild.Id))
            return;

        // Check for first message of the day using atomic Redis operations
        var key = $"xp:first_msg:{user.Guild.Id}:{user.Id}";
        var dateString = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Use Redis for distributed first-message tracking
        var redis = cacheManager.GetRedisDatabase();

        // Atomic operation: Only set if the key doesn't exist or has different value
        var wasSet = await redis.StringSetAsync(
            key,
            dateString,
            TimeSpan.FromDays(2),
            When.NotExists,
            CommandFlags.FireAndForget);

        if (wasSet)
        {
            // Award first message bonus (only if first message of the day)
            var settings = await cacheManager.GetGuildXpSettingsAsync(user.Guild.Id);
            if (settings.FirstMessageBonus > 0)
            {
                QueueXpGain(
                    user.Guild.Id,
                    user.Id,
                    settings.FirstMessageBonus,
                    channelId,
                    XpSource.FirstMessage
                );
            }
        }
    }

    /// <summary>
    ///     Processes batches of XP updates with adaptive timing.
    /// </summary>
    private async void ProcessXpBatch(object state)
    {
        // Skip if already processing or queue is empty
        if (Interlocked.CompareExchange(ref processingInProgress, 1, 0) != 0 ||
            xpUpdateQueue.IsEmpty)
        {
            return;
        }

        var startTime = DateTime.UtcNow;
        var itemsProcessed = 0;
        var batchSize = Math.Min(BatchSize, xpUpdateQueue.Count);

        try
        {
            // Dequeue items into a batch
            var batchItems = new List<XpGainItem>(batchSize);
            while (batchItems.Count < batchSize && xpUpdateQueue.TryDequeue(out var item))
            {
                batchItems.Add(item);
                itemsProcessed++;
            }

            if (batchItems.Count == 0)
                return;

            // Group items by guild and user for bulk processing
            var groupedByGuild = batchItems
                .GroupBy(x => x.GuildId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Process each guild with a throttled connection
            if (await dbThrottle.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    await ProcessXpBatchForGuilds(groupedByGuild);
                }
                finally
                {
                    dbThrottle.Release();
                }
            }
            else
            {
                Log.Warning("Could not acquire database throttle after waiting 5 seconds");

                // Re-queue items if we couldn't process them
                foreach (var item in batchItems)
                {
                    // Only re-queue if we're not at capacity
                    if (xpUpdateQueue.Count < MaxQueueSize)
                    {
                        xpUpdateQueue.Enqueue(item);
                    }
                }
            }

            // Update statistics
            Interlocked.Add(ref totalProcessed, itemsProcessed);

            // Adjust timing based on performance
            AdjustProcessingInterval(startTime, itemsProcessed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing XP batch");
        }
        finally
        {
            // Reset processing flag
            Interlocked.Exchange(ref processingInProgress, 0);
        }
    }

    /// <summary>
    ///     Adjusts the processing interval based on load and performance.
    /// </summary>
    private void AdjustProcessingInterval(DateTime startTime, int itemsProcessed)
    {
        if (itemsProcessed == 0)
            return;

        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var queueSize = xpUpdateQueue.Count;

        // Calculate target interval based on current load
        var targetInterval = TimeSpan.FromSeconds(5); // Default

        if (queueSize > 1000)
        {
            // High load - process more frequently
            targetInterval = TimeSpan.FromSeconds(1);
        }
        else if (queueSize > 500)
        {
            // Medium load
            targetInterval = TimeSpan.FromSeconds(2);
        }
        else if (queueSize < 100 && elapsedMs < 100)
        {
            // Light load - save resources
            targetInterval = TimeSpan.FromSeconds(10);
        }

        // Only change timer if significantly different (to avoid timer thrashing)
        if (Math.Abs((targetInterval - processingInterval).TotalSeconds) > 1)
        {
            processingInterval = targetInterval;
            processingTimer.Change(processingInterval, processingInterval);

            Log.Debug(
                "Adjusted XP processing interval to {Interval}s based on queue size {QueueSize} and processing time {ProcessingTime}ms",
                processingInterval.TotalSeconds, queueSize, elapsedMs);
        }
    }

    /// <summary>
    ///     Processes XP updates for multiple guilds in a batch using a single connection.
    /// </summary>
    private async Task ProcessXpBatchForGuilds(Dictionary<ulong, List<XpGainItem>> groupedByGuild)
    {
        if (groupedByGuild.Count == 0)
            return;

        var notificationsToSend = new List<XpNotification>();
        var roleRewardsToGrant = new List<RoleRewardItem>();
        var currencyRewardsToGrant = new List<CurrencyRewardItem>();
        var competitionUpdates = new List<CompetitionUpdateItem>();

        // Get a single connection for the entire batch
        await using var db = await dbFactory.CreateConnectionAsync();

        foreach (var guildEntry in groupedByGuild)
        {
            var guildId = guildEntry.Key;
            var guildItems = guildEntry.Value;

            try
            {
                // Get guild settings once for the entire guild
                var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);

                // Skip if XP is disabled for this guild
                if (settings.XpGainDisabled)
                    continue;

                // Check if this guild has active competitions (once per guild)
                var activeCompetitions = await competitionManager.GetActiveCompetitionsAsync(guildId);

                // Process each user's combined XP in this guild
                var userGroups = guildItems.GroupBy(x => x.UserId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Process all users in this guild as a batch
                await ProcessUserBatchForGuild(
                    db,
                    guildId,
                    userGroups,
                    settings,
                    activeCompetitions,
                    notificationsToSend,
                    roleRewardsToGrant,
                    currencyRewardsToGrant,
                    competitionUpdates);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing XP batch for guild {GuildId}", guildId);
            }
        }

        // Process all notifications and rewards after all users are updated
        await Task.WhenAll(
            rewardManager.SendNotificationsAsync(notificationsToSend),
            rewardManager.GrantRoleRewardsAsync(roleRewardsToGrant),
            rewardManager.GrantCurrencyRewardsAsync(currencyRewardsToGrant),
            competitionManager.UpdateCompetitionsAsync(db, competitionUpdates)
        );
    }

    /// <summary>
    ///     Processes a batch of users for a specific guild with bulk operations.
    /// </summary>
    private async Task ProcessUserBatchForGuild(
        MewdekoDb db,
        ulong guildId,
        Dictionary<ulong, List<XpGainItem>> userGroups,
        GuildXpSetting settings,
        List<XpCompetition> activeCompetitions,
        List<XpNotification> notificationsToSend,
        List<RoleRewardItem> roleRewardsToGrant,
        List<CurrencyRewardItem> currencyRewardsToGrant,
        List<CompetitionUpdateItem> competitionUpdates)
    {
        if (userGroups.Count == 0)
            return;

        // Get all user IDs in this batch
        var userIds = userGroups.Keys.ToList();

        // Get all existing XP records for these users in a single query
        var existingUserXp = await db.GuildUserXps
            .Where(x => x.GuildId == guildId && userIds.Contains(x.UserId))
            .ToDictionaryAsync(k => k.UserId, v => v);

        // Prepare collections for bulk operations
        var xpRecordsToInsert = new List<GuildUserXp>();
        var xpRecordsToUpdate = new List<GuildUserXp>();
        var now = DateTime.UtcNow;

        // Process each user
        foreach (var userEntry in userGroups)
        {
            var userId = userEntry.Key;
            var userItems = userEntry.Value;

            // Calculate total XP for this user
            var totalXpGain = userItems.Sum(x => x.Amount);
            if (totalXpGain <= 0)
                continue;

            // Get last message details for notifications
            var lastItem = userItems.OrderByDescending(x => x.Timestamp).FirstOrDefault();
            if (lastItem == null)
                continue;

            var sources = string.Join(", ", userItems.Select(x => x.Source.ToString()).Distinct());

            try
            {
                // Check if user exists in the DB already
                if (existingUserXp.TryGetValue(userId, out var userXp))
                {
                    // Calculate levels before update
                    var oldLevel = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType);

                    // Update XP and last activity
                    userXp.TotalXp += totalXpGain;
                    userXp.LastActivity = now;

                    // Calculate new level
                    var newLevel = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType);

                    // Check for level up
                    if (newLevel > oldLevel)
                    {
                        userXp.LastLevelUp = now;

                        // Add notification if enabled
                        if ((XpNotificationType)userXp.NotifyType != XpNotificationType.None)
                        {
                            notificationsToSend.Add(new XpNotification
                            {
                                GuildId = guildId,
                                UserId = userId,
                                Level = newLevel,
                                ChannelId = lastItem.ChannelId,
                                NotificationType = (XpNotificationType)userXp.NotifyType,
                                Sources = sources
                            });
                        }

                        // Get rewards for new levels
                        for (var level = oldLevel + 1; level <= newLevel; level++)
                        {
                            // Check for role rewards
                            var roleReward = await rewardManager.GetRoleRewardForLevelAsync(db, guildId, level);
                            if (roleReward != null)
                            {
                                roleRewardsToGrant.Add(new RoleRewardItem
                                {
                                    GuildId = guildId, UserId = userId, RoleId = roleReward.RoleId
                                });
                            }

                            // Check for currency rewards
                            var currencyReward = await rewardManager.GetCurrencyRewardForLevelAsync(db, guildId, level);
                            if (currencyReward != null)
                            {
                                currencyRewardsToGrant.Add(new CurrencyRewardItem
                                {
                                    GuildId = guildId, UserId = userId, Amount = currencyReward.Amount
                                });
                            }
                        }
                    }

                    // Add to update batch
                    xpRecordsToUpdate.Add(userXp);

                    // Add competition updates
                    foreach (var competition in activeCompetitions)
                    {
                        competitionUpdates.Add(new CompetitionUpdateItem
                        {
                            CompetitionId = competition.Id,
                            UserId = userId,
                            XpGained = totalXpGain,
                            CurrentLevel =
                                XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType)
                        });
                    }
                }
                else
                {
                    // Create new user XP record
                    var newUserXp = new GuildUserXp
                    {
                        GuildId = guildId,
                        UserId = userId,
                        TotalXp = totalXpGain,
                        LastActivity = now,
                        LastLevelUp = now,
                        NotifyType = (int)XpNotificationType.Channel // Default
                    };

                    // Add to insert batch
                    xpRecordsToInsert.Add(newUserXp);

                    // Check if user gained enough XP to level up
                    var newLevel = XpCalculator.CalculateLevel(newUserXp.TotalXp, (XpCurveType)settings.XpCurveType);
                    if (newLevel > 0)
                    {
                        // Add notification for first level
                        notificationsToSend.Add(new XpNotification
                        {
                            GuildId = guildId,
                            UserId = userId,
                            Level = newLevel,
                            ChannelId = lastItem.ChannelId,
                            NotificationType = XpNotificationType.Channel,
                            Sources = sources
                        });

                        // Check for rewards at this level
                        var roleReward = await rewardManager.GetRoleRewardForLevelAsync(db, guildId, newLevel);
                        if (roleReward != null)
                        {
                            roleRewardsToGrant.Add(new RoleRewardItem
                            {
                                GuildId = guildId, UserId = userId, RoleId = roleReward.RoleId
                            });
                        }

                        var currencyReward = await rewardManager.GetCurrencyRewardForLevelAsync(db, guildId, newLevel);
                        if (currencyReward != null)
                        {
                            currencyRewardsToGrant.Add(new CurrencyRewardItem
                            {
                                GuildId = guildId, UserId = userId, Amount = currencyReward.Amount
                            });
                        }
                    }

                    // Add competition updates for new users
                    foreach (var competition in activeCompetitions)
                    {
                        competitionUpdates.Add(new CompetitionUpdateItem
                        {
                            CompetitionId = competition.Id,
                            UserId = userId,
                            XpGained = totalXpGain,
                            CurrentLevel = newLevel
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing XP for user {UserId} in guild {GuildId}", userId, guildId);
            }
        }

        // Execute bulk operations
        try
        {
            // Bulk insert new records
            if (xpRecordsToInsert.Count > 0)
            {
                await db.BulkCopyAsync(new BulkCopyOptions
                {
                    TableName = "GuildUserXps"
                }, xpRecordsToInsert);

                // Update cache for new records
                foreach (var record in xpRecordsToInsert)
                {
                    cacheManager.UpdateUserXpCacheAsync(record);
                }

                Log.Debug("Bulk inserted {Count} new XP records for guild {GuildId}",
                    xpRecordsToInsert.Count, guildId);
            }

            // Bulk update existing records
            if (xpRecordsToUpdate.Count > 0)
            {
                await db.BulkCopyAsync(xpRecordsToUpdate);

                // Update cache for updated records
                foreach (var record in xpRecordsToUpdate)
                {
                    cacheManager.UpdateUserXpCacheAsync(record);
                }

                Log.Debug("Bulk updated {Count} existing XP records for guild {GuildId}",
                    xpRecordsToUpdate.Count, guildId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing bulk operations for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Processes XP decay for inactive users with bulk operations.
    /// </summary>
    private async void ProcessXpDecay(object state)
    {
        try
        {
            // Get a fresh DB connection for decay processing
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get all guild settings with XP decay enabled
            var guildsWithDecay = await db.GuildXpSettings
                .Where(g => g.EnableXpDecay)
                .ToListAsync();

            if (guildsWithDecay.Count == 0)
                return;

            Log.Information("Processing XP decay for {Count} guilds with decay enabled", guildsWithDecay.Count);

            foreach (var settings in guildsWithDecay)
            {
                try
                {
                    // Calculate inactive threshold
                    var inactiveThreshold = DateTime.UtcNow.AddDays(-settings.InactivityDaysBeforeDecay);

                    // Process in batches to avoid memory issues with large guilds
                    const int decayBatchSize = 500;
                    var totalDecayed = 0;
                    var hasMoreRecords = true;
                    var lastUserId = 0UL;

                    while (hasMoreRecords)
                    {
                        // Get batch of inactive users
                        var inactiveUsers = await db.GuildUserXps
                            .Where(x =>
                                x.GuildId == settings.GuildId &&
                                x.LastActivity < inactiveThreshold &&
                                x.UserId > lastUserId)
                            .OrderBy(x => x.UserId)
                            .Take(decayBatchSize)
                            .ToListAsync();

                        hasMoreRecords = inactiveUsers.Count == decayBatchSize;

                        if (inactiveUsers.Count == 0)
                            break;

                        // Update last user ID for pagination
                        lastUserId = inactiveUsers[^1].UserId;

                        // Apply decay to all users in batch
                        foreach (var user in inactiveUsers)
                        {
                            var decayAmount = (long)(user.TotalXp * (settings.DailyDecayPercentage / 100.0));
                            if (decayAmount > 0)
                            {
                                user.TotalXp -= decayAmount;

                                // Ensure total XP doesn't go below 0
                                if (user.TotalXp < 0)
                                    user.TotalXp = 0;

                                totalDecayed++;
                            }
                        }

                        // Bulk update decayed users
                        await db.GuildUserXps.BulkCopyAsync(inactiveUsers);

                        // Update cache for each decayed user
                        foreach (var user in inactiveUsers)
                        {
                            cacheManager.UpdateUserXpCacheAsync(user);
                        }
                    }

                    if (totalDecayed > 0)
                    {
                        Log.Information("Applied XP decay to {Count} inactive users in guild {GuildId}",
                            totalDecayed, settings.GuildId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing XP decay for guild {GuildId}", settings.GuildId);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in XP decay processing");
        }
    }

    /// <summary>
    ///     Cleans up expired cache entries and reports statistics.
    /// </summary>
    private async void CleanupCaches(object state)
    {
        try
        {
            Log.Debug("Running cache cleanup task");

            // Clean up Redis caches
            await cacheManager.CleanupCachesAsync();

            // Clear active user tracking to prevent memory leaks
            if (activeUserIds.Count > 10000)
            {
                activeUserIds.Clear();
                Log.Debug("Cleared active user tracking (had {Count} entries)", activeUserIds.Count);
            }

            // Log statistics
            Log.Information("XP Background Processor stats: Queue size: {QueueSize}, " +
                            "Total processed: {TotalProcessed}, Active users tracked: {ActiveUsers}",
                xpUpdateQueue.Count, totalProcessed, activeUserIds.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning up caches");
        }
    }

    /// <summary>
    ///     Disposes resources used by the XP background processor.
    /// </summary>
    public void Dispose()
    {
        processingTimer?.Dispose();
        decayTimer?.Dispose();
        cleanupTimer?.Dispose();
        dbThrottle?.Dispose();

        // Process any remaining items in the queue
        ProcessFinalBatches();
    }

    /// <summary>
    ///     Processes any remaining XP items when shutting down.
    /// </summary>
    private void ProcessFinalBatches()
    {
        Log.Information("Processing final XP batches before shutdown...");

        try
        {
            // Process a fixed number of batches on shutdown
            var remainingItems = xpUpdateQueue.Count;
            var batches = Math.Min(10, remainingItems / BatchSize + 1);

            for (var i = 0; i < batches && !xpUpdateQueue.IsEmpty; i++)
            {
                // Force processing to continue despite flag
                Interlocked.Exchange(ref processingInProgress, 0);
                ProcessXpBatch(null!);
                Thread.Sleep(100); // Short delay between batches
            }

            // Log how many items we couldn't process
            var unprocessed = xpUpdateQueue.Count;
            if (unprocessed > 0)
            {
                Log.Warning("{Count} XP updates remain unprocessed during shutdown", unprocessed);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing final XP batches");
        }
    }
}