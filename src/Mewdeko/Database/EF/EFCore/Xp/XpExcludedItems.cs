using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;
using Mewdeko.Modules.Xp.Models;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents an item excluded from XP gain.
/// </summary>
[Table("XpExcludedItems")]
public class XpExcludedItem : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Required]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the excluded item ID (channel, role, or user ID).
    /// </summary>
    [Required]
    public ulong ItemId { get; set; }

    /// <summary>
    ///     Gets or sets the type of the excluded item.
    /// </summary>
    [Required]
    public ExcludedItemType ItemType { get; set; }
}