namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Represents a currency reward to be granted to a user.
/// </summary>
public class CurrencyRewardItem
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID to receive the reward.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the currency amount to award.
    /// </summary>
    public long Amount { get; set; }
}