namespace Mewdeko.Modules.Tickets.Common;

/// <summary>
///     Template system for common ticket configurations
/// </summary>
public static class TicketTemplates
{
    /// <summary>
    ///     Gets all available ticket templates
    /// </summary>
    /// <returns>List of available templates</returns>
    public static List<TicketTemplate> GetAllTemplates()
    {
        return new List<TicketTemplate>
        {
            GetBasicSupportTemplate(),
            GetDepartmentTemplate(),
            GetGamingServerTemplate(),
            GetBusinessTemplate(),
            GetModApplicationTemplate()
        };
    }

    /// <summary>
    ///     Gets the basic support template
    /// </summary>
    /// <returns>Basic support ticket template</returns>
    public static TicketTemplate GetBasicSupportTemplate()
    {
        return new TicketTemplate
        {
            Id = "basic_support",
            Name = "Basic Support",
            Description = "Simple support system with general help and bug reports",
            EmbedConfig =
                "title: 🎫 Support Tickets\ndescription: Need help? Click a button below to create a support ticket!\ncolor: blue",
            Buttons = new List<ButtonTemplate>
            {
                new()
                {
                    Label = "General Support",
                    Emoji = "❓",
                    Style = ButtonStyle.Primary,
                    ModalConfig =
                        "title: General Support Request\nfields:\n- What do you need help with?|long|required",
                    Settings = "auto_close_hours: 48\nresponse_time_minutes: 60\nsave_transcripts: true"
                },
                new()
                {
                    Label = "Bug Report",
                    Emoji = "🐛",
                    Style = ButtonStyle.Danger,
                    ModalConfig =
                        "title: Bug Report\nfields:\n- What bug did you encounter?|long|required\n- Steps to reproduce|long|required\n- Expected behavior|short|optional",
                    Settings = "auto_close_hours: 72\nresponse_time_minutes: 30\nsave_transcripts: true"
                }
            }
        };
    }

    /// <summary>
    ///     Gets the department-based template
    /// </summary>
    /// <returns>Department-based ticket template</returns>
    public static TicketTemplate GetDepartmentTemplate()
    {
        return new TicketTemplate
        {
            Id = "department",
            Name = "Department Support",
            Description = "Organized by different support departments",
            EmbedConfig =
                "title: 🏢 Support Departments\ndescription: Select the department that best matches your needs:\ncolor: green",
            SelectMenus = new List<SelectMenuTemplate>
            {
                new()
                {
                    Placeholder = "Choose a department...",
                    Options =
                        "Technical Support|💻|Issues with the service\nBilling|💳|Payment and subscription questions\nGeneral Inquiry|❓|Other questions",
                    SharedSettings = "auto_close_hours: 24\nresponse_time_minutes: 45\nsave_transcripts: true"
                }
            }
        };
    }

    /// <summary>
    ///     Gets the gaming server template
    /// </summary>
    /// <returns>Gaming server ticket template</returns>
    public static TicketTemplate GetGamingServerTemplate()
    {
        return new TicketTemplate
        {
            Id = "gaming",
            Name = "Gaming Server",
            Description = "Common gaming server support categories",
            EmbedConfig = "title: 🎮 Server Support\ndescription: Get help with server-related issues:\ncolor: purple",
            Buttons = new List<ButtonTemplate>
            {
                new()
                {
                    Label = "Player Report",
                    Emoji = "⚠️",
                    Style = ButtonStyle.Danger,
                    ModalConfig =
                        "title: Player Report\nfields:\n- Player Name/ID|short|required\n- What happened?|long|required\n- Evidence (describe)|long|optional",
                    Settings = "auto_close_hours: 24\nresponse_time_minutes: 30\nsave_transcripts: true"
                },
                new()
                {
                    Label = "Ban Appeal",
                    Emoji = "🛡️",
                    Style = ButtonStyle.Secondary,
                    ModalConfig =
                        "title: Ban Appeal\nfields:\n- Your Username|short|required\n- Reason for appeal|long|required\n- When were you banned?|short|optional",
                    Settings = "auto_close_hours: 168\nresponse_time_minutes: 120\nsave_transcripts: true"
                },
                new()
                {
                    Label = "Technical Issue",
                    Emoji = "🔧",
                    Style = ButtonStyle.Primary,
                    ModalConfig =
                        "title: Technical Issue\nfields:\n- Describe the issue|long|required\n- What were you doing when it happened?|long|optional",
                    Settings = "auto_close_hours: 48\nresponse_time_minutes: 60\nsave_transcripts: true"
                }
            }
        };
    }

    /// <summary>
    ///     Gets the business template
    /// </summary>
    /// <returns>Business ticket template</returns>
    public static TicketTemplate GetBusinessTemplate()
    {
        return new TicketTemplate
        {
            Id = "business",
            Name = "Business",
            Description = "Professional business support setup",
            EmbedConfig =
                "title: 💼 Business Support\ndescription: Professional support for all your business needs:\ncolor: #2F3136",
            SelectMenus = new List<SelectMenuTemplate>
            {
                new()
                {
                    Placeholder = "Select support category...",
                    Options =
                        "Sales Inquiry|💰|Questions about our products and services\nTechnical Support|🔧|Technical issues and troubleshooting\nBilling & Payments|💳|Billing questions and payment issues\nAccount Management|👤|Account-related requests\nPartnership|🤝|Business partnership inquiries",
                    SharedSettings = "auto_close_hours: 72\nresponse_time_minutes: 15\nsave_transcripts: true"
                }
            }
        };
    }

    /// <summary>
    ///     Gets the mod application template
    /// </summary>
    /// <returns>Moderator application ticket template</returns>
    public static TicketTemplate GetModApplicationTemplate()
    {
        return new TicketTemplate
        {
            Id = "mod_application",
            Name = "Mod Applications",
            Description = "Template for moderator applications",
            EmbedConfig = "title: 🛡️ Staff Applications\ndescription: Apply to become a staff member:\ncolor: gold",
            Buttons = new List<ButtonTemplate>
            {
                new()
                {
                    Label = "Apply for Moderator",
                    Emoji = "🛡️",
                    Style = ButtonStyle.Success,
                    ModalConfig =
                        "title: Moderator Application\nfields:\n- Your age|short|required\n- Why do you want to be a moderator?|long|required\n- Previous moderation experience|long|optional\n- Timezone|short|required\n- Availability (hours per day)|short|required",
                    Settings = "auto_close_hours: 168\nresponse_time_minutes: 1440\nsave_transcripts: true"
                }
            }
        };
    }
}

/// <summary>
///     Data classes for template system
/// </summary>
public class TicketTemplate
{
    /// <summary>
    ///     Gets or sets the template ID
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    ///     Gets or sets the template name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the template description
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    ///     Gets or sets the embed configuration
    /// </summary>
    public string EmbedConfig { get; set; }

    /// <summary>
    ///     Gets or sets the button templates
    /// </summary>
    public List<ButtonTemplate> Buttons { get; set; } = new();

    /// <summary>
    ///     Gets or sets the select menu templates
    /// </summary>
    public List<SelectMenuTemplate> SelectMenus { get; set; } = new();
}

/// <summary>
///     Template for button configuration
/// </summary>
public class ButtonTemplate
{
    /// <summary>
    ///     Gets or sets the button label
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    ///     Gets or sets the button emoji
    /// </summary>
    public string Emoji { get; set; }

    /// <summary>
    ///     Gets or sets the button style
    /// </summary>
    public ButtonStyle Style { get; set; }

    /// <summary>
    ///     Gets or sets the modal configuration
    /// </summary>
    public string ModalConfig { get; set; }

    /// <summary>
    ///     Gets or sets the button settings
    /// </summary>
    public string Settings { get; set; }
}

/// <summary>
///     Template for select menu configuration
/// </summary>
public class SelectMenuTemplate
{
    /// <summary>
    ///     Gets or sets the placeholder text
    /// </summary>
    public string Placeholder { get; set; }

    /// <summary>
    ///     Gets or sets the options configuration
    /// </summary>
    public string Options { get; set; }

    /// <summary>
    ///     Gets or sets the shared settings for all options
    /// </summary>
    public string SharedSettings { get; set; }
}