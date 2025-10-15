using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     Response model for OAuth2 token requests (both initial and refresh)
/// </summary>
public class TokenResponse
{
    /// <summary>
    ///     The access token that can be used to make API calls
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    ///     The refresh token that can be used to get a new access token when the current one expires
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    ///     Token lifetime duration in seconds
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    ///     The scopes that this token has access to
    /// </summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    ///     The type of token, typically "Bearer"
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    ///     Calculates when this token will expire based on current time and expires_in
    /// </summary>
    public DateTime ExpiresAt => DateTime.UtcNow.AddSeconds(ExpiresIn);

    /// <summary>
    ///     Checks if the token is expired or will expire within the specified buffer time
    /// </summary>
    /// <param name="bufferSeconds">
    ///     Buffer time in seconds before expiration to consider token expired (default: 300 = 5
    ///     minutes)
    /// </param>
    /// <returns>True if the token is expired or will expire soon</returns>
    public bool IsExpired(int bufferSeconds = 300)
    {
        return DateTime.UtcNow.AddSeconds(bufferSeconds) >= ExpiresAt;
    }
}

/// <summary>
///     Request model for obtaining an access token
/// </summary>
public class TokenRequest
{
    /// <summary>
    ///     The authorization code received from the OAuth redirect
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    ///     Must be "authorization_code" for initial token requests
    /// </summary>
    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; } = "authorization_code";

    /// <summary>
    ///     Your client ID
    /// </summary>
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    ///     Your client secret
    /// </summary>
    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    ///     The redirect URI that was used in the authorization request
    /// </summary>
    [JsonPropertyName("redirect_uri")]
    public string RedirectUri { get; set; } = string.Empty;
}

/// <summary>
///     Request model for refreshing an access token
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    ///     Must be "refresh_token" for refresh requests
    /// </summary>
    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; } = "refresh_token";

    /// <summary>
    ///     The user's refresh token
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    ///     Your client ID
    /// </summary>
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    ///     Your client secret
    /// </summary>
    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>
///     OAuth error response model
/// </summary>
public class OAuthError
{
    /// <summary>
    ///     The error code
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    /// <summary>
    ///     Human-readable description of the error
    /// </summary>
    [JsonPropertyName("error_description")]
    public string ErrorDescription { get; set; } = string.Empty;

    /// <summary>
    ///     URI to more information about the error
    /// </summary>
    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; set; }
}

/// <summary>
///     Constants for OAuth2 grant types
/// </summary>
public static class GrantTypes
{
    /// <summary>
    ///     Authorization code grant type for initial token requests
    /// </summary>
    public const string AuthorizationCode = "authorization_code";

    /// <summary>
    ///     Refresh token grant type for refreshing access tokens
    /// </summary>
    public const string RefreshToken = "refresh_token";
}

/// <summary>
///     Constants for OAuth2 scopes available in Patreon API v2
/// </summary>
public static class Scopes
{
    /// <summary>
    ///     Provides read access to data about the user
    /// </summary>
    public const string Identity = "identity";

    /// <summary>
    ///     Provides read access to the user's email
    /// </summary>
    public const string IdentityEmail = "identity[email]";

    /// <summary>
    ///     Provides read access to the user's memberships
    /// </summary>
    public const string IdentityMemberships = "identity.memberships";

    /// <summary>
    ///     Provides read access to basic campaign data
    /// </summary>
    public const string Campaigns = "campaigns";

    /// <summary>
    ///     Provides read, write, update, and delete access to the campaign's webhooks
    /// </summary>
    public const string CampaignsWebhook = "w:campaigns.webhook";

    /// <summary>
    ///     Provides read access to data about a campaign's members
    /// </summary>
    public const string CampaignsMembers = "campaigns.members";

    /// <summary>
    ///     Provides read access to the member's email
    /// </summary>
    public const string CampaignsMembersEmail = "campaigns.members[email]";

    /// <summary>
    ///     Provides read access to the member's address
    /// </summary>
    public const string CampaignsMembersAddress = "campaigns.members.address";

    /// <summary>
    ///     Provides read access to the posts on a campaign
    /// </summary>
    public const string CampaignsPosts = "campaigns.posts";

    /// <summary>
    ///     Helper method to build scope string from multiple scopes
    /// </summary>
    /// <param name="scopes">Array of scope constants</param>
    /// <returns>Space-separated scope string</returns>
    public static string BuildScopeString(params string[] scopes)
    {
        return string.Join(" ", scopes);
    }
}