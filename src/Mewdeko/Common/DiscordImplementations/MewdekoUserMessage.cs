// ReSharper disable NotNullMemberIsNotInitialized

// ReSharper disable UnassignedGetOnlyAutoProperty

// ReSharper disable AssignNullToNotNullAttribute

using System.Text.RegularExpressions;
using Poll = Discord.Poll;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace Mewdeko.Common.DiscordImplementations;

/// <summary>
///     Class used for faking messages for commands like Sudo
/// </summary>
public class MewdekoUserMessage : IUserMessage
{
    private string _content = "";

    /// <summary>
    /// Creates a MewdekoUserMessage with common default values auto-filled
    /// </summary>
    public MewdekoUserMessage()
    {
        // Auto-fill with sensible defaults
        Tags = new List<ITag>();
        MentionedChannelIds = new List<ulong>();
        MentionedRoleIds = new List<ulong>();
        MentionedUserIds = new List<ulong>();
        Reactions = new Dictionary<IEmote, ReactionMetadata>();
        Components = new List<IMessageComponent>();
        Stickers = new List<IStickerItem>();

        // CleanContent will be auto-updated when Content is set

        // Common message defaults
        MentionedEveryone = false;
        Flags = MessageFlags.None;
    }

    /// <inheritdoc />
    public ulong Id
    {
        get
        {
            return 0;
        }
    }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt
    {
        get
        {
            return DateTime.Now;
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task AddReactionAsync(IEmote emote, RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task RemoveReactionAsync(IEmote emote, IUser user, RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task RemoveReactionAsync(IEmote emote, ulong userId, RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task RemoveAllReactionsAsync(RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task RemoveAllReactionsForEmoteAsync(IEmote emote, RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit,
        RequestOptions options = null,
        ReactionType type = ReactionType.Normal)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public MessageType Type
    {
        get
        {
            return MessageType.Default;
        }
    }

    /// <inheritdoc />
    public MessageSource Source
    {
        get
        {
            return MessageSource.User;
        }
    }

    /// <inheritdoc />
    public bool IsTTS
    {
        get
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool IsPinned
    {
        get
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool IsSuppressed
    {
        get
        {
            return false;
        }
    }

    /// <inheritdoc />
    public string Content
    {
        get => _content;
        set
        {
            _content = value ?? "";
            // Auto-update CleanContent when Content is set
            CleanContent = _content;
        }
    }

    /// <inheritdoc />
    public string CleanContent { get; set; } = "";

    /// <inheritdoc />
    public DateTimeOffset Timestamp
    {
        get
        {
            return DateTimeOffset.Now;
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? EditedTimestamp
    {
        get
        {
            return DateTimeOffset.Now;
        }
    }

    /// <inheritdoc />
    public IMessageChannel Channel { get; set; }

    /// <inheritdoc />
    public IUser? Author { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<IAttachment> Attachments { get; set; } = new List<IAttachment>();

    /// <inheritdoc />
    public IReadOnlyCollection<IEmbed> Embeds { get; set; } = new List<IEmbed>();

    /// <inheritdoc />
    public IReadOnlyCollection<ITag> Tags { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<ulong> MentionedChannelIds { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<ulong> MentionedRoleIds { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<ulong> MentionedUserIds { get; set; }

    /// <inheritdoc />
    public bool MentionedEveryone { get; set; }

    /// <inheritdoc />
    public MessageActivity Activity { get; set; }

    /// <inheritdoc />
    public MessageApplication Application { get; set; }

    /// <inheritdoc />
    public MessageReference Reference { get; set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<IEmote, ReactionMetadata> Reactions { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<IMessageComponent> Components { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<IStickerItem> Stickers { get; set; }

    /// <inheritdoc />
    public MessageFlags? Flags { get; set; }

    /// <inheritdoc />
    public IMessageInteraction Interaction { get; set; }

    /// <inheritdoc />
    public Task ModifyAsync(Action<MessageProperties> func, RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task PinAsync(RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task UnpinAsync(RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task CrosspostAsync(RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public string Resolve(
        TagHandling userHandling = TagHandling.Name,
        TagHandling channelHandling = TagHandling.Name,
        TagHandling roleHandling = TagHandling.Name,
        TagHandling everyoneHandling = TagHandling.Ignore,
        TagHandling emojiHandling = TagHandling.Name)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task EndPollAsync(RequestOptions options)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetPollAnswerVotersAsync(uint answerId, int? limit = null,
        ulong? afterId = null,
        RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public MessageResolvedData ResolvedData { get; }

    /// <inheritdoc />
    public IUserMessage ReferencedMessage { get; set; }

    /// <inheritdoc />
    public IMessageInteractionMetadata InteractionMetadata { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<MessageSnapshot> ForwardedMessages { get; }

    /// <inheritdoc />
    public Poll? Poll { get; }

    /// <inheritdoc />
    public IThreadChannel Thread
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    /// <inheritdoc />
    public MessageRoleSubscriptionData RoleSubscriptionData
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    /// <inheritdoc />
    public PurchaseNotification PurchaseNotification { get; }

    /// <inheritdoc />
    public MessageCallData? CallData { get; }

    /// <summary>
    /// Creates a MewdekoUserMessage with auto-populated mention properties based on content
    /// </summary>
    /// <param name="content">Message content to parse for mentions</param>
    /// <param name="author">Message author</param>
    /// <param name="channel">Channel where message was sent</param>
    /// <returns>A MewdekoUserMessage with auto-filled properties</returns>
    public static MewdekoUserMessage CreateWithMentions(string content, IUser author, IMessageChannel channel)
    {
        var message = new MewdekoUserMessage
        {
            Content = content, Author = author, Channel = channel
        };

        // Parse mentions from content
        if (!string.IsNullOrEmpty(content))
        {
            var mentionedUsers = new List<ulong>();
            var mentionedRoles = new List<ulong>();
            var mentionedChannels = new List<ulong>();

            // Parse user mentions <@123456789> or <@!123456789>
            var userMentions = Regex.Matches(content, @"<@!?(\d+)>");
            foreach (Match match in userMentions)
            {
                if (ulong.TryParse(match.Groups[1].Value, out var userId))
                    mentionedUsers.Add(userId);
            }

            // Parse role mentions <@&123456789>
            var roleMentions = Regex.Matches(content, @"<@&(\d+)>");
            foreach (Match match in roleMentions)
            {
                if (ulong.TryParse(match.Groups[1].Value, out var roleId))
                    mentionedRoles.Add(roleId);
            }

            // Parse channel mentions <#123456789>
            var channelMentions = Regex.Matches(content, @"<#(\d+)>");
            foreach (Match match in channelMentions)
            {
                if (ulong.TryParse(match.Groups[1].Value, out var channelId))
                    mentionedChannels.Add(channelId);
            }

            // Check for @everyone or @here
            message.MentionedEveryone = content.Contains("@everyone") || content.Contains("@here");

            // Set the collections
            message.MentionedUserIds = mentionedUsers;
            message.MentionedRoleIds = mentionedRoles;
            message.MentionedChannelIds = mentionedChannels;
        }

        return message;
    }
}