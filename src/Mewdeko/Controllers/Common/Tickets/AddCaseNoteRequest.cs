namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for adding a case note
/// </summary>
public class AddCaseNoteRequest
{
    /// <summary>
    ///     The content of the note
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    ///     The ID of the note author
    /// </summary>
    public ulong AuthorId { get; set; }
}