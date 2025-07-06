﻿using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

namespace Mewdeko.Database.Common;

/// <summary>
///     Represents a key for stream data, combining a stream type and name.
/// </summary>
public readonly record struct StreamDataKey
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamDataKey" /> struct.
    /// </summary>
    /// <param name="type">The type of the stream.</param>
    /// <param name="name">The name of the stream.</param>
    public StreamDataKey(FType type, string? name)
    {
        Type = type;
        Name = name;
    }

    /// <summary>
    ///     Gets the type of the followed stream.
    /// </summary>
    /// <value>The type of the stream, as defined in the FollowedStream.FType enumeration.</value>
    public FType Type { get; }

    /// <summary>
    ///     Gets the name of the stream.
    /// </summary>
    /// <value>The name of the stream.</value>
    public string? Name { get; }
}