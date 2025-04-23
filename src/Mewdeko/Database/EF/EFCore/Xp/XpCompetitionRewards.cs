using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents a reward for an XP competition.
/// </summary>
[Table("XpCompetitionRewards")]
public class XpCompetitionReward : DbEntity
{
    /// <summary>
    ///     Gets or sets the competition ID.
    /// </summary>
    [Required]
    public int CompetitionId { get; set; }

    /// <summary>
    ///     Gets or sets the placement position (1 for 1st place, etc.).
    /// </summary>
    [Required]
    public int Position { get; set; }

    /// <summary>
    ///     Gets or sets the role ID to award (0 for none).
    /// </summary>
    [Required]
    public ulong RoleId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the XP amount to award.
    /// </summary>
    [Required]
    public int XpAmount { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the currency amount to award.
    /// </summary>
    [Required]
    public long CurrencyAmount { get; set; } = 0;

    /// <summary>
    ///     Gets or sets a custom reward description.
    /// </summary>
    public string CustomReward { get; set; } = "";
}