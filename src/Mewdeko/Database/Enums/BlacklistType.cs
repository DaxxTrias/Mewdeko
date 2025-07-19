namespace Mewdeko.Database.Enums;

/// <summary>
///     Represents the types of items that can be blacklisted.
/// </summary>
public enum BlacklistType
{
    /// <summary>
    ///     Represents a blacklisted server.
    /// </summary>
    Server,

    /// <summary>
    ///     Represents a blacklisted channel.
    /// </summary>
    Channel,

    /// <summary>
    ///     Represents a blacklisted user.
    /// </summary>
    User
}