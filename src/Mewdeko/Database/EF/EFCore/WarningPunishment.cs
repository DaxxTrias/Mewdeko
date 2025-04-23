using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;
using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a warning punishment in a guild.
/// </summary>
[Table("WarningPunishment")]
public class WarningPunishment : DbEntity
{
    /// <summary>
    /// Gets or sets the guild Id.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the warning count.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    ///     Gets or sets the punishment action.
    /// </summary>
    public PunishmentAction Punishment { get; set; }

    /// <summary>
    ///     Gets or sets the time for the punishment.
    /// </summary>
    public int Time { get; set; }

    /// <summary>
    ///     Gets or sets the role ID for the punishment.
    /// </summary>
    public ulong? RoleId { get; set; }
}