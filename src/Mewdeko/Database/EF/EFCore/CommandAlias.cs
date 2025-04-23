using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a command alias in the database.
/// </summary>
[Table("CommandAlias")]
public class CommandAlias : DbEntity
{
    /// <summary>
    ///     Gets or sets the trigger for this command alias.
    /// </summary>
    public string? Trigger { get; set; }

    /// <summary>
    ///     Gets or sets the mapping (target command) for this alias.
    /// </summary>
    public string? Mapping { get; set; }

    /// <summary>
    /// Gets or sets the guild Id.
    /// </summary>
    public ulong GuildId { get; set; }
}