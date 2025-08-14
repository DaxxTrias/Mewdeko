using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Modules.Counting.Common;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Modules.Counting.Services;

/// <summary>
/// Service for managing counting statistics and user performance tracking.
/// </summary>
public class CountingStatsService : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly IMemoryCache cache;
    private readonly ILogger<CountingStatsService> logger;

    // Cache keys
    private const string USER_STATS_CACHE_KEY = "counting_user_stats_{0}_{1}";
    private const string LEADERBOARD_CACHE_KEY = "counting_leaderboard_{0}_{1}";

    /// <summary>
    /// Initializes a new instance of the CountingStatsService.
    /// </summary>
    public CountingStatsService(
        IDataConnectionFactory dbFactory,
        IMemoryCache cache,
        ILogger<CountingStatsService> logger)
    {
        this.dbFactory = dbFactory;
        this.cache = cache;
        this.logger = logger;
    }

    /// <summary>
    /// Updates user statistics after a successful count.
    /// </summary>
    public async Task UpdateUserStatsAsync(ulong channelId, ulong userId, long number)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get or create user stats
            var stats = await db.CountingStats
                .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == userId);

            if (stats == null)
            {
                stats = new CountingStats
                {
                    ChannelId = channelId,
                    UserId = userId,
                    ContributionsCount = 1,
                    CurrentStreak = 1,
                    HighestStreak = 1,
                    LastContribution = DateTime.UtcNow,
                    TotalNumbersCounted = number,
                    ErrorsCount = 0,
                    Accuracy = 100.0,
                    TotalTimeSpent = 0
                };
                await db.InsertAsync(stats);
            }
            else
            {
                // Calculate time between counts
                var timeSinceLastCount = stats.LastContribution.HasValue
                    ? (DateTime.UtcNow - stats.LastContribution.Value).TotalSeconds
                    : 0;

                // Update streak
                var newStreak = stats.CurrentStreak + 1;
                var newHighestStreak = Math.Max(stats.HighestStreak, newStreak);

                // Calculate new accuracy
                var totalAttempts = stats.ContributionsCount + stats.ErrorsCount + 1;
                var successfulCounts = stats.ContributionsCount + 1;
                var newAccuracy = (double)successfulCounts / totalAttempts * 100.0;

                await db.CountingStats
                    .Where(x => x.Id == stats.Id)
                    .UpdateAsync(x => new CountingStats
                    {
                        ContributionsCount = x.ContributionsCount + 1,
                        CurrentStreak = newStreak,
                        HighestStreak = newHighestStreak,
                        LastContribution = DateTime.UtcNow,
                        TotalNumbersCounted = x.TotalNumbersCounted + number,
                        Accuracy = newAccuracy,
                        TotalTimeSpent = x.TotalTimeSpent + (long)timeSinceLastCount
                    });
            }

            // Clear cache
            cache.Remove(string.Format(USER_STATS_CACHE_KEY, channelId, userId));
            ClearLeaderboardCache(channelId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating user stats for user {UserId} in channel {ChannelId}", userId, channelId);
        }
    }

    /// <summary>
    /// Increments error count for a user and updates accuracy.
    /// </summary>
    public async Task IncrementUserErrorsAsync(ulong channelId, ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get or create user stats
            var stats = await db.CountingStats
                .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == userId);

            if (stats == null)
            {
                stats = new CountingStats
                {
                    ChannelId = channelId,
                    UserId = userId,
                    ContributionsCount = 0,
                    CurrentStreak = 0,
                    HighestStreak = 0,
                    LastContribution = DateTime.UtcNow,
                    TotalNumbersCounted = 0,
                    ErrorsCount = 1,
                    Accuracy = 0.0,
                    TotalTimeSpent = 0
                };
                await db.InsertAsync(stats);
            }
            else
            {
                // Reset current streak on error
                var totalAttempts = stats.ContributionsCount + stats.ErrorsCount + 1;
                var successfulCounts = stats.ContributionsCount;
                var newAccuracy = totalAttempts > 0 ? (double)successfulCounts / totalAttempts * 100.0 : 0.0;

                await db.CountingStats
                    .Where(x => x.Id == stats.Id)
                    .UpdateAsync(x => new CountingStats
                    {
                        CurrentStreak = 0,
                        ErrorsCount = x.ErrorsCount + 1,
                        Accuracy = newAccuracy
                    });
            }

            // Clear cache
            cache.Remove(string.Format(USER_STATS_CACHE_KEY, channelId, userId));
            ClearLeaderboardCache(channelId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error incrementing user errors for user {UserId} in channel {ChannelId}", userId, channelId);
        }
    }

    /// <summary>
    /// Gets user statistics for a specific channel.
    /// </summary>
    public async Task<CountingStats?> GetUserStatsAsync(ulong channelId, ulong userId)
    {
        var cacheKey = string.Format(USER_STATS_CACHE_KEY, channelId, userId);

        if (cache.TryGetValue(cacheKey, out CountingStats? cachedStats))
            return cachedStats;

        await using var db = await dbFactory.CreateConnectionAsync();
        var stats = await db.CountingStats
            .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == userId);

        if (stats != null)
        {
            cache.Set(cacheKey, stats, TimeSpan.FromMinutes(5));
        }

        return stats;
    }

    /// <summary>
    /// Gets leaderboard for a counting channel.
    /// </summary>
    public async Task<List<CountingLeaderboardEntry>> GetLeaderboardAsync(ulong channelId, LeaderboardType type = LeaderboardType.Contributions, int limit = 10)
    {
        var cacheKey = string.Format(LEADERBOARD_CACHE_KEY, channelId, type);

        if (cache.TryGetValue(cacheKey, out List<CountingLeaderboardEntry>? cachedLeaderboard))
            return cachedLeaderboard ?? new List<CountingLeaderboardEntry>();

        await using var db = await dbFactory.CreateConnectionAsync();

        var query = db.CountingStats
            .Where(x => x.ChannelId == channelId);

        query = type switch
        {
            LeaderboardType.Contributions => query.OrderByDescending(x => x.ContributionsCount),
            LeaderboardType.Streak => query.OrderByDescending(x => x.HighestStreak),
            LeaderboardType.Accuracy => query.OrderByDescending(x => x.Accuracy),
            LeaderboardType.TotalNumbers => query.OrderByDescending(x => x.TotalNumbersCounted),
            _ => query.OrderByDescending(x => x.ContributionsCount)
        };

        var stats = await query.Take(limit).ToListAsync();

        var leaderboard = stats.Select((stat, index) => new CountingLeaderboardEntry
        {
            Rank = index + 1,
            UserId = stat.UserId,
            ContributionsCount = stat.ContributionsCount,
            HighestStreak = stat.HighestStreak,
            CurrentStreak = stat.CurrentStreak,
            Accuracy = Math.Round(stat.Accuracy, 2),
            TotalNumbersCounted = stat.TotalNumbersCounted,
            LastContribution = stat.LastContribution
        }).ToList();

        cache.Set(cacheKey, leaderboard, TimeSpan.FromMinutes(10));
        return leaderboard;
    }

    /// <summary>
    /// Gets comprehensive statistics for a counting channel.
    /// </summary>
    public async Task<CountingChannelStats> GetChannelStatsAsync(ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var userStats = await db.CountingStats
            .Where(x => x.ChannelId == channelId)
            .ToListAsync();

        var totalParticipants = userStats.Count;
        var totalContributions = userStats.Sum(x => x.ContributionsCount);
        var totalErrors = userStats.Sum(x => x.ErrorsCount);
        var averageAccuracy = userStats.Any() ? userStats.Average(x => x.Accuracy) : 0;

        var topContributor = userStats
            .OrderByDescending(x => x.ContributionsCount)
            .FirstOrDefault();

        var milestoneCount = await db.CountingMilestones
            .CountAsync(x => x.ChannelId == channelId);

        var lastActivity = userStats
            .Where(x => x.LastContribution.HasValue)
            .OrderByDescending(x => x.LastContribution)
            .FirstOrDefault()?.LastContribution;

        return new CountingChannelStats
        {
            Channel = null!, // This will be filled by the calling service
            TotalParticipants = totalParticipants,
            TotalErrors = totalErrors,
            MilestonesReached = milestoneCount,
            TopContributor = topContributor,
            LastActivity = lastActivity,
            AverageAccuracy = Math.Round(averageAccuracy, 2)
        };
    }

    /// <summary>
    /// Resets all user streaks in a channel (used when channel is reset).
    /// </summary>
    public async Task ResetChannelStreaksAsync(ulong channelId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            await db.CountingStats
                .Where(x => x.ChannelId == channelId)
                .UpdateAsync(x => new CountingStats
                {
                    CurrentStreak = 0
                });

            // Clear all related caches
            ClearAllCacheForChannel(channelId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting streaks for channel {ChannelId}", channelId);
        }
    }

    /// <summary>
    /// Updates the leaderboard rankings for a channel.
    /// </summary>
    public async Task UpdateLeaderboardAsync(ulong channelId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Clear existing leaderboard
            await db.CountingLeaderboard
                .Where(x => x.ChannelId == channelId)
                .DeleteAsync();

            // Get top users by contributions
            var topUsers = await db.CountingStats
                .Where(x => x.ChannelId == channelId)
                .OrderByDescending(x => x.ContributionsCount)
                .ThenByDescending(x => x.HighestStreak)
                .ThenByDescending(x => x.Accuracy)
                .ToListAsync();

            // Insert new leaderboard entries
            var leaderboardEntries = topUsers.Select((stat, index) => new CountingLeaderboard
            {
                ChannelId = channelId,
                UserId = stat.UserId,
                Score = CalculateScore(stat),
                Rank = index + 1,
                LastUpdated = DateTime.UtcNow
            }).ToList();

            if (leaderboardEntries.Any())
            {
                await db.CountingLeaderboard.BulkCopyAsync(leaderboardEntries);
            }

            // Clear leaderboard cache
            ClearLeaderboardCache(channelId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating leaderboard for channel {ChannelId}", channelId);
        }
    }

    /// <summary>
    /// Calculates a score for a user based on their statistics.
    /// </summary>
    private static long CalculateScore(CountingStats stats)
    {
        // Weighted scoring: contributions (50%) + streak (30%) + accuracy (20%)
        var contributionScore = stats.ContributionsCount * 50;
        var streakScore = stats.HighestStreak * 30;
        var accuracyScore = (long)(stats.Accuracy * 2); // Max 200 points for 100% accuracy

        return contributionScore + streakScore + accuracyScore;
    }

    /// <summary>
    /// Gets user ranking in a channel.
    /// </summary>
    public async Task<int?> GetUserRankAsync(ulong channelId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var userStats = await db.CountingStats
            .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == userId);

        if (userStats == null) return null;

        var rank = await db.CountingStats
            .Where(x => x.ChannelId == channelId)
            .CountAsync(x => x.ContributionsCount > userStats.ContributionsCount ||
                            (x.ContributionsCount == userStats.ContributionsCount && x.HighestStreak > userStats.HighestStreak) ||
                            (x.ContributionsCount == userStats.ContributionsCount && x.HighestStreak == userStats.HighestStreak && x.Accuracy > userStats.Accuracy));

        return rank + 1;
    }

    /// <summary>
    /// Clears all cache entries for a specific channel.
    /// </summary>
    private void ClearAllCacheForChannel(ulong channelId)
    {
        ClearLeaderboardCache(channelId);
    }

    /// <summary>
    /// Clears leaderboard cache for a channel.
    /// </summary>
    private void ClearLeaderboardCache(ulong channelId)
    {
        foreach (var type in Enum.GetValues<LeaderboardType>())
        {
            cache.Remove(string.Format(LEADERBOARD_CACHE_KEY, channelId, type));
        }
    }
}

/// <summary>
/// Types of leaderboards for counting channels.
/// </summary>
public enum LeaderboardType
{
    /// <summary>
    /// Leaderboard sorted by number of contributions.
    /// </summary>
    Contributions,

    /// <summary>
    /// Leaderboard sorted by highest streak achieved.
    /// </summary>
    Streak,

    /// <summary>
    /// Leaderboard sorted by accuracy percentage.
    /// </summary>
    Accuracy,

    /// <summary>
    /// Leaderboard sorted by total of all numbers counted.
    /// </summary>
    TotalNumbers
}