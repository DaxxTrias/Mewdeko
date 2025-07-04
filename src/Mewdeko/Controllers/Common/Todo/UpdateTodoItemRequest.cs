namespace Mewdeko.Controllers.Common.Todo;

/// <summary>
///     Request model for updating a todo item
/// </summary>
public class UpdateTodoItemRequest
{
    /// <summary>
    ///     New title for the item
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     New description for the item
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     User ID making the update
    /// </summary>
    public ulong UserId { get; set; }
}