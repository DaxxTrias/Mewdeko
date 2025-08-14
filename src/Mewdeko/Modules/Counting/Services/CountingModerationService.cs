using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Counting.Common;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Modules.Counting.Services;

/// <summary>
/// Service for handling moderation actions related to counting violations.
/// </summary>
public class CountingModerationService : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly IMemoryCache cache;
    private readonly ILogger<CountingModerationService> logger;
    private readonly DiscordShardedClient client;

    // Cache keys
    private const string USER_BAN_CACHE_KEY = "counting_user_ban_{0}_{1}";
    private const string CHANNEL_VIOLATIONS_CACHE_KEY = "counting_violations_{0}";

    /// <summary>
    /// Initializes a new instance of the CountingModerationService.
    /// </summary>
    public CountingModerationService(
        IDataConnectionFactory dbFactory,
        IMemoryCache cache,
        ILogger<CountingModerationService> logger,
        DiscordShardedClient client)
    {
        this.dbFactory = dbFactory;
        this.cache = cache;
        this.logger = logger;
        this.client = client;
    }

    /// <summary>
    /// Handles when a user submits a wrong number.
    /// </summary>
    public async Task HandleCountingErrorAsync(ulong channelId, ulong userId, long actualNumber, long expectedNumber)
    {
        try
        {
            // Log the violation
            await LogViolationAsync(channelId, userId, CountingViolationType.WrongNumber,
                $"Expected: {expectedNumber}, Got: {actualNumber}");

            // Check if user has too many recent violations
            var violationCount = await GetRecentViolationCountAsync(channelId, userId);

            if (violationCount >= 5) // Configurable threshold
            {
                await ApplyTemporaryTimeoutAsync(channelId, userId, TimeSpan.FromMinutes(5));
            }
            else if (violationCount >= 10)
            {
                await BanUserFromCountingAsync(channelId, userId, TimeSpan.FromHours(1),
                    "Too many counting violations");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling counting error for user {UserId} in channel {ChannelId}",
                userId, channelId);
        }
    }

    /// <summary>
    /// Handles when the maximum number is reached in a channel.
    /// </summary>
    public async Task HandleMaxNumberReachedAsync(ulong channelId, ulong userId, long maxNumber)
    {
        try
        {
            await LogViolationAsync(channelId, userId, CountingViolationType.MaxNumberReached,
                $"Max number {maxNumber} reached");

            // Could trigger special rewards or channel reset here
            await NotifyModeratorsAsync(channelId,
                $"Maximum number {maxNumber} reached by <@{userId}> in <#{channelId}>");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling max number reached for user {UserId} in channel {ChannelId}",
                userId, channelId);
        }
    }

    /// <summary>
    /// Checks if a user is banned from counting in a specific channel.
    /// </summary>
    public async Task<bool> IsUserBannedAsync(ulong channelId, ulong userId)
    {
        var cacheKey = string.Format(USER_BAN_CACHE_KEY, channelId, userId);

        if (cache.TryGetValue(cacheKey, out bool isBanned))
            return isBanned;

        await using var db = await dbFactory.CreateConnectionAsync();

        var activeBan = await db.CountingEvents
            .Where(x => x.ChannelId == channelId &&
                       x.UserId == userId &&
                       x.EventType == (int)CountingEventType.UserBanned)
            .OrderByDescending(x => x.Timestamp)
            .FirstOrDefaultAsync();

        if (activeBan == null)
        {
            cache.Set(cacheKey, false, TimeSpan.FromMinutes(5));
            return false;
        }

        // Check if ban has expired (if details contain expiry info)
        if (activeBan.Details != null &&
            DateTime.TryParse(activeBan.Details.Split('|').LastOrDefault(), out var expiryTime))
        {
            var isStillBanned = DateTime.UtcNow < expiryTime;
            cache.Set(cacheKey, isStillBanned, TimeSpan.FromMinutes(5));
            return isStillBanned;
        }

        // Permanent ban
        cache.Set(cacheKey, true, TimeSpan.FromMinutes(5));
        return true;
    }

    /// <summary>
    /// Bans a user from counting in a channel for a specified duration.
    /// </summary>
    public async Task BanUserFromCountingAsync(ulong channelId, ulong userId, TimeSpan? duration = null, string? reason = null)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var expiryTime = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : (DateTime?)null;
            var details = $"{reason ?? "Counting violations"}";
            if (expiryTime.HasValue)
                details += $"|{expiryTime:O}";

            var banEvent = new CountingEvents
            {
                ChannelId = channelId,
                EventType = (int)CountingEventType.UserBanned,
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Details = details
            };

            await db.InsertAsync(banEvent);

            // Clear cache
            cache.Remove(string.Format(USER_BAN_CACHE_KEY, channelId, userId));

            // Notify user and moderators
            await NotifyUserBannedAsync(channelId, userId, duration, reason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error banning user {UserId} from counting in channel {ChannelId}", userId, channelId);
        }
    }

    /// <summary>
    /// Unbans a user from counting in a channel.
    /// </summary>
    public async Task UnbanUserFromCountingAsync(ulong channelId, ulong userId, ulong unbannedBy, string? reason = null)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var unbanEvent = new CountingEvents
            {
                ChannelId = channelId,
                EventType = (int)CountingEventType.UserBanned, // We'll use negative event type or details to indicate unban
                UserId = unbannedBy,
                Timestamp = DateTime.UtcNow,
                Details = $"Unbanned user {userId}: {reason ?? "Manual unban"}"
            };

            await db.InsertAsync(unbanEvent);

            // Clear cache
            cache.Remove(string.Format(USER_BAN_CACHE_KEY, channelId, userId));

            logger.LogInformation("User {UserId} unbanned from counting in channel {ChannelId} by {UnbannedBy}",
                userId, channelId, unbannedBy);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unbanning user {UserId} from counting in channel {ChannelId}", userId, channelId);
        }
    }

    /// <summary>
    /// Applies a temporary timeout to a user for counting violations.
    /// </summary>
    public async Task ApplyTemporaryTimeoutAsync(ulong channelId, ulong userId, TimeSpan duration)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var timeoutEvent = new CountingEvents
            {
                ChannelId = channelId,
                EventType = (int)CountingEventType.UserTimeout,
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Details = $"Timeout for {duration.TotalMinutes} minutes due to violations"
            };

            await db.InsertAsync(timeoutEvent);

            // Set cache entry for timeout duration
            var cacheKey = string.Format(USER_BAN_CACHE_KEY, channelId, userId);
            cache.Set(cacheKey, true, duration);

            logger.LogInformation("User {UserId} timed out from counting in channel {ChannelId} for {Duration}",
                userId, channelId, duration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying timeout to user {UserId} in channel {ChannelId}", userId, channelId);
        }
    }

    /// <summary>
    /// Gets the number of recent violations for a user.
    /// </summary>
    private async Task<int> GetRecentViolationCountAsync(ulong channelId, ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var cutoffTime = DateTime.UtcNow.AddHours(-1); // Look at violations in the last hour

            return await db.CountingEvents
                .Where(x => x.ChannelId == channelId &&
                           x.UserId == userId &&
                           x.EventType == (int)CountingEventType.WrongNumber &&
                           x.Timestamp >= cutoffTime)
                .CountAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting violation count for user {UserId} in channel {ChannelId}", userId, channelId);
            return 0;
        }
    }

    /// <summary>
    /// Logs a counting violation.
    /// </summary>
    private async Task LogViolationAsync(ulong channelId, ulong userId, CountingViolationType violationType, string details)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var violationEvent = new CountingEvents
            {
                ChannelId = channelId,
                EventType = (int)CountingEventType.WrongNumber, // Map violation types to event types as needed
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                Details = $"{violationType}: {details}"
            };

            await db.InsertAsync(violationEvent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error logging violation for user {UserId} in channel {ChannelId}", userId, channelId);
        }
    }

    /// <summary>
    /// Notifies moderators about important counting events.
    /// </summary>
    private async Task NotifyModeratorsAsync(ulong channelId, string message)
    {
        try
        {
            var channel = client.GetChannel(channelId) as ITextChannel;
            var guild = channel?.Guild;

            if (guild != null)
            {
                // Find moderators or log channel to notify
                // This would depend on the guild's configuration
                logger.LogInformation("Moderator notification for channel {ChannelId}: {Message}", channelId, message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error notifying moderators for channel {ChannelId}", channelId);
        }
    }

    /// <summary>
    /// Notifies a user that they've been banned from counting.
    /// </summary>
    private async Task NotifyUserBannedAsync(ulong channelId, ulong userId, TimeSpan? duration, string? reason)
    {
        try
        {
            var user = client.GetUser(userId);
            if (user != null)
            {
                var durationText = duration.HasValue
                    ? $"for {duration.Value.TotalMinutes} minutes"
                    : "permanently";

                var message = $"You have been banned from counting in <#{channelId}> {durationText}.";
                if (!string.IsNullOrEmpty(reason))
                    message += $"\nReason: {reason}";

                try
                {
                    await user.SendMessageAsync(message);
                }
                catch
                {
                    // User might have DMs disabled, that's okay
                }
            }

            logger.LogInformation("User {UserId} banned from counting in channel {ChannelId}", userId, channelId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error notifying banned user {UserId} for channel {ChannelId}", userId, channelId);
        }
    }

    /// <summary>
    /// Gets violation statistics for a channel.
    /// </summary>
    public async Task<CountingViolationStats> GetViolationStatsAsync(ulong channelId, TimeSpan? period = null)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var cutoffTime = period.HasValue ? DateTime.UtcNow.Subtract(period.Value) : DateTime.MinValue;

            var violations = await db.CountingEvents
                .Where(x => x.ChannelId == channelId &&
                           x.Timestamp >= cutoffTime &&
                           (x.EventType == (int)CountingEventType.WrongNumber ||
                            x.EventType == (int)CountingEventType.UserTimeout ||
                            x.EventType == (int)CountingEventType.UserBanned))
                .ToListAsync();

            return new CountingViolationStats
            {
                TotalViolations = violations.Count,
                WrongNumberCount = violations.Count(x => x.EventType == (int)CountingEventType.WrongNumber),
                TimeoutCount = violations.Count(x => x.EventType == (int)CountingEventType.UserTimeout),
                BanCount = violations.Count(x => x.EventType == (int)CountingEventType.UserBanned),
                TopViolators = violations
                    .GroupBy(x => x.UserId)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => new UserViolationSummary
                    {
                        UserId = g.Key,
                        ViolationCount = g.Count(),
                        LastViolation = g.Max(x => x.Timestamp) ?? DateTime.MinValue
                    })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting violation stats for channel {ChannelId}", channelId);
            return new CountingViolationStats();
        }
    }
}

/// <summary>
/// Types of counting violations.
/// </summary>
public enum CountingViolationType
{
    /// <summary>
    /// User submitted wrong number.
    /// </summary>
    WrongNumber,

    /// <summary>
    /// User counted consecutively when not allowed.
    /// </summary>
    ConsecutiveCounting,

    /// <summary>
    /// User exceeded rate limit.
    /// </summary>
    RateLimit,

    /// <summary>
    /// Maximum number was reached.
    /// </summary>
    MaxNumberReached,

    /// <summary>
    /// User used invalid number format.
    /// </summary>
    InvalidFormat,

    /// <summary>
    /// User lacks required role.
    /// </summary>
    InsufficientPermissions
}

/// <summary>
/// Statistics about violations in a counting channel.
/// </summary>
public class CountingViolationStats
{
    /// <summary>
    /// Total number of violations.
    /// </summary>
    public int TotalViolations { get; set; }

    /// <summary>
    /// Number of wrong number violations.
    /// </summary>
    public int WrongNumberCount { get; set; }

    /// <summary>
    /// Number of timeouts applied.
    /// </summary>
    public int TimeoutCount { get; set; }

    /// <summary>
    /// Number of bans applied.
    /// </summary>
    public int BanCount { get; set; }

    /// <summary>
    /// Top violators in the channel.
    /// </summary>
    public List<UserViolationSummary> TopViolators { get; set; } = new();
}

/// <summary>
/// Summary of violations for a specific user.
/// </summary>
public class UserViolationSummary
{
    /// <summary>
    /// The user's Discord ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Total number of violations by this user.
    /// </summary>
    public int ViolationCount { get; set; }

    /// <summary>
    /// When the user's last violation occurred.
    /// </summary>
    public DateTime LastViolation { get; set; }
}