using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Database entity representing reputation requirements for using specific commands.
/// </summary>
[Table("RepCommandRequirements")]
public class RepCommandRequirement
{
    /// <summary>
    ///     The guild ID where this requirement applies.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The command name that has reputation requirements.
    /// </summary>
    [Column("CommandName", CanBeNull = false)]
    public string CommandName { get; set; } = null!;

    /// <summary>
    ///     The minimum reputation required to use this command.
    /// </summary>
    [Column("MinReputation")]
    public int MinReputation { get; set; }

    /// <summary>
    ///     The specific reputation type required (if null, uses total reputation).
    /// </summary>
    [Column("RequiredRepType", CanBeNull = true)]
    public string? RequiredRepType { get; set; }

    /// <summary>
    ///     Channels where this requirement applies (if null, applies to all channels).
    /// </summary>
    [Column("RestrictedChannels", CanBeNull = true)]
    public string? RestrictedChannels { get; set; }

    /// <summary>
    ///     The message to show when user doesn't meet requirements.
    /// </summary>
    [Column("DenialMessage", CanBeNull = true)]
    public string? DenialMessage { get; set; }

    /// <summary>
    ///     Whether this requirement is currently active.
    /// </summary>
    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Roles that bypass this requirement.
    /// </summary>
    [Column("BypassRoles", CanBeNull = true)]
    public string? BypassRoles { get; set; }

    /// <summary>
    ///     Whether to show the reputation requirement in command help.
    /// </summary>
    [Column("ShowInHelp")]
    public bool ShowInHelp { get; set; } = true;
}