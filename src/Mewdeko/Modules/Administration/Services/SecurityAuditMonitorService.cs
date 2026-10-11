using System.Threading;
using DataModel;
using Discord.Net;
using Discord.Rest;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Monitors guild audit logs for critical Administrator permission escalation.
/// </summary>
public sealed class SecurityAuditMonitorService : INService, IReadyExecutor, IUnloadableService
{
    private const int AuditBackfillLimit = 25;
    private const int MaxProcessedAuditLogIds = 10000;
    private static readonly TimeSpan MissingAuditLogPermissionRetry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    private const string ModuleName = "SecurityAuditMonitorService";

    private readonly DiscordShardedClient client;
    private readonly IBotCredentials credentials;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<SecurityAuditMonitorService> logger;
    private readonly UserPunishService punishService;
    private readonly ConcurrentDictionary<ulong, DateTimeOffset> auditLogAccessRetryAfter = new();
    private readonly ConcurrentDictionary<ulong, byte> processedAuditLogIds = new();
    private readonly ConcurrentDictionary<ulong, SecurityAuditSetting> settingsCache = new();
    private readonly SemaphoreSlim scanLock = new(1, 1);
    private readonly CancellationTokenSource scanCts = new();
    private Task? scannerTask;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SecurityAuditMonitorService"/> class.
    /// </summary>
    public SecurityAuditMonitorService(
        DiscordShardedClient client,
        IBotCredentials credentials,
        IDataConnectionFactory dbFactory,
        EventHandler eventHandler,
        UserPunishService punishService,
        ILogger<SecurityAuditMonitorService> logger)
    {
        this.client = client;
        this.credentials = credentials;
        this.dbFactory = dbFactory;
        this.eventHandler = eventHandler;
        this.punishService = punishService;
        this.logger = logger;

        eventHandler.Subscribe("AuditLogCreated", ModuleName, OnAuditLogCreatedAsync);
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        await LoadSettingsAsync().ConfigureAwait(false);
        await ScanAllGuildsAsync(scanCts.Token).ConfigureAwait(false);
        scannerTask = RunScannerAsync(scanCts.Token);
    }

    /// <inheritdoc />
    public async Task Unload()
    {
        eventHandler.Unsubscribe("AuditLogCreated", ModuleName, OnAuditLogCreatedAsync);
        await scanCts.CancelAsync().ConfigureAwait(false);

        if (scannerTask is not null)
        {
            try
            {
                await scannerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during service unload.
            }
        }

        scanCts.Dispose();
        scanLock.Dispose();
    }

    /// <summary>
    ///     Gets or creates security audit settings for a guild.
    /// </summary>
    public async Task<SecurityAuditSetting> GetOrCreateSettingsAsync(ulong guildId)
    {
        if (settingsCache.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var settings = await db.SecurityAuditSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = new SecurityAuditSetting
            {
                GuildId = guildId,
                Enabled = true,
                DateAdded = DateTime.UtcNow
            };

            settings.Id = await db.InsertWithInt32IdentityAsync(settings).ConfigureAwait(false);
        }

        settingsCache[guildId] = settings;
        return settings;
    }

    /// <summary>
    ///     Sets the security audit alert channel.
    /// </summary>
    public async Task SetAlertChannelAsync(IGuild guild, ITextChannel? channel)
    {
        var settings = await GetOrCreateSettingsAsync(guild.Id).ConfigureAwait(false);
        settings.AlertChannelId = channel?.Id;
        await SaveSettingsAsync(settings).ConfigureAwait(false);
    }

    /// <summary>
    ///     Enables or disables security audit monitoring for a guild.
    /// </summary>
    public async Task SetEnabledAsync(IGuild guild, bool enabled)
    {
        var settings = await GetOrCreateSettingsAsync(guild.Id).ConfigureAwait(false);
        settings.Enabled = enabled;
        await SaveSettingsAsync(settings).ConfigureAwait(false);
    }

    /// <summary>
    ///     Enables or disables direct messages to bot owners for critical alerts.
    /// </summary>
    public async Task SetOwnerDmAsync(IGuild guild, bool enabled)
    {
        var settings = await GetOrCreateSettingsAsync(guild.Id).ConfigureAwait(false);
        settings.DmBotOwnersOnCritical = enabled;
        await SaveSettingsAsync(settings).ConfigureAwait(false);
    }

    private async Task LoadSettingsAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var settings = await db.SecurityAuditSettings.ToListAsync().ConfigureAwait(false);
        settingsCache.Clear();

        foreach (var setting in settings)
        {
            settingsCache[setting.GuildId] = setting;
        }
    }

    private async Task SaveSettingsAsync(SecurityAuditSetting settings)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        await db.UpdateAsync(settings).ConfigureAwait(false);
        settingsCache[settings.GuildId] = settings;
    }

    private async Task RunScannerAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(ScanInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await ScanAllGuildsAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ScanAllGuildsAsync(CancellationToken cancellationToken)
    {
        if (!await scanLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return;

        try
        {
            foreach (var guild in client.Guilds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ScanGuildAsync(guild, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            scanLock.Release();
        }
    }

    private async Task ScanGuildAsync(SocketGuild guild, CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateSettingsAsync(guild.Id).ConfigureAwait(false);
        if (!settings.Enabled)
            return;

        if (auditLogAccessRetryAfter.TryGetValue(guild.Id, out var retryAfter) &&
            retryAfter > DateTimeOffset.UtcNow)
            return;

        IReadOnlyCollection<RestAuditLogEntry> entries;
        try
        {
            entries = (await guild.GetAuditLogsAsync(AuditBackfillLimit).FlattenAsync().ConfigureAwait(false))
                .OfType<RestAuditLogEntry>()
                .OrderBy(x => x.CreatedAt)
                .ToList();

            auditLogAccessRetryAfter.TryRemove(guild.Id, out _);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
        {
            auditLogAccessRetryAfter[guild.Id] = DateTimeOffset.UtcNow + MissingAuditLogPermissionRetry;
            logger.LogWarning(
                "Security audit backfill skipped for guild {GuildName} ({GuildId}) because Discord denied audit-log access. Grant View Audit Log, or Administrator, to enable backfill scanning. Live audit-log gateway alerts may still arrive when Discord emits them.",
                guild.Name,
                guild.Id);
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to scan security audit logs for guild {GuildName} ({GuildId})", guild.Name,
                guild.Id);
            return;
        }

        var newestSeen = settings.LastSeenAuditLogId;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (settings.LastSeenAuditLogId is not null && entry.Id <= settings.LastSeenAuditLogId.Value)
                continue;

            await ProcessAuditLogEntryAsync(guild, entry, entry.Id, settings).ConfigureAwait(false);

            if (newestSeen is null || entry.Id > newestSeen.Value)
                newestSeen = entry.Id;
        }

        if (newestSeen is not null && newestSeen != settings.LastSeenAuditLogId)
            await UpdateLastSeenAsync(settings, newestSeen.Value).ConfigureAwait(false);
    }

    private async Task OnAuditLogCreatedAsync(SocketAuditLogEntry entry, SocketGuild guild)
    {
        var settings = await GetOrCreateSettingsAsync(guild.Id).ConfigureAwait(false);
        if (!settings.Enabled)
            return;

        await ProcessAuditLogEntryAsync(guild, entry, entry.Id, settings).ConfigureAwait(false);
        if (settings.LastSeenAuditLogId is null || entry.Id > settings.LastSeenAuditLogId.Value)
            await UpdateLastSeenAsync(settings, entry.Id).ConfigureAwait(false);
    }

    private async Task UpdateLastSeenAsync(SecurityAuditSetting settings, ulong auditLogId)
    {
        settings.LastSeenAuditLogId = auditLogId;

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        await db.SecurityAuditSettings
            .Where(x => x.GuildId == settings.GuildId)
            .Set(x => x.LastSeenAuditLogId, auditLogId)
            .UpdateAsync()
            .ConfigureAwait(false);

        settingsCache[settings.GuildId] = settings;
    }

    private async Task ProcessAuditLogEntryAsync(
        SocketGuild guild,
        IAuditLogEntry entry,
        ulong auditLogId,
        SecurityAuditSetting settings)
    {
        if (processedAuditLogIds.Count > MaxProcessedAuditLogIds)
            processedAuditLogIds.Clear();

        if (!processedAuditLogIds.TryAdd(auditLogId, 0))
            return;

        var auditEvents = await BuildSecurityAuditEventsAsync(guild, entry, auditLogId).ConfigureAwait(false);
        foreach (var auditEvent in auditEvents)
        {
            var match = SecurityAuditClassifier.Classify(auditEvent);
            if (match is null)
                continue;

            await AlertAsync(guild, auditEvent, match, settings).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<SecurityAuditEvent>> BuildSecurityAuditEventsAsync(
        SocketGuild guild,
        IAuditLogEntry entry,
        ulong auditLogId)
    {
        if (entry.User is null)
            return [];

        return entry.Action switch
        {
            ActionType.RoleCreated => BuildRoleCreatedEvents(guild, entry, auditLogId),
            ActionType.RoleUpdated => BuildRoleUpdatedEvents(guild, entry, auditLogId),
            ActionType.MemberRoleUpdated => await BuildMemberRoleUpdatedEventsAsync(guild, entry, auditLogId)
                .ConfigureAwait(false),
            ActionType.BotAdded => await BuildBotAddedEventsAsync(guild, entry, auditLogId).ConfigureAwait(false),
            ActionType.IntegrationCreated or ActionType.IntegrationUpdated => BuildIntegrationEvents(
                guild, entry, auditLogId),
            _ => []
        };
    }

    private IReadOnlyList<SecurityAuditEvent> BuildRoleCreatedEvents(SocketGuild guild, IAuditLogEntry entry,
        ulong auditLogId)
    {
        ulong roleId;
        string? roleName = null;
        GuildPermissions? permissions = null;

        switch (entry.Data)
        {
            case SocketRoleCreateAuditLogData data:
                roleId = data.RoleId;
                roleName = data.Properties.Name;
                permissions = data.Properties.Permissions;
                break;
            case RoleCreateAuditLogData data:
                roleId = data.RoleId;
                roleName = data.Properties.Name;
                permissions = data.Properties.Permissions;
                break;
            default:
                return [];
        }

        if (roleId == 0)
            return [];

        var role = guild.GetRole(roleId);

        return
        [
            CreateAuditEvent(
                guild,
                entry,
                auditLogId,
                roleId: roleId,
                roleName: roleName ?? role?.Name,
                afterPermissions: permissions ?? role?.Permissions,
                roleIsManaged: role?.IsManaged == true)
        ];
    }

    private IReadOnlyList<SecurityAuditEvent> BuildRoleUpdatedEvents(SocketGuild guild, IAuditLogEntry entry,
        ulong auditLogId)
    {
        ulong roleId;
        string? roleName = null;
        GuildPermissions? beforePermissions = null;
        GuildPermissions? afterPermissions = null;

        switch (entry.Data)
        {
            case SocketRoleUpdateAuditLogData data:
                roleId = data.RoleId;
                beforePermissions = data.Before.Permissions;
                afterPermissions = data.After.Permissions;
                roleName = data.After.Name ?? data.Before.Name;
                break;
            case RoleUpdateAuditLogData data:
                roleId = data.RoleId;
                beforePermissions = data.Before.Permissions;
                afterPermissions = data.After.Permissions;
                roleName = data.After.Name ?? data.Before.Name;
                break;
            default:
                return [];
        }

        if (roleId == 0)
            return [];

        var role = guild.GetRole(roleId);

        return
        [
            CreateAuditEvent(
                guild,
                entry,
                auditLogId,
                roleId: roleId,
                roleName: roleName ?? role?.Name,
                beforePermissions: beforePermissions,
                afterPermissions: afterPermissions ?? role?.Permissions,
                roleIsManaged: role?.IsManaged == true)
        ];
    }

    private async Task<IReadOnlyList<SecurityAuditEvent>> BuildMemberRoleUpdatedEventsAsync(
        SocketGuild guild,
        IAuditLogEntry entry,
        ulong auditLogId)
    {
        IEnumerable<(ulong RoleId, string? Name, string? IntegrationType)> changes;
        ulong targetId;

        switch (entry.Data)
        {
            case SocketMemberRoleAuditLogData data:
                changes = data.Roles
                    .Where(x => x.Added)
                    .Select(x => (x.RoleId, x.Name, data.IntegrationType?.ToString()));
                targetId = data.Target.Id;
                break;
            case MemberRoleAuditLogData data:
                changes = data.Roles
                    .Where(x => x.Added)
                    .Select(x => (x.RoleId, x.Name, data.IntegrationType?.ToString()));
                targetId = data.Target.Id;
                break;
            default:
                return [];
        }

        if (targetId == 0)
            return [];

        var targetUser = await GetGuildUserAsync(guild, targetId).ConfigureAwait(false);
        var events = new List<SecurityAuditEvent>();

        foreach (var (roleId, roleName, integrationType) in changes)
        {
            var role = guild.GetRole(roleId);
            events.Add(CreateAuditEvent(
                guild,
                entry,
                auditLogId,
                targetUserId: targetId,
                targetUserName: targetUser?.ToString(),
                roleId: roleId,
                roleName: role?.Name ?? roleName,
                afterPermissions: role?.Permissions,
                targetIsBot: targetUser?.IsBot == true,
                roleIsManaged: role?.IsManaged == true,
                integrationType: integrationType));
        }

        return events;
    }

    private async Task<IReadOnlyList<SecurityAuditEvent>> BuildBotAddedEventsAsync(
        SocketGuild guild,
        IAuditLogEntry entry,
        ulong auditLogId)
    {
        ulong targetId;
        switch (entry.Data)
        {
            case SocketBotAddAuditLogData data:
                targetId = data.Target.Id;
                break;
            case BotAddAuditLogData data:
                targetId = data.Target.Id;
                break;
            default:
                return [];
        }

        if (targetId == 0)
            return [];

        var targetUser = await GetGuildUserAsync(guild, targetId).ConfigureAwait(false);
        return
        [
            CreateAuditEvent(
                guild,
                entry,
                auditLogId,
                targetUserId: targetId,
                targetUserName: targetUser?.ToString(),
                afterPermissions: targetUser?.GuildPermissions,
                targetIsBot: true)
        ];
    }

    private IReadOnlyList<SecurityAuditEvent> BuildIntegrationEvents(SocketGuild guild, IAuditLogEntry entry,
        ulong auditLogId)
    {
        ulong? roleId;
        string? roleName;
        string? integrationType;

        switch (entry.Data)
        {
            case SocketIntegrationCreatedAuditLogData data:
                roleId = data.Data.RoleId;
                roleName = data.Data.Name;
                integrationType = data.Data.Type;
                break;
            case SocketIntegrationUpdatedAuditLogData data:
                roleId = data.After.RoleId;
                roleName = data.After.Name;
                integrationType = data.After.Type;
                break;
            case IntegrationCreatedAuditLogData data:
                roleId = data.Data.RoleId;
                roleName = data.Data.Name;
                integrationType = data.Data.Type;
                break;
            case IntegrationUpdatedAuditLogData data:
                roleId = data.After.RoleId;
                roleName = data.After.Name;
                integrationType = data.After.Type;
                break;
            default:
                return [];
        }

        if (roleId is null)
            return [];

        var role = guild.GetRole(roleId.Value);
        return
        [
            CreateAuditEvent(
                guild,
                entry,
                auditLogId,
                roleId: roleId.Value,
                roleName: role?.Name ?? roleName,
                afterPermissions: role?.Permissions,
                roleIsManaged: role?.IsManaged == true,
                integrationType: integrationType)
        ];
    }

    private async Task<IGuildUser?> GetGuildUserAsync(SocketGuild guild, ulong userId)
    {
        var socketUser = guild.GetUser(userId);
        if (socketUser is not null)
            return socketUser;

        try
        {
            return await client.Rest.GetGuildUserAsync(guild.Id, userId).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static SecurityAuditEvent CreateAuditEvent(
        SocketGuild guild,
        IAuditLogEntry entry,
        ulong auditLogId,
        ulong? targetUserId = null,
        string? targetUserName = null,
        ulong? roleId = null,
        string? roleName = null,
        GuildPermissions? beforePermissions = null,
        GuildPermissions? afterPermissions = null,
        bool targetIsBot = false,
        bool roleIsManaged = false,
        string? integrationType = null)
    {
        return new SecurityAuditEvent(
            auditLogId,
            entry.Action,
            guild.Id,
            guild.Name,
            entry.User.Id,
            entry.User.ToString(),
            targetUserId,
            targetUserName,
            roleId,
            roleName,
            beforePermissions,
            afterPermissions,
            targetIsBot,
            roleIsManaged,
            integrationType,
            entry.Reason,
            entry.CreatedAt);
    }

    private async Task AlertAsync(
        SocketGuild guild,
        SecurityAuditEvent auditEvent,
        SecurityAuditMatch match,
        SecurityAuditSetting settings)
    {
        logger.LogCritical(
            "CRITICAL SECURITY AUDIT: {Rule} in guild {GuildName} ({GuildId}); audit {AuditLogId}; action {ActionType}; executor {ExecutorName} ({ExecutorId}); target {TargetId}; role {Role}",
            match.Rule,
            auditEvent.GuildName,
            auditEvent.GuildId,
            auditEvent.AuditLogId,
            auditEvent.ActionType,
            auditEvent.ExecutorName,
            auditEvent.ExecutorId,
            auditEvent.TargetUserId,
            FormatLogRole(auditEvent));

        var channel = await ResolveAlertChannelAsync(guild, auditEvent, settings).ConfigureAwait(false);
        if (channel is not null)
        {
            var components = BuildAlertComponents(auditEvent, match);
            await channel.SendMessageAsync(
                    components: components.Build(),
                    flags: MessageFlags.ComponentsV2,
                    allowedMentions: AllowedMentions.None)
                .ConfigureAwait(false);
        }

        if (settings.DmBotOwnersOnCritical)
            await DmBotOwnersAsync(auditEvent, match).ConfigureAwait(false);
    }

    private async Task<SocketTextChannel?> ResolveAlertChannelAsync(
        SocketGuild guild,
        SecurityAuditEvent auditEvent,
        SecurityAuditSetting settings)
    {
        var configuredChannel = GetTextChannel(guild, settings.AlertChannelId);
        if (configuredChannel is not null)
            return configuredChannel;

        var warnlogChannelId = await punishService.GetWarnlogChannel(guild.Id).ConfigureAwait(false);
        var warnlogChannel = GetTextChannel(guild, warnlogChannelId == 0 ? null : warnlogChannelId);
        if (warnlogChannel is not null)
            return warnlogChannel;

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var logSettings = await db.LoggingV2.FirstOrDefaultAsync(x => x.GuildId == guild.Id).ConfigureAwait(false);
        var fallbackId = auditEvent.ActionType switch
        {
            ActionType.RoleCreated => logSettings?.RoleCreatedId,
            ActionType.RoleUpdated => logSettings?.RoleUpdatedId,
            ActionType.MemberRoleUpdated => logSettings?.UserRoleAddedId,
            ActionType.BotAdded or ActionType.IntegrationCreated or ActionType.IntegrationUpdated =>
                logSettings?.ServerUpdatedId,
            _ => logSettings?.LogOtherId
        };

        return GetTextChannel(guild, fallbackId);
    }

    private static SocketTextChannel? GetTextChannel(SocketGuild guild, ulong? channelId)
    {
        if (channelId is null or 0)
            return null;

        return guild.GetTextChannel(channelId.Value);
    }

    private static ComponentBuilderV2 BuildAlertComponents(SecurityAuditEvent auditEvent, SecurityAuditMatch match)
    {
        var details =
            $"**Rule:** `{match.Rule}`\n" +
            $"**Action:** `{auditEvent.ActionType}`\n" +
            $"**Summary:** {match.Summary}\n" +
            $"**Executor:** {FormatUserMention(auditEvent.ExecutorId, auditEvent.ExecutorName)}\n" +
            $"**Target:** {FormatOptionalUserMention(auditEvent.TargetUserId, auditEvent.TargetUserName)}\n" +
            $"**Role:** {FormatOptionalRoleMention(auditEvent.RoleId, auditEvent.RoleName)}\n" +
            $"**Bot/Integration related:** {(match.IsBotOrIntegrationRelated ? "yes" : "no")}\n" +
            $"**Reason:** {Format.Code(auditEvent.Reason ?? "No audit reason provided")}\n" +
            $"**Audit log ID:** `{auditEvent.AuditLogId}`\n" +
            $"**Time:** {auditEvent.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC";

        var permissions =
            $"**Before permissions:** {FormatPermissions(auditEvent.BeforePermissions)}\n" +
            $"**After permissions:** {FormatPermissions(auditEvent.AfterPermissions)}";

        return new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder("# CRITICAL SECURITY AUDIT: Administrator granted"),
                new TextDisplayBuilder(details)
            ], Mewdeko.ErrorColor)
            .WithSeparator()
            .WithTextDisplay(permissions);
    }

    private async Task DmBotOwnersAsync(SecurityAuditEvent auditEvent, SecurityAuditMatch match)
    {
        var message =
            $"CRITICAL SECURITY AUDIT in {auditEvent.GuildName} ({auditEvent.GuildId})\n" +
            $"{match.Summary}\n" +
            $"Executor: {auditEvent.ExecutorName} ({auditEvent.ExecutorId})\n" +
            $"Audit log ID: {auditEvent.AuditLogId}";

        foreach (var ownerId in credentials.OwnerIds)
        {
            try
            {
                var user = await client.Rest.GetUserAsync(ownerId).ConfigureAwait(false);
                if (user is null)
                    continue;

                var channel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                await channel.SendMessageAsync(message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to DM security audit alert to bot owner {OwnerId}", ownerId);
            }
        }
    }

    private static string FormatUserMention(ulong userId, string? displayName)
    {
        return $"{MentionUtils.MentionUser(userId)} `{userId}` ({displayName ?? "unknown"})";
    }

    private static string FormatOptionalUserMention(ulong? userId, string? displayName)
    {
        return userId is null ? displayName ?? "n/a" : FormatUserMention(userId.Value, displayName);
    }

    private static string FormatOptionalRoleMention(ulong? roleId, string? roleName)
    {
        return roleId is null
            ? roleName ?? "n/a"
            : $"{MentionUtils.MentionRole(roleId.Value)} `{roleId.Value}` ({roleName ?? "unknown"})";
    }

    private static string FormatLogRole(SecurityAuditEvent auditEvent)
    {
        if (auditEvent.RoleId is null)
            return auditEvent.RoleName ?? "n/a";

        return string.IsNullOrWhiteSpace(auditEvent.RoleName)
            ? auditEvent.RoleId.Value.ToString()
            : $"{auditEvent.RoleName} ({auditEvent.RoleId.Value})";
    }

    private static string FormatPermissions(GuildPermissions? permissions)
    {
        return permissions is null ? "n/a" : Format.Code(string.Join(", ", permissions.Value.ToList()));
    }
}
