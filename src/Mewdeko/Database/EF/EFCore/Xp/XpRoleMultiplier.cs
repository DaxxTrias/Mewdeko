using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents an XP multiplier for a specific role.
/// </summary>
[Table("XpRoleMultipliers")]
public class XpRoleMultiplier : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Required]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the role ID.
    /// </summary>
    [Required]
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Gets or sets the multiplier value.
    /// </summary>
    [Required]
    public double Multiplier { get; set; } = 1.0;
}