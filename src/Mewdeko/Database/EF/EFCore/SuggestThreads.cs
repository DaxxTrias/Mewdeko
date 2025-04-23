﻿using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a suggestion thread in a guild.
/// </summary>
public class SuggestThreads : DbEntity
{
    /// <summary>
    ///     Gets or sets the message ID.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     Gets or sets the thread channel ID.
    /// </summary>
    public ulong ThreadChannelId { get; set; }
}