namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Stores information for a deleted message snipe, including the message, user, and channel details.
/// </summary>
public class SnipeStore
{
    /// <summary>
    ///     Gets or sets the ID of the guild where the message was sent.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who sent the message.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the channel where the message was sent.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the original message.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     Gets or sets the original timestamp when the message was sent.
    /// </summary>
    public DateTimeOffset MessageTimestamp { get; set; }

    /// <summary>
    ///     Gets or sets the content of the sniped message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    ///     Gets or sets the attachment metadata captured from the sniped message.
    /// </summary>
    public List<SnipeAttachmentStore>? Attachments { get; set; }

    /// <summary>
    ///     Gets or sets the content of the reference message, if any.
    /// </summary>
    public string ReferenceMessage { get; set; }

    /// <summary>
    ///     Gets or sets the serialized JSON data of the message (embeds, attachments, components).
    /// </summary>
    public string? JsonData { get; set; }

    /// <summary>
    ///     Indicates whether the message was edited.
    /// </summary>
    public bool Edited { get; set; }

    /// <summary>
    ///     Gets or sets the date and time when the message was added to the snipe store.
    /// </summary>
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Represents an attachment captured for a sniped message.
/// </summary>
public class SnipeAttachmentStore
{
    /// <summary>
    ///     Gets or sets the attachment filename.
    /// </summary>
    public string? Filename { get; set; }

    /// <summary>
    ///     Gets or sets the direct attachment URL.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    ///     Gets or sets the proxy URL for the attachment.
    /// </summary>
    public string? ProxyUrl { get; set; }
}