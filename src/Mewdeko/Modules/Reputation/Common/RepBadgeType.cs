namespace Mewdeko.Modules.Reputation.Common;

/// <summary>
///     Defines the types of badges that can be earned in the reputation system.
/// </summary>
public static class RepBadgeType
{
    /// <summary>
    ///     Badge for giving reputation for the first time.
    /// </summary>
    public const string FirstRepGiven = "first_rep_given";

    /// <summary>
    ///     Badge for receiving reputation for the first time.
    /// </summary>
    public const string FirstRepReceived = "first_rep_received";

    /// <summary>
    ///     Badge for reaching 100 reputation.
    /// </summary>
    public const string Milestone100 = "milestone_100";

    /// <summary>
    ///     Badge for reaching 500 reputation.
    /// </summary>
    public const string Milestone500 = "milestone_500";

    /// <summary>
    ///     Badge for reaching 1000 reputation.
    /// </summary>
    public const string Milestone1000 = "milestone_1000";

    /// <summary>
    ///     Badge for reaching 5000 reputation.
    /// </summary>
    public const string Milestone5000 = "milestone_5000";

    /// <summary>
    ///     Badge for reaching 10000 reputation.
    /// </summary>
    public const string Milestone10000 = "milestone_10000";

    /// <summary>
    ///     Badge for giving rep daily for 7 days straight.
    /// </summary>
    public const string ConsistentContributor7 = "consistent_contributor_7";

    /// <summary>
    ///     Badge for giving rep daily for 30 days straight.
    /// </summary>
    public const string ConsistentContributor30 = "consistent_contributor_30";

    /// <summary>
    ///     Badge for giving rep daily for 100 days straight.
    /// </summary>
    public const string ConsistentContributor100 = "consistent_contributor_100";

    /// <summary>
    ///     Badge for high helper reputation.
    /// </summary>
    public const string HelperBadge = "helper_badge";

    /// <summary>
    ///     Badge for high artist reputation.
    /// </summary>
    public const string ArtistBadge = "artist_badge";

    /// <summary>
    ///     Badge for high memer reputation.
    /// </summary>
    public const string MemerBadge = "memer_badge";

    /// <summary>
    ///     Badge for giving rep to 50 unique users.
    /// </summary>
    public const string Generous50 = "generous_50";

    /// <summary>
    ///     Badge for giving rep to 100 unique users.
    /// </summary>
    public const string Generous100 = "generous_100";

    /// <summary>
    ///     Badge for participating in a seasonal event.
    /// </summary>
    public const string SeasonalParticipant = "seasonal_participant";

    /// <summary>
    ///     Badge for completing a weekly challenge.
    /// </summary>
    public const string WeeklyChallenger = "weekly_challenger";

    /// <summary>
    ///     Badge for completing a monthly challenge.
    /// </summary>
    public const string MonthlyChallenger = "monthly_challenger";

    /// <summary>
    ///     Badge for being top contributor in a server-wide challenge.
    /// </summary>
    public const string ChallengeChampion = "challenge_champion";

    /// <summary>
    ///     Badge for maintaining a 365-day streak.
    /// </summary>
    public const string YearStreak = "year_streak";

    /// <summary>
    ///     Gets badge display information.
    /// </summary>
    public static (string emoji, string name, string description) GetBadgeInfo(string badgeType)
    {
        return badgeType switch
        {
            FirstRepGiven => ("🎯", "First Steps", "Gave reputation for the first time"),
            FirstRepReceived => ("⭐", "Rising Star", "Received reputation for the first time"),
            Milestone100 => ("💯", "Century", "Reached 100 reputation"),
            Milestone500 => ("🔥", "Blazing", "Reached 500 reputation"),
            Milestone1000 => ("💎", "Prestigious", "Reached 1,000 reputation"),
            Milestone5000 => ("🏆", "Elite", "Reached 5,000 reputation"),
            Milestone10000 => ("👑", "Legendary", "Reached 10,000 reputation"),
            ConsistentContributor7 => ("📅", "Week Warrior", "Gave rep daily for 7 days"),
            ConsistentContributor30 => ("📆", "Monthly Master", "Gave rep daily for 30 days"),
            ConsistentContributor100 => ("🗓️", "Century Streak", "Gave rep daily for 100 days"),
            HelperBadge => ("🤝", "Community Helper", "High helper reputation"),
            ArtistBadge => ("🎨", "Creative Soul", "High artist reputation"),
            MemerBadge => ("😂", "Meme Lord", "High memer reputation"),
            Generous50 => ("💝", "Generous Heart", "Gave rep to 50 unique users"),
            Generous100 => ("💖", "Philanthropist", "Gave rep to 100 unique users"),
            SeasonalParticipant => ("🎊", "Event Participant", "Participated in a seasonal event"),
            WeeklyChallenger => ("🎯", "Weekly Warrior", "Completed a weekly challenge"),
            MonthlyChallenger => ("🏅", "Monthly Master", "Completed a monthly challenge"),
            ChallengeChampion => ("🥇", "Challenge Champion", "Top contributor in server challenge"),
            YearStreak => ("🌟", "Eternal Flame", "Maintained a 365-day streak"),
            _ => ("❓", "Unknown", "Unknown badge type")
        };
    }
}