using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a username associated with a user ID.
/// </summary>
public class Usernames : DbEntity
{
    /// <summary>
    ///     Gets or sets the username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    public ulong UserId { get; set; }
}