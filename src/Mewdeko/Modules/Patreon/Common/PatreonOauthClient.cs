using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     A client created by a developer, used for getting OAuth2 access tokens
/// </summary>
public class OAuthClient : PatreonResource
{
    /// <summary>
    ///     OAuth client attributes containing all the client's properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public OAuthClientAttributes Attributes { get; set; } = new();

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public OAuthClientRelationships? Relationships { get; set; }
}

/// <summary>
///     All attributes for a Patreon OAuth client
/// </summary>
public class OAuthClientAttributes
{
    /// <summary>
    ///     The author name provided during client setup. Can be null.
    /// </summary>
    [JsonPropertyName("author_name")]
    public string? AuthorName { get; set; }

    /// <summary>
    ///     The client's secret
    /// </summary>
    [JsonPropertyName("client_secret")]
    public string? ClientSecret { get; set; }

    /// <summary>
    ///     The description provided during client setup
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    ///     The domain provided during client setup. Can be null.
    /// </summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    /// <summary>
    ///     The URL of the icon used in the OAuth authorization flow. Can be null.
    /// </summary>
    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }

    /// <summary>
    ///     The name provided during client setup
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     The URL of the privacy policy provided during client setup. Can be null.
    /// </summary>
    [JsonPropertyName("privacy_policy_url")]
    public string? PrivacyPolicyUrl { get; set; }

    /// <summary>
    ///     The allowable redirect URIs for the OAuth authorization flow
    /// </summary>
    [JsonPropertyName("redirect_uris")]
    public string? RedirectUris { get; set; }

    /// <summary>
    ///     The URL of the terms of service provided during client setup. Can be null.
    /// </summary>
    [JsonPropertyName("tos_url")]
    public string? TosUrl { get; set; }

    /// <summary>
    ///     The Patreon API version the client is targeting
    /// </summary>
    [JsonPropertyName("version")]
    public int? Version { get; set; }

    /// <summary>
    ///     Client category
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }
}

/// <summary>
///     OAuth client relationships to other resources
/// </summary>
public class OAuthClientRelationships
{
    /// <summary>
    ///     The campaign of the user who created the OAuth Client
    /// </summary>
    [JsonPropertyName("campaign")]
    public SingleRelationship? Campaign { get; set; }

    /// <summary>
    ///     The user who created the OAuth Client
    /// </summary>
    [JsonPropertyName("user")]
    public SingleRelationship? User { get; set; }
}