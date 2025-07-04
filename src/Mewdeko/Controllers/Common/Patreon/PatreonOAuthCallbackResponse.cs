namespace Mewdeko.Controllers.Common.Patreon;

/// <summary>
///     Response model for OAuth callback
/// </summary>
public class PatreonOAuthCallbackResponse
{
    /// <summary>
    ///     Whether the OAuth flow was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Success or error message
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    ///     Discord guild ID
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Patreon campaign ID
    /// </summary>
    public string? CampaignId { get; set; }
}