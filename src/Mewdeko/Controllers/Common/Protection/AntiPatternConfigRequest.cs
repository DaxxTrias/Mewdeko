using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-pattern configuration
/// </summary>
public class AntiPatternConfigRequest
{
    /// <summary>
    ///     Whether anti-pattern protection should be enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The punishment action to be applied when the protection is triggered
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration of the punishment in minutes, if applicable
    /// </summary>
    public int PunishDuration { get; set; }

    /// <summary>
    ///     The role ID to be assigned as punishment, if applicable
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     Whether to check account age
    /// </summary>
    public bool CheckAccountAge { get; set; }

    /// <summary>
    ///     Maximum account age in months to flag
    /// </summary>
    public int MaxAccountAgeMonths { get; set; } = 6;

    /// <summary>
    ///     Whether to check join timing
    /// </summary>
    public bool CheckJoinTiming { get; set; }

    /// <summary>
    ///     Maximum hours between account creation and join
    /// </summary>
    public double MaxJoinHours { get; set; } = 48.0;

    /// <summary>
    ///     Whether to check for batch account creation
    /// </summary>
    public bool CheckBatchCreation { get; set; }

    /// <summary>
    ///     Whether to check if user is offline
    /// </summary>
    public bool CheckOfflineStatus { get; set; }

    /// <summary>
    ///     Whether to flag very new accounts
    /// </summary>
    public bool CheckNewAccounts { get; set; }

    /// <summary>
    ///     Days to consider an account as new
    /// </summary>
    public int NewAccountDays { get; set; } = 7;

    /// <summary>
    ///     Minimum score to trigger punishment
    /// </summary>
    public int MinimumScore { get; set; } = 15;
}