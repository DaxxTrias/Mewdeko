using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
///     Represents a saved chat log in the database.
/// </summary>
[Table("ChatLogs")]
public class ChatLog : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID associated with this chat log.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID where the messages were logged from.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the channel name where the messages were logged from.
    /// </summary>
    public string ChannelName { get; set; }

    /// <summary>
    ///     Gets or sets the user-defined name for this chat log.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the user ID of who created this log.
    /// </summary>
    public ulong CreatedBy { get; set; }

    /// <summary>
    ///     Gets or sets the JSON string containing the message data.
    /// </summary>
    public string Messages { get; set; }

    /// <summary>
    ///     Gets or sets the count of messages in this log.
    /// </summary>
    public int MessageCount { get; set; }
}