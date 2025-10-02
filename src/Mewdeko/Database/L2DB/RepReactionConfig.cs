using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepReactionConfig")]
public class RepReactionConfig
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("EmojiName", CanBeNull = false)]
    public string EmojiName { get; set; } = null!;

    [Column("EmojiId")]
    public ulong? EmojiId { get; set; }

    [Column("RepAmount")]
    public int RepAmount { get; set; } = 1;

    [Column("RepType", CanBeNull = false)]
    public string RepType { get; set; } = "standard";

    [Column("CooldownMinutes")]
    public int CooldownMinutes { get; set; } = 60;

    [Column("RequiredRoleId")]
    public ulong? RequiredRoleId { get; set; }

    [Column("MinMessageAgeMinutes")]
    public int MinMessageAgeMinutes { get; set; } = 0;

    [Column("MinMessageLength")]
    public int MinMessageLength { get; set; } = 0;

    [Column("IsEnabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("AllowedChannels")]
    public string? AllowedChannels { get; set; }

    [Column("AllowedReceiverRoles")]
    public string? AllowedReceiverRoles { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}