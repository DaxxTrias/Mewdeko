using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a configuration for deleting messages on command channels.
/// </summary>
public class DelMsgOnCmdChannel : DbEntity
{
    /// <summary>
    ///     Gets or sets the ID of the channel.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether message deletion is enabled for this channel.
    /// </summary>
    public bool State { get; set; } = true;

    /// <summary>
    /// Gets or sets the guild Id.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets the hash code for this instance.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return ChannelId.GetHashCode();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is DelMsgOnCmdChannel x
               && x.ChannelId == ChannelId;
    }
}