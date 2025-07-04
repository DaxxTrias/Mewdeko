namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for adding a button to a panel
/// </summary>
public class AddButtonRequest
{
    /// <summary>
    ///     The button label
    /// </summary>
    public string Label { get; set; } = null!;

    /// <summary>
    ///     Optional emoji for the button
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    ///     The button style
    /// </summary>
    public ButtonStyle Style { get; set; } = ButtonStyle.Primary;

    /// <summary>
    ///     Optional JSON for ticket opening message
    /// </summary>
    public string? OpenMessageJson { get; set; }

    /// <summary>
    ///     Optional JSON for ticket creation modal
    /// </summary>
    public string? ModalJson { get; set; }

    /// <summary>
    ///     Format for ticket channel names
    /// </summary>
    public string? ChannelFormat { get; set; }

    /// <summary>
    ///     Optional category for ticket channels
    /// </summary>
    public ulong? CategoryId { get; set; }

    /// <summary>
    ///     Optional category for archived tickets
    /// </summary>
    public ulong? ArchiveCategoryId { get; set; }

    /// <summary>
    ///     List of support role IDs
    /// </summary>
    public List<ulong>? SupportRoles { get; set; }

    /// <summary>
    ///     List of viewer role IDs
    /// </summary>
    public List<ulong>? ViewerRoles { get; set; }

    /// <summary>
    ///     Optional auto-close duration
    /// </summary>
    public TimeSpan? AutoCloseTime { get; set; }

    /// <summary>
    ///     Optional required response time
    /// </summary>
    public TimeSpan? RequiredResponseTime { get; set; }

    /// <summary>
    ///     Maximum active tickets per user
    /// </summary>
    public int MaxActiveTickets { get; set; } = 1;

    /// <summary>
    ///     List of allowed priority IDs
    /// </summary>
    public List<string>? AllowedPriorities { get; set; }

    /// <summary>
    ///     Optional default priority
    /// </summary>
    public string? DefaultPriority { get; set; }
}