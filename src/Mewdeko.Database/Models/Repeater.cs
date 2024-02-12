using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

namespace Mewdeko.Database.Models;

[Table("GuildRepeater")]
public class Repeater : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong? LastMessageId { get; set; }
    public string Message { get; set; }
    public TimeSpan Interval { get; set; }
    public TimeSpan? StartTimeOfDay { get; set; }
    public bool NoRedundant { get; set; }
}