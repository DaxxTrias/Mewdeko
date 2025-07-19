using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Administration;

/// <summary>
///     Request model for anti-raid settings
/// </summary>
public class AntiRaidRequest
{
    /// <summary>
    ///     The number of users that triggers the anti-raid protection
    /// </summary>
    public int UserThreshold { get; set; }

    /// <summary>
    ///     The time period in seconds in which the user threshold must be reached
    /// </summary>
    public int Seconds { get; set; }

    /// <summary>
    ///     The punishment action to be applied when the protection is triggered
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration of the punishment in minutes, if applicable
    /// </summary>
    public int MinutesDuration { get; set; }
}