#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a moderator-configured name that anti-pattern protection matches after normalization.
/// </summary>
[Table("AntiPatternName")]
public class AntiPatternName
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the anti-pattern setting this watched name belongs to.
    /// </summary>
    [Column("AntiPatternSettingId")]
    public int AntiPatternSettingId { get; set; }

    /// <summary>
    ///     Gets or sets the original moderator-entered name.
    /// </summary>
    [Column("OriginalName")]
    public string OriginalName { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the normalized form used for matching.
    /// </summary>
    [Column("NormalizedName")]
    public string NormalizedName { get; set; } = null!;

    /// <summary>
    ///     Gets or sets whether this watched name checks Discord usernames.
    /// </summary>
    [Column("CheckUsername")]
    public bool CheckUsername { get; set; }

    /// <summary>
    ///     Gets or sets whether this watched name checks display names and nicknames.
    /// </summary>
    [Column("CheckDisplayName")]
    public bool CheckDisplayName { get; set; }

    /// <summary>
    ///     Gets or sets when this watched name was added.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}
