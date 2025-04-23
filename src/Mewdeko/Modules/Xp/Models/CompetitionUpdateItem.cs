namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Represents an update to a user's competition entry.
/// </summary>
public class CompetitionUpdateItem
{
    /// <summary>
    ///     Gets or sets the competition ID.
    /// </summary>
    public int CompetitionId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the amount of XP gained.
    /// </summary>
    public int XpGained { get; set; }

    /// <summary>
    ///     Gets or sets the user's current level.
    /// </summary>
    public int CurrentLevel { get; set; }
}