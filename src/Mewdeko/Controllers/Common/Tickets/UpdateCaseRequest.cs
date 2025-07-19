namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for updating a case
/// </summary>
public class UpdateCaseRequest
{
    /// <summary>
    ///     The updated title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     The updated description
    /// </summary>
    public string? Description { get; set; }
}