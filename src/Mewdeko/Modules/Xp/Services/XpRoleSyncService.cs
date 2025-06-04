using DataModel;
using LinqToDB;
using Mewdeko.Modules.Xp.Models;
using Serilog;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Synchronizes XP role rewards for users in the guild.
/// </summary>
public class XpRoleSyncService : INService
{
    private readonly XpCacheManager cacheManager;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<ulong, bool> guildSyncInProgress = new();
    private readonly ConcurrentDictionary<ulong, DateTime> lastSyncTimes = new();
    private readonly XpService xpService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpRoleSyncService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="xpService">The xp service</param>
    /// <param name="cacheManager">The xp cache manager.</param>
    public XpRoleSyncService(IDataConnectionFactory dbFactory, XpService xpService, XpCacheManager cacheManager)
    {
        this.dbFactory = dbFactory;
        this.xpService = xpService;
        this.cacheManager = cacheManager;
    }

    /// <summary>
    ///     Synchronizes all users' roles in the specified guild based on their XP levels.
    /// </summary>
    /// <param name="guild">The guild to synchronize roles for.</param>
    /// <param name="progressCallback">Optional callback to report synchronization progress.</param>
    /// <returns>A task representing the asynchronous operation with sync results.</returns>
    public async Task<RoleSyncResult> SyncAllUsersAsync(IGuild guild,
        Func<RoleSyncProgress, Task>? progressCallback = null)
    {
        if (!guildSyncInProgress.TryAdd(guild.Id, true))
        {
            throw new InvalidOperationException("Guild sync already in progress");
        }

        try
        {
            var startTime = DateTime.UtcNow;
            var result = new RoleSyncResult
            {
                GuildId = guild.Id, StartTime = startTime
            };

            var batchData = await GetBatchSyncDataAsync(guild.Id);

            if (batchData.UsersWithXp.Count == 0)
            {
                result.CompletionTime = DateTime.UtcNow;
                return result;
            }

            if (batchData.RoleRewards.Count == 0)
            {
                result.CompletionTime = DateTime.UtcNow;
                result.SkippedUsers = batchData.UsersWithXp.Count;
                return result;
            }

            var allMembers = await guild.GetUsersAsync();
            var memberDict = allMembers.ToDictionary(u => u.Id, u => u);

            result.TotalUsers = batchData.UsersWithXp.Count;

            var lastProgressUpdate = DateTime.UtcNow;
            var progressUpdateInterval = TimeSpan.FromSeconds(10);

            for (var i = 0; i < batchData.UsersWithXp.Count; i++)
            {
                var userXp = batchData.UsersWithXp[i];

                try
                {
                    var syncResult = await SyncUserRolesBatchAsync(guild, userXp, batchData, memberDict);

                    result.ProcessedUsers++;
                    result.RolesAdded += syncResult.RolesAdded;
                    result.RolesRemoved += syncResult.RolesRemoved;
                    result.ErrorUsers += syncResult.HasError ? 1 : 0;

                    if (progressCallback != null &&
                        (DateTime.UtcNow - lastProgressUpdate >= progressUpdateInterval ||
                         i == batchData.UsersWithXp.Count - 1))
                    {
                        var progress = new RoleSyncProgress
                        {
                            CurrentUser = i + 1,
                            TotalUsers = batchData.UsersWithXp.Count,
                            PercentComplete = (double)(i + 1) / batchData.UsersWithXp.Count * 100,
                            EstimatedTimeRemaining =
                                TimeSpan.FromMilliseconds(50 * (batchData.UsersWithXp.Count - i - 1)),
                            RolesAdded = result.RolesAdded,
                            RolesRemoved = result.RolesRemoved,
                            ErrorCount = result.ErrorUsers
                        };

                        await progressCallback(progress);
                        lastProgressUpdate = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorUsers++;
                    Log.Warning(ex, "Failed to sync roles for user {UserId} in guild {GuildId}", userXp.UserId,
                        guild.Id);
                }
            }

            result.CompletionTime = DateTime.UtcNow;
            return result;
        }
        finally
        {
            guildSyncInProgress.TryRemove(guild.Id, out _);
        }
    }

    /// <summary>
    ///     Gets all data needed for role synchronization in batch operations.
    /// </summary>
    /// <param name="guildId">The guild ID to get data for.</param>
    /// <returns>A task representing the asynchronous operation with batch sync data.</returns>
    private async Task<BatchSyncData> GetBatchSyncDataAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var usersWithXp = await db.UserXpStats
            .Where(x => x.GuildId == guildId && x.Xp > 0)
            .ToListAsync();

        var roleRewards = await db.XpRoleRewards
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Level)
            .ToListAsync();

        var xpSettings = await cacheManager.GetGuildXpSettingsAsync(guildId);

        var userIds = usersWithXp.Select(u => u.UserId).ToList();
        var guildUserXps = await db.GuildUserXps
            .Where(x => x.GuildId == guildId && userIds.Contains(x.UserId))
            .ToListAsync();

        var userLevels = new Dictionary<ulong, int>();
        foreach (var userXp in guildUserXps)
        {
            var level = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)xpSettings.XpCurveType);
            userLevels[userXp.UserId] = level;
        }

        return new BatchSyncData
        {
            UsersWithXp = usersWithXp, RoleRewards = roleRewards, UserLevels = userLevels, XpSettings = xpSettings
        };
    }

    /// <summary>
    ///     Synchronizes roles for a specific user using pre-fetched batch data.
    /// </summary>
    /// <param name="guild">The guild containing the user.</param>
    /// <param name="userXp">The user's XP data.</param>
    /// <param name="batchData">Pre-fetched batch data containing all necessary information.</param>
    /// <param name="memberDict">Pre-fetched guild members dictionary.</param>
    /// <returns>A task representing the asynchronous operation with sync results.</returns>
    private async Task<UserRoleSyncResult> SyncUserRolesBatchAsync(
        IGuild guild,
        UserXpStat userXp,
        BatchSyncData batchData,
        Dictionary<ulong, IGuildUser> memberDict)
    {
        var result = new UserRoleSyncResult
        {
            UserId = userXp.UserId
        };

        try
        {
            if (!memberDict.TryGetValue(userXp.UserId, out var user))
            {
                result.HasError = true;
                result.ErrorMessage = "User not found in guild";
                return result;
            }

            var currentLevel = batchData.UserLevels.GetValueOrDefault(userXp.UserId, 0);

            var rolesToHave = batchData.RoleRewards.Where(r => r.Level <= currentLevel).Select(r => r.RoleId)
                .ToHashSet();
            var currentRoleRewardIds = user.RoleIds.Where(roleId => batchData.RoleRewards.Any(r => r.RoleId == roleId))
                .ToHashSet();

            var rolesToAdd = rolesToHave.Except(currentRoleRewardIds).ToList();
            var rolesToRemove = currentRoleRewardIds.Except(rolesToHave).ToList();

            foreach (var roleId in rolesToAdd)
            {
                try
                {
                    var role = guild.GetRole(roleId);
                    if (role == null) continue;
                    await user.AddRoleAsync(role);
                    result.RolesAdded++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to add role {RoleId} to user {UserId}", roleId, userXp.UserId);
                }
            }

            foreach (var roleId in rolesToRemove)
            {
                try
                {
                    var role = guild.GetRole(roleId);
                    if (role == null) continue;
                    await user.RemoveRoleAsync(role);
                    result.RolesRemoved++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to remove role {RoleId} from user {UserId}", roleId, userXp.UserId);
                }
            }

            result.Level = currentLevel;
            result.ProcessedSuccessfully = true;
        }
        catch (Exception ex)
        {
            result.HasError = true;
            result.ErrorMessage = ex.Message;
            Log.Error(ex, "Error syncing roles for user {UserId} in guild {GuildId}", userXp.UserId, guild.Id);
        }

        return result;
    }

    /// <summary>
    ///     Gets multiple user levels at once for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userIds">List of user IDs to get levels for.</param>
    /// <returns>A dictionary mapping user IDs to their levels.</returns>
    public async Task<Dictionary<ulong, int>> GetUserLevelsBatchAsync(ulong guildId, List<ulong> userIds)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var guildUserXps = await db.GuildUserXps
            .Where(x => x.GuildId == guildId && userIds.Contains(x.UserId))
            .ToListAsync();

        var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);

        var result = new Dictionary<ulong, int>();
        foreach (var userXp in guildUserXps)
        {
            var level = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType);
            result[userXp.UserId] = level;
        }

        return result;
    }

    /// <summary>
    ///     Synchronizes roles for a specific user based on their XP level.
    /// </summary>
    /// <param name="guild">The guild containing the user.</param>
    /// <param name="userId">The ID of the user to synchronize roles for.</param>
    /// <returns>A task representing the asynchronous operation with sync results.</returns>
    public async Task<UserRoleSyncResult> SyncUserRolesAsync(IGuild guild, ulong userId)
    {
        var lastSync = lastSyncTimes.GetValueOrDefault(userId, DateTime.MinValue);
        var timeSinceLastSync = DateTime.UtcNow - lastSync;

        if (timeSinceLastSync < TimeSpan.FromMinutes(5))
        {
            var remainingCooldown = TimeSpan.FromMinutes(5) - timeSinceLastSync;
            throw new InvalidOperationException(
                $"User sync on cooldown for {remainingCooldown.TotalMinutes:F1} more minutes");
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        var userXp = await db.UserXpStats
            .FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.UserId == userId);

        if (userXp == null)
        {
            return new UserRoleSyncResult
            {
                UserId = userId, HasError = true, ErrorMessage = "User has no XP data"
            };
        }

        var roleRewards = await db.XpRoleRewards
            .Where(x => x.GuildId == guild.Id)
            .OrderBy(x => x.Level)
            .ToListAsync();

        if (roleRewards.Count == 0)
        {
            return new UserRoleSyncResult
            {
                UserId = userId, HasError = true, ErrorMessage = "No role rewards configured"
            };
        }

        var result = await SyncUserRolesInternalAsync(guild, userXp, roleRewards);
        lastSyncTimes[userId] = DateTime.UtcNow;

        return result;
    }

    /// <summary>
    ///     Internal method to synchronize roles for a specific user.
    /// </summary>
    /// <param name="guild">The guild containing the user.</param>
    /// <param name="userXp">The user's XP data.</param>
    /// <param name="roleRewards">The available role rewards for the guild.</param>
    /// <returns>A task representing the asynchronous operation with sync results.</returns>
    private async Task<UserRoleSyncResult> SyncUserRolesInternalAsync(IGuild guild, UserXpStat userXp,
        List<XpRoleReward> roleRewards)
    {
        var result = new UserRoleSyncResult
        {
            UserId = userXp.UserId
        };

        try
        {
            var user = await guild.GetUserAsync(userXp.UserId);
            if (user == null)
            {
                result.HasError = true;
                result.ErrorMessage = "User not found in guild";
                return result;
            }

            var currentLevel = await xpService.GetUserLevelAsync(guild.Id, userXp.UserId);
            var rolesToHave = roleRewards.Where(r => r.Level <= currentLevel).Select(r => r.RoleId).ToHashSet();
            var currentRoleRewardIds =
                user.RoleIds.Where(roleId => roleRewards.Any(r => r.RoleId == roleId)).ToHashSet();

            var rolesToAdd = rolesToHave.Except(currentRoleRewardIds).ToList();
            var rolesToRemove = currentRoleRewardIds.Except(rolesToHave).ToList();

            foreach (var roleId in rolesToAdd)
            {
                try
                {
                    var role = guild.GetRole(roleId);
                    if (role == null) continue;
                    await user.AddRoleAsync(role);
                    result.RolesAdded++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to add role {RoleId} to user {UserId}", roleId, userXp.UserId);
                }
            }

            foreach (var roleId in rolesToRemove)
            {
                try
                {
                    var role = guild.GetRole(roleId);
                    if (role == null) continue;
                    await user.RemoveRoleAsync(role);
                    result.RolesRemoved++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to remove role {RoleId} from user {UserId}", roleId, userXp.UserId);
                }
            }

            result.Level = currentLevel;
            result.ProcessedSuccessfully = true;
        }
        catch (Exception ex)
        {
            result.HasError = true;
            result.ErrorMessage = ex.Message;
            Log.Error(ex, "Error syncing roles for user {UserId} in guild {GuildId}", userXp.UserId, guild.Id);
        }

        return result;
    }
}

/// <summary>
///     Batch data container for role synchronization.
/// </summary>
public class BatchSyncData
{
    /// <summary>
    ///     List of users with XP in the guild.
    /// </summary>
    public List<UserXpStat> UsersWithXp { get; set; } = new();

    /// <summary>
    ///     List of role rewards for the guild.
    /// </summary>
    public List<XpRoleReward> RoleRewards { get; set; } = new();

    /// <summary>
    ///     Pre-calculated user levels dictionary for fast lookups.
    /// </summary>
    public Dictionary<ulong, int> UserLevels { get; set; } = new();

    /// <summary>
    ///     Guild XP settings.
    /// </summary>
    public GuildXpSetting XpSettings { get; set; }
}