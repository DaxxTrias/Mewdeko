namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for linking tickets to a case
/// </summary>
public class LinkTicketsRequest
{
    /// <summary>
    ///     The ticket IDs to link
    /// </summary>
    public List<int> TicketIds { get; set; } = null!;
}