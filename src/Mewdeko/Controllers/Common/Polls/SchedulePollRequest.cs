using Mewdeko.Modules.Games.Common;

namespace Mewdeko.Controllers.Common.Polls;

/// <summary>
/// Request model for scheduling a poll to be created at a future time.
/// </summary>
public class SchedulePollRequest
{
    /// <summary>
    /// Gets or sets the poll question text.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of poll options.
    /// </summary>
    public List<PollOptionRequest> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the type of poll to create.
    /// </summary>
    public PollType Type { get; set; }

    /// <summary>
    /// Gets or sets the Discord channel ID where the poll will be posted.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets when the poll should be created.
    /// </summary>
    public DateTime ScheduledFor { get; set; }

    /// <summary>
    /// Gets or sets the poll duration in minutes before auto-close.
    /// </summary>
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// Gets or sets whether users can select multiple options.
    /// </summary>
    public bool AllowMultipleVotes { get; set; }

    /// <summary>
    /// Gets or sets whether votes should be anonymous.
    /// </summary>
    public bool IsAnonymous { get; set; }

    /// <summary>
    /// Gets or sets the list of role IDs that can vote on this poll.
    /// </summary>
    public List<ulong>? AllowedRoles { get; set; }

    /// <summary>
    /// Gets or sets the embed color as a hex string.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets whether users can change their votes.
    /// </summary>
    public bool AllowVoteChanges { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show vote counts while the poll is active.
    /// </summary>
    public bool ShowResults { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show progress bars for vote counts.
    /// </summary>
    public bool ShowProgressBars { get; set; } = true;

    /// <summary>
    /// Gets or sets the Discord user ID of the person scheduling the poll.
    /// </summary>
    public ulong UserId { get; set; }
}