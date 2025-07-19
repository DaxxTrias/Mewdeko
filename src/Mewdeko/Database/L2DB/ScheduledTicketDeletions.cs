using DataModel;
using LinqToDB.Mapping;

namespace Mewdeko.Database.L2DB;

/// <summary>
///     Represents a scheduled ticket deletion in the database
/// </summary>
[Table("ScheduledTicketDeletions")]
public class ScheduledTicketDeletion
{
    /// <summary>
    ///     Primary key for the scheduled deletion record
    /// </summary>
    [PrimaryKey]
    [Identity]
    public int Id { get; set; }

    /// <summary>
    ///     The ID of the ticket to be deleted
    /// </summary>
    [Column("TicketId")]
    [NotNull]
    public int TicketId { get; set; }

    /// <summary>
    ///     The guild ID where the ticket exists
    /// </summary>
    [Column("GuildId")]
    [NotNull]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The channel ID of the ticket to be deleted
    /// </summary>
    [Column("ChannelId")]
    [NotNull]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     When the deletion was scheduled
    /// </summary>
    [Column("ScheduledAt")]
    [NotNull]
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When the deletion should be executed
    /// </summary>
    [Column("ExecuteAt")]
    [NotNull]
    public DateTime ExecuteAt { get; set; }

    /// <summary>
    ///     Whether the deletion has been processed
    /// </summary>
    [Column("IsProcessed")]
    [NotNull]
    public bool IsProcessed { get; set; } = false;

    /// <summary>
    ///     When the deletion was processed (if processed)
    /// </summary>
    [Column("ProcessedAt")]
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    ///     Reason for failure if the deletion failed
    /// </summary>
    [Column("FailureReason")]
    public string? FailureReason { get; set; }

    /// <summary>
    ///     Number of times this deletion has been retried
    /// </summary>
    [Column("RetryCount")]
    [NotNull]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    ///     Navigation property back to the ticket
    /// </summary>
    [Association(ThisKey = nameof(TicketId), OtherKey = nameof(Ticket.Id))]
    public Ticket Ticket { get; set; } = null!;
}