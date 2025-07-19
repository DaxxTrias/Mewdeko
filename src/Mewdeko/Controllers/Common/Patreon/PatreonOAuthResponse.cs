namespace Mewdeko.Controllers.Common.Patreon;

/// <summary>
///     Response model for OAuth URL generation
/// </summary>
public class PatreonOAuthResponse
{
    /// <summary>
    ///     The OAuth authorization URL
    /// </summary>
    public string AuthorizationUrl { get; set; } = null!;

    /// <summary>
    ///     The state parameter for the OAuth flow
    /// </summary>
    public string State { get; set; } = null!;
}