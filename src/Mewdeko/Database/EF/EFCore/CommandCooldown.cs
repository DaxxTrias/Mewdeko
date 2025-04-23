using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a cooldown configuration for a command.
/// </summary>
public class CommandCooldown : DbEntity
{
    /// <summary>
    ///     Gets or sets the cooldown duration in seconds.
    /// </summary>
    public int Seconds { get; set; }

    /// <summary>
    /// Gets or sets the guild Id.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the command this cooldown applies to.
    /// </summary>
    public string? CommandName { get; set; }
}