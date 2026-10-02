namespace Mewdeko.Database.Enums;

/// <summary>
///     The kind of dashboard activity recorded in a <see cref="DataModel.DashboardAuditLog" /> entry.
/// </summary>
public enum AuditAction
{
    /// <summary>
    ///     The user read data.
    /// </summary>
    View = 0,

    /// <summary>
    ///     The user created a resource.
    /// </summary>
    Create = 1,

    /// <summary>
    ///     The user modified an existing resource.
    /// </summary>
    Update = 2,

    /// <summary>
    ///     The user removed a resource.
    /// </summary>
    Delete = 3,

    /// <summary>
    ///     The user accessed the dashboard itself.
    /// </summary>
    Access = 4
}
