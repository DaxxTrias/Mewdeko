using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepCustomType")]
public class RepCustomType
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("TypeName")]
    public string? TypeName { get; set; }

    [Column("DisplayName")]
    public string? DisplayName { get; set; }

    [Column("Description")]
    public string? Description { get; set; }

    [Column("EmojiIcon")]
    public string? EmojiIcon { get; set; }

    [Column("Color")]
    public string? Color { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("Multiplier")]
    public decimal Multiplier { get; set; } = 1.0m;

    [Column("CountsTowardTotal")]
    public bool CountsTowardTotal { get; set; } = true;

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}