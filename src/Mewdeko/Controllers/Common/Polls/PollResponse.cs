using Mewdeko.Modules.Games.Common;

namespace Mewdeko.Controllers.Common.Polls;

/// <summary>
/// Response model containing poll information.
/// </summary>
public class PollResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the poll.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild ID where the poll was created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord channel ID where the poll message is posted.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the Discord channel name where the poll is posted.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// Gets or sets the Discord message ID of the poll message.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user ID of the poll creator.
    /// </summary>
    public ulong CreatorId { get; set; }

    /// <summary>
    /// Gets or sets the username of the poll creator.
    /// </summary>
    public string? CreatorName { get; set; }

    /// <summary>
    /// Gets or sets the poll question text.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of poll.
    /// </summary>
    public PollType Type { get; set; }

    /// <summary>
    /// Gets or sets the poll options.
    /// </summary>
    public List<PollOptionResponse> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets when the poll was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the poll expires and auto-closes.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets when the poll was manually closed.
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Gets or sets whether the poll is currently active and accepting votes.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the poll statistics.
    /// </summary>
    public PollStatsResponse? Stats { get; set; }
}

/// <summary>
/// Response model for a poll option.
/// </summary>
public class PollOptionResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the poll option.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display text for this option.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the zero-based index position of this option in the poll.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the hex color code for this option's visual display.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the Discord emote string for this option.
    /// </summary>
    public string? Emote { get; set; }

    /// <summary>
    /// Gets or sets the number of votes for this option.
    /// </summary>
    public int VoteCount { get; set; }

    /// <summary>
    /// Gets or sets the percentage of total votes for this option.
    /// </summary>
    public double VotePercentage { get; set; }
}