namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Represents a user's XP statistics in a guild.
/// </summary>
public class UserXpStats
{
    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the total XP amount.
    /// </summary>
    public long TotalXp { get; set; }

    /// <summary>
    ///     Gets or sets the user's level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    ///     Gets or sets the XP within the current level.
    /// </summary>
    public long LevelXp { get; set; }

    /// <summary>
    ///     Gets or sets the XP required for the next level.
    /// </summary>
    public long RequiredXp { get; set; }

    /// <summary>
    ///     Gets or sets the user's rank in the guild.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    ///     Gets or sets the bonus XP amount.
    /// </summary>
    public long BonusXp { get; set; }
}