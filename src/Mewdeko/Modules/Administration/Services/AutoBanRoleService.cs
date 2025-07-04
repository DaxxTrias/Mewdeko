using DataModel;
using LinqToDB;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service for automatically banning users who are assigned a specified AutoBanRole.
/// </summary>
public class AutoBanRoleService : INService
{
    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutoBanRoleService" /> class.
    /// </summary>
    /// <param name="eventHandler">The event handler</param>
    /// <param name="dbFactory">The database connection factory</param>
    public AutoBanRoleService(EventHandler eventHandler, IDataConnectionFactory dbFactory)
    {
        this.dbFactory = dbFactory;
        var eventHandler1 = eventHandler;
        eventHandler1.Subscribe("GuildMemberUpdated", "AutoBanRoleService", OnGuildMemberUpdated);
    }

    /// <summary>
    ///     Handles the GuildMemberUpdated event to check for auto-ban roles.
    /// </summary>
    /// <param name="before">The cached state of the user before the update</param>
    /// <param name="after">The current state of the user after the update</param>
    private async Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
    {
        // Ensure we have the before state
        var beforeUser = before.HasValue ? before.Value : null;
        if (beforeUser == null)
            return;

        // Get newly added roles
        var addedRoles = after.Roles.Except(beforeUser.Roles).ToList();
        if (!addedRoles.Any())
            return;

        // Get auto-ban roles for this guild
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var autoBanRoleIds = await db.AutoBanRoles
            .Where(x => x.GuildId == after.Guild.Id)
            .Select(x => x.RoleId)
            .ToListAsync()
            .ConfigureAwait(false);

        // Check if any added role is an auto-ban role
        var rolesSet = autoBanRoleIds.ToHashSet();
        if (!addedRoles.Any(x => rolesSet.Contains(x.Id)))
            return;

        // Ban the user
        try
        {
            await after.Guild.AddBanAsync(after, 0, "Auto-ban role assigned").ConfigureAwait(false);
        }
        catch
        {
            // Log error if needed, but don't throw
        }
    }

    /// <summary>
    ///     Gets all AutoBanRole IDs for a specific guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get auto-ban roles for</param>
    /// <returns>A list of role IDs that trigger auto-ban</returns>
    public async Task<List<ulong>> GetAutoBanRoles(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        return await db.AutoBanRoles
            .Where(x => x.GuildId == guildId)
            .Select(x => x.RoleId)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a role to the list of AutoBanRoles.
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="roleId">The role ID to add to auto-ban list</param>
    /// <returns>True if the role was added successfully, false if it already exists</returns>
    public async Task<bool> AddAutoBanRole(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        // Check if already exists
        var exists = await db.AutoBanRoles
            .AnyAsync(x => x.GuildId == guildId && x.RoleId == roleId)
            .ConfigureAwait(false);

        if (exists)
            return false;

        // Insert new auto-ban role
        await db.InsertAsync(new AutoBanRole
        {
            GuildId = guildId, RoleId = roleId
        }).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    ///     Removes a role from the list of AutoBanRoles.
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="roleId">The role ID to remove from auto-ban list</param>
    /// <returns>True if the role was removed successfully, false if it didn't exist</returns>
    public async Task<bool> RemoveAutoBanRole(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var deletedRows = await db.AutoBanRoles
            .Where(x => x.GuildId == guildId && x.RoleId == roleId)
            .DeleteAsync()
            .ConfigureAwait(false);

        return deletedRows > 0;
    }
}