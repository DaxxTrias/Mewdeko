using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore.Filters;

/// <summary>
///     Represents a word that is filtered in a guild.
/// </summary>
[Table("FilteredWord")]
public class FilteredWord : DbEntity
{
    /// <summary>
    ///     Gets or sets the word to be filtered.
    /// </summary>
    public string? Word { get; set; }

    /// <summary>
    /// Gets or sets the guild Id.
    /// </summary>
    public ulong GuildId { get; set; }

}