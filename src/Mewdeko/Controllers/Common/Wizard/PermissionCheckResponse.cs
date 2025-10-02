namespace Mewdeko.Controllers.Common.Wizard;

/// <summary>
///     Response model for bot permission verification
/// </summary>
public class PermissionCheckResponse
{
    /// <summary>
    ///     Guild ID being checked
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Bot user ID
    /// </summary>
    public ulong BotId { get; set; }

    /// <summary>
    ///     Whether the bot has all required permissions
    /// </summary>
    public bool HasAllRequiredPermissions { get; set; }

    /// <summary>
    ///     Individual permission check results
    /// </summary>
    public PermissionCheckResult[] PermissionResults { get; set; } = Array.Empty<PermissionCheckResult>();

    /// <summary>
    ///     Missing permissions that are critical
    /// </summary>
    public GuildPermission[] MissingCriticalPermissions { get; set; } = Array.Empty<GuildPermission>();

    /// <summary>
    ///     Missing permissions that are recommended
    /// </summary>
    public GuildPermission[] MissingRecommendedPermissions { get; set; } = Array.Empty<GuildPermission>();

    /// <summary>
    ///     Suggested invite URL to fix permission issues
    /// </summary>
    public string? SuggestedInviteUrl { get; set; }

    /// <summary>
    ///     Whether the bot can function with current permissions
    /// </summary>
    public bool CanFunction { get; set; }

    /// <summary>
    ///     Overall health status
    /// </summary>
    public PermissionHealthStatus HealthStatus { get; set; }
}

/// <summary>
///     Result for individual permission check
/// </summary>
public class PermissionCheckResult
{
    /// <summary>
    ///     The permission being checked
    /// </summary>
    public GuildPermission Permission { get; set; }

    /// <summary>
    ///     Human-readable name of the permission
    /// </summary>
    public string PermissionName { get; set; } = "";

    /// <summary>
    ///     Whether the bot has this permission
    /// </summary>
    public bool HasPermission { get; set; }

    /// <summary>
    ///     Importance level of this permission
    /// </summary>
    public PermissionImportance Importance { get; set; }

    /// <summary>
    ///     Description of what this permission is used for
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    ///     Features that require this permission
    /// </summary>
    public string[] RequiredForFeatures { get; set; } = Array.Empty<string>();
}

/// <summary>
///     Permission importance levels
/// </summary>
public enum PermissionImportance
{
    /// <summary>
    ///     Bot cannot function without this permission
    /// </summary>
    Critical,

    /// <summary>
    ///     Strongly recommended for proper functionality
    /// </summary>
    Recommended,

    /// <summary>
    ///     Nice to have for enhanced features
    /// </summary>
    Optional
}

/// <summary>
///     Overall permission health status
/// </summary>
public enum PermissionHealthStatus
{
    /// <summary>
    ///     All critical and recommended permissions present
    /// </summary>
    Excellent,

    /// <summary>
    ///     All critical permissions present, some recommended missing
    /// </summary>
    Good,

    /// <summary>
    ///     Some critical permissions missing but bot can still function
    /// </summary>
    Warning,

    /// <summary>
    ///     Missing too many critical permissions, bot may not work properly
    /// </summary>
    Poor
}