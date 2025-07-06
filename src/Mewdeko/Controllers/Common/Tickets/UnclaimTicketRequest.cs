namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for unclaiming a ticket
/// </summary>
public class UnclaimTicketRequest
{
    /// <summary>
    ///     The ID of the staff member unclaiming the ticket
    /// </summary>
    public ulong StaffId { get; set; }
}