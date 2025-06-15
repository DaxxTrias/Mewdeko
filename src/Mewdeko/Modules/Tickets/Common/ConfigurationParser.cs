using System.Text.Json;
using Serilog;
using Embed = Mewdeko.Common.Embed;

namespace Mewdeko.Modules.Tickets.Common;

/// <summary>
///     Configuration parser for handling user input
/// </summary>
public static class ConfigurationParser
{
    /// <summary>
    /// Creates modal JSON in the format expected by TicketService
    /// </summary>
    /// <param name="modalConfig">The modal configuration text</param>
    /// <returns>A JSON string containing only the fields dictionary</returns>
    public static string CreateModalJson(string modalConfig)
    {
        if (string.IsNullOrEmpty(modalConfig)) return null;

        var config = ParseModalConfiguration(modalConfig);

        // Service expects Dictionary<string, ModalFieldConfig>, not the entire ModalConfiguration
        var fieldsOnly = config.Fields;
        var json = JsonSerializer.Serialize(fieldsOnly, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true // For debugging
        });

        // Add logging to see what we're actually generating
        Log.Information($"Generated modal JSON: {json}");

        return json;
    }

    /// <summary>
    ///     Parses key-value pairs from user input text
    /// </summary>
    /// <param name="input">The input text containing key: value pairs</param>
    /// <returns>Dictionary of parsed key-value pairs</returns>
    public static Dictionary<string, string> ParseKeyValuePairs(string input)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(input)) return result;

        var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                result[parts[0].ToLower().Replace(" ", "_")] = parts[1];
            }
        }

        return result;
    }

    /// <summary>
    ///     Parses modal field configuration from user input
    /// </summary>
    /// <param name="modalConfig">The modal configuration text</param>
    /// <returns>A ModalConfiguration object</returns>
    public static ModalConfiguration ParseModalConfiguration(string modalConfig)
    {
        var config = new ModalConfiguration();
        if (string.IsNullOrEmpty(modalConfig)) return config;

        var lines = modalConfig.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
            {
                config.Title = line[6..].Trim();
            }
            else if (line.StartsWith("- ") && line.Contains('|'))
            {
                var fieldParts = line[2..].Split('|', StringSplitOptions.TrimEntries);
                if (fieldParts.Length >= 2)
                {
                    var fieldId = fieldParts[0].ToLower().Replace(" ", "_");
                    var fieldConfig = new ModalFieldConfig
                    {
                        Label = fieldParts[0],
                        Style = fieldParts[1].ToLower() == "long" ? 2 : 1,
                        Required = fieldParts.Length > 2 && fieldParts[2].ToLower().Contains("required"),
                        MinLength = 1,
                        MaxLength = fieldParts[1].ToLower() == "long" ? 4000 : 1000
                    };
                    config.Fields[fieldId] = fieldConfig;
                }
            }
        }

        return config;
    }

    /// <summary>
    ///     Parses button style from string input
    /// </summary>
    /// <param name="styleString">The style string (primary, secondary, success, danger)</param>
    /// <returns>The corresponding ButtonStyle enum value</returns>
    public static ButtonStyle ParseButtonStyle(string styleString)
    {
        return styleString?.ToLower() switch
        {
            "primary" => ButtonStyle.Primary,
            "secondary" => ButtonStyle.Secondary,
            "success" => ButtonStyle.Success,
            "danger" => ButtonStyle.Danger,
            _ => ButtonStyle.Primary
        };
    }

    /// <summary>
    ///     Parses role mentions and names from input text
    /// </summary>
    /// <param name="roleInput">The role input text containing mentions or names</param>
    /// <param name="guild">The guild to search for roles</param>
    /// <returns>List of role IDs</returns>
    public static Task<List<ulong>> ParseRoles(string roleInput, IGuild guild)
    {
        var roleIds = new List<ulong>();
        if (string.IsNullOrEmpty(roleInput)) return Task.FromResult(roleIds);

        var roles = guild.Roles;
        var roleParts = roleInput.Split(',', StringSplitOptions.TrimEntries);

        foreach (var rolePart in roleParts)
        {
            // Handle role mention format <@&123456789>
            if (rolePart.StartsWith("<@&") && rolePart.EndsWith(">"))
            {
                var idString = rolePart[3..^1];
                if (ulong.TryParse(idString, out var roleId))
                {
                    roleIds.Add(roleId);
                    continue;
                }
            }

            // Handle @RoleName format
            var roleName = rolePart.StartsWith("@") ? rolePart[1..] : rolePart;
            var role = roles.FirstOrDefault(r =>
                string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));

            if (role != null)
            {
                roleIds.Add(role.Id);
            }
        }

        return Task.FromResult(roleIds);
    }

    /// <summary>
    ///     Creates an embed configuration from user input
    /// </summary>
    /// <param name="embedConfig">The embed configuration text</param>
    /// <returns>A JSON string representing the embed</returns>
    public static string CreateEmbedJson(string embedConfig)
    {
        var settings = ParseKeyValuePairs(embedConfig);

        var embed = new Embed
        {
            Title = settings.GetValueOrDefault("title", "Support Tickets"),
            Description = settings.GetValueOrDefault("description", "Click a button below to create a ticket!"),
            Color = ParseColor(settings.GetValueOrDefault("color", "blue"))
        };

        var newEmbed = new NewEmbed
        {
            Embed = embed
        };

        return JsonSerializer.Serialize(new[]
        {
            newEmbed
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    ///     Parses color from string input
    /// </summary>
    /// <param name="colorString">The color string (name or hex)</param>
    /// <returns>A Color object</returns>
    private static Color ParseColor(string colorString)
    {
        return colorString?.ToLower() switch
        {
            "red" => Color.Red,
            "green" => Color.Green,
            "blue" => Color.Blue,
            "yellow" => Color.Gold,
            "purple" => Color.Purple,
            "orange" => Color.Orange,
            "pink" => new Color(255, 105, 180),
            _ when colorString?.StartsWith("#") == true => ParseHexColor(colorString),
            _ => Color.Blue
        };
    }

    /// <summary>
    ///     Parses hex color string to Color object
    /// </summary>
    /// <param name="hex">The hex color string</param>
    /// <returns>A Color object</returns>
    private static Color ParseHexColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                var r = Convert.ToByte(hex[..2], 16);
                var g = Convert.ToByte(hex.Substring(2, 2), 16);
                var b = Convert.ToByte(hex.Substring(4, 2), 16);
                return new Color(r, g, b);
            }
        }
        catch
        {
            // Fall back to regular Mewdeko color if it somehow fails.
        }

        return Mewdeko.OkColor;
    }
}