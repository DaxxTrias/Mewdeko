namespace Mewdeko.Controllers.Common.Todo;

/// <summary>
///     Request model for setting due date
/// </summary>
public class SetDueDateRequest
{
    /// <summary>
    ///     The new due date (null to remove)
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    ///     User ID making the change
    /// </summary>
    public ulong UserId { get; set; }
}