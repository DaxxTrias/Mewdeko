using Discord.Interactions;

namespace Mewdeko.Modules.Tickets.Common;

/// <summary>
/// All-in-one modal for button creation with simplified configuration
/// </summary>
public class QuickButtonSetupModal : IModal
{
    /// <summary>
    /// Gets the modal title
    /// </summary>
    public string Title => "Create Ticket Button";

    /// <summary>
    /// Gets or sets the button label and basic settings
    /// </summary>
    [InputLabel("Button Settings")]
    [ModalTextInput("button_basic", TextInputStyle.Paragraph,
        "label: General Support\nemoji: 🎫\nstyle: primary")]
    public string ButtonBasic { get; set; }

    /// <summary>
    /// Gets or sets the modal form configuration
    /// </summary>
    [InputLabel("Modal Form (optional - leave blank for no modal)")]
    [ModalTextInput("modal_config", TextInputStyle.Paragraph,
        "title: Support Request\nfields:\n- What do you need help with?|long|required\n- Priority level|short|optional")]
    public string ModalConfig { get; set; }

    /// <summary>
    /// Gets or sets the ticket behavior settings
    /// </summary>
    [InputLabel("Behavior Settings (optional)")]
    [ModalTextInput("behavior", TextInputStyle.Paragraph,
        "auto_close_hours: 48\nresponse_time_minutes: 60\nsave_transcripts: true")]
    public string BehaviorSettings { get; set; }

    /// <summary>
    /// Gets or sets the categories and permissions
    /// </summary>
    [InputLabel("Categories & Roles (optional)")]
    [ModalTextInput("permissions", TextInputStyle.Paragraph,
        "ticket_category: Support Tickets\narchive_category: Closed Tickets\nsupport_roles: @Support, @Moderator")]
    public string PermissionSettings { get; set; }
}

/// <summary>
/// Modal for select menu creation with multiple options
/// </summary>
public class QuickSelectMenuSetupModal : IModal
{
    /// <summary>
    /// Gets the modal title
    /// </summary>
    public string Title => "Create Select Menu";

    /// <summary>
    /// Gets or sets the menu configuration
    /// </summary>
    [InputLabel("Menu Settings")]
    [ModalTextInput("menu_config", TextInputStyle.Short,
        "placeholder: Choose a support category...")]
    public string MenuSettings { get; set; }

    /// <summary>
    /// Gets or sets the menu options (up to 25)
    /// </summary>
    [InputLabel("Options (one per line, format: Label|Emoji|Description)")]
    [ModalTextInput("options", TextInputStyle.Paragraph,
        "Technical Support|💻|Get help with technical issues\nBilling Questions|💳|Questions about payments\nGeneral Inquiry|❓|Other questions")]
    public string Options { get; set; }

    /// <summary>
    /// Gets or sets shared settings for all options
    /// </summary>
    [InputLabel("Shared Settings for All Options (optional)")]
    [ModalTextInput("shared_settings", TextInputStyle.Paragraph,
        "auto_close_hours: 24\nresponse_time_minutes: 30\nsave_transcripts: true")]
    public string SharedSettings { get; set; }
}

/// <summary>
/// Modal for complete panel creation with embed configuration
/// </summary>
public class PanelCreationWizardModal : IModal
{
    /// <summary>
    /// Gets the modal title
    /// </summary>
    public string Title => "Create Ticket Panel";

    /// <summary>
    /// Gets or sets the panel embed configuration
    /// </summary>
    [InputLabel("Panel Embed")]
    [ModalTextInput("embed_config", TextInputStyle.Paragraph,
        "title: Support Tickets\ndescription: Click a button below to create a ticket!\ncolor: blue")]
    public string EmbedConfig { get; set; }

    /// <summary>
    /// Gets or sets initial buttons to create
    /// </summary>
    [InputLabel("Initial Buttons (optional)")]
    [ModalTextInput("buttons", TextInputStyle.Paragraph,
        "General Support|🎫|primary\nBug Report|🐛|danger")]
    public string InitialButtons { get; set; }

    /// <summary>
    /// Gets or sets default settings for all buttons
    /// </summary>
    [InputLabel("Default Button Settings (optional)")]
    [ModalTextInput("default_settings", TextInputStyle.Paragraph,
        "auto_close_hours: 48\nsave_transcripts: true")]
    public string DefaultSettings { get; set; }
}