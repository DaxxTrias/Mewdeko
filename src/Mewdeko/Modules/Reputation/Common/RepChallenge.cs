namespace Mewdeko.Modules.Reputation.Common;

/// <summary>
///     Challenge type constants.
/// </summary>
public static class RepChallengeType
{
    /// <summary>
    ///     Daily challenge.
    /// </summary>
    public const string Daily = "daily";

    /// <summary>
    ///     Weekly challenge.
    /// </summary>
    public const string Weekly = "weekly";

    /// <summary>
    ///     Monthly challenge.
    /// </summary>
    public const string Monthly = "monthly";

    /// <summary>
    ///     Custom duration challenge.
    /// </summary>
    public const string Custom = "custom";
}

/// <summary>
///     Challenge goal type constants.
/// </summary>
public static class RepChallengeGoalType
{
    /// <summary>
    ///     Give reputation to X unique users.
    /// </summary>
    public const string GiveRepUniqueUsers = "give_rep_unique_users";

    /// <summary>
    ///     Earn X total reputation.
    /// </summary>
    public const string EarnRepAmount = "earn_rep_amount";

    /// <summary>
    ///     Give X total reputation.
    /// </summary>
    public const string GiveRepAmount = "give_rep_amount";

    /// <summary>
    ///     Maintain daily reputation streak for X days.
    /// </summary>
    public const string MaintainStreak = "maintain_streak";

    /// <summary>
    ///     Earn reputation in X different channels.
    /// </summary>
    public const string EarnInDifferentChannels = "earn_in_different_channels";

    /// <summary>
    ///     Complete reaction-based reputation X times.
    /// </summary>
    public const string ReactionRepCount = "reaction_rep_count";
}