using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.RoleStates.Services;

/// <summary>
///     Provides services for managing user role states within a guild. This includes saving roles before a user leaves or
///     is banned, and optionally restoring them upon rejoining.
/// </summary>
public class RoleStatesService : INService
{
    private readonly DbContextProvider dbProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RoleStatesService" /> class.
    /// </summary>
    /// <param name="dbProvider">The database service to interact with stored data.</param>
    /// <param name="eventHandler">The event handler to subscribe to guild member events.</param>
    public RoleStatesService(DbContextProvider dbProvider, EventHandler eventHandler)
    {
        this.dbProvider = dbProvider;
        eventHandler.UserLeft += OnUserLeft;
        eventHandler.UserBanned += OnUserBanned;
        eventHandler.UserJoined += OnUserJoined;
    }

    private async Task OnUserBanned(SocketUser args, SocketGuild arsg2)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        if (args is not SocketGuildUser usr) return;
        var roleStateSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == arsg2.Id);
        if (roleStateSettings is null || !roleStateSettings.Enabled || !roleStateSettings.ClearOnBan) return;

        var roleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == arsg2.Id && x.UserId == usr.Id);
        if (roleState is null) return;
        dbContext.Remove(roleState);
        await dbContext.SaveChangesAsync();
    }

    private async Task OnUserJoined(IGuildUser usr)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var roleStateSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == usr.Guild.Id);
        if (roleStateSettings is null || !roleStateSettings.Enabled) return;

        if (roleStateSettings.IgnoreBots && usr.IsBot) return;

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? []
            : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

        if (deniedUsers.Contains(usr.Id)) return;

        var roleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == usr.Guild.Id && x.UserId == usr.Id);
        if (roleState is null || string.IsNullOrWhiteSpace(roleState.SavedRoles)) return;

        var savedRoleIds = roleState.SavedRoles.Split(',').Select(ulong.Parse).ToList();

        if (savedRoleIds.Any())
        {
            try
            {
                await usr.AddRolesAsync(savedRoleIds);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to assign roles to {User} in {Guild}. Most likely missing permissions\n{Exception}",
                    usr.Username, usr.Guild, ex);
            }
        }
    }


    private async Task OnUserLeft(IGuild args, IUser args2)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var roleStateSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == args.Id);
        if (roleStateSettings is null || !roleStateSettings.Enabled) return;

        if (roleStateSettings.IgnoreBots && args2.IsBot) return;

        var deniedRoles = string.IsNullOrWhiteSpace(roleStateSettings.DeniedRoles)
            ? []
            : roleStateSettings.DeniedRoles.Split(',').Select(ulong.Parse).ToList();

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? []
            : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

        if (deniedUsers.Contains(args2.Id)) return;

        if (args2 is not SocketGuildUser usr) return;

        var rolesToSave = usr.Roles.Where(x => !x.IsManaged && !x.IsEveryone).Select(x => x.Id);
        if (deniedRoles.Any())
        {
            rolesToSave = rolesToSave.Except(deniedRoles);
        }

        if (!rolesToSave.Any()) return;

        var roleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == args.Id && x.UserId == usr.Id);
        if (roleState is null)
        {
            var newRoleState = new UserRoleStates
            {
                UserName = usr.ToString(),
                GuildId = args.Id,
                UserId = usr.Id,
                SavedRoles = string.Join(",", rolesToSave)
            };
            await dbContext.UserRoleStates.AddAsync(newRoleState);
        }
        else
        {
            roleState.SavedRoles = string.Join(",", rolesToSave);
            dbContext.Update(roleState);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    ///     Toggles the role state feature on or off for a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating the operation success.</returns>
    public async Task<bool> ToggleRoleStates(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var roleStateSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (roleStateSettings is null)
        {
            var toAdd = new RoleStateSettings
            {
                GuildId = guildId, Enabled = true
            };
            await dbContext.RoleStateSettings.AddAsync(toAdd);
            await dbContext.SaveChangesAsync();
            return true;
        }

        roleStateSettings.Enabled = !roleStateSettings.Enabled;
        dbContext.RoleStateSettings.Update(roleStateSettings);
        await dbContext.SaveChangesAsync();
        return roleStateSettings.Enabled;
    }

    /// <summary>
    ///     Retrieves the role state settings for a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing the <see cref="RoleStateSettings" /> or null if
    ///     not found.
    /// </returns>
    public async Task<RoleStateSettings?> GetRoleStateSettings(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == guildId) ?? null;
    }

    /// <summary>
    ///     Retrieves a user's saved role state within a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing the <see cref="UserRoleStates" /> or null if not
    ///     found.
    /// </returns>
    public async Task<UserRoleStates?> GetUserRoleState(ulong guildId, ulong userId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId) ??
               null;
    }

    /// <summary>
    ///     Retrieves all user role states within a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of <see cref="UserRoleStates" />.</returns>
    public async Task<List<UserRoleStates>> GetAllUserRoleStates(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.UserRoleStates.Where(x => x.GuildId == guildId).ToListAsync();
    }

    /// <summary>
    ///     Updates the role state settings for a guild.
    /// </summary>
    /// <param name="roleStateSettings">The <see cref="RoleStateSettings" /> to be updated.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UpdateRoleStateSettings(RoleStateSettings roleStateSettings)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        dbContext.RoleStateSettings.Update(roleStateSettings);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    ///     Toggles the option to ignore bots when saving and restoring roles.
    /// </summary>
    /// <param name="roleStateSettings">The <see cref="RoleStateSettings" /> to be updated.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating if bots are now ignored.</returns>
    public async Task<bool> ToggleIgnoreBots(RoleStateSettings roleStateSettings)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        roleStateSettings.IgnoreBots = !roleStateSettings.IgnoreBots;

        dbContext.RoleStateSettings.Update(roleStateSettings);
        await dbContext.SaveChangesAsync();

        return roleStateSettings.IgnoreBots;
    }

    /// <summary>
    ///     Toggles the option to clear saved roles upon a user's ban.
    /// </summary>
    /// <param name="roleStateSettings">The <see cref="RoleStateSettings" /> to be updated.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a boolean indicating if roles are cleared on
    ///     ban.
    /// </returns>
    public async Task<bool> ToggleClearOnBan(RoleStateSettings roleStateSettings)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        roleStateSettings.ClearOnBan = !roleStateSettings.ClearOnBan;

        dbContext.RoleStateSettings.Update(roleStateSettings);
        await dbContext.SaveChangesAsync();

        return roleStateSettings.ClearOnBan;
    }

    /// <summary>
    ///     Adds roles to a user's saved role state.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="roleIds">The roles to be added.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a tuple with a boolean indicating success and an
    ///     optional error message.
    /// </returns>
    public async Task<(bool, string)> AddRolesToUserRoleState(ulong guildId, ulong userId, IEnumerable<ulong> roleIds)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var userRoleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (userRoleState == null)
        {
            return (false, "No role state found for this user.");
        }

        var savedRoleIds = userRoleState.SavedRoles.Split(',').Select(ulong.Parse).ToList();
        var anyRoleAdded = false;

        foreach (var roleId in roleIds.Where(roleId => !savedRoleIds.Contains(roleId)))
        {
            savedRoleIds.Add(roleId);
            anyRoleAdded = true;
        }

        if (!anyRoleAdded)
        {
            return (false, "No roles to add.");
        }

        userRoleState.SavedRoles = string.Join(",", savedRoleIds);
        dbContext.Update(userRoleState);
        await dbContext.SaveChangesAsync();

        return (true, "");
    }

    /// <summary>
    ///     Removes roles from a user's saved role state.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="roleIds">The roles to be removed.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a tuple with a boolean indicating success and an
    ///     optional error message.
    /// </returns>
    public async Task<(bool, string)> RemoveRolesFromUserRoleState(ulong guildId, ulong userId,
        IEnumerable<ulong> roleIds)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var userRoleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (userRoleState == null)
        {
            return (false, "No role state found for this user.");
        }

        var savedRoleIds = userRoleState.SavedRoles.Split(',').Select(ulong.Parse).ToList();
        var anyRoleRemoved = false;

        foreach (var roleId in roleIds.Where(roleId => savedRoleIds.Contains(roleId)))
        {
            savedRoleIds.Remove(roleId);
            anyRoleRemoved = true;
        }

        if (!anyRoleRemoved)
        {
            return (false, "No roles to remove.");
        }

        userRoleState.SavedRoles = string.Join(",", savedRoleIds);
        dbContext.Update(userRoleState);
        await dbContext.SaveChangesAsync();

        return (true, "");
    }

    /// <summary>
    ///     Deletes a user's role state.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a boolean indicating if the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> DeleteUserRoleState(ulong userId, ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var userRoleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
        if (userRoleState is null) return false;
        dbContext.UserRoleStates.Remove(userRoleState);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Applies the saved role state from one user to another.
    /// </summary>
    /// <param name="sourceUserId">The source user's unique identifier.</param>
    /// <param name="targetUser">The target <see cref="IGuildUser" />.</param>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a boolean indicating if the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> ApplyUserRoleStateToAnotherUser(ulong sourceUserId, IGuildUser targetUser, ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();


        var sourceUserRoleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == sourceUserId);

        if (sourceUserRoleState is null || string.IsNullOrWhiteSpace(sourceUserRoleState.SavedRoles)) return false;


        var sourceUserSavedRoleIds = sourceUserRoleState.SavedRoles.Split(',').Select(ulong.Parse).ToList();
        var rolesToAssign = targetUser.Guild.Roles.Where(role => sourceUserSavedRoleIds.Contains(role.Id)).ToList();

        if (!rolesToAssign.Any()) return false;
        try
        {
            await targetUser.AddRolesAsync(rolesToAssign);
        }
        catch
        {
            Log.Error("Failed to assign roles to user {User}", targetUser.Username);
        }

        return true;
    }

    /// <summary>
    ///     Sets a user's role state manually.
    /// </summary>
    /// <param name="user">The <see cref="IUser" /> whose role state is to be set.</param>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="roles">The roles to be saved.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetRoleStateManually(IUser user, ulong guildId, IEnumerable<ulong> roles)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var userRoleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == user.Id);
        if (userRoleState is null)
        {
            userRoleState = new UserRoleStates
            {
                GuildId = guildId, UserId = user.Id, UserName = user.ToString(), SavedRoles = string.Join(",", roles)
            };
            await dbContext.UserRoleStates.AddAsync(userRoleState);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            userRoleState.SavedRoles = string.Join(",", roles);
            dbContext.UserRoleStates.Update(userRoleState);
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    ///     Transfers role states from one guild to another, automatically mapping roles by name.
    /// </summary>
    /// <param name="sourceGuild">The source guild to transfer role states from.</param>
    /// <param name="targetGuild">The target guild to transfer role states to.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a tuple with the count of transferred role states
    ///     and any error message.
    /// </returns>
    public async Task<(int TransferCount, string ErrorMessage)> TransferRoleStates(
        IGuild sourceGuild,
        IGuild targetGuild)
    {
        if (sourceGuild.Id == targetGuild.Id)
            return (0, "Source and target guilds cannot be the same.");

        await using var dbContext = await dbProvider.GetContextAsync();

        // Check if role states are enabled for the source guild
        var sourceSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == sourceGuild.Id);
        var targetSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == targetGuild.Id);

        if (sourceSettings is null || !sourceSettings.Enabled)
            return (0, "Role states are not enabled for the source guild.");

        if (targetSettings is null)
        {
            // Create settings for target guild if they don't exist
            targetSettings = new RoleStateSettings
            {
                GuildId = targetGuild.Id,
                Enabled = true,
                IgnoreBots = sourceSettings.IgnoreBots,
                ClearOnBan = sourceSettings.ClearOnBan,
                DeniedRoles = "",
                DeniedUsers = ""
            };
            await dbContext.RoleStateSettings.AddAsync(targetSettings);
            await dbContext.SaveChangesAsync();
        }
        else if (!targetSettings.Enabled)
        {
            // Enable role states for target guild
            targetSettings.Enabled = true;
            dbContext.RoleStateSettings.Update(targetSettings);
            await dbContext.SaveChangesAsync();
        }

        // Get all user role states from the source guild
        var sourceRoleStates = await dbContext.UserRoleStates
            .Where(x => x.GuildId == sourceGuild.Id)
            .ToListAsync();

        if (!sourceRoleStates.Any())
            return (0, "No role states found in the source guild.");

        // Get all roles from both guilds to create a name-based mapping
        var sourceRoles = sourceGuild.Roles.ToDictionary(r => r.Id, r => r);
        var targetRoles = targetGuild.Roles;

        // Create role mapping based on role names
        var roleMapping = new Dictionary<ulong, ulong>();
        foreach (var sourceRole in sourceRoles.Values)
        {
            var matchingTargetRole = targetRoles.FirstOrDefault(r =>
                r.Name.Equals(sourceRole.Name, StringComparison.OrdinalIgnoreCase) &&
                !r.IsManaged &&
                r.Id != targetGuild.Id);

            if (matchingTargetRole != null)
            {
                roleMapping[sourceRole.Id] = matchingTargetRole.Id;
            }
        }

        // Log the role mapping results
        Log.Information("Found {MappedRoles} matching roles between guilds by name", roleMapping.Count);

        var transferCount = 0;
        var skippedCount = 0;

        foreach (var sourceState in sourceRoleStates)
        {
            // Check if a role state already exists for this user in the target guild
            var existingTargetState = await dbContext.UserRoleStates
                .FirstOrDefaultAsync(x => x.GuildId == targetGuild.Id && x.UserId == sourceState.UserId);

            // Skip if no roles to transfer
            if (string.IsNullOrWhiteSpace(sourceState.SavedRoles))
            {
                skippedCount++;
                continue;
            }

            // Parse saved roles
            var sourceRoleIds = sourceState.SavedRoles.Split(',').Select(ulong.Parse).ToList();

            // Map roles based on name mapping
            var targetRoleIds = new List<ulong>();
            foreach (var roleId in sourceRoleIds)
            {
                // Check if this role exists in the source guild
                if (!sourceRoles.TryGetValue(roleId, out var sourceRole))
                    continue;

                // Check if we have a mapping for this role
                if (roleMapping.TryGetValue(roleId, out var mappedRoleId))
                {
                    targetRoleIds.Add(mappedRoleId);
                }
            }

            if (!targetRoleIds.Any())
            {
                skippedCount++;
                continue;
            }

            // Create or update the role state in the target guild
            if (existingTargetState is null)
            {
                var newTargetState = new UserRoleStates
                {
                    UserName = sourceState.UserName,
                    GuildId = targetGuild.Id,
                    UserId = sourceState.UserId,
                    SavedRoles = string.Join(",", targetRoleIds)
                };
                await dbContext.UserRoleStates.AddAsync(newTargetState);
            }
            else
            {
                existingTargetState.SavedRoles = string.Join(",", targetRoleIds);
                dbContext.UserRoleStates.Update(existingTargetState);
            }

            transferCount++;
        }

        await dbContext.SaveChangesAsync();

        var resultMessage = transferCount > 0
            ? $"Successfully transferred {transferCount} role states to the target guild."
            : "No role states were transferred.";

        if (skippedCount > 0)
        {
            resultMessage += $" Skipped {skippedCount} users with no valid role mappings.";
        }

        return (transferCount, resultMessage);
    }

    /// <summary>
    ///     Saves the current role states of all users in a guild.
    /// </summary>
    /// <param name="guild">The guild to save role states for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a tuple with the count of saved role states
    ///     and any error message.
    /// </returns>
    public async Task<(int SavedCount, string ErrorMessage)> SaveAllUserRoleStates(IGuild guild)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        // Check if role states are enabled for this guild
        var roleStateSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == guild.Id);
        if (roleStateSettings is null || !roleStateSettings.Enabled)
            return (0, "Role states are not enabled for this guild.");

        // Get denied roles and users
        var deniedRoles = string.IsNullOrWhiteSpace(roleStateSettings.DeniedRoles)
            ? []
            : roleStateSettings.DeniedRoles.Split(',').Select(ulong.Parse).ToList();

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? []
            : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

        // Get all users from the guild
        var allUsers = await guild.GetUsersAsync();

        var savedCount = 0;

        foreach (var user in allUsers)
        {
            // Skip if user is denied
            if (deniedUsers.Contains(user.Id))
                continue;

            // Skip bots if configured to ignore them
            if (roleStateSettings.IgnoreBots && user.IsBot)
                continue;

            // Get user's current roles
            var roles = user.RoleIds.Where(r =>
            {
                var role = guild.GetRole(r);
                return role is { IsManaged: false } && role.Id != guild.Id;
            });

            // Filter out denied roles
            if (deniedRoles.Any())
            {
                roles = roles.Except(deniedRoles);
            }

            // Skip if no roles to save
            if (!roles.Any())
                continue;

            // Check if a role state already exists for this user
            var existingRoleState = await dbContext.UserRoleStates
                .FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.UserId == user.Id);

            // Create or update the role state
            if (existingRoleState is null)
            {
                var newRoleState = new UserRoleStates
                {
                    UserName = user.ToString(),
                    GuildId = guild.Id,
                    UserId = user.Id,
                    SavedRoles = string.Join(",", roles)
                };
                await dbContext.UserRoleStates.AddAsync(newRoleState);
            }
            else
            {
                existingRoleState.SavedRoles = string.Join(",", roles);
                dbContext.UserRoleStates.Update(existingRoleState);
            }

            savedCount++;
        }

        await dbContext.SaveChangesAsync();

        return (savedCount, savedCount > 0
            ? $"Successfully saved role states for {savedCount} users in the guild."
            : "No role states were saved.");
    }
}