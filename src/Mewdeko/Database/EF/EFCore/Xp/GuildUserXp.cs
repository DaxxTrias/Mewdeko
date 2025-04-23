using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;
using Mewdeko.Modules.Xp.Models;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents a user's XP data within a guild.
/// </summary>
[Table("GuildUserXp")]
public class GuildUserXp : DbEntity
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
    ///     Gets or sets the user's current total XP in this guild.
    /// </summary>
    [Required]
    public long TotalXp { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the manually awarded bonus XP.
    /// </summary>
    [Required]
    public long BonusXp { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the user's last activity timestamp.
    /// </summary>
    [Required]
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the user's notification preference.
    /// </summary>
    [Required]
    public XpNotificationType NotifyType { get; set; } = XpNotificationType.Channel;

    /// <summary>
    ///     Gets or sets when the user last leveled up.
    /// </summary>
    [Required]
    public DateTime LastLevelUp { get; set; } = DateTime.UtcNow;
}