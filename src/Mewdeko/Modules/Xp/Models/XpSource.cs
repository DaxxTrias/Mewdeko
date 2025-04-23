namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Defines the source of an XP gain.
/// </summary>
public enum XpSource
{
    /// <summary>
    ///     XP gained from sending a message.
    /// </summary>
    Message,

    /// <summary>
    ///     XP gained from voice channel activity.
    /// </summary>
    Voice,

    /// <summary>
    ///     XP manually added by a command or administrator.
    /// </summary>
    Manual,

    /// <summary>
    ///     XP bonus for the first message of the day.
    /// </summary>
    FirstMessage
}