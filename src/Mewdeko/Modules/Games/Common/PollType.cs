namespace Mewdeko.Modules.Games.Common;

/// <summary>
/// Defines the types of polls that can be created.
/// </summary>
public enum PollType
{
    /// <summary>
    /// A simple yes/no poll with two options.
    /// </summary>
    YesNo = 0,

    /// <summary>
    /// A single-choice poll where users can select one option.
    /// </summary>
    SingleChoice = 1,

    /// <summary>
    /// A multi-choice poll where users can select multiple options.
    /// </summary>
    MultiChoice = 2,

    /// <summary>
    /// An anonymous poll where voter identities are hidden.
    /// </summary>
    Anonymous = 3,

    /// <summary>
    /// A role-restricted poll where only specific roles can vote.
    /// </summary>
    RoleRestricted = 4
}

/// <summary>
/// Defines the current status of a poll.
/// </summary>
public enum PollStatus
{
    /// <summary>
    /// The poll is active and accepting votes.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The poll was manually closed by a user.
    /// </summary>
    Closed = 1,

    /// <summary>
    /// The poll automatically closed due to expiration.
    /// </summary>
    Expired = 2,

    /// <summary>
    /// The poll was deleted and is no longer available.
    /// </summary>
    Deleted = 3
}

/// <summary>
/// Defines the result of a vote attempt.
/// </summary>
public enum VoteResult
{
    /// <summary>
    /// The vote was successfully recorded.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The vote was changed from a previous selection.
    /// </summary>
    Changed = 1,

    /// <summary>
    /// The vote was removed (user unvoted).
    /// </summary>
    Removed = 2,

    /// <summary>
    /// The poll is no longer accepting votes.
    /// </summary>
    PollClosed = 3,

    /// <summary>
    /// The user is not allowed to vote on this poll.
    /// </summary>
    NotAllowed = 4,

    /// <summary>
    /// The selected option is invalid.
    /// </summary>
    InvalidOption = 5,

    /// <summary>
    /// The user has already voted and changes are not allowed.
    /// </summary>
    AlreadyVoted = 6
}