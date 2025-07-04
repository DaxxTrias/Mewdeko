using Mewdeko.Modules.Games.Common;

namespace Mewdeko.Controllers.Common.Polls;

/// <summary>
/// Request model for creating a poll template.
/// </summary>
public class CreateTemplateRequest
{
    /// <summary>
    /// Gets or sets the template name for identification.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template question text.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template options.
    /// </summary>
    public List<PollOptionRequest> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the default poll type for this template.
    /// </summary>
    public PollType DefaultType { get; set; }

    /// <summary>
    /// Gets or sets whether users can select multiple options by default.
    /// </summary>
    public bool AllowMultipleVotes { get; set; }

    /// <summary>
    /// Gets or sets whether votes should be anonymous by default.
    /// </summary>
    public bool IsAnonymous { get; set; }

    /// <summary>
    /// Gets or sets the default embed color as a hex string.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets whether users can change their votes by default.
    /// </summary>
    public bool AllowVoteChanges { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show vote counts while the poll is active by default.
    /// </summary>
    public bool ShowResults { get; set; } = true;

    /// <summary>
    /// Gets or sets the Discord user ID of the person creating the template.
    /// </summary>
    public ulong UserId { get; set; }
}