using LinqToDB;
using LinqToDB.Mapping;
using Mewdeko.Database.Enums;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A single dashboard activity record.
/// </summary>
[Table("DashboardAuditLogs")]
public class DashboardAuditLog
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild the action was scoped to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The Discord user id behind the request.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     The Discord username at the time of the action.
    /// </summary>
    [Column("UserName")]
    public string UserName { get; set; } = "";

    /// <summary>
    ///     The kind of activity recorded.
    /// </summary>
    [Column("Action")]
    public AuditAction Action { get; set; }

    /// <summary>
    ///     The dashboard section the action belonged to.
    /// </summary>
    [Column("Section")]
    public string Section { get; set; } = "";

    /// <summary>
    ///     The bot API endpoint that was hit.
    /// </summary>
    [Column("Endpoint")]
    public string Endpoint { get; set; } = "";

    /// <summary>
    ///     The HTTP method of the request.
    /// </summary>
    [Column("HttpMethod")]
    public string HttpMethod { get; set; } = "";

    /// <summary>
    ///     For mutations, a JSON document describing the change.
    /// </summary>
    [Column("Changes", DataType = DataType.BinaryJson)]
    public string? Changes { get; set; }

    /// <summary>
    ///     The client user agent, when available.
    /// </summary>
    [Column("UserAgent")]
    public string? UserAgent { get; set; }

    /// <summary>
    ///     When the action occurred (UTC).
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
