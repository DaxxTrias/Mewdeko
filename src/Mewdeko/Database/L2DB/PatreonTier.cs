using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
/// Represents a Patreon tier/reward level cached for a guild
/// </summary>
[Table("PatreonTiers")]
public class PatreonTier
{
    /// <summary>
    /// Primary key for the tier record
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    /// Discord guild ID this tier is configured for
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Patreon tier ID from the API
    /// </summary>
    [Column("TierId")]
    public string TierId { get; set; } = null!;

    /// <summary>
    /// Title/name of the tier
    /// </summary>
    [Column("TierTitle")]
    public string TierTitle { get; set; } = null!;

    /// <summary>
    /// Required amount in cents for this tier
    /// </summary>
    [Column("AmountCents")]
    public int AmountCents { get; set; }

    /// <summary>
    /// Discord role ID to assign to supporters of this tier
    /// </summary>
    [Column("DiscordRoleId")]
    public ulong DiscordRoleId { get; set; }

    /// <summary>
    /// Description of the tier benefits
    /// </summary>
    [Column("Description")]
    public string? Description { get; set; }

    /// <summary>
    /// Date when this tier mapping was added to the database
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }

    /// <summary>
    /// Whether this tier mapping is currently active
    /// </summary>
    [Column("IsActive")]
    public bool IsActive { get; set; }
}