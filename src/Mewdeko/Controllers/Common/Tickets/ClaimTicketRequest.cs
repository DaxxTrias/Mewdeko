namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for claiming a ticket
/// </summary>
public class ClaimTicketRequest
{
    /// <summary>
    ///     The ID of the staff member claiming the ticket
    /// </summary>
    public ulong StaffId { get; set; }
}