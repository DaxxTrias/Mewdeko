namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for removing tags from a ticket
/// </summary>
public class RemoveTagsRequest
{
    /// <summary>
    ///     The tag IDs to remove
    /// </summary>
    public List<string> TagIds { get; set; } = null!;

    /// <summary>
    ///     The ID of the staff member removing the tags
    /// </summary>
    public ulong StaffId { get; set; }
}