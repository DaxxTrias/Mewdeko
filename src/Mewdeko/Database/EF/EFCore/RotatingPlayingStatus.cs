using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a rotating playing status.
/// </summary>
[Table("RotatingStatus")]
public class RotatingPlayingStatus : DbEntity
{
    /// <summary>
    ///     Gets or sets the status.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    ///     Gets or sets the activity type.
    /// </summary>
    public ActivityType Type { get; set; }
}