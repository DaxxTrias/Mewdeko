namespace Mewdeko.Modules.Reputation.Common;

/// <summary>
///     Represents a poll or vote that uses reputation weighting.
/// </summary>
public class RepWeightedVote
{
    /// <summary>
    ///     Unique identifier for the vote.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Guild ID where this vote is active.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Channel ID where the vote was created.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Message ID of the vote embed.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     User who created the vote.
    /// </summary>
    public ulong CreatorId { get; set; }

    /// <summary>
    ///     Title of the vote.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Description or question.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Vote options (JSON array).
    /// </summary>
    public string OptionsJson { get; set; } = "[]";

    /// <summary>
    ///     Type of vote (single_choice, multiple_choice, yes_no).
    /// </summary>
    public string VoteType { get; set; } = "single_choice";

    /// <summary>
    ///     Weight calculation method (linear, logarithmic, tiered).
    /// </summary>
    public string WeightMethod { get; set; } = "linear";

    /// <summary>
    ///     Weight multiplier configuration (JSON).
    /// </summary>
    public string? WeightConfigJson { get; set; }

    /// <summary>
    ///     Minimum reputation to vote.
    /// </summary>
    public int MinRepToVote { get; set; } = 0;

    /// <summary>
    ///     Maximum weight per user (0 = unlimited).
    /// </summary>
    public int MaxWeightPerUser { get; set; } = 0;

    /// <summary>
    ///     Whether to show live results.
    /// </summary>
    public bool ShowLiveResults { get; set; } = true;

    /// <summary>
    ///     Whether to show voter names.
    /// </summary>
    public bool ShowVoterNames { get; set; } = false;

    /// <summary>
    ///     Whether anonymous voting is allowed.
    /// </summary>
    public bool AllowAnonymous { get; set; } = false;

    /// <summary>
    ///     Start time of the vote.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    ///     End time of the vote.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    ///     Whether the vote is closed.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    ///     Role restrictions (JSON array of role IDs).
    /// </summary>
    public string? RequiredRoles { get; set; }

    /// <summary>
    ///     Custom reputation type to use for weighting.
    /// </summary>
    public string? CustomRepType { get; set; }

    /// <summary>
    ///     Date the vote was created.
    /// </summary>
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Records individual votes in a weighted vote.
/// </summary>
public class RepWeightedVoteRecord
{
    /// <summary>
    ///     Vote ID this record belongs to.
    /// </summary>
    public int VoteId { get; set; }

    /// <summary>
    ///     User who voted.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Option(s) chosen (JSON array for multiple choice).
    /// </summary>
    public string ChosenOptionsJson { get; set; } = "[]";

    /// <summary>
    ///     User's reputation at time of vote.
    /// </summary>
    public int UserReputation { get; set; }

    /// <summary>
    ///     Calculated vote weight.
    /// </summary>
    public decimal VoteWeight { get; set; }

    /// <summary>
    ///     Whether this vote was anonymous.
    /// </summary>
    public bool IsAnonymous { get; set; }

    /// <summary>
    ///     When the vote was cast.
    /// </summary>
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Configuration for vote weight calculation.
/// </summary>
public class RepVoteWeightConfig
{
    /// <summary>
    ///     Base weight for all users (before reputation multiplier).
    /// </summary>
    public decimal BaseWeight { get; set; } = 1.0m;

    /// <summary>
    ///     Reputation tiers for tiered weighting.
    /// </summary>
    public List<RepWeightTier>? Tiers { get; set; }

    /// <summary>
    ///     Logarithm base for logarithmic weighting.
    /// </summary>
    public double LogBase { get; set; } = 10.0;

    /// <summary>
    ///     Linear multiplier (weight = base + (rep * multiplier)).
    /// </summary>
    public decimal LinearMultiplier { get; set; } = 0.01m;

    /// <summary>
    ///     Cap multiplier at this value.
    /// </summary>
    public decimal MaxMultiplier { get; set; } = 10.0m;
}

/// <summary>
///     Defines a reputation tier for tiered vote weighting.
/// </summary>
public class RepWeightTier
{
    /// <summary>
    ///     Minimum reputation for this tier.
    /// </summary>
    public int MinRep { get; set; }

    /// <summary>
    ///     Weight multiplier for this tier.
    /// </summary>
    public decimal Weight { get; set; }
}

/// <summary>
///     Result data for a weighted vote.
/// </summary>
public class RepVoteResults
{
    /// <summary>
    ///     Vote ID.
    /// </summary>
    public int VoteId { get; set; }

    /// <summary>
    ///     Total number of voters.
    /// </summary>
    public int TotalVoters { get; set; }

    /// <summary>
    ///     Total weighted votes.
    /// </summary>
    public decimal TotalWeightedVotes { get; set; }

    /// <summary>
    ///     Results per option.
    /// </summary>
    public List<RepVoteOptionResult> OptionResults { get; set; } = new();

    /// <summary>
    ///     Winning option(s).
    /// </summary>
    public List<string> Winners { get; set; } = new();
}

/// <summary>
///     Result for a single vote option.
/// </summary>
public class RepVoteOptionResult
{
    /// <summary>
    ///     Option text.
    /// </summary>
    public string Option { get; set; } = string.Empty;

    /// <summary>
    ///     Number of raw votes.
    /// </summary>
    public int RawVotes { get; set; }

    /// <summary>
    ///     Weighted vote total.
    /// </summary>
    public decimal WeightedVotes { get; set; }

    /// <summary>
    ///     Percentage of weighted votes.
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    ///     List of voters (if not anonymous).
    /// </summary>
    public List<string>? Voters { get; set; }
}