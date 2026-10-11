namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     Normalized audit-log event data used by the security audit classifier.
/// </summary>
public sealed record SecurityAuditEvent(
    ulong AuditLogId,
    ActionType ActionType,
    ulong GuildId,
    string GuildName,
    ulong ExecutorId,
    string ExecutorName,
    ulong? TargetUserId,
    string? TargetUserName,
    ulong? RoleId,
    string? RoleName,
    GuildPermissions? BeforePermissions,
    GuildPermissions? AfterPermissions,
    bool TargetIsBot,
    bool RoleIsManaged,
    string? IntegrationType,
    string? Reason,
    DateTimeOffset CreatedAt);

/// <summary>
///     Administrator escalation patterns monitored by the security audit service.
/// </summary>
public enum SecurityAuditRule
{
    /// <summary>
    ///     A new role was created with Administrator.
    /// </summary>
    AdministratorRoleCreated,

    /// <summary>
    ///     Administrator was added to an existing role.
    /// </summary>
    AdministratorPermissionAdded,

    /// <summary>
    ///     A member received a role that already has Administrator.
    /// </summary>
    AdministratorRoleAssigned,

    /// <summary>
    ///     A bot currently has Administrator immediately after being added.
    /// </summary>
    AdministratorBotAdded,

    /// <summary>
    ///     An integration-created or integration-managed role has Administrator.
    /// </summary>
    AdministratorIntegrationRole
}

/// <summary>
///     Result of classifying a normalized security audit event.
/// </summary>
public sealed record SecurityAuditMatch(
    SecurityAuditRule Rule,
    string Summary,
    bool IsBotOrIntegrationRelated);
