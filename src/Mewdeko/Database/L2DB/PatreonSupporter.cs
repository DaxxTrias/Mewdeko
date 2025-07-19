using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a Patreon supporter/patron cached for a guild
/// </summary>
[Table("PatreonSupporters")]
public class PatreonSupporter
{
    /// <summary>
    ///     Primary key for the supporter record
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Discord guild ID this supporter is associated with
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Patreon user ID from the API
    /// </summary>
    [Column("PatreonUserId")]
    public string PatreonUserId { get; set; } = null!;

    /// <summary>
    ///     Discord user ID if linked to this supporter
    /// </summary>
    [Column("DiscordUserId")]
    public ulong DiscordUserId { get; set; }

    /// <summary>
    ///     Full name of the supporter
    /// </summary>
    [Column("FullName")]
    public string FullName { get; set; } = null!;

    /// <summary>
    ///     Email address of the supporter
    /// </summary>
    [Column("Email")]
    public string? Email { get; set; }

    /// <summary>
    ///     Current tier ID the supporter is entitled to
    /// </summary>
    [Column("TierId")]
    public string? TierId { get; set; }

    /// <summary>
    ///     Current pledge amount in cents
    /// </summary>
    [Column("AmountCents")]
    public int AmountCents { get; set; }

    /// <summary>
    ///     Current patron status (active_patron, declined_patron, former_patron)
    /// </summary>
    [Column("PatronStatus")]
    public string PatronStatus { get; set; } = null!;

    /// <summary>
    ///     Date when the pledge relationship started
    /// </summary>
    [Column("PledgeRelationshipStart")]
    public DateTime? PledgeRelationshipStart { get; set; }

    /// <summary>
    ///     Date of the last successful charge
    /// </summary>
    [Column("LastChargeDate")]
    public DateTime? LastChargeDate { get; set; }

    /// <summary>
    ///     Status of the last charge attempt
    /// </summary>
    [Column("LastChargeStatus")]
    public string? LastChargeStatus { get; set; }

    /// <summary>
    ///     Total amount in cents supported over the supporter's lifetime
    /// </summary>
    [Column("LifetimeAmountCents")]
    public int LifetimeAmountCents { get; set; }

    /// <summary>
    ///     Amount in cents the supporter is currently entitled to based on their pledge
    /// </summary>
    [Column("CurrentlyEntitledAmountCents")]
    public int CurrentlyEntitledAmountCents { get; set; }

    /// <summary>
    ///     Date when this supporter record was last updated from the API
    /// </summary>
    [Column("LastUpdated")]
    public DateTime LastUpdated { get; set; }
}