using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents a user's entry in an XP competition.
/// </summary>
[Table("XpCompetitionEntries")]
public class XpCompetitionEntry : DbEntity
{
    /// <summary>
    ///     Gets or sets the competition ID.
    /// </summary>
    [Required]
    public int CompetitionId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    [Required]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the starting XP amount.
    /// </summary>
    [Required]
    public long StartingXp { get; set; }

    /// <summary>
    ///     Gets or sets the current XP amount.
    /// </summary>
    [Required]
    public long CurrentXp { get; set; }

    /// <summary>
    ///     Gets or sets when the target was achieved (for ReachLevel competitions).
    /// </summary>
    public DateTime? AchievedTargetAt { get; set; } = null;

    /// <summary>
    ///     Gets or sets the final placement.
    /// </summary>
    [Required]
    public int FinalPlacement { get; set; } = 0;
}