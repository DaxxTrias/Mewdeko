using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a feed subscription in the database.
/// </summary>
[Table("FeedSub")]
public class FeedSub : DbEntity
{
    /// <summary>
    /// Gets or sets the guild Id.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the channel where feed updates will be posted.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the feed.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    ///     Gets or sets the message template for feed updates.
    /// </summary>
    public string? Message { get; set; } = "-";
}