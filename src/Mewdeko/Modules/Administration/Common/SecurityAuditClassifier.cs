namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     Classifies normalized audit-log entries into critical security events.
/// </summary>
public static class SecurityAuditClassifier
{
    /// <summary>
    ///     Determines whether an audit-log event represents Administrator permission escalation.
    /// </summary>
    /// <param name="auditEvent">The normalized audit event.</param>
    /// <returns>A critical match when the event grants Administrator; otherwise <see langword="null"/>.</returns>
    public static SecurityAuditMatch? Classify(SecurityAuditEvent auditEvent)
    {
        if (!HasAdministrator(auditEvent.AfterPermissions))
            return null;

        var isBotOrIntegration = auditEvent.TargetIsBot || auditEvent.RoleIsManaged ||
                                 !string.IsNullOrWhiteSpace(auditEvent.IntegrationType) ||
                                 auditEvent.ActionType is ActionType.BotAdded or ActionType.IntegrationCreated
                                     or ActionType.IntegrationUpdated;

        return auditEvent.ActionType switch
        {
            ActionType.RoleCreated => new SecurityAuditMatch(
                SecurityAuditRule.AdministratorRoleCreated,
                $"Role {FormatRole(auditEvent)} was created with Administrator.",
                isBotOrIntegration),

            ActionType.RoleUpdated when !HasAdministrator(auditEvent.BeforePermissions) => new SecurityAuditMatch(
                SecurityAuditRule.AdministratorPermissionAdded,
                $"Role {FormatRole(auditEvent)} was granted Administrator.",
                isBotOrIntegration),

            ActionType.MemberRoleUpdated => new SecurityAuditMatch(
                SecurityAuditRule.AdministratorRoleAssigned,
                $"{FormatUser(auditEvent)} was given Administrator via {FormatRole(auditEvent)}.",
                isBotOrIntegration),

            ActionType.BotAdded => new SecurityAuditMatch(
                SecurityAuditRule.AdministratorBotAdded,
                $"{FormatUser(auditEvent)} was added and currently has Administrator.",
                true),

            ActionType.IntegrationCreated or ActionType.IntegrationUpdated => new SecurityAuditMatch(
                SecurityAuditRule.AdministratorIntegrationRole,
                $"Integration role {FormatRole(auditEvent)} has Administrator.",
                true),

            _ => null
        };
    }

    private static bool HasAdministrator(GuildPermissions? permissions)
    {
        return permissions?.Has(GuildPermission.Administrator) == true;
    }

    private static string FormatRole(SecurityAuditEvent auditEvent)
    {
        if (auditEvent.RoleId is null)
            return auditEvent.RoleName ?? "unknown role";

        return string.IsNullOrWhiteSpace(auditEvent.RoleName)
            ? auditEvent.RoleId.Value.ToString()
            : $"{auditEvent.RoleName} ({auditEvent.RoleId.Value})";
    }

    private static string FormatUser(SecurityAuditEvent auditEvent)
    {
        if (auditEvent.TargetUserId is null)
            return auditEvent.TargetUserName ?? "unknown user";

        return string.IsNullOrWhiteSpace(auditEvent.TargetUserName)
            ? auditEvent.TargetUserId.Value.ToString()
            : $"{auditEvent.TargetUserName} ({auditEvent.TargetUserId.Value})";
    }
}
