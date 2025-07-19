using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-alt configuration
/// </summary>
public class AntiAltConfigRequest
{
    /// <summary>
    ///     Whether anti-alt protection should be enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The minimum account age in minutes
    /// </summary>
    public int MinAgeMinutes { get; set; }

    /// <summary>
    ///     The punishment action to be applied when the protection is triggered
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration of the action in minutes, if applicable
    /// </summary>
    public int ActionDurationMinutes { get; set; }

    /// <summary>
    ///     The ID of the role to be added as punishment, if applicable
    /// </summary>
    public ulong? RoleId { get; set; }
}