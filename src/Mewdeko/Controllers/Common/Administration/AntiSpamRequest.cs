using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Administration;

/// <summary>
///     Request model for anti-spam settings
/// </summary>
public class AntiSpamRequest
{
    /// <summary>
    ///     The number of messages that triggers the anti-spam protection
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    ///     The punishment action to be applied when the protection is triggered
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration of the punishment in minutes, if applicable
    /// </summary>
    public int PunishDurationMinutes { get; set; }

    /// <summary>
    ///     The ID of the role to be added as punishment, if applicable
    /// </summary>
    public ulong? RoleId { get; set; }
}