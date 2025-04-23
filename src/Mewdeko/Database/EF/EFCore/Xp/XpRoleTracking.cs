using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;

namespace Mewdeko.Database.EF.EFCore.Xp;

/// <summary>
///     Represents XP tracking for a role over a specific period.
/// </summary>
[Table("XpRoleTracking")]
public class XpRoleTracking : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the role ID.
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Gets or sets the total XP gained by members with this role.
    /// </summary>
    public long TotalXpGained { get; set; }

    /// <summary>
    ///     Gets or sets when the tracking started.
    /// </summary>
    public DateTime StartTracking { get; set; }

    /// <summary>
    ///     Gets or sets when the tracking ended (null if ongoing).
    /// </summary>
    public DateTime? EndTracking { get; set; } = null;

    /// <summary>
    ///     Gets or sets the title of the tracking period (e.g., "March Competition").
    /// </summary>
    public string TrackingTitle { get; set; } = string.Empty;

    /// <summary>
    ///     Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return GuildId.GetHashCode() ^ RoleId.GetHashCode() ^ StartTracking.GetHashCode();
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj)
    {
        return obj is XpRoleTracking xrt &&
               xrt.GuildId == GuildId &&
               xrt.RoleId == RoleId &&
               xrt.StartTracking == StartTracking;
    }
}