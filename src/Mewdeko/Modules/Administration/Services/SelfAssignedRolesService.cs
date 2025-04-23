using DataModel;
using LinqToDB;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     The service for managing self-assigned roles.
/// </summary>
public class SelfAssignedRolesService(IDataConnectionFactory dbFactory, GuildSettingsService gss) : INService
{
    /// <summary>
    ///     Enum representing the possible results of an assign operation.
    /// </summary>
    public enum AssignResult
    {
        /// <summary>
        ///     The role was successfully assigned.
        /// </summary>
        Assigned, // successfully removed

        /// <summary>
        ///     The role is not assignable.
        /// </summary>
        ErrNotAssignable, // not assignable (error)

        /// <summary>
        ///     The user already has the role.
        /// </summary>
        ErrAlreadyHave, // you already have that role (error)

        /// <summary>
        ///     The bot doesn't have the necessary permissions.
        /// </summary>
        ErrNotPerms, // bot doesn't have perms (error)

        /// <summary>
        ///     The user does not meet the level requirement.
        /// </summary>
        ErrLvlReq // you are not required level (error)
    }

    /// <summary>
    ///     Enum representing the possible results of a remove operation.
    /// </summary>
    public enum RemoveResult
    {
        /// <summary>
        ///     The role was successfully removed.
        /// </summary>
        Removed, // successfully removed

        /// <summary>
        ///     The role is not assignable.
        /// </summary>
        ErrNotAssignable, // not assignable (error)

        /// <summary>
        ///     The user does not have the role.
        /// </summary>
        ErrNotHave, // you don't have a role you want to remove (error)

        /// <summary>
        ///     The bot doesn't have the necessary permissions.
        /// </summary>
        ErrNotPerms // bot doesn't have perms (error)
    }

   /// <summary>
    ///     Adds a new self-assignable role to a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the role to.</param>
    /// <param name="role">The role to add.</param>
    /// <param name="group">The group number for the role.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> AddNew(ulong guildId, IRole role, int group)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.GetTable<SelfAssignableRole>()
            .AnyAsync(s => s.RoleId == role.Id && s.GuildId == guildId).ConfigureAwait(false);

        if (exists) return false;

        var newSar = new SelfAssignableRole()
        {
            Group = group, RoleId = role.Id, GuildId = guildId
        };
        await db.InsertAsync(newSar).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    ///     Toggles the auto-deletion of self-assigned role messages for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the setting for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating the new value of the
    ///     setting.
    /// </returns>
    public async Task<bool> ToggleAdSarm(ulong guildId)
    {
        var config = await gss.GetGuildConfig(guildId);
        if (config == null) return false; // Or default, or throw

        config.AutoDeleteSelfAssignedRoleMessages = !config.AutoDeleteSelfAssignedRoleMessages;
        await gss.UpdateGuildConfig(guildId, config);

        return config.AutoDeleteSelfAssignedRoleMessages;
    }

    /// <summary>
    ///     Assigns a self-assignable role to a guild user.
    /// </summary>
    /// <param name="guildUser">The guild user to assign the role to.</param>
    /// <param name="role">The role to assign.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains the result of the operation, a boolean
    ///     indicating whether auto-deletion is enabled, and an extra object containing additional information about the
    ///     operation.
    /// </returns>
    public async Task<(AssignResult Result, bool AutoDelete, object? extra)> Assign(IGuildUser guildUser, IRole role)
    {
        object? extra = null;
        var (autoDelete, exclusive, roles) = await GetAdAndRoles(guildUser.Guild.Id);

        if (roles == null)
        {
             Log.Warning("SelfAssignableRoles collection is null for Guild {GuildId}", guildUser.GuildId);
             return (AssignResult.ErrNotAssignable, autoDelete, null);
        }

        var selfAssignedRoles = roles as SelfAssignableRole[] ?? roles.ToArray();
        var theRoleYouWant = Array.Find<SelfAssignableRole>(selfAssignedRoles, r => r.RoleId == role.Id);

        if (theRoleYouWant == null)
            return (AssignResult.ErrNotAssignable, autoDelete, null);

        if (guildUser.RoleIds.Contains(role.Id))
            return (AssignResult.ErrAlreadyHave, autoDelete, null);

        if (exclusive)
        {
            var roleIdsInGroup = selfAssignedRoles
                .Where<SelfAssignableRole>(x => x.Group == theRoleYouWant.Group && x.RoleId != role.Id)
                .Select(x => x.RoleId)
                .ToHashSet();

            var rolesToRemove = guildUser.RoleIds
                .Where(userRoleId => roleIdsInGroup.Contains(userRoleId))
                .Select(id => guildUser.Guild.GetRole(id))
                .Where(r => r != null)
                .ToList();

            if (rolesToRemove.Any())
            {
                 try
                 {
                     await guildUser.RemoveRolesAsync(rolesToRemove).ConfigureAwait(false);
                     await Task.Delay(300).ConfigureAwait(false);
                 }
                 catch (Exception ex)
                 {
                     Log.Warning(ex, "Error removing exclusive roles for SAR assignment for user {UserId}", guildUser.Id);
                 }
            }
        }

        try
        {
            await guildUser.AddRoleAsync(role).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to add role {RoleId} to user {UserId}", role.Id, guildUser.Id);
            extra = ex;
            return (AssignResult.ErrNotPerms, autoDelete, extra);
        }

        return (AssignResult.Assigned, autoDelete, null);
    }

    /// <summary>
    ///     Sets the name of a self-assignable role group in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the name for.</param>
    /// <param name="group">The group number to set the name for.</param>
    /// <param name="name">The new name for the group.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> SetNameAsync(ulong guildId, int group, string name)
    {
        var set = false;
        await using var db = await dbFactory.CreateConnectionAsync();

        var toUpdate = await db.GetTable<GroupName>()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Number == group).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(name))
        {
            if (toUpdate != null)
                await db.DeleteAsync(toUpdate).ConfigureAwait(false);
        }
        else if (toUpdate == null)
        {
            await db.InsertAsync(new GroupName
            {
                GuildId = guildId, Name = name, Number = group
            }).ConfigureAwait(false);
            set = true;
        }
        else
        {
            toUpdate.Name = name;
            await db.UpdateAsync(toUpdate).ConfigureAwait(false);
            set = true;
        }
        return set;
    }

    /// <summary>
    ///     Removes a self-assignable role from a guild user.
    /// </summary>
    /// <param name="guildUser">The guild user to remove the role from.</param>
    /// <param name="role">The role to remove.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains the result of the operation and a boolean
    ///     indicating whether auto-deletion is enabled.
    /// </returns>
    public async Task<(RemoveResult Result, bool AutoDelete)> Remove(IGuildUser guildUser, IRole role)
    {
        var (autoDelete, _, roles) = await GetAdAndRoles(guildUser.Guild.Id);

        if (roles == null || !roles.Any(r => r.RoleId == role.Id))
            return (RemoveResult.ErrNotAssignable, autoDelete);

        if (!guildUser.RoleIds.Contains(role.Id))
            return (RemoveResult.ErrNotHave, autoDelete);

        try
        {
            await guildUser.RemoveRoleAsync(role).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to remove role {RoleId} from user {UserId}", role.Id, guildUser.Id);
            return (RemoveResult.ErrNotPerms, autoDelete);
        }

        return (RemoveResult.Removed, autoDelete);
    }

    /// <summary>
    ///     Removes a self-assignable role from a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to remove the role from.</param>
    /// <param name="roleId">The ID of the role to remove.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> RemoveSar(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.SelfAssignableRoles
            .Where(x => x.GuildId == guildId && x.RoleId == roleId)
            .DeleteAsync().ConfigureAwait(false);
        return deletedCount > 0;
    }

    /// <summary>
    ///     Retrieves the auto-delete, exclusive, and self-assignable roles for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the information for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a tuple with a boolean indicating whether
    ///     auto-deletion is enabled, a boolean indicating whether exclusive self-assignable roles are enabled, and a
    ///     collection of self-assignable roles. Returns null for the roles collection if config not found.
    /// </returns>
    public async Task<(bool AutoDelete, bool Exclusive, IEnumerable<SelfAssignableRole>? Roles)> GetAdAndRoles(ulong guildId)
    {
        var config = await gss.GetGuildConfig(guildId);
        if (config == null)
            return (false, false, null); // Return default flags and null roles if config is missing

        await using var db = await dbFactory.CreateConnectionAsync();
        var roles = await db.SelfAssignableRoles
            .Where(x => x.GuildId == guildId)
            .ToListAsync().ConfigureAwait(false);

        return (config.AutoDeleteSelfAssignedRoleMessages, config.ExclusiveSelfAssignedRoles, roles);
    }

    /// <summary>
    ///     Sets the level requirement for a self-assignable role in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the level requirement in.</param>
    /// <param name="role">The role to set the level requirement for.</param>
    /// <param name="level">The new level requirement.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> SetLevelReq(ulong guildId, IRole role, int level)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var sar = await db.SelfAssignableRoles
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == role.Id).ConfigureAwait(false);

        if (sar != null)
        {
            sar.LevelRequirement = level;
            await db.UpdateAsync(sar).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Toggles the exclusive self-assignable roles setting for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the setting for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating the new value of the
    ///     setting.
    /// </returns>
    public async Task<bool> ToggleEsar(ulong guildId)
    {
        var config = await gss.GetGuildConfig(guildId);
        if (config == null) return false; // Or default, or throw

        config.ExclusiveSelfAssignedRoles = !config.ExclusiveSelfAssignedRoles;
        await gss.UpdateGuildConfig(guildId, config);

        return config.ExclusiveSelfAssignedRoles;
    }

    /// <summary>
    ///     Retrieves the exclusive setting, self-assignable roles, and group names for a guild.
    /// </summary>
    /// <param name="guild">The guild to retrieve the information for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a tuple with a boolean indicating whether
    ///     exclusive self-assignable roles are enabled, a collection of tuples containing self-assignable role models and
    ///     their corresponding roles, and a dictionary mapping group numbers to their names.
    /// </returns>
    public async Task<(bool Exclusive, IEnumerable<(SelfAssignableRole Model, IRole Role)> Roles, IDictionary<int, string> GroupNames)> GetRoles(IGuild guild)
    {
        var config = await gss.GetGuildConfig(guild.Id);
        var exclusive = config?.ExclusiveSelfAssignedRoles ?? false;

        await using var db = await dbFactory.CreateConnectionAsync();

        var groupNamesEntities = await db.GetTable<GroupName>()
            .Where(x => x.GuildId == guild.Id)
            .ToListAsync().ConfigureAwait(false);
        var groupNames = groupNamesEntities.ToDictionary(x => x.Number, x => x.Name);

        var roleModels = await db.SelfAssignableRoles
            .Where(x => x.GuildId == guild.Id)
            .ToListAsync().ConfigureAwait(false);

        var rolesWithIRole = roleModels
            .Select(x => (Model: x, Role: guild.GetRole(x.RoleId)))
            .ToList(); // ToList to avoid multiple enumerations

        var modelsToRemove = rolesWithIRole
            .Where(x => x.Role == null) // Find models where the Discord role doesn't exist anymore
            .Select(x => x.Model)
            .ToList();

        if (modelsToRemove.Any())
        {
            try
            {
                var idsToRemove = modelsToRemove.Select(m => m.Id).ToList();
                await db.SelfAssignableRoles
                    .Where(x => idsToRemove.Contains(x.Id))
                    .DeleteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                 Log.Error(ex, "Failed to remove non-existent roles from SelfAssignableRoles for Guild {GuildId}", guild.Id);
            }
        }

        var validRoles = rolesWithIRole.Where(x => x.Role != null)!; // Filter out nulls after potential removal attempt

        return (exclusive, validRoles, groupNames);
    }
}