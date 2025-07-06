namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for batch adding role to tickets
/// </summary>
public class BatchAddRoleRequest
{
    /// <summary>
    ///     The role ID to add to all active tickets
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Whether the role should have view-only permissions
    /// </summary>
    public bool ViewOnly { get; set; }
}