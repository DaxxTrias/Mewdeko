namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for unlinking tickets from a case
/// </summary>
public class UnlinkTicketsRequest
{
    /// <summary>
    ///     The ticket IDs to unlink
    /// </summary>
    public List<int> TicketIds { get; set; } = null!;
}