using Mewdeko.Database.EF.EFCore.Base;
using Mewdeko.Modules.Chat_Triggers.Common;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a chat trigger configuration.
/// </summary>
public class ChatTriggers : DbEntity
{
    /// <summary>
    ///     Gets or sets the number of times this trigger has been used.
    /// </summary>
    public ulong UseCount { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger is a regular expression.
    /// </summary>
    public bool IsRegex { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger can only be used by the owner.
    /// </summary>
    public bool OwnerOnly { get; set; } = false;

    /// <summary>
    ///     Gets or sets the ID of the guild where this trigger is active. Null if global.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the response to be sent when the trigger is activated.
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    ///     Gets or sets the trigger text or pattern.
    /// </summary>
    public string? Trigger { get; set; }

    /// <summary>
    ///     Gets or sets the prefix requirement type for this trigger.
    /// </summary>
    public RequirePrefixType PrefixType { get; set; } = RequirePrefixType.None;

    /// <summary>
    ///     Gets or sets the custom prefix for this trigger.
    /// </summary>
    public string? CustomPrefix { get; set; } = "";

    /// <summary>
    ///     Gets or sets a value indicating whether the triggering message should be automatically deleted.
    /// </summary>
    public bool AutoDeleteTrigger { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether the bot should react to the triggering message.
    /// </summary>
    public bool ReactToTrigger { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether the bot should not respond to the trigger.
    /// </summary>
    public bool NoRespond { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether the response should be sent as a DM.
    /// </summary>
    public bool DmResponse { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger can be activated if it's contained anywhere in the message.
    /// </summary>
    public bool ContainsAnywhere { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether the trigger allows targeting.
    /// </summary>
    public bool AllowTarget { get; set; } = false;

    /// <summary>
    ///     Gets or sets the reactions to be added when the trigger is activated.
    /// </summary>
    public string? Reactions { get; set; }

    /// <summary>
    ///     Gets or sets the roles to be granted when the trigger is activated.
    /// </summary>
    public string? GrantedRoles { get; set; } = "";

    /// <summary>
    ///     Gets or sets the roles to be removed when the trigger is activated.
    /// </summary>
    public string? RemovedRoles { get; set; } = "";

    /// <summary>
    ///     Gets or sets the type of role grant for this trigger.
    /// </summary>
    public CtRoleGrantType RoleGrantType { get; set; }

    /// <summary>
    ///     Gets or sets the valid trigger types for this chat trigger.
    /// </summary>
    public ChatTriggerType ValidTriggerTypes { get; set; } = (ChatTriggerType)0b1111;

    /// <summary>
    ///     Gets or sets the ID of the associated application command.
    /// </summary>
    public ulong ApplicationCommandId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the name of the associated application command.
    /// </summary>
    public string? ApplicationCommandName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the description of the associated application command.
    /// </summary>
    public string? ApplicationCommandDescription { get; set; } = "";

    /// <summary>
    ///     Gets or sets the type of the associated application command.
    /// </summary>
    public CtApplicationCommandType ApplicationCommandType { get; set; } = CtApplicationCommandType.None;

    /// <summary>
    ///     Gets or sets a value indicating whether the response should be ephemeral.
    /// </summary>
    public bool EphemeralResponse { get; set; } = false;

    /// <summary>
    ///     Gets or sets the ID of the channel for crossposting.
    /// </summary>
    public ulong CrosspostingChannelId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the webhook URL for crossposting.
    /// </summary>
    public string? CrosspostingWebhookUrl { get; set; } = "";

    /// <summary>
    ///     Determines whether this trigger is global (not associated with a specific guild).
    /// </summary>
    /// <returns>True if the trigger is global, false otherwise.</returns>
    public bool IsGlobal()
    {
        return GuildId is null or 0;
    }
}

/// <summary>
///     Represents a reaction response configuration.
/// </summary>
public class ReactionResponse : DbEntity
{
    /// <summary>
    ///     Gets or sets a value indicating whether this reaction response is owner-only.
    /// </summary>
    public bool OwnerOnly { get; set; } = false;

    /// <summary>
    ///     Gets or sets the text response for this reaction.
    /// </summary>
    public string? Text { get; set; }
}