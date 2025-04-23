namespace Mewdeko.Modules.Music.Common;

/// <summary>
///     Specifies the auto disconnect options.
/// </summary>
public enum AutoDisconnect
{
    /// <summary>
    ///     No auto disconnect.
    /// </summary>
    None,

    /// <summary>
    ///     Auto disconnect based on voice activity.
    /// </summary>
    Voice,

    /// <summary>
    ///     Auto disconnect when the queue is empty.
    /// </summary>
    Queue,

    /// <summary>
    ///     Auto disconnect based on either voice activity or an empty queue.
    /// </summary>
    Either
}

/// <summary>
///     Specifies the player repeat type.
/// </summary>
public enum PlayerRepeatType
{
    /// <summary>
    ///     No repeat.
    /// </summary>
    None,

    /// <summary>
    ///     Repeat the current track.
    /// </summary>
    Track,

    /// <summary>
    ///     Repeat the entire queue.
    /// </summary>
    Queue,

    /// <summary>
    ///     Repeat the current song.
    /// </summary>
    Song = 1,

    /// <summary>
    ///     Repeat all tracks.
    /// </summary>
    All = 2,

    /// <summary>
    ///     Turn off repeat.
    /// </summary>
    Off = 0
}