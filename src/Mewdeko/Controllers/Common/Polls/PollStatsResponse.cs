namespace Mewdeko.Controllers.Common.Polls;

/// <summary>
/// Response model containing poll statistics and voting data.
/// </summary>
public class PollStatsResponse
{
    /// <summary>
    /// Gets or sets the total number of votes cast.
    /// </summary>
    public int TotalVotes { get; set; }

    /// <summary>
    /// Gets or sets the number of unique users who voted.
    /// </summary>
    public int UniqueVoters { get; set; }

    /// <summary>
    /// Gets or sets the vote count for each option by index.
    /// </summary>
    public Dictionary<int, int> OptionVotes { get; set; } = new();

    /// <summary>
    /// Gets or sets the chronological list of vote events.
    /// </summary>
    public List<VoteHistoryResponse> VoteHistory { get; set; } = [];

    /// <summary>
    /// Gets or sets the vote distribution by Discord role.
    /// </summary>
    public Dictionary<string, int> VotesByRole { get; set; } = new();

    /// <summary>
    /// Gets or sets the average time from poll creation to vote.
    /// </summary>
    public TimeSpan AverageVoteTime { get; set; }

    /// <summary>
    /// Gets or sets the participation rate as a percentage.
    /// </summary>
    public double ParticipationRate { get; set; }

    /// <summary>
    /// Gets or sets hourly vote counts for the past 24 hours.
    /// </summary>
    public Dictionary<int, int> HourlyVoteCounts { get; set; } = new();

    /// <summary>
    /// Gets or sets the most popular voting time (hour of day).
    /// </summary>
    public int PeakVotingHour { get; set; }
}

/// <summary>
/// Response model for a vote history entry.
/// </summary>
public class VoteHistoryResponse
{
    /// <summary>
    /// Gets or sets the Discord user ID of the voter.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the voter at the time of voting.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the selected option indices.
    /// </summary>
    public int[] OptionIndices { get; set; } = [];

    /// <summary>
    /// Gets or sets when the vote was cast.
    /// </summary>
    public DateTime VotedAt { get; set; }

    /// <summary>
    /// Gets or sets whether this vote was cast anonymously.
    /// </summary>
    public bool IsAnonymous { get; set; }

    /// <summary>
    /// Gets or sets the roles the user had at the time of voting.
    /// </summary>
    public List<string> UserRoles { get; set; } = [];
}