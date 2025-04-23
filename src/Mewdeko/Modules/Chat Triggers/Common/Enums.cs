namespace Mewdeko.Modules.Chat_Triggers.Common;

/// <summary>
///     Specifies the type of role grant for chat triggers.
/// </summary>
public enum CtRoleGrantType
{
    /// <summary>
    ///     Grant or remove roles from the sender of the message.
    /// </summary>
    Sender,

    /// <summary>
    ///     Grant or remove roles from the mentioned user(s) in the message.
    /// </summary>
    Mentioned,

    /// <summary>
    ///     Grant or remove roles from both the sender and mentioned user(s).
    /// </summary>
    Both
}

/// <summary>
///     Specifies the type of application command for chat triggers.
/// </summary>
public enum CtApplicationCommandType
{
    /// <summary>
    ///     No application command associated.
    /// </summary>
    None,

    /// <summary>
    ///     A slash command.
    /// </summary>
    Slash,

    /// <summary>
    ///     A message context menu command.
    /// </summary>
    Message,

    /// <summary>
    ///     A user context menu command.
    /// </summary>
    User
}

/// <summary>
///     Specifies the types of chat triggers.
/// </summary>
[Flags]
public enum ChatTriggerType
{
    /// <summary>
    ///     Triggered by a regular message.
    /// </summary>
    Message = 0b0001,

    /// <summary>
    ///     Triggered by an interaction.
    /// </summary>
    Interaction = 0b0010,

    /// <summary>
    ///     Triggered by a button press.
    /// </summary>
    Button = 0b0100

    // Commented out as not yet developed
    // /// <summary>
    // /// Triggered by reactions.
    // /// </summary>
    // Reactions = 0b10000,
}

/// <summary>
///     Specifies the prefix requirement type for chat triggers.
/// </summary>
public enum RequirePrefixType
{
    /// <summary>
    ///     No prefix required.
    /// </summary>
    None,

    /// <summary>
    ///     Requires the global prefix.
    /// </summary>
    Global,

    /// <summary>
    ///     Requires either the guild-specific prefix or the global prefix.
    /// </summary>
    GuildOrGlobal,

    /// <summary>
    ///     Requires the guild-specific prefix if set, otherwise no prefix.
    /// </summary>
    GuildOrNone,

    /// <summary>
    ///     Requires a custom prefix.
    /// </summary>
    Custom
}