using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-spam configuration
/// </summary>
public class AntiSpamConfigRequest
{
    /// <summary>
    ///     Whether anti-spam protection should be enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The number of messages that triggers the anti-spam protection
    /// </summary>
    public int MessageThreshold { get; set; }

    /// <summary>
    ///     The punishment action to be applied when the protection is triggered
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration of the mute in minutes, if applicable
    /// </summary>
    public int MuteTime { get; set; }

    /// <summary>
    ///     The ID of the role to be added as punishment, if applicable
    /// </summary>
    public ulong? RoleId { get; set; }
}