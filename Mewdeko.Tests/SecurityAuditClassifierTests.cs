using Discord;
using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Tests;

[TestFixture]
public class SecurityAuditClassifierTests
{
    [Test]
    public void Classify_RoleCreatedWithAdministrator_ReturnsCriticalMatch()
    {
        var result = SecurityAuditClassifier.Classify(CreateEvent(
            ActionType.RoleCreated,
            afterPermissions: new GuildPermissions(administrator: true)));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Rule, Is.EqualTo(SecurityAuditRule.AdministratorRoleCreated));
    }

    [Test]
    public void Classify_RoleUpdatedAddingAdministrator_ReturnsCriticalMatch()
    {
        var result = SecurityAuditClassifier.Classify(CreateEvent(
            ActionType.RoleUpdated,
            beforePermissions: new GuildPermissions(manageRoles: true),
            afterPermissions: new GuildPermissions(administrator: true)));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Rule, Is.EqualTo(SecurityAuditRule.AdministratorPermissionAdded));
    }

    [Test]
    public void Classify_RoleUpdatedAlreadyHadAdministrator_IgnoresEvent()
    {
        var result = SecurityAuditClassifier.Classify(CreateEvent(
            ActionType.RoleUpdated,
            beforePermissions: new GuildPermissions(administrator: true),
            afterPermissions: new GuildPermissions(administrator: true)));

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Classify_MemberRoleUpdatedWithAdminRole_ReturnsCriticalMatch()
    {
        var result = SecurityAuditClassifier.Classify(CreateEvent(
            ActionType.MemberRoleUpdated,
            targetUserId: 456,
            afterPermissions: new GuildPermissions(administrator: true)));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Rule, Is.EqualTo(SecurityAuditRule.AdministratorRoleAssigned));
    }

    [Test]
    public void Classify_NonAdminRole_IgnoresEvent()
    {
        var result = SecurityAuditClassifier.Classify(CreateEvent(
            ActionType.RoleCreated,
            afterPermissions: new GuildPermissions(manageRoles: true)));

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Classify_AdminBotRoleAssignment_IsTaggedAsBotOrIntegrationRelated()
    {
        var result = SecurityAuditClassifier.Classify(CreateEvent(
            ActionType.MemberRoleUpdated,
            targetUserId: 456,
            targetIsBot: true,
            afterPermissions: new GuildPermissions(administrator: true)));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsBotOrIntegrationRelated, Is.True);
    }

    [Test]
    public void Classify_AdminIntegrationRole_IsTaggedAsBotOrIntegrationRelated()
    {
        var result = SecurityAuditClassifier.Classify(CreateEvent(
            ActionType.IntegrationCreated,
            roleIsManaged: true,
            integrationType: "discord",
            afterPermissions: new GuildPermissions(administrator: true)));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Rule, Is.EqualTo(SecurityAuditRule.AdministratorIntegrationRole));
        Assert.That(result.IsBotOrIntegrationRelated, Is.True);
    }

    private static SecurityAuditEvent CreateEvent(
        ActionType actionType,
        GuildPermissions? beforePermissions = null,
        GuildPermissions? afterPermissions = null,
        ulong? targetUserId = null,
        bool targetIsBot = false,
        bool roleIsManaged = false,
        string? integrationType = null)
    {
        return new SecurityAuditEvent(
            123,
            actionType,
            789,
            "Guild",
            111,
            "Executor",
            targetUserId,
            targetUserId is null ? null : "Target",
            222,
            "Admin Role",
            beforePermissions,
            afterPermissions,
            targetIsBot,
            roleIsManaged,
            integrationType,
            null,
            DateTimeOffset.UtcNow);
    }
}
