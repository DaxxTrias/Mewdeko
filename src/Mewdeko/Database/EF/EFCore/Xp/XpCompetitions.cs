using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;
using Mewdeko.Modules.Xp.Models;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents an XP competition.
/// </summary>
[Table("XpCompetitions")]
public class XpCompetition : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Required]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the competition name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the competition type.
    /// </summary>
    [Required]
    public XpCompetitionType Type { get; set; } = XpCompetitionType.MostGained;

    /// <summary>
    ///     Gets or sets when the competition starts.
    /// </summary>
    [Required]
    public DateTime StartTime { get; set; }

    /// <summary>
    ///     Gets or sets when the competition ends.
    /// </summary>
    [Required]
    public DateTime EndTime { get; set; }

    /// <summary>
    ///     Gets or sets the target level for ReachLevel competition type.
    /// </summary>
    [Required]
    public int TargetLevel { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the announcement channel for updates.
    /// </summary>
    public ulong? AnnouncementChannelId { get; set; } = null;
}