namespace Mewdeko.Controllers.Common.MultiGreets;

/// <summary>
///     Request model for webhook updates
/// </summary>
public class WebhookUpdateRequest
{
    /// <summary>
    ///     The name of the webhook
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The URL of the webhook's avatar image
    /// </summary>
    public string? AvatarUrl { get; set; }
}