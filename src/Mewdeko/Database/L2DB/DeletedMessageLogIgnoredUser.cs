using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("DeletedMessageLogIgnoredUsers")]
public class DeletedMessageLogIgnoredUser
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("Note")]
    public string? Note { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    [Column("DateModified")]
    public DateTime? DateModified { get; set; }
}
