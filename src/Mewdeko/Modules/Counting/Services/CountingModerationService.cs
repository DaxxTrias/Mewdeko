using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Counting.Common;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Modules.Counting.Services;

/// <summary>
/// Service for handling moderation actions related to counting violations.
/// </summary>
public class CountingModerationService : INService
{
    // Cache keys
    private const string UserBanCacheKey = "counting_user_ban_{0}_{1}";
    private const string ModerationConfigCacheKey = "counting_moderation_config_{0}";
    private const string DefaultConfigCacheKey = "counting_moderation_defaults_{0}";
    private const string LastHintMessageCacheKey = "counting_last_hint_{0}";

    private readonly IMemoryCache cache;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<CountingModerationService> logger;
    private readonly GeneratedBotStrings strings;
    private readonly UserPunishService userPunishService;

    /// <summary>
    /// Initializes a new instance of the CountingModerationService.
    /// </summary>
    public CountingModerationService(
        IDataConnectionFactory dbFactory,
        IMemoryCache cache,
        ILogger<CountingModerationService> logger,
        DiscordShardedClient client,
        UserPunishService userPunishService, GeneratedBotStrings strings)
    {
        this.dbFactory = dbFactory;
        this.cache = cache;
        this.logger = logger;
        this.client = client;
        this.userPunishService = userPunishService;
        this.strings = strings;
    }

    /// <summary>
    /// Handles when a user submits a wrong number.
    /// </summary>
    public async Task HandleCountingErrorAsync(ulong channelId, ulong userId, long actualNumber, long expectedNumber)
    {
        try
        {
            var config = await GetModerationConfigAsync(channelId);
            if (config == null || !config.EnableModeration)
                return;

            // Log the violation
            await LogViolationAsync(channelId, userId, CountingViolationType.WrongNumber,
                $"Expected: {expectedNumber}, Got: {actualNumber}");

            // Track wrong count in time window
            var wrongCount = await TrackWrongCountAsync(channelId, userId, config.TimeWindowHours);

            // Check for tiered punishments
            await CheckAndApplyTieredPunishmentsAsync(channelId, userId, wrongCount);
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
        var cacheKey = string.Format(UserBanCacheKey, channelId, userId);

        if (cache.TryGetValue(cacheKey, out bool isBanned))
            return isBanned;

        await using var db = await dbFactory.CreateConnectionAsync();

        var activeBan = await db.CountingUserBans
            .Where(x => x.ChannelId == channelId &&
                        x.UserId == userId &&
                        x.IsActive)
            .FirstOrDefaultAsync();

        if (activeBan == null)
        {
            cache.Set(cacheKey, false, TimeSpan.FromMinutes(5));
            return false;
        }

        // Check if ban has expired
        if (activeBan.ExpiresAt.HasValue)
        {
            var isStillBanned = DateTime.UtcNow < activeBan.ExpiresAt.Value;
            if (!isStillBanned)
            {
                // Ban has expired, deactivate it
                await db.CountingUserBans
                    .Where(x => x.Id == activeBan.Id)
                    .UpdateAsync(x => new CountingUserBans
                    {
                        IsActive = false
                    });
            }

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
    public async Task<bool> BanUserFromCountingAsync(ulong channelId, ulong userId, ulong bannedBy,
        TimeSpan? duration = null, string? reason = null)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var expiryTime = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : (DateTime?)null;

            // Create counting user ban entry
            var userBan = new CountingUserBans
            {
                ChannelId = channelId,
                UserId = userId,
                BannedBy = bannedBy,
                BannedAt = DateTime.UtcNow,
                ExpiresAt = expiryTime,
                Reason = reason ?? "Counting violations",
                IsActive = true
            };

            await db.InsertAsync(userBan);

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
            cache.Remove(string.Format(UserBanCacheKey, channelId, userId));

            // Notify user and moderators
            await NotifyUserBannedAsync(channelId, userId, duration, reason);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error banning user {UserId} from counting in channel {ChannelId}", userId, channelId);
            return false;
        }
    }

    /// <summary>
    /// Unbans a user from counting in a channel.
    /// </summary>
    public async Task<bool> UnbanUserFromCountingAsync(ulong channelId, ulong userId, ulong unbannedBy,
        string? reason = null)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var unbanEvent = new CountingEvents
            {
                ChannelId = channelId,
                EventType =
                    (int)CountingEventType.UserBanned, // We'll use negative event type or details to indicate unban
                UserId = unbannedBy,
                Timestamp = DateTime.UtcNow,
                Details = $"Unbanned user {userId}: {reason ?? "Manual unban"}"
            };

            await db.InsertAsync(unbanEvent);

            // Clear cache
            cache.Remove(string.Format(UserBanCacheKey, channelId, userId));

            // Also update CountingUserBans table
            await db.CountingUserBans
                .Where(x => x.ChannelId == channelId && x.UserId == userId && x.IsActive)
                .UpdateAsync(x => new CountingUserBans
                {
                    IsActive = false
                });

            logger.LogInformation("User {UserId} unbanned from counting in channel {ChannelId} by {UnbannedBy}",
                userId, channelId, unbannedBy);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unbanning user {UserId} from counting in channel {ChannelId}", userId,
                channelId);
            return false;
        }
    }

    /// <summary>
    /// Logs a counting violation.
    /// </summary>
    private async Task LogViolationAsync(ulong channelId, ulong userId, CountingViolationType violationType,
        string details)
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

    /// <summary>
    ///     Purges all bans for a specific channel.
    /// </summary>
    public async Task<bool> PurgeChannelBansAsync(ulong channelId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            await db.CountingUserBans.Where(x => x.ChannelId == channelId).DeleteAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error purging bans for channel {ChannelId}", channelId);
            return false;
        }
    }

    /// <summary>
    ///     Gets the effective moderation configuration for a channel.
    /// </summary>
    public async Task<CountingModerationEffectiveConfig?> GetModerationConfigAsync(ulong channelId)
    {
        var cacheKey = string.Format(ModerationConfigCacheKey, channelId);

        if (cache.TryGetValue(cacheKey, out CountingModerationEffectiveConfig? cachedConfig))
            return cachedConfig;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Get channel-specific config
        var channelConfig = await db.CountingModerationConfig
            .FirstOrDefaultAsync(x => x.ChannelId == channelId);

        // Get guild defaults
        var channel = client.GetChannel(channelId) as IGuildChannel;
        if (channel == null) return null;

        var guildDefaults = await GetGuildDefaultsAsync(channel.GuildId);

        // Combine channel config with defaults
        var effectiveConfig = new CountingModerationEffectiveConfig
        {
            ChannelId = channelId,
            EnableModeration = channelConfig?.EnableModeration ?? guildDefaults?.EnableModeration ?? false,
            WrongCountThreshold = channelConfig?.WrongCountThreshold ?? guildDefaults?.WrongCountThreshold ?? 3,
            TimeWindowHours = channelConfig?.TimeWindowHours ?? guildDefaults?.TimeWindowHours ?? 24,
            PunishmentAction =
                (PunishmentAction)(channelConfig?.PunishmentAction ?? guildDefaults?.PunishmentAction ?? 0),
            PunishmentDurationMinutes =
                channelConfig?.PunishmentDurationMinutes ?? guildDefaults?.PunishmentDurationMinutes ?? 0,
            PunishmentRoleId = channelConfig?.PunishmentRoleId ?? guildDefaults?.PunishmentRoleId,
            IgnoreRoles = ParseRoleIds(channelConfig?.IgnoreRoles ?? guildDefaults?.IgnoreRoles),
            DeleteIgnoredMessages =
                channelConfig?.DeleteIgnoredMessages ?? guildDefaults?.DeleteIgnoredMessages ?? false,
            RequiredRoles = ParseRoleIds(channelConfig?.RequiredRoles ?? guildDefaults?.RequiredRoles),
            BannedRoles = ParseRoleIds(channelConfig?.BannedRoles ?? guildDefaults?.BannedRoles),
            PunishNonNumbers = channelConfig?.PunishNonNumbers ?? guildDefaults?.PunishNonNumbers ?? false,
            DeleteNonNumbers = channelConfig?.DeleteNonNumbers ?? guildDefaults?.DeleteNonNumbers ?? true,
            PunishEdits = channelConfig?.PunishEdits ?? guildDefaults?.PunishEdits ?? false,
            DeleteEdits = channelConfig?.DeleteEdits ?? guildDefaults?.DeleteEdits ?? true
        };

        cache.Set(cacheKey, effectiveConfig, TimeSpan.FromMinutes(10));
        return effectiveConfig;
    }

    /// <summary>
    /// Gets guild-wide default moderation configuration.
    /// </summary>
    public async Task<CountingModerationDefaults?> GetGuildDefaultsAsync(ulong guildId)
    {
        var cacheKey = string.Format(DefaultConfigCacheKey, guildId);

        if (cache.TryGetValue(cacheKey, out CountingModerationDefaults? cachedDefaults))
            return cachedDefaults;

        await using var db = await dbFactory.CreateConnectionAsync();
        var defaults = await db.CountingModerationDefaults
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (defaults != null)
            cache.Set(cacheKey, defaults, TimeSpan.FromMinutes(30));

        return defaults;
    }

    /// <summary>
    /// Tracks wrong count for a user within a time window.
    /// </summary>
    public async Task<int> TrackWrongCountAsync(ulong channelId, ulong userId, int timeWindowHours)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var windowStart = DateTime.UtcNow.AddHours(-timeWindowHours);

        // Find existing window or create new one
        var existingWindow = await db.CountingUserWrongCounts
            .FirstOrDefaultAsync(x => x.ChannelId == channelId &&
                                      x.UserId == userId &&
                                      x.WindowStartAt >= windowStart);

        if (existingWindow != null)
        {
            // Update existing window
            await db.CountingUserWrongCounts
                .Where(x => x.Id == existingWindow.Id)
                .UpdateAsync(x => new CountingUserWrongCounts
                {
                    WrongCount = x.WrongCount + 1, LastWrongAt = DateTime.UtcNow
                });
            return existingWindow.WrongCount + 1;
        }
        else
        {
            // Create new window
            var newWindow = new CountingUserWrongCounts
            {
                ChannelId = channelId,
                UserId = userId,
                WrongCount = 1,
                WindowStartAt = DateTime.UtcNow,
                LastWrongAt = DateTime.UtcNow
            };
            await db.InsertAsync(newWindow);
            return 1;
        }
    }

    /// <summary>
    /// Checks if a user should be ignored based on their roles.
    /// </summary>
    public async Task<bool> ShouldIgnoreUserAsync(ulong channelId, IGuildUser user)
    {
        var config = await GetModerationConfigAsync(channelId);
        if (config == null) return false;

        var userRoleIds = user.RoleIds.ToHashSet();

        // Check if user has banned roles
        if (config.BannedRoles.Any(roleId => userRoleIds.Contains(roleId)))
            return true;

        // Check if user has required roles (if any are specified)
        if (config.RequiredRoles.Any() && !config.RequiredRoles.Any(roleId => userRoleIds.Contains(roleId)))
            return true;

        // Check if user has ignore roles
        if (config.IgnoreRoles.Any(roleId => userRoleIds.Contains(roleId)))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if ignored messages should be deleted.
    /// </summary>
    public async Task<bool> ShouldDeleteIgnoredMessagesAsync(ulong channelId, IGuildUser user)
    {
        var config = await GetModerationConfigAsync(channelId);
        if (config == null) return false;

        var userRoleIds = user.RoleIds.ToHashSet();
        return config.DeleteIgnoredMessages && config.IgnoreRoles.Any(roleId => userRoleIds.Contains(roleId));
    }

    /// <summary>
    /// Sets guild-wide default moderation configuration.
    /// </summary>
    public async Task<bool> SetGuildDefaultsAsync(ulong guildId, CountingModerationDefaults defaults)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var existing = await db.CountingModerationDefaults
                .FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (existing != null)
            {
                // Update existing
                await db.CountingModerationDefaults
                    .Where(x => x.Id == existing.Id)
                    .UpdateAsync(x => new CountingModerationDefaults
                    {
                        EnableModeration = defaults.EnableModeration,
                        WrongCountThreshold = defaults.WrongCountThreshold,
                        TimeWindowHours = defaults.TimeWindowHours,
                        PunishmentAction = defaults.PunishmentAction,
                        PunishmentDurationMinutes = defaults.PunishmentDurationMinutes,
                        PunishmentRoleId = defaults.PunishmentRoleId,
                        IgnoreRoles = defaults.IgnoreRoles,
                        DeleteIgnoredMessages = defaults.DeleteIgnoredMessages,
                        RequiredRoles = defaults.RequiredRoles,
                        BannedRoles = defaults.BannedRoles,
                        UpdatedAt = DateTime.UtcNow
                    });
            }
            else
            {
                // Insert new
                defaults.GuildId = guildId;
                await db.InsertAsync(defaults);
            }

            // Clear cache
            cache.Remove(string.Format(DefaultConfigCacheKey, guildId));

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting guild defaults for guild {GuildId}", guildId);
            return false;
        }
    }

    /// <summary>
    /// Sets channel-specific moderation configuration.
    /// </summary>
    public async Task<bool> SetChannelConfigAsync(ulong channelId, CountingModerationConfig config)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var existing = await db.CountingModerationConfig
                .FirstOrDefaultAsync(x => x.ChannelId == channelId);

            if (existing != null)
            {
                // Update existing
                await db.CountingModerationConfig
                    .Where(x => x.Id == existing.Id)
                    .UpdateAsync(x => new CountingModerationConfig
                    {
                        UseDefaults = config.UseDefaults,
                        EnableModeration = config.EnableModeration,
                        WrongCountThreshold = config.WrongCountThreshold,
                        TimeWindowHours = config.TimeWindowHours,
                        PunishmentAction = config.PunishmentAction,
                        PunishmentDurationMinutes = config.PunishmentDurationMinutes,
                        PunishmentRoleId = config.PunishmentRoleId,
                        IgnoreRoles = config.IgnoreRoles,
                        DeleteIgnoredMessages = config.DeleteIgnoredMessages,
                        RequiredRoles = config.RequiredRoles,
                        BannedRoles = config.BannedRoles,
                        UpdatedAt = DateTime.UtcNow
                    });
            }
            else
            {
                // Insert new
                config.ChannelId = channelId;
                await db.InsertAsync(config);
            }

            // Clear cache
            cache.Remove(string.Format(ModerationConfigCacheKey, channelId));

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting channel config for channel {ChannelId}", channelId);
            return false;
        }
    }

    /// <summary>
    ///     Parses comma-separated role IDs into a list.
    /// </summary>
    private static List<ulong> ParseRoleIds(string? roleIdsString)
    {
        if (string.IsNullOrEmpty(roleIdsString))
            return new List<ulong>();

        return roleIdsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => ulong.TryParse(x.Trim(), out _))
            .Select(x => ulong.Parse(x.Trim()))
            .ToList();
    }

    /// <summary>
    /// Checks for and applies tiered punishments based on wrong count.
    /// </summary>
    public async Task CheckAndApplyTieredPunishmentsAsync(ulong channelId, ulong userId, int wrongCount)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var channel = client.GetChannel(channelId) as IGuildChannel;
            if (channel == null) return;

            // Get punishments for this channel and guild defaults
            var punishments = await db.CountingModerationPunishments
                .Where(x => x.GuildId == channel.GuildId &&
                            (x.ChannelId == channelId || x.ChannelId == null) &&
                            x.Count == wrongCount)
                .OrderBy(x => x.ChannelId.HasValue ? 0 : 1) // Channel-specific first, then guild defaults
                .ToListAsync();

            var punishment = punishments.FirstOrDefault();
            if (punishment == null) return;

            var guild = channel.Guild;
            var user = await guild.GetUserAsync(userId);
            if (user == null) return;

            // Apply the punishment
            await userPunishService.ApplyPunishment(guild, user, client.CurrentUser,
                (PunishmentAction)punishment.Punishment, punishment.Time, punishment.RoleId,
                $"Wrong count #{wrongCount} reached");

            // Log the applied punishment
            var appliedPunishment = new CountingAppliedPunishments
            {
                ChannelId = channelId,
                UserId = userId,
                PunishmentAction = punishment.Punishment,
                DurationMinutes = punishment.Time,
                RoleId = punishment.RoleId,
                WrongCountAtApplication = wrongCount,
                Reason = $"Tiered punishment for {wrongCount} wrong counts",
                ExpiresAt = punishment.Time > 0 ? DateTime.UtcNow.AddMinutes(punishment.Time) : null
            };

            await db.InsertAsync(appliedPunishment);

            logger.LogInformation(
                "Applied tiered punishment {PunishmentAction} to user {UserId} in channel {ChannelId} for {WrongCount} wrong counts",
                (PunishmentAction)punishment.Punishment, userId, channelId, wrongCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying tiered punishment to user {UserId} in channel {ChannelId}", userId,
                channelId);
        }
    }

    /// <summary>
    /// Sets a tiered punishment for a specific wrong count.
    /// </summary>
    public async Task<bool> SetTieredPunishmentAsync(ulong guildId, ulong? channelId, int count,
        PunishmentAction punishment, int time = 0, ulong? roleId = null)
    {
        // Validate parameters similar to UserPunishService.WarnPunish
        if (punishment is PunishmentAction.Softban or PunishmentAction.Kick or PunishmentAction.RemoveRoles &&
            time > 0)
            return false;

        if (count <= 0 || time > 0 && time > TimeSpan.FromDays(49).TotalMinutes)
            return false;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Remove existing punishment for this count
            await db.CountingModerationPunishments
                .Where(x => x.GuildId == guildId && x.ChannelId == channelId && x.Count == count)
                .DeleteAsync();

            // Add new punishment
            var newPunishment = new CountingModerationPunishments
            {
                GuildId = guildId,
                ChannelId = channelId,
                Count = count,
                Punishment = (int)punishment,
                Time = time,
                RoleId = punishment == PunishmentAction.AddRole ? roleId : null
            };

            await db.InsertAsync(newPunishment);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error setting tiered punishment for guild {GuildId}, channel {ChannelId}, count {Count}",
                guildId, channelId, count);
            return false;
        }
    }

    /// <summary>
    /// Removes a tiered punishment for a specific wrong count.
    /// </summary>
    public async Task<bool> RemoveTieredPunishmentAsync(ulong guildId, ulong? channelId, int count)
    {
        if (count <= 0) return false;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var rowsAffected = await db.CountingModerationPunishments
                .Where(x => x.GuildId == guildId && x.ChannelId == channelId && x.Count == count)
                .DeleteAsync();

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error removing tiered punishment for guild {GuildId}, channel {ChannelId}, count {Count}",
                guildId, channelId, count);
            return false;
        }
    }

    /// <summary>
    /// Gets all tiered punishments for a guild/channel.
    /// </summary>
    public async Task<List<CountingModerationPunishments>> GetTieredPunishmentsAsync(ulong guildId,
        ulong? channelId = null)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            return await db.CountingModerationPunishments
                .Where(x => x.GuildId == guildId && (channelId == null || x.ChannelId == channelId))
                .OrderBy(x => x.Count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting tiered punishments for guild {GuildId}, channel {ChannelId}",
                guildId, channelId);
            return new List<CountingModerationPunishments>();
        }
    }

    /// <summary>
    /// Clears wrong counts for a specific user in a channel.
    /// </summary>
    public async Task<bool> ClearUserWrongCountsAsync(ulong channelId, ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var rowsAffected = await db.CountingUserWrongCounts
                .Where(x => x.ChannelId == channelId && x.UserId == userId)
                .DeleteAsync();

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing wrong counts for user {UserId} in channel {ChannelId}", userId,
                channelId);
            return false;
        }
    }

    /// <summary>
    ///     Clears wrong counts for all users in a channel.
    /// </summary>
    public async Task<bool> ClearChannelWrongCountsAsync(ulong channelId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var rowsAffected = await db.CountingUserWrongCounts
                .Where(x => x.ChannelId == channelId)
                .DeleteAsync();

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing wrong counts for channel {ChannelId}", channelId);
            return false;
        }
    }

    /// <summary>
    /// Gets current wrong count for a user in a channel.
    /// </summary>
    public async Task<int> GetUserWrongCountAsync(ulong channelId, ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var config = await GetModerationConfigAsync(channelId);
            if (config == null) return 0;

            var windowStart = DateTime.UtcNow.AddHours(-config.TimeWindowHours);

            var wrongCount = await db.CountingUserWrongCounts
                .Where(x => x.ChannelId == channelId && x.UserId == userId && x.WindowStartAt >= windowStart)
                .SumAsync(x => x.WrongCount);

            return wrongCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting wrong count for user {UserId} in channel {ChannelId}", userId,
                channelId);
            return 0;
        }
    }

    /// <summary>
    /// Handles non-number messages in counting channels.
    /// </summary>
    public async Task HandleNonNumberMessageAsync(ulong channelId, ulong userId, IUserMessage message)
    {
        try
        {
            var config = await GetModerationConfigAsync(channelId);
            if (config == null) return;

            // Delete message if configured
            if (config.DeleteNonNumbers)
            {
                try
                {
                    await message.DeleteAsync();
                }
                catch
                {
                    // Ignore deletion errors
                }
            }

            // Apply punishment if configured
            if (config.PunishNonNumbers)
            {
                await LogViolationAsync(channelId, userId, CountingViolationType.NonNumberMessage,
                    $"Non-number message: {message.Content}");

                var wrongCount = await TrackWrongCountAsync(channelId, userId, config.TimeWindowHours);
                await CheckAndApplyTieredPunishmentsAsync(channelId, userId, wrongCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling non-number message from user {UserId} in channel {ChannelId}", userId,
                channelId);
        }
    }

    /// <summary>
    /// Handles message edits in counting channels.
    /// </summary>
    public async Task HandleMessageEditAsync(ulong channelId, ulong userId, IUserMessage? originalMessage,
        IUserMessage editedMessage)
    {
        try
        {
            var config = await GetModerationConfigAsync(channelId);
            if (config == null) return;

            // Send "last number was x" message (prevent duplicates with cache)
            var hintCacheKey = string.Format(LastHintMessageCacheKey, channelId);
            if (!cache.TryGetValue(hintCacheKey, out _))
            {
                try
                {
                    var channel = editedMessage.Channel as ITextChannel;
                    var countingChannel = await GetCountingChannelAsync(channelId);
                    if (countingChannel != null)
                    {
                        var nextNumber = countingChannel.CurrentNumber + countingChannel.Increment;
                        var embed = new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription(strings.CountingLastNumberWas(channel.GuildId,
                                countingChannel.CurrentNumber, nextNumber))
                            .Build();
                        await channel.SendMessageAsync(embed: embed);

                        // Cache to prevent duplicates for 5 seconds
                        cache.Set(hintCacheKey, true, TimeSpan.FromSeconds(5));
                    }
                }
                catch
                {
                    // ignored
                }
            }

            // Delete edited message if configured
            if (config.DeleteEdits)
            {
                try
                {
                    await editedMessage.DeleteAsync();
                }
                catch
                {
                    // Ignore deletion errors
                }
            }

            // Apply punishment if configured
            if (config.PunishEdits)
            {
                var logMessage = originalMessage != null
                    ? $"Edited message from '{originalMessage.Content}' to '{editedMessage.Content}'"
                    : $"Edited message to '{editedMessage.Content}' (original not cached)";

                await LogViolationAsync(channelId, userId, CountingViolationType.MessageEdit, logMessage);

                var wrongCount = await TrackWrongCountAsync(channelId, userId, config.TimeWindowHours);
                await CheckAndApplyTieredPunishmentsAsync(channelId, userId, wrongCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling message edit from user {UserId} in channel {ChannelId}", userId,
                channelId);
        }
    }

    /// <summary>
    ///     Gets counting channel info (needed for last number message).
    /// </summary>
    private async Task<CountingChannel?> GetCountingChannelAsync(ulong channelId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            return await db.CountingChannels
                .FirstOrDefaultAsync(x => x.ChannelId == channelId && x.IsActive);
        }
        catch
        {
            return null;
        }
    }
}