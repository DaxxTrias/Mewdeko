namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for setting ticket priority
/// </summary>
public class SetPriorityRequest
{
    /// <summary>
    ///     The priority ID to set
    /// </summary>
    public string PriorityId { get; set; } = null!;

    /// <summary>
    ///     The ID of the staff member setting the priority
    /// </summary>
    public ulong StaffId { get; set; }
}