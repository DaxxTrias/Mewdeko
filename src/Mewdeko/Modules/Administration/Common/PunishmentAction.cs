namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     Represents the types of punishment actions that can be taken.
/// </summary>
public enum PunishmentAction
{
    /// <summary>
    ///     Mute the user, preventing them from sending messages.
    /// </summary>
    Mute,

    /// <summary>
    ///     Kick the user from the server.
    /// </summary>
    Kick,

    /// <summary>
    ///     Ban the user from the server.
    /// </summary>
    Ban,

    /// <summary>
    ///     Softban the user (ban and immediately unban to clear recent messages).
    /// </summary>
    Softban,

    /// <summary>
    ///     Remove all roles from the user.
    /// </summary>
    RemoveRoles,

    /// <summary>
    ///     Mute the user in text channels only.
    /// </summary>
    ChatMute,

    /// <summary>
    ///     Mute the user in voice channels only.
    /// </summary>
    VoiceMute,

    /// <summary>
    ///     Add a specific role to the user.
    /// </summary>
    AddRole,

    /// <summary>
    ///     Delete the user's message that triggered the action.
    /// </summary>
    Delete,

    /// <summary>
    ///     Issue a warning to the user.
    /// </summary>
    Warn,

    /// <summary>
    ///     Temporarily restrict the user's access to the server.
    /// </summary>
    Timeout,

    /// <summary>
    ///     Take no action.
    /// </summary>
    None
}