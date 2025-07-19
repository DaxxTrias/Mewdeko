namespace Mewdeko.Controllers.Common.Todo;

/// <summary>
///     Request model for tag operations
/// </summary>
public class TagRequest
{
    /// <summary>
    ///     The tag to add or remove
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    ///     User ID making the change
    /// </summary>
    public ulong UserId { get; set; }
}