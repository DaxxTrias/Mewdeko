using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents a currency reward for reaching a specific XP level.
/// </summary>
[Table("XpCurrencyRewards")]
public class XpCurrencyReward : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Required]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the level required for this reward.
    /// </summary>
    [Required]
    public int Level { get; set; }

    /// <summary>
    ///     Gets or sets the amount of currency to award.
    /// </summary>
    [Required]
    public long Amount { get; set; }
}