namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Represents a role reward to be granted to a user.
/// </summary>
public class RoleRewardItem
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID to receive the role.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the role ID to award.
    /// </summary>
    public ulong RoleId { get; set; }
}