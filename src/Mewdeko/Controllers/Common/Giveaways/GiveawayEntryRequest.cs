namespace Mewdeko.Controllers.Common.Giveaways;

/// <summary>
///     Represents the request payload for entering a giveaway via the API.
/// </summary>
public class GiveawayEntryRequest
{
    /// <summary>
    ///     The ID of the guild where the giveaway is running.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The unique ID of the giveaway to enter.
    /// </summary>
    public int GiveawayId { get; set; }

    /// <summary>
    ///     The Discord user ID of the participant.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The Cloudflare Turnstile token for captcha verification.
    /// </summary>
    public string TurnstileToken { get; set; } = null!;
}