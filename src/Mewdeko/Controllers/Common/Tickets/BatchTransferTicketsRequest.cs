namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for batch transferring tickets between staff members
/// </summary>
public class BatchTransferTicketsRequest
{
    /// <summary>
    ///     The ID of the staff member to transfer tickets from
    /// </summary>
    public ulong FromStaffId { get; set; }

    /// <summary>
    ///     The ID of the staff member to transfer tickets to
    /// </summary>
    public ulong ToStaffId { get; set; }
}