using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a group name associated with a guild configuration.
/// </summary>
public class GroupName : DbEntity
{
    /// <summary>
    /// Gets or sets the guild Id.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the group number.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    ///     Gets or sets the name of the group.
    /// </summary>
    public string? Name { get; set; }
}