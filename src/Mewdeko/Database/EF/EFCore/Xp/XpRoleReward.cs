using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents a role reward for reaching a specific XP level.
/// </summary>
[Table("XpRoleRewards")]
public class XpRoleReward : DbEntity
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
    ///     Gets or sets the role ID to award.
    /// </summary>
    [Required]
    public ulong RoleId { get; set; }
}