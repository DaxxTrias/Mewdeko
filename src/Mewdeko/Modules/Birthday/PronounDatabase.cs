using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Birthday;

/// <summary>
/// Represents a PronounDB user in version 2 of the database system.
/// </summary>
/// <remarks>
/// This class is intended to hold the pronoun sets associated with a user.
/// Typically used for working with the PronounDB API data.
/// </remarks>
public class PronounDbV2User
{
    /// <summary>
    /// Gets or sets the dictionary containing pronoun sets for a user.
    /// </summary>
    /// <remarks>
    /// Each key in the dictionary represents a pronoun set identifier, and its corresponding value is an array of pronouns.
    /// Typically utilized to map pronoun set identifiers to their respective pronouns from the PronounDB data.
    /// </remarks>
    [JsonPropertyName("sets")]
    public Dictionary<string, string[]> Sets { get; set; } = new();
}