namespace Mewdeko.Controllers.Common.Giveaways;

/// <summary>
///     Represents the expected response structure from the Cloudflare Turnstile verification endpoint.
/// </summary>
public class TurnstileVerificationResponse
{
    /// <summary>
    ///     Indicates whether the token verification was successful.
    /// </summary>
    public bool Success { get; set; }
}