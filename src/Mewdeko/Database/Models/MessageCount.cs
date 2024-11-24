using System.ComponentModel.DataAnnotations;
using LinqToDB.Mapping;

namespace Mewdeko.Database.Models;

/// <summary>
///     Count of messages per user, channel, etc
/// </summary>
public class MessageCount
{

    /// <summary>
    ///     Gets or sets the unique identifier for the entity.
    /// </summary>
    [Key]
    [Identity]
    public long Id { get; set; }

    /// <summary>
    ///     Gets or sets the date and time when the entity was added.
    ///     Defaults to the current UTC date and time.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "timestamp without time zone")]
    public DateTime? DateAdded { get; set; } = DateTime.Now;

    /// <summary>
    ///     The guild to be able to look up count
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The channel for the message
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The UserId for lookups
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The count for this combination
    /// </summary>
    public ulong Count { get; set; }

    /// <summary>
    ///     Comma-separated string of timestamps for messages in the last 30 days
    /// </summary>
    public string RecentTimestamps { get; set; } = string.Empty;

    /// <summary>
    ///     Adds a new timestamp and removes any older than 30 days
    /// </summary>
    public void AddTimestamp(DateTime timestamp)
    {
        var timestamps = RecentTimestamps.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(DateTime.Parse)
            .Where(t => t >= DateTime.UtcNow.AddDays(-30))
            .Concat(new[]
            {
                timestamp
            })
            .OrderByDescending(t => t)
            .Take(1000) // Limit to 1000 timestamps to prevent excessive string length
            .ToList();

        RecentTimestamps = string.Join(",", timestamps.Select(t => t.ToString("O")));
    }
}