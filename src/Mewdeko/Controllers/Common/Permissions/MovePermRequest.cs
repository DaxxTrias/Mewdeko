namespace Mewdeko.Controllers.Common.Permissions;

/// <summary>
///     Request model for moving permissions
/// </summary>
public class MovePermRequest
{
    /// <summary>
    ///     The source index to move from
    /// </summary>
    public int From { get; set; }

    /// <summary>
    ///     The destination index to move to
    /// </summary>
    public int To { get; set; }
}