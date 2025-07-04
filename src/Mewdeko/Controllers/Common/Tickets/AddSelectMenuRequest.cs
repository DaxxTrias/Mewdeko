namespace Mewdeko.Controllers.Common.Tickets;

/// <summary>
///     Request model for adding a select menu
/// </summary>
public class AddSelectMenuRequest
{
    /// <summary>
    ///     The placeholder text for the menu
    /// </summary>
    public string Placeholder { get; set; } = null!;

    /// <summary>
    ///     Label for the first option
    /// </summary>
    public string FirstOptionLabel { get; set; } = null!;

    /// <summary>
    ///     Description for the first option
    /// </summary>
    public string? FirstOptionDescription { get; set; }

    /// <summary>
    ///     Emoji for the first option
    /// </summary>
    public string? FirstOptionEmoji { get; set; }
}