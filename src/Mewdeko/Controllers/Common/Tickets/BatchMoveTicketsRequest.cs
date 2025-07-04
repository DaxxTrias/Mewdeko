namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for batch moving tickets
/// </summary>
public class BatchMoveTicketsRequest
{
    /// <summary>
    ///     The source category ID
    /// </summary>
    public ulong SourceCategoryId { get; set; }

    /// <summary>
    ///     The target category ID
    /// </summary>
    public ulong TargetCategoryId { get; set; }
}