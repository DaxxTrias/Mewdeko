namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for updating select menu placeholder
/// </summary>
public class UpdatePlaceholderRequest
{
    /// <summary>
    ///     The new placeholder text
    /// </summary>
    public string Placeholder { get; set; } = null!;
}