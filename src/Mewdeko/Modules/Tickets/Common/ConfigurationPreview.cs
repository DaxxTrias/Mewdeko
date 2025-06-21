using System.Text;

namespace Mewdeko.Modules.Tickets.Common;

/// <summary>
/// Preview generator for showing users what their configuration will look like
/// </summary>
public static class ConfigurationPreview
{
    /// <summary>
    /// Generates a preview embed for button configuration
    /// </summary>
    /// <param name="basicSettings">Basic button settings</param>
    /// <param name="modalConfig">Modal configuration</param>
    /// <param name="behaviorSettings">Behavior settings</param>
    /// <param name="permissionSettings">Permission settings</param>
    /// <returns>An embed showing the button preview</returns>
    public static EmbedBuilder GenerateButtonPreview(
        Dictionary<string, string> basicSettings,
        string modalConfig,
        Dictionary<string, string> behaviorSettings,
        Dictionary<string, string> permissionSettings,
        string buttonPreviewLocalized,
        string reviewButtonConfigLocalized)
    {
        var embed = new EmbedBuilder()
            .WithTitle(buttonPreviewLocalized)
            .WithDescription(reviewButtonConfigLocalized)
            .WithColor(GetStyleColor(ConfigurationParser.ParseButtonStyle(basicSettings.GetValueOrDefault("style"))));

        // Basic information
        var basicInfo = new StringBuilder();
        basicInfo.AppendLine($"**Label:** {basicSettings.GetValueOrDefault("label", "Unnamed Button")}");
        basicInfo.AppendLine($"**Emoji:** {basicSettings.GetValueOrDefault("emoji", "None")}");
        basicInfo.AppendLine($"**Style:** {basicSettings.GetValueOrDefault("style", "primary")}");
        embed.AddField("📝 Basic Info", basicInfo.ToString(), true);

        // Modal configuration
        if (!string.IsNullOrEmpty(modalConfig))
        {
            var modalFields = ParseModalPreview(modalConfig);
            embed.AddField("📋 Modal Form", modalFields, true);
        }
        else
        {
            embed.AddField("📋 Modal Form", "No modal form - ticket created immediately", true);
        }

        // Behavior settings
        var behaviorText = new List<string>();
        if (behaviorSettings.TryGetValue("auto_close_hours", out var setting))
            behaviorText.Add($"Auto-close: {setting}h");
        if (behaviorSettings.TryGetValue("response_time_minutes", out var behaviorSetting))
            behaviorText.Add($"Response time: {behaviorSetting}m");
        if (behaviorSettings.TryGetValue("save_transcripts", out var setting1))
            behaviorText.Add($"Save transcripts: {setting1}");

        if (behaviorText.Any())
            embed.AddField("⚙️ Behavior", string.Join("\n", behaviorText), true);

        // Permission settings
        var permissionText = new List<string>();
        if (permissionSettings.TryGetValue("ticket_category", out var permissionSetting))
            permissionText.Add($"Category: {permissionSetting}");
        if (permissionSettings.TryGetValue("archive_category", out var permissionSetting1))
            permissionText.Add($"Archive: {permissionSetting1}");
        if (permissionSettings.TryGetValue("support_roles", out var setting2))
            permissionText.Add($"Support: {setting2}");

        if (permissionText.Any())
            embed.AddField("🔒 Permissions", string.Join("\n", permissionText), true);

        return embed;
    }

    /// <summary>
    /// Generates a preview for select menu configuration
    /// </summary>
    /// <param name="placeholder">The placeholder text</param>
    /// <param name="options">The options configuration</param>
    /// <param name="sharedSettings">Shared settings for all options</param>
    /// <returns>An embed showing the select menu preview</returns>
    public static EmbedBuilder GenerateSelectMenuPreview(string placeholder, string options, string sharedSettings)
    {
        var embed = new EmbedBuilder()
            .WithTitle("🔍 Select Menu Preview")
            .WithDescription("Review your select menu configuration:")
            .WithColor(Color.Green);

        embed.AddField("📝 Menu Settings", $"**Placeholder:** {placeholder}", true);

        var optionLines = options.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var optionPreview = new StringBuilder();
        foreach (var line in optionLines.Take(10)) // Show first 10 options
        {
            var parts = line.Split('|');
            if (parts.Length < 2) continue;
            optionPreview.AppendLine($"{(parts.Length > 1 ? parts[1] : "•")} {parts[0]}");
            if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
                optionPreview.AppendLine($"  ↳ {parts[2]}");
        }

        if (optionLines.Length > 10)
            optionPreview.AppendLine($"... and {optionLines.Length - 10} more options");

        embed.AddField("📋 Options", optionPreview.ToString());

        if (!string.IsNullOrEmpty(sharedSettings))
        {
            var settings = ConfigurationParser.ParseKeyValuePairs(sharedSettings);
            var settingsText = new List<string>();

            if (settings.TryGetValue("auto_close_hours", out var setting))
                settingsText.Add($"Auto-close: {setting}h");
            if (settings.TryGetValue("response_time_minutes", out var setting1))
                settingsText.Add($"Response time: {setting1}m");
            if (settings.TryGetValue("save_transcripts", out var setting2))
                settingsText.Add($"Save transcripts: {setting2}");

            if (settingsText.Any())
                embed.AddField("⚙️ Shared Settings", string.Join("\n", settingsText), true);
        }

        return embed;
    }

    /// <summary>
    /// Gets the color associated with a button style
    /// </summary>
    /// <param name="style">The button style</param>
    /// <returns>The Discord color for the style</returns>
    private static Color GetStyleColor(ButtonStyle style) => style switch
    {
        ButtonStyle.Primary => Color.Blue,
        ButtonStyle.Secondary => Color.LightGrey,
        ButtonStyle.Success => Color.Green,
        ButtonStyle.Danger => Color.Red,
        _ => Color.Default
    };

    /// <summary>
    /// Parses modal configuration for preview display
    /// </summary>
    /// <param name="modalConfig">The modal configuration</param>
    /// <returns>A formatted string showing the modal fields</returns>
    private static string ParseModalPreview(string modalConfig)
    {
        var lines = modalConfig.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var fields = new List<string>();
        var title = "Create Ticket";

        foreach (var line in lines)
        {
            if (line.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
            {
                title = line[6..].Trim();
            }
            else if (line.StartsWith("- "))
            {
                var fieldParts = line[2..].Split('|');
                if (fieldParts.Length >= 2)
                {
                    var fieldName = fieldParts[0];
                    var fieldType = fieldParts[1];
                    var required = fieldParts.Length > 2 && fieldParts[2].Contains("required");
                    fields.Add($"• {fieldName} ({fieldType}, {(required ? "required" : "optional")})");
                }
            }
        }

        var result = $"**Title:** {title}\n";
        if (fields.Any())
            result += "**Fields:**\n" + string.Join("\n", fields);
        else
            result += "**Fields:** None configured";

        return result;
    }
}