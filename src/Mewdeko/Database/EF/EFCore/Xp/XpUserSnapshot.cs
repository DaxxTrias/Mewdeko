using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents a user's XP statistics snapshot for historical tracking.
/// </summary>
[Table("XpUserSnapshots")]
public class XpUserSnapshot : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Required]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    [Required]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets when the snapshot was taken.
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the total XP at the time of the snapshot.
    /// </summary>
    [Required]
    public long TotalXp { get; set; }

    /// <summary>
    ///     Gets or sets the XP level at the time of the snapshot.
    /// </summary>
    [Required]
    public int Level { get; set; }
}