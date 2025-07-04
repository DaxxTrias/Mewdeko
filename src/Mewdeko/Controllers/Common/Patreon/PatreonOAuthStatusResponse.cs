namespace Mewdeko.Controllers.Common.Patreon;

/// <summary>
///     Response model for OAuth status
/// </summary>
public class PatreonOAuthStatusResponse
{
    /// <summary>
    ///     Whether Patreon integration is configured for this guild
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>
    ///     Patreon campaign ID if configured
    /// </summary>
    public string? CampaignId { get; set; }

    /// <summary>
    ///     Last time supporters were synced
    /// </summary>
    public DateTime? LastSync { get; set; }

    /// <summary>
    ///     When the OAuth token expires
    /// </summary>
    public DateTime? TokenExpiry { get; set; }
}