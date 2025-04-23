using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents a time-limited XP boost event.
/// </summary>
[Table("XpBoostEvents")]
public class XpBoostEvent : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Required]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the event name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the XP multiplier for this event.
    /// </summary>
    [Required]
    public double Multiplier { get; set; } = 2.0;

    /// <summary>
    ///     Gets or sets when the event starts.
    /// </summary>
    [Required]
    public DateTime StartTime { get; set; }

    /// <summary>
    ///     Gets or sets when the event ends.
    /// </summary>
    [Required]
    public DateTime EndTime { get; set; }

    /// <summary>
    ///     Gets or sets the channels this boost applies to (comma-separated IDs, empty for all).
    /// </summary>
    public string ApplicableChannels { get; set; } = "";

    /// <summary>
    ///     Gets or sets the roles this boost applies to (comma-separated IDs, empty for all).
    /// </summary>
    public string ApplicableRoles { get; set; } = "";
}