using System.Collections.Concurrent;
using System.Threading;
using LinqToDB;
using DataModel;
using Mewdeko.Database.EF.EFCore.Enums;
using Mewdeko.Modules.Xp.Models;

using Serilog;

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

    // Multi-threaded queue for XP updates
    private readonly ConcurrentQueue<XpGainItem> xpUpdateQueue = new();

    // Thread-safe flag indicating whether we're currently processing
    private int processingInProgress;

    // Process XP updates in batches of this size
    private readonly int batchSize = 100;

    // Throttling control
    private readonly SemaphoreSlim dbThrottle = new(5, 5); // Max 5 concurrent DB operations

    // Timers for background processing
    private readonly Timer processingTimer;
    private readonly Timer decayTimer;
    private readonly Timer cleanupTimer;

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

        // Initialize background processing timers
        processingTimer = new Timer(ProcessXpBatch, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        decayTimer = new Timer(ProcessXpDecay, null, TimeSpan.FromHours(6), TimeSpan.FromHours(6));
        cleanupTimer = new Timer(CleanupCaches, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    /// <summary>
    ///     Queues an XP gain for processing.
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

        xpUpdateQueue.Enqueue(new XpGainItem
        {
            GuildId = guildId,
            UserId = userId,
            Amount = amount,
            ChannelId = channelId,
            Source = source,
            Timestamp = DateTime.UtcNow
        });
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

        // Check for first message of the day
        var key = $"xp:first_msg:{user.Guild.Id}:{user.Id}";
        var dateString = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Use Redis for distributed first-message tracking
        var redis = cacheManager.GetRedisDatabase();
        var lastDateString = await redis.StringGetAsync(key);

        if (!lastDateString.HasValue || lastDateString != dateString)
        {
            await redis.StringSetAsync(key, dateString, TimeSpan.FromDays(2));

            // Award first message bonus
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

    private async void ProcessXpBatch(object state)
    {
        //Log.Information("ProcessXpBatch called, Queue count: {Count}, Processing flag: {Flag}",
            //xpUpdateQueue.Count, processingInProgress);

        // Skip if already processing or queue is empty
        if (Interlocked.CompareExchange(ref processingInProgress, 1, 0) != 0)
        {
            //Log.Information("Skipping batch processing - already in progress");
            return;
        }

        if (xpUpdateQueue.IsEmpty)
        {
            //Log.Information("Skipping batch processing - queue is empty");
            Interlocked.Exchange(ref processingInProgress, 0); // Reset flag in this branch too
            return;
        }

        try
        {
            //Log.Information("Starting to dequeue items, available throttle slots: {Available}/{Max}",
                //dbThrottle.CurrentCount);

            var batchItems = new List<XpGainItem>();
            var processedCount = 0;

            // Dequeue items up to batch size
            while (processedCount < batchSize && xpUpdateQueue.TryDequeue(out var item))
            {
                batchItems.Add(item);
                processedCount++;
            }

            //Log.Information("Dequeued {Count} items", batchItems.Count);

            if (batchItems.Count == 0)
            {
                //Log.Warning("No items dequeued despite queue not being empty - possible race condition");
                return;
            }

            // Group items by guild and user for bulk processing
            var groupedItems = batchItems.GroupBy(x => (x.GuildId, x.UserId)).ToList();
            //Log.Information("Grouped into {Count} user batches", groupedItems.Count);

            // Process batch using throttling
            var waitResult = await dbThrottle.WaitAsync(TimeSpan.FromSeconds(5));
            if (!waitResult)
            {
                //Log.Warning("Could not acquire dbThrottle within timeout period - throttle may be deadlocked");
                return;
            }

            try
            {
                //Log.Information("Processing XP groups");
                await ProcessXpGroups(groupedItems);
                //Log.Information("Finished processing XP groups");
            }
            finally
            {
                dbThrottle.Release();
               // Log.Information("Released dbThrottle");
            }
        }
        catch (Exception)
        {
            //Log.Error(ex, "Error processing XP batch");
        }
        finally
        {
            // Reset processing flag
            Interlocked.Exchange(ref processingInProgress, 0);
            //Log.Information("Reset processing flag from {OldValue} to 0", oldValue);
        }
    }

    /// <summary>
    ///     Processes the XP for grouped items by guild and user with concurrency handling.
    /// </summary>
    private async Task ProcessXpGroups(List<IGrouping<(ulong GuildId, ulong UserId), XpGainItem>> groupedItems)
    {
        var notificationsToSend = new List<XpNotification>();
        var roleRewardsToGrant = new List<RoleRewardItem>();
        var currencyRewardsToGrant = new List<CurrencyRewardItem>();
        var competitionUpdates = new List<CompetitionUpdateItem>();

        try
        {
            // Process each user's XP in a guild
            foreach (var group in groupedItems)
            {
                var guildId = group.Key.GuildId;
                var userId = group.Key.UserId;
                var totalXp = group.Sum(x => x.Amount);
                var sources = string.Join(", ", group.Select(x => x.Source.ToString()).Distinct());
                var lastItem = group.LastOrDefault();

                try
                {
                    // Create a separate connection for each user update to avoid conflicts
                    await using var db = await dbFactory.CreateConnectionAsync();

                    // Flag indicating whether we need to retry due to concurrency
                    var success = false;
                    var retryCount = 0;
                    const int maxRetries = 3;

                    // Keep retrying until success or max retries reached
                    while (!success && retryCount < maxRetries)
                    {
                        try
                        {
                            // Get user XP or create if doesn't exist using LinqToDB
                            var userXp = await db.GuildUserXps
                                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

                            var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);

                            if (userXp == null)
                            {
                                // Create new user XP record
                                userXp = new GuildUserXp
                                {
                                    GuildId = guildId,
                                    UserId = userId,
                                    TotalXp = totalXp,
                                    LastActivity = DateTime.UtcNow,
                                    LastLevelUp = DateTime.UtcNow,
                                    NotifyType = (int)XpNotificationType.Channel // Default
                                };

                                // Insert using LinqToDB
                                await db.InsertAsync(userXp);

                                // We successfully created the record
                                success = true;

                                // New users start at level 0, so check if they gained enough XP to level up
                                var newLevel = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType);
                                if (newLevel > 0 && userXp.NotifyType != (int)XpNotificationType.None)
                                {
                                    // Add notification for first level
                                    notificationsToSend.Add(new XpNotification
                                    {
                                        GuildId = guildId,
                                        UserId = userId,
                                        Level = newLevel,
                                        ChannelId = lastItem?.ChannelId ?? 0,
                                        NotificationType = (XpNotificationType)userXp.NotifyType,
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
                            }
                            else
                            {
                                // Calculate levels before update
                                var oldLevel = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType);

                                // Update XP and last activity
                                userXp.TotalXp += totalXp;
                                userXp.LastActivity = DateTime.UtcNow;

                                // Calculate new level
                                var newLevel = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType);

                                // Check for level up
                                if (newLevel > oldLevel)
                                {
                                    userXp.LastLevelUp = DateTime.UtcNow;

                                    // Add notification if enabled
                                    if ((XpNotificationType)userXp.NotifyType != XpNotificationType.None)
                                    {
                                        notificationsToSend.Add(new XpNotification
                                        {
                                            GuildId = guildId,
                                            UserId = userId,
                                            Level = newLevel,
                                            ChannelId = lastItem?.ChannelId ?? 0,
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

                                // Update using LinqToDB
                                await db.UpdateAsync(userXp);

                                // If we get here, the update was successful
                                success = true;
                            }

                            // Add competition updates if successful
                            if (success)
                            {
                                // Update competition XP
                                var activeCompetitions = await competitionManager.GetActiveCompetitionsAsync(guildId);
                                foreach (var competition in activeCompetitions)
                                {
                                    competitionUpdates.Add(new CompetitionUpdateItem
                                    {
                                        CompetitionId = competition.Id,
                                        UserId = userId,
                                        XpGained = totalXp,
                                        CurrentLevel = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType)
                                    });
                                }

                                // Update cache
                                cacheManager.UpdateUserXpCacheAsync(userXp);
                            }
                        }
                        catch (Exception ex) when (IsConcurrencyException(ex))
                        {
                            // Increment retry counter
                            retryCount++;

                            if (retryCount >= maxRetries)
                            {
                                Log.Warning(
                                    "Failed to update XP for user {UserId} in guild {GuildId} after {Retries} retries",
                                    userId, guildId, maxRetries);
                            }
                            else
                            {
                                // Wait a bit before retrying (with increasing delay)
                                await Task.Delay(50 * retryCount);

                                // Log the retry
                                Log.Debug("Retrying XP update for user {UserId} in guild {GuildId} (attempt {Attempt})",
                                    userId, guildId, retryCount + 1);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log any other exceptions and break the retry loop
                            Log.Error(ex, "Error updating XP for user {UserId} in guild {GuildId}", userId, guildId);
                            retryCount = maxRetries; // Force exit from loop
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing XP for user {UserId} in guild {GuildId}", userId, guildId);
                }
            }

            // Process notifications and rewards outside of individual user transactions
            await Task.WhenAll(
                rewardManager.SendNotificationsAsync(notificationsToSend),
                rewardManager.GrantRoleRewardsAsync(roleRewardsToGrant),
                rewardManager.GrantCurrencyRewardsAsync(currencyRewardsToGrant),
                competitionManager.UpdateCompetitionsAsync(await dbFactory.CreateConnectionAsync(), competitionUpdates)
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing XP group updates");
        }
    }

    /// <summary>
    /// Determines if an exception is related to concurrency conflicts
    /// </summary>
    private bool IsConcurrencyException(Exception ex)
    {
        // Check for specific database error codes related to concurrency
        if (ex is LinqToDBException linqEx)
        {
            // You may need to adjust these conditions based on your database provider
            return linqEx.Message.Contains("concurrency") ||
                   linqEx.Message.Contains("deadlock") ||
                   linqEx.Message.Contains("conflict");
        }

        // For SQL Server
        if (ex is Npgsql.PostgresException pgEx)
        {
            return pgEx.SqlState == "40P01" || // Deadlock detected
                   pgEx.SqlState == "55P03" || // Lock not available
                   pgEx.SqlState == "40001" || // Serialization failure
                   pgEx.SqlState == "40P02" || // Lock timeout
                   pgEx.SqlState == "57014";   // Query canceled (could be due to statement timeout)
        }

        return false;
    }

    /// <summary>
    ///     Processes XP decay for inactive users.
    /// </summary>
    private async void ProcessXpDecay(object state)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get all guild settings with XP decay enabled using LinqToDB
            var guildsWithDecay = await db.GuildXpSettings
                .Where(g => g.EnableXpDecay)
                .ToListAsync();

            if (guildsWithDecay.Count == 0)
                return;

            foreach (var settings in guildsWithDecay)
            {
                try
                {
                    var inactiveThreshold = DateTime.UtcNow.AddDays(-settings.InactivityDaysBeforeDecay);

                    // Find inactive users in this guild using LinqToDB
                    var inactiveUsers = await db.GuildUserXps
                        .Where(x => x.GuildId == settings.GuildId && x.LastActivity < inactiveThreshold)
                        .ToListAsync();

                    if (inactiveUsers.Count == 0)
                        continue;

                    Log.Information("Processing XP decay for {Count} inactive users in guild {GuildId}",
                        inactiveUsers.Count, settings.GuildId);

                    foreach (var user in inactiveUsers)
                    {
                        var decayAmount = (long)(user.TotalXp * (settings.DailyDecayPercentage / 100.0));

                        if (decayAmount > 0)
                        {
                            user.TotalXp -= decayAmount;

                            // Ensure total XP doesn't go below 0
                            if (user.TotalXp < 0)
                                user.TotalXp = 0;

                            // Update using LinqToDB
                            await db.UpdateAsync(user);

                            // Update cache
                            cacheManager.UpdateUserXpCacheAsync(user);
                        }
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
    ///     Cleans up expired cache entries.
    /// </summary>
    private async void CleanupCaches(object state)
    {
        try
        {
            Log.Debug("Running cache cleanup task");
            await cacheManager.CleanupCachesAsync();
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
        processingTimer.Dispose();
        decayTimer.Dispose();
        cleanupTimer.Dispose();
        dbThrottle.Dispose();

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
            // Try to process any remaining items in the queue
            while (!xpUpdateQueue.IsEmpty)
            {
                ProcessXpBatch(null!);
                Thread.Sleep(100); // Short delay between batches
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing final XP batches");
        }
    }
}