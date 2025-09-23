namespace Mewdeko.Modules.Starboard.Common;

/// <summary>
///     DTO for starboard statistics
/// </summary>
public class StarboardStatsDto
{
    /// <summary>
    ///     The user who gets starred the most.
    /// </summary>
    public UserStarStats? MostStarredUser { get; set; }

    /// <summary>
    ///     The channel with the most starboard activity.
    /// </summary>
    public ChannelStarStats? MostActiveChannel { get; set; }

    /// <summary>
    ///     The user who gives the most stars.
    /// </summary>
    public StarrerStats? MostActiveStarrer { get; set; }

    /// <summary>
    ///     Total number of starred messages.
    /// </summary>
    public int TotalStarredMessages { get; set; }

    /// <summary>
    ///     Total number of stars given.
    /// </summary>
    public int TotalStars { get; set; }
}

/// <summary>
///     Statistics for a user's starred messages
/// </summary>
public class UserStarStats
{
    /// <summary>
    ///     The user's Discord ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Total number of stars received.
    /// </summary>
    public int TotalStars { get; set; }

    /// <summary>
    ///     Number of messages that were starred.
    /// </summary>
    public int MessageCount { get; set; }
}

/// <summary>
///     Statistics for a channel's starred messages
/// </summary>
public class ChannelStarStats
{
    /// <summary>
    ///     The channel's Discord ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Total number of stars in this channel.
    /// </summary>
    public int TotalStars { get; set; }

    /// <summary>
    ///     Number of starred messages from this channel.
    /// </summary>
    public int MessageCount { get; set; }
}

/// <summary>
///     Statistics for a user who gives stars
/// </summary>
public class StarrerStats
{
    /// <summary>
    ///     The user's Discord ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Number of stars given by this user.
    /// </summary>
    public int StarsGiven { get; set; }

    /// <summary>
    ///     Number of different emotes used by this user.
    /// </summary>
    public int UniqueEmotesUsed { get; set; }
}

/// <summary>
///     User-specific starboard statistics
/// </summary>
public class UserStarboardStatsDto
{
    /// <summary>
    ///     Number of messages this user has had starred.
    /// </summary>
    public int MessagesStarred { get; set; }

    /// <summary>
    ///     Total number of stars received on all messages.
    /// </summary>
    public int StarsReceived { get; set; }

    /// <summary>
    ///     Total number of stars given by this user.
    /// </summary>
    public int StarsGiven { get; set; }

    /// <summary>
    ///     This user's most starred posts.
    /// </summary>
    public List<TopStarredPost> TopStarredPosts { get; set; } = new();

    /// <summary>
    ///     Users this user stars the most.
    /// </summary>
    public List<UserStarGiven> MostStarredUsers { get; set; } = new();

    /// <summary>
    ///     Users who star this user the most.
    /// </summary>
    public List<UserStarGiven> TopFans { get; set; } = new();
}

/// <summary>
///     Represents a top starred post
/// </summary>
/// <param name="MessageId">The message ID.</param>
/// <param name="StarCount">Number of stars.</param>
/// <param name="Emote">The emote used.</param>
public record TopStarredPost(ulong MessageId, int StarCount, string Emote);

/// <summary>
///     Represents a user and how many stars they've given/received
/// </summary>
/// <param name="UserId">The user's Discord ID.</param>
/// <param name="Count">The star count.</param>
public record UserStarGiven(ulong UserId, int Count);

/// <summary>
///     DTO for starboard highlight information
/// </summary>
public class StarboardHighlight
{
    /// <summary>
    ///     The ID of the message that was starred
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     The ID of the channel containing the starred message
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The content of the starred message
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    ///     The author's display name
    /// </summary>
    public string AuthorName { get; set; } = "";

    /// <summary>
    ///     The author's avatar URL
    /// </summary>
    public string? AuthorAvatarUrl { get; set; }

    /// <summary>
    ///     The image URL if the message contains an image
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     The star emote used for this starboard
    /// </summary>
    public string StarEmote { get; set; } = "⭐";

    /// <summary>
    ///     The number of stars this message has
    /// </summary>
    public int StarCount { get; set; }

    /// <summary>
    ///     When the message was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}