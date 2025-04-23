using LinqToDB;
using DataModel;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service for automatically banning users who add a specified AutoBanRole.
/// </summary>
public class AutoBanRoleService : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutoBanRoleService" /> class.
    /// </summary>
    /// <param name="eventHandler">The event handler</param>
    /// <param name="dbFactory">The database connection factory</param>
    public AutoBanRoleService(EventHandler eventHandler, IDataConnectionFactory dbFactory)
    {
        this.dbFactory = dbFactory;
        this.eventHandler = eventHandler;
        this.eventHandler.GuildMemberUpdated += OnGuildMemberUpdated;
    }


    private async Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> args, SocketGuildUser arsg2)
    {
        var before = args.HasValue ? args.Value : null;
        if (before == null)
            return;

        var addedRoles = arsg2.Roles.Except(before.Roles);
        if (!addedRoles.Any()) return;

        await using var db = await dbFactory.CreateConnectionAsync();
        var autoBanRoleIds = await db.AutoBanRoles
            .Where(x => x.GuildId == arsg2.Guild.Id)
            .Select(x => x.RoleId)
            .ToListAsync();

        var rolesSet = autoBanRoleIds.ToHashSet();

        if (!addedRoles.Any(x => rolesSet.Contains(x.Id))) return;

        try
        {
            await arsg2.Guild.AddBanAsync(arsg2, 0, "Auto-ban role");
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    ///     Adds a role to the list of AutoBanRoles.
    /// </summary>
    /// <param name="guildId">The guild id</param>
    /// <param name="roleId">The role to add to autoban</param>
    /// <returns>A bool depending on whether the role was added</returns>
    public async Task<bool> AddAutoBanRole(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var exists = await db.AutoBanRoles
            .AnyAsync(x => x.GuildId == guildId && x.RoleId == roleId);

        if (exists) return false;

        await db.InsertAsync(new AutoBanRole
        {
            GuildId = guildId, RoleId = roleId
        });
        return true;
    }

    /// <summary>
    ///     Removes a role from the list of AutoBanRoles.
    /// </summary>
    /// <param name="guildId">The guild id</param>
    /// <param name="roleId">The role to remove</param>
    /// <returns>A bool depending on whether the role was removed</returns>
    public async Task<bool> RemoveAutoBanRole(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedRows = await db.AutoBanRoles
            .Where(x => x.GuildId == guildId && x.RoleId == roleId)
            .DeleteAsync();
        return deletedRows > 0;
    }
}