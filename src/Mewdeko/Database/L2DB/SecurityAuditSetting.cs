using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("SecurityAuditSettings")]
public class SecurityAuditSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    [Column("AlertChannelId")]
    public ulong? AlertChannelId { get; set; }

    [Column("DmBotOwnersOnCritical")]
    public bool DmBotOwnersOnCritical { get; set; }

    [Column("PingRoleId")]
    public ulong? PingRoleId { get; set; }

    [Column("LastSeenAuditLogId")]
    public ulong? LastSeenAuditLogId { get; set; }

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
