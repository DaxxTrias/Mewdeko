#nullable enable
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a muted user ID.
/// </summary>
[Table("MutedUserId")]
public class MutedUserId : DbEntity
{
    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the roles of the muted user.
    /// </summary>
    public string? Roles { get; set; }

    /// <summary>
    /// Gets or sets the guild Id.
    /// </summary>
    public ulong? GuildId { get; set; }
}