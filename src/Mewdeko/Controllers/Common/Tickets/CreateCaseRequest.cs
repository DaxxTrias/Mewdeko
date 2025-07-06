namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for creating a case
/// </summary>
public class CreateCaseRequest
{
    /// <summary>
    ///     The title of the case
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    ///     The description of the case
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    ///     The ID of the case creator
    /// </summary>
    public ulong CreatorId { get; set; }
}