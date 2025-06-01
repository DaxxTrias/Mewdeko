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
    private readonly IDataConnectionFactory dbFactory;
    private readonly XpService xpService;
    private readonly ConcurrentDictionary<ulong, DateTime> lastSyncTimes = new();
    private readonly ConcurrentDictionary<ulong, bool> guildSyncInProgress = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpRoleSyncService"/> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    public XpRoleSyncService(IDataConnectionFactory dbFactory, XpService xpService)
    {
        this.dbFactory = dbFactory;
        this.xpService = xpService;
    }

    /// <summary>
    ///     Synchronizes all users' roles in the specified guild based on their XP levels.
    /// </summary>
    /// <param name="guild">The guild to synchronize roles for.</param>
    /// <param name="progressCallback">Optional callback to report synchronization progress.</param>
    /// <returns>A task representing the asynchronous operation with sync results.</returns>
    public async Task<RoleSyncResult> SyncAllUsersAsync(IGuild guild, Func<RoleSyncProgress, Task>? progressCallback = null)
    {
        if (!guildSyncInProgress.TryAdd(guild.Id, true))
        {
            throw new InvalidOperationException("Guild sync already in progress");
        }

        try
        {
            var startTime = DateTime.UtcNow;
            var result = new RoleSyncResult { GuildId = guild.Id, StartTime = startTime };

            await using var db = await dbFactory.CreateConnectionAsync();

            var usersWithXp = await db.UserXpStats
                .Where(x => x.GuildId == guild.Id && x.Xp > 0)
                .ToListAsync();

            if (usersWithXp.Count == 0)
            {
                result.CompletionTime = DateTime.UtcNow;
                return result;
            }

            var roleRewards = await db.XpRoleRewards
                .Where(x => x.GuildId == guild.Id)
                .OrderBy(x => x.Level)
                .ToListAsync();

            if (roleRewards.Count == 0)
            {
                result.CompletionTime = DateTime.UtcNow;
                result.SkippedUsers = usersWithXp.Count;
                return result;
            }

            result.TotalUsers = usersWithXp.Count;
            var estimatedTimePerUser = TimeSpan.FromMilliseconds(500);
            result.EstimatedCompletion = startTime.Add(TimeSpan.FromMilliseconds(estimatedTimePerUser.TotalMilliseconds * usersWithXp.Count));

            for (var i = 0; i < usersWithXp.Count; i++)
            {
                var userXp = usersWithXp[i];

                try
                {
                    var syncResult = await SyncUserRolesInternalAsync(guild, userXp, roleRewards);

                    result.ProcessedUsers++;
                    result.RolesAdded += syncResult.RolesAdded;
                    result.RolesRemoved += syncResult.RolesRemoved;
                    result.ErrorUsers += syncResult.HasError ? 1 : 0;

                    if (progressCallback != null)
                    {
                        var progress = new RoleSyncProgress
                        {
                            CurrentUser = i + 1,
                            TotalUsers = usersWithXp.Count,
                            PercentComplete = (double)(i + 1) / usersWithXp.Count * 100,
                            EstimatedTimeRemaining = TimeSpan.FromMilliseconds(estimatedTimePerUser.TotalMilliseconds * (usersWithXp.Count - i - 1)),
                            RolesAdded = result.RolesAdded,
                            RolesRemoved = result.RolesRemoved,
                            ErrorCount = result.ErrorUsers
                        };

                        await progressCallback(progress);
                    }

                    await Task.Delay(250);
                }
                catch (Exception ex)
                {
                    result.ErrorUsers++;
                    Log.Warning(ex, "Failed to sync roles for user {UserId} in guild {GuildId}", userXp.UserId, guild.Id);
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
            throw new InvalidOperationException($"User sync on cooldown for {remainingCooldown.TotalMinutes:F1} more minutes");
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        var userXp = await db.UserXpStats
            .FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.UserId == userId);

        if (userXp == null)
        {
            return new UserRoleSyncResult { UserId = userId, HasError = true, ErrorMessage = "User has no XP data" };
        }

        var roleRewards = await db.XpRoleRewards
            .Where(x => x.GuildId == guild.Id)
            .OrderBy(x => x.Level)
            .ToListAsync();

        if (roleRewards.Count == 0)
        {
            return new UserRoleSyncResult { UserId = userId, HasError = true, ErrorMessage = "No role rewards configured" };
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
    private async Task<UserRoleSyncResult> SyncUserRolesInternalAsync(IGuild guild, UserXpStat userXp, List<XpRoleReward> roleRewards)
    {
        var result = new UserRoleSyncResult { UserId = userXp.UserId };

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
            var currentRoleRewardIds = user.RoleIds.Where(roleId => roleRewards.Any(r => r.RoleId == roleId)).ToHashSet();

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
                    await Task.Delay(100);
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
                    await Task.Delay(100);
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