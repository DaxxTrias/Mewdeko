using System.ComponentModel.DataAnnotations;

namespace Mewdeko.Modules.Reputation.Common;

/// <summary>
///     Data structure for importing/exporting reputation data.
/// </summary>
public class RepImportData
{
    /// <summary>
    ///     Source system (MEE6, Tatsumaki, UnbelievaBoat, Mewdeko).
    /// </summary>
    [Required]
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>
    ///     Version of the export format.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    ///     When this export was created.
    /// </summary>
    public DateTime ExportedAt { get; set; }

    /// <summary>
    ///     Guild ID this data is from.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Guild name for reference.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    ///     User reputation data.
    /// </summary>
    public List<RepUserImportData> Users { get; set; } = new();

    /// <summary>
    ///     Configuration settings (optional).
    /// </summary>
    public RepConfigImportData? Configuration { get; set; }

    /// <summary>
    ///     Custom reputation types (optional).
    /// </summary>
    public List<RepCustomTypeImportData>? CustomTypes { get; set; }

    /// <summary>
    ///     Role rewards (optional).
    /// </summary>
    public List<RepRoleRewardImportData>? RoleRewards { get; set; }
}

/// <summary>
///     User data for import/export.
/// </summary>
public class RepUserImportData
{
    /// <summary>
    ///     User ID.
    /// </summary>
    [Required]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Username for reference.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    ///     Total reputation (or equivalent from source system).
    /// </summary>
    public int TotalRep { get; set; }

    /// <summary>
    ///     Custom reputation values (type -> amount).
    /// </summary>
    public Dictionary<string, int>? CustomRep { get; set; }

    /// <summary>
    ///     Current streak (if available).
    /// </summary>
    public int? CurrentStreak { get; set; }

    /// <summary>
    ///     Longest streak (if available).
    /// </summary>
    public int? LongestStreak { get; set; }

    /// <summary>
    ///     Last activity date (if available).
    /// </summary>
    public DateTime? LastActivity { get; set; }

    /// <summary>
    ///     Additional data from source system (JSON).
    /// </summary>
    public string? SourceData { get; set; }
}

/// <summary>
///     Configuration data for import.
/// </summary>
public class RepConfigImportData
{
    /// <summary>
    ///     Whether reputation is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Default cooldown in minutes.
    /// </summary>
    public int DefaultCooldownMinutes { get; set; } = 60;

    /// <summary>
    ///     Daily reputation limit.
    /// </summary>
    public int DailyLimit { get; set; } = 10;

    /// <summary>
    ///     Weekly reputation limit.
    /// </summary>
    public int? WeeklyLimit { get; set; }

    /// <summary>
    ///     Other configuration as JSON.
    /// </summary>
    public string? AdditionalConfig { get; set; }
}

/// <summary>
///     Custom reputation type for import.
/// </summary>
public class RepCustomTypeImportData
{
    /// <summary>
    ///     Type name.
    /// </summary>
    [Required]
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    ///     Display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     Description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Emoji icon.
    /// </summary>
    public string? EmojiIcon { get; set; }

    /// <summary>
    ///     Multiplier for this type.
    /// </summary>
    public decimal Multiplier { get; set; } = 1.0m;
}

/// <summary>
///     Role reward data for import.
/// </summary>
public class RepRoleRewardImportData
{
    /// <summary>
    ///     Role ID (may need mapping).
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Role name for reference.
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    ///     Reputation required.
    /// </summary>
    public int RepRequired { get; set; }

    /// <summary>
    ///     Remove on reputation drop.
    /// </summary>
    public bool RemoveOnDrop { get; set; } = true;
}

/// <summary>
///     Result of an import operation.
/// </summary>
public class RepImportResult
{
    /// <summary>
    ///     Whether the import was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Number of users imported.
    /// </summary>
    public int UsersImported { get; set; }

    /// <summary>
    ///     Number of users skipped.
    /// </summary>
    public int UsersSkipped { get; set; }

    /// <summary>
    ///     Number of users updated.
    /// </summary>
    public int UsersUpdated { get; set; }

    /// <summary>
    ///     Error messages if any.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    ///     Warning messages if any.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    ///     Detailed log of the import process.
    /// </summary>
    public List<string> Log { get; set; } = new();
}