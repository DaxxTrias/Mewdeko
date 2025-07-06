namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for adding tags to a ticket
/// </summary>
public class AddTagsRequest
{
    /// <summary>
    ///     The tag IDs to add
    /// </summary>
    public List<string> TagIds { get; set; } = null!;

    /// <summary>
    ///     The ID of the staff member adding the tags
    /// </summary>
    public ulong StaffId { get; set; }
}