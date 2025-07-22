namespace Mewdeko.Controllers.Common.Todo;

/// <summary>
///     Request model for creating a todo list
/// </summary>
public class CreateTodoListRequest
{
    /// <summary>
    ///     Name of the todo list
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Optional description for the todo list
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     User ID of the list creator
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Whether this is a server-wide list
    /// </summary>
    public bool IsServerList { get; set; }
}