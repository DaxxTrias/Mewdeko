using LinqToDB.Mapping;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a message that has been posted to a starboard.
/// </summary>
[Table("StarboardPost")]
public class StarboardPost : DbEntity
{
    /// <summary>
    ///     Gets or sets the ID of the original message.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the starboard post message.
    /// </summary>
    public ulong PostId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the starboard configuration this post belongs to.
    /// </summary>
    public int StarboardConfigId { get; set; }
}