namespace Mewdeko.Modules.Reputation.Common;

/// <summary>
///     Result of a reputation giving operation.
/// </summary>
public class GiveRepResult
{
    /// <summary>
    ///     The result type of the reputation operation.
    /// </summary>
    public GiveRepResultType Result { get; set; }

    /// <summary>
    ///     The user's new total reputation after the operation.
    /// </summary>
    public int NewTotal { get; set; }

    /// <summary>
    ///     The amount of reputation given in this operation.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    ///     The remaining cooldown time before the user can give reputation again.
    /// </summary>
    public TimeSpan? CooldownRemaining { get; set; }

    /// <summary>
    ///     The daily limit for giving reputation.
    /// </summary>
    public int DailyLimit { get; set; }

    /// <summary>
    ///     The weekly limit for giving reputation.
    /// </summary>
    public int WeeklyLimit { get; set; }

    /// <summary>
    ///     The required number of days for minimum account age.
    /// </summary>
    public int RequiredDays { get; set; }

    /// <summary>
    ///     The required number of hours for minimum server membership.
    /// </summary>
    public int RequiredHours { get; set; }

    /// <summary>
    ///     The required number of messages for minimum message count.
    /// </summary>
    public int RequiredMessages { get; set; }
}

/// <summary>
///     Type of result from a reputation giving operation.
/// </summary>
public enum GiveRepResultType
{
    /// <summary>
    ///     The operation completed successfully.
    /// </summary>
    Success,

    /// <summary>
    ///     The operation failed due to a cooldown period.
    /// </summary>
    Cooldown,

    /// <summary>
    ///     The operation failed due to reaching the daily limit.
    /// </summary>
    DailyLimit,

    /// <summary>
    ///     The operation failed due to reaching the weekly limit.
    /// </summary>
    WeeklyLimit,

    /// <summary>
    ///     The operation failed because reputation is disabled in the channel.
    /// </summary>
    ChannelDisabled,

    /// <summary>
    ///     The operation failed because the target user's reputation is frozen.
    /// </summary>
    UserFrozen,

    /// <summary>
    ///     The operation failed because the giver's account is too new.
    /// </summary>
    MinimumAccountAge,

    /// <summary>
    ///     The operation failed because the giver hasn't been in the server long enough.
    /// </summary>
    MinimumServerMembership,

    /// <summary>
    ///     The operation failed because the giver hasn't sent enough messages.
    /// </summary>
    MinimumMessages,

    /// <summary>
    ///     The operation failed because the reputation system is disabled.
    /// </summary>
    Disabled
}

/// <summary>
///     Statistics for a user's reputation activity.
/// </summary>
public class RepUserStats
{
    /// <summary>
    ///     The user's total reputation points.
    /// </summary>
    public int TotalRep { get; set; }

    /// <summary>
    ///     The user's rank in the server based on reputation.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    ///     The total amount of reputation the user has given to others.
    /// </summary>
    public int TotalGiven { get; set; }

    /// <summary>
    ///     The total amount of reputation the user has received from others.
    /// </summary>
    public int TotalReceived { get; set; }

    /// <summary>
    ///     The user's current daily giving streak.
    /// </summary>
    public int CurrentStreak { get; set; }

    /// <summary>
    ///     The user's longest daily giving streak.
    /// </summary>
    public int LongestStreak { get; set; }

    /// <summary>
    ///     The last time the user gave reputation to someone.
    /// </summary>
    public DateTime? LastGivenAt { get; set; }

    /// <summary>
    ///     The last time the user received reputation from someone.
    /// </summary>
    public DateTime? LastReceivedAt { get; set; }

    /// <summary>
    ///     The user's custom reputation types and their amounts.
    /// </summary>
    public Dictionary<string, (int amount, string displayName, string? emoji)> CustomReputations { get; set; } = new();
}