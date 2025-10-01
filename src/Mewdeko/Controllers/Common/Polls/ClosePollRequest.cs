namespace Mewdeko.Controllers.Common.Polls;

/// <summary>
/// Request model for closing a poll.
/// </summary>
public class ClosePollRequest
{
    /// <summary>
    /// Gets or sets the reason for closing the poll.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets whether to notify voters that the poll was closed.
    /// </summary>
    public bool NotifyVoters { get; set; } = false;

    /// <summary>
    /// Gets or sets the Discord user ID of the person closing the poll.
    /// </summary>
    public ulong UserId { get; set; }
}