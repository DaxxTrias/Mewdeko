namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for updating anti-pattern configuration
/// </summary>
public class UpdateAntiPatternConfigRequest
{
    /// <summary>
    ///     Whether to check account age
    /// </summary>
    public bool? CheckAccountAge { get; set; }

    /// <summary>
    ///     Maximum account age in months to flag
    /// </summary>
    public int? MaxAccountAgeMonths { get; set; }

    /// <summary>
    ///     Whether to check join timing
    /// </summary>
    public bool? CheckJoinTiming { get; set; }

    /// <summary>
    ///     Maximum hours between account creation and join
    /// </summary>
    public double? MaxJoinHours { get; set; }

    /// <summary>
    ///     Whether to check for batch account creation
    /// </summary>
    public bool? CheckBatchCreation { get; set; }

    /// <summary>
    ///     Whether to check if user is offline
    /// </summary>
    public bool? CheckOfflineStatus { get; set; }

    /// <summary>
    ///     Whether to flag very new accounts
    /// </summary>
    public bool? CheckNewAccounts { get; set; }

    /// <summary>
    ///     Days to consider an account as new
    /// </summary>
    public int? NewAccountDays { get; set; }

    /// <summary>
    ///     Minimum score to trigger punishment
    /// </summary>
    public int? MinimumScore { get; set; }
}