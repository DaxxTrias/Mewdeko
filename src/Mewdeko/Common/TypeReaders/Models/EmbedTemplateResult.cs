namespace Mewdeko.Common.TypeReaders.Models;

/// <summary>
///     Represents the result of parsing an embed template or JSON, containing all components
///     that SmartEmbed.TryParse returns, plus the original input for storage.
/// </summary>
public class EmbedTemplateResult
{
    /// <summary>
    ///     The parsed Discord embeds.
    /// </summary>
    public Discord.Embed[]? Embeds { get; set; }

    /// <summary>
    ///     The plain text content.
    /// </summary>
    public string? PlainText { get; set; }

    /// <summary>
    ///     The component builder for Discord components.
    /// </summary>
    public ComponentBuilder? Components { get; set; }

    /// <summary>
    ///     The content that should be stored in the database.
    ///     - For template references: the actual template JSON
    ///     - For raw JSON/embed: the original JSON input
    ///     - For plain text: the original text input
    /// </summary>
    public string ContentToStore { get; set; } = "";
}