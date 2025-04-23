using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a word blacklist for publishing in a channel.
/// </summary>
[Table("PublishWordBlacklist")]
public class PublishWordBlacklist : DbEntity
{
    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the word to be blacklisted.
    /// </summary>
    public string? Word { get; set; }
}