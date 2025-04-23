using Mewdeko.Database.EF.EFCore.Base;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a stream followed in a guild.
/// </summary>
public class FollowedStream : DbEntity
{

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the username of the stream.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     Gets or sets the type of the stream.
    /// </summary>
    public FType Type { get; set; }

    /// <summary>
    ///     Gets or sets the message associated with the followed stream.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="other">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    protected bool Equals(FollowedStream other)
    {
        return ChannelId == other.ChannelId &&
               string.Equals(Username.Trim(), other.Username.Trim(), StringComparison.InvariantCultureIgnoreCase) &&
               Type == other.Type;
    }

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(ChannelId, Username, (int)Type);
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is FollowedStream fs && Equals(fs);
    }
}