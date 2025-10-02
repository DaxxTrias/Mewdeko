using DataModel;

namespace Mewdeko.Modules.Counting.Common;

/// <summary>
/// Result of setting up a counting channel.
/// </summary>
public class CountingSetupResult
{
    /// <summary>
    /// Whether the setup was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if setup failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The counting channel that was created.
    /// </summary>
    public CountingChannel? Channel { get; set; }

    /// <summary>
    /// The configuration for the counting channel.
    /// </summary>
    public CountingChannelConfig? Config { get; set; }
}

/// <summary>
/// Result of processing a counting attempt.
/// </summary>
public class CountingResult
{
    /// <summary>
    /// Whether the counting attempt was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The type of error that occurred if unsuccessful.
    /// </summary>
    public CountingError ErrorType { get; set; }

    /// <summary>
    /// The number that was counted.
    /// </summary>
    public long Number { get; set; }

    /// <summary>
    /// The number that was expected.
    /// </summary>
    public long ExpectedNumber { get; set; }

    /// <summary>
    /// The actual number that was submitted.
    /// </summary>
    public long ActualNumber { get; set; }

    /// <summary>
    /// Whether this count set a new record.
    /// </summary>
    public bool IsNewRecord { get; set; }

    /// <summary>
    /// Error message if the attempt failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of parsing a number from text.
/// </summary>
public class NumberParseResult
{
    /// <summary>
    /// Whether the parsing was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The parsed number value.
    /// </summary>
    public long Number { get; set; }

    /// <summary>
    /// Error message if parsing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Statistics for a counting channel.
/// </summary>
public class CountingChannelStats
{
    /// <summary>
    /// The counting channel these stats belong to.
    /// </summary>
    public CountingChannel Channel { get; set; } = null!;

    /// <summary>
    /// Total number of unique participants.
    /// </summary>
    public int TotalParticipants { get; set; }

    /// <summary>
    /// Total number of counting errors.
    /// </summary>
    public int TotalErrors { get; set; }

    /// <summary>
    /// Number of milestones reached.
    /// </summary>
    public int MilestonesReached { get; set; }

    /// <summary>
    /// The user with the most contributions.¢
    /// </summary>
    public CountingStats? TopContributor { get; set; }

    /// <summary>
    /// When the last counting activity occurred.
    /// </summary>
    public DateTime? LastActivity { get; set; }

    /// <summary>
    /// Average accuracy across all users.
    /// </summary>
    public double AverageAccuracy { get; set; }

    /// <summary>
    /// Average time between counts.
    /// </summary>
    public TimeSpan AverageTimeBetweenCounts { get; set; }
}

/// <summary>
/// Leaderboard entry for a user in a counting channel.
/// </summary>
public class CountingLeaderboardEntry
{
    /// <summary>
    /// The user's rank on the leaderboard.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// The Discord user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// The user's avatar URL.
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    /// Total number of contributions by this user.
    /// </summary>
    public long ContributionsCount { get; set; }

    /// <summary>
    /// The user's highest counting streak.
    /// </summary>
    public int HighestStreak { get; set; }

    /// <summary>
    /// The user's current counting streak.
    /// </summary>
    public int CurrentStreak { get; set; }

    /// <summary>
    /// The user's counting accuracy percentage.
    /// </summary>
    public double Accuracy { get; set; }

    /// <summary>
    /// Total of all numbers counted by this user.
    /// </summary>
    public long TotalNumbersCounted { get; set; }

    /// <summary>
    /// When the user last contributed to counting.
    /// </summary>
    public DateTime? LastContribution { get; set; }
}

/// <summary>
/// Achievement data for counting system.
/// </summary>
public class CountingAchievement
{
    /// <summary>
    /// Unique identifier for the achievement.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Display name of the achievement.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Description of what the achievement is for.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Icon or emoji representing the achievement.
    /// </summary>
    public string Icon { get; set; } = "";

    /// <summary>
    /// Points awarded for this achievement.
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Whether this is a rare achievement.
    /// </summary>
    public bool IsRare { get; set; }

    /// <summary>
    /// When the achievement was unlocked.
    /// </summary>
    public DateTime UnlockedAt { get; set; }
}

/// <summary>
/// Competition data for counting channels.
/// </summary>
public class CountingCompetition
{
    /// <summary>
    /// Unique identifier for the competition.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Display name of the competition.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Description of the competition.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// When the competition starts.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// When the competition ends.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Whether the competition is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// List of channel IDs participating in the competition.
    /// </summary>
    public List<ulong> ParticipatingChannels { get; set; } = new();

    /// <summary>
    /// Additional settings for the competition.
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// Current leaderboard for the competition.
    /// </summary>
    public List<CountingLeaderboardEntry> Leaderboard { get; set; } = new();
}

/// <summary>
/// Export data for counting channels.
/// </summary>
public class CountingExportData
{
    /// <summary>
    /// The counting channel being exported.
    /// </summary>
    public CountingChannel Channel { get; set; } = null!;

    /// <summary>
    /// Configuration settings for the channel.
    /// </summary>
    public CountingChannelConfig Config { get; set; } = null!;

    /// <summary>
    /// Statistics for all users in the channel.
    /// </summary>
    public List<CountingStats> UserStats { get; set; } = new();

    /// <summary>
    /// All milestones reached in the channel.
    /// </summary>
    public List<CountingMilestones> Milestones { get; set; } = new();

    /// <summary>
    /// Recent events and activity in the channel.
    /// </summary>
    public List<CountingEvents> RecentEvents { get; set; } = new();

    /// <summary>
    /// When the export was created.
    /// </summary>
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who created the export.
    /// </summary>
    public string ExportedBy { get; set; } = "";
}