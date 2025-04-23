using DataModel;
using Discord.Rest;
using LinqToDB;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Moderation.Services;
using Serilog;
using DiscordShardedClient = Discord.WebSocket.DiscordShardedClient;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service for managing log commands.
/// </summary>
public class LogCommandService : INService, IReadyExecutor
{
    /// <summary>
    ///     Log category types.
    /// </summary>
    public enum LogCategoryTypes
    {
        /// <summary> All available log event types. </summary>
        All,

        /// <summary> Events related to users (join, leave, roles, name changes, voice). </summary>
        Users,

        /// <summary> Events related to thread creation, deletion, updates. </summary>
        Threads,

        /// <summary> Events related to role creation, deletion, updates. </summary>
        Roles,

        /// <summary> Events related to server-wide settings changes and server events. </summary>
        Server,

        /// <summary> Events related to channel creation, deletion, updates. </summary>
        Channel,

        /// <summary> Events related to message updates and deletions. </summary>
        Messages,

        /// <summary> Events related to moderation actions (bans, unbans, mutes). </summary>
        Moderation,

        /// <summary> Disables logging for all event types. </summary>
        None
    }

    /// <summary>
    ///     Dictionary of log types.
    /// </summary>
    public enum LogType
    {
        /// <summary> Miscellaneous or custom log events not covered by other types. </summary>
        Other,

        /// <summary> A scheduled server event was created. </summary>
        EventCreated,

        /// <summary> A role's properties (name, color, perms, etc.) were updated. </summary>
        RoleUpdated,

        /// <summary> A new role was created. </summary>
        RoleCreated,

        /// <summary> A role was deleted. </summary>
        RoleDeleted,

        /// <summary> Server settings (name, icon, owner, etc.) were updated. </summary>
        ServerUpdated,

        /// <summary> A new thread was created. </summary>
        ThreadCreated,

        /// <summary> One or more roles were added to a user. </summary>
        UserRoleAdded,

        /// <summary> One or more roles were removed from a user. </summary>
        UserRoleRemoved,

        /// <summary> A user's global username changed. </summary>
        UsernameUpdated,

        /// <summary> A user's server nickname changed. </summary>
        NicknameUpdated,

        /// <summary> A thread was deleted. </summary>
        ThreadDeleted,

        /// <summary> A thread's properties (name, archive status, etc.) were updated. </summary>
        ThreadUpdated,

        /// <summary> A message's content was edited. </summary>
        MessageUpdated,

        /// <summary> A message was deleted. </summary>
        MessageDeleted,

        /// <summary> A user joined the server. </summary>
        UserJoined,

        /// <summary> A user left (or was kicked from) the server. </summary>
        UserLeft,

        /// <summary> A user was banned from the server. </summary>
        UserBanned,

        /// <summary> A user was unbanned from the server. </summary>
        UserUnbanned,

        /// <summary> A user's profile properties (avatar, global name) changed. </summary>
        UserUpdated,

        /// <summary> A new channel was created. </summary>
        ChannelCreated,

        /// <summary> A channel was deleted. </summary>
        ChannelDestroyed,

        /// <summary> A channel's properties (name, topic, permissions, etc.) were updated. </summary>
        ChannelUpdated,

        /// <summary> A user's voice state changed (join, leave, move, mute, deafen, stream). </summary>
        VoicePresence,

        /// <summary> A user potentially used Text-to-Speech in a voice channel (Detection not guaranteed). </summary>
        VoicePresenceTts,

        /// <summary> A user was muted (voice or text). </summary>
        UserMuted
    }

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;


    /// <summary>
    ///     Constructs a new instance of the NewLogCommandService.
    /// </summary>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="cache">The data cache.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="handler">The event handler.</param>
    /// <param name="muteService">The mute service.</param>
    public LogCommandService(IDataConnectionFactory dbFactory, IDataCache cache, DiscordShardedClient client,
        EventHandler handler,
        MuteService muteService)
    {
        this.dbFactory = dbFactory;
        this.client = client;

        handler.EventCreated += OnEventCreated;
        handler.RoleUpdated += OnRoleUpdated;
        handler.RoleCreated += OnRoleCreated;
        handler.RoleDeleted += OnRoleDeleted;
        handler.GuildUpdated += OnGuildUpdated;
        handler.ThreadCreated += OnThreadCreated;
        handler.GuildMemberUpdated += OnUserRoleAdded;
        handler.GuildMemberUpdated += OnUserRoleRemoved;
        handler.UserUpdated += OnUsernameUpdated;
        handler.GuildMemberUpdated += OnNicknameUpdated;
        handler.ThreadDeleted += OnThreadDeleted;
        handler.ThreadUpdated += OnThreadUpdated;
        handler.MessageUpdated += OnMessageUpdated;
        handler.MessageDeleted += OnMessageDeleted;
        handler.UserJoined += OnUserJoined;
        handler.UserLeft += OnUserLeft;
        handler.UserUpdated += OnUserUpdated;
        handler.ChannelCreated += OnChannelCreated;
        handler.ChannelDestroyed += OnChannelDestroyed;
        handler.ChannelUpdated += OnChannelUpdated;
        handler.UserVoiceStateUpdated += OnVoicePresence;
        handler.UserVoiceStateUpdated += OnVoicePresenceTts;
        handler.AuditLogCreated += OnAuditLogCreated;
        muteService.UserMuted += OnUserMuted;
        muteService.UserUnmuted += OnUserUnmuted;
    }

    /// <summary>
    ///     Dictionary of log settings for each guild.
    /// </summary>
    public ConcurrentDictionary<ulong, LoggingV2> GuildLogSettings { get; set; }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        try
        {
            var logSettingsList = await db.LoggingV2.ToListAsync().ConfigureAwait(false);
            var dict = logSettingsList.ToDictionary(g => g.GuildId, g => g);
            GuildLogSettings = new ConcurrentDictionary<ulong, LoggingV2>(dict);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load LoggingV2 settings on ready.");
            GuildLogSettings = new ConcurrentDictionary<ulong, LoggingV2>();
        }
    }

    /// <summary>
    ///     Handles the creation of audit logs.
    /// </summary>
    /// <param name="args">The audit log entry.</param>
    /// <param name="arsg2">The guild where the audit log was created.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnAuditLogCreated(SocketAuditLogEntry args, SocketGuild arsg2)
    {
        if (args.Action == ActionType.Ban)
        {
            if (args.Data is BanAuditLogData data)
                await OnUserBanned(data.Target, arsg2, args.User);
        }
        else if (args.Action == ActionType.Unban)
        {
            if (args.Data is UnbanAuditLogData data)
                await OnUserUnbanned(data.Target, arsg2, args.User);
        }
    }

    /// <summary>
    ///     Handles the creation of a role.
    /// </summary>
    /// <param name="args">The role that was created.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnRoleCreated(SocketRole args)
    {
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            if (logSetting.RoleCreatedId is null or 0)
                return;

            var channel = args.Guild.GetTextChannel(logSetting.RoleCreatedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await args.Guild.GetAuditLogsAsync(1, actionType: ActionType.RoleCreated).FlattenAsync();
            var auditLog = auditLogs.FirstOrDefault();
            if (auditLog == null) return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Role Created")
                .WithDescription($"`Name:` {args.Name}\n" +
                                 $"`Id:` {args.Id}\n" +
                                 $"`Color:` {args.Color}\n" +
                                 $"`Hoisted:` {args.IsHoisted}\n" +
                                 $"`Mentionable:` {args.IsMentionable}\n" +
                                 $"`Position:` {args.Position}\n" +
                                 $"`Permissions:` {string.Join(", ", args.Permissions.ToList())}\n" +
                                 $"`Created By:` {auditLog.User.Mention} | {auditLog.User.Id}\n" +
                                 $"`Managed:` {args.IsManaged}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a guild is updated.
    /// </summary>
    /// <param name="args">The original guild before the update.</param>
    /// <param name="arsg2">The updated guild.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnGuildUpdated(SocketGuild args, SocketGuild arsg2)
    {
        if (GuildLogSettings.TryGetValue(args.Id, out var logSetting))
        {
            if (logSetting.ServerUpdatedId is null or 0)
                return;

            var channel = args.GetTextChannel(logSetting.ServerUpdatedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await args.GetAuditLogsAsync(1, actionType: ActionType.GuildUpdated).FlattenAsync();
            var auditLog = auditLogs.FirstOrDefault();
            if (auditLog == null) return;

            var eb = new EmbedBuilder();
            var updatedByStr = $"`Updated By:` {auditLog.User.Mention} | {auditLog.User.Id}";

            if (args.Name != arsg2.Name)
                eb.WithOkColor().WithTitle("Server Name Updated")
                    .WithDescription($"`New Name:` {arsg2.Name}\n`Old Name:` {args.Name}\n{updatedByStr}");
            else if (args.IconUrl != arsg2.IconUrl)
                eb.WithOkColor().WithTitle("Server Icon Updated").WithDescription(updatedByStr)
                    .WithThumbnailUrl(args.IconUrl).WithImageUrl(arsg2.IconUrl);
            else if (args.BannerUrl != arsg2.BannerUrl)
                eb.WithOkColor().WithTitle("Server Banner Updated").WithDescription(updatedByStr)
                    .WithThumbnailUrl(args.BannerUrl).WithImageUrl(arsg2.BannerUrl);
            else if (args.SplashUrl != arsg2.SplashUrl)
                eb.WithOkColor().WithTitle("Server Splash Updated").WithDescription(updatedByStr)
                    .WithThumbnailUrl(args.SplashUrl).WithImageUrl(arsg2.SplashUrl);
            else if (args.VanityURLCode != arsg2.VanityURLCode)
                eb.WithOkColor().WithTitle("Server Vanity URL Updated").WithDescription(
                    $"`New Vanity URL:` {arsg2.VanityURLCode ?? "None"}\n`Old Vanity URL:` {args.VanityURLCode ?? "None"}\n{updatedByStr}");
            else if (args.OwnerId != arsg2.OwnerId)
                eb.WithOkColor().WithTitle("Server Owner Updated").WithDescription(
                    $"`New Owner:` {arsg2.Owner.Mention} | {arsg2.Owner.Id}\n`Old Owner:` {args.Owner.Mention} | {args.Owner.Id}\n{updatedByStr}");
            else if (args.AFKChannel?.Id != arsg2.AFKChannel?.Id)
                eb.WithOkColor().WithTitle("Server AFK Channel Updated").WithDescription(
                    $"`New AFK Channel:` {arsg2.AFKChannel?.Mention ?? "None"} | {arsg2.AFKChannel?.Id.ToString() ?? "N/A"}\n`Old AFK Channel:` {args.AFKChannel?.Mention ?? "None"} | {args.AFKChannel?.Id.ToString() ?? "N/A"}\n{updatedByStr}");
            else if (args.AFKTimeout != arsg2.AFKTimeout)
                eb.WithOkColor().WithTitle("Server AFK Timeout Updated").WithDescription(
                    $"`New AFK Timeout:` {arsg2.AFKTimeout}\n`Old AFK Timeout:` {args.AFKTimeout}\n{updatedByStr}");
            else if (args.DefaultMessageNotifications != arsg2.DefaultMessageNotifications)
                eb.WithOkColor().WithTitle("Server Default Message Notifications Updated").WithDescription(
                    $"`New Default Message Notifications:` {arsg2.DefaultMessageNotifications}\n`Old Default Message Notifications:` {args.DefaultMessageNotifications}\n{updatedByStr}");
            else if (args.ExplicitContentFilter != arsg2.ExplicitContentFilter)
                eb.WithOkColor().WithTitle("Server Explicit Content Filter Updated").WithDescription(
                    $"`New Explicit Content Filter:` {arsg2.ExplicitContentFilter}\n`Old Explicit Content Filter:` {args.ExplicitContentFilter}\n{updatedByStr}");
            else if (args.MfaLevel != arsg2.MfaLevel)
                eb.WithOkColor().WithTitle("Server MFA Level Updated").WithDescription(
                    $"`New MFA Level:` {arsg2.MfaLevel}\n`Old MFA Level:` {args.MfaLevel}\n{updatedByStr}");
            else if (args.VerificationLevel != arsg2.VerificationLevel)
                eb.WithOkColor().WithTitle("Server Verification Level Updated").WithDescription(
                    $"`New Verification Level:` {arsg2.VerificationLevel}\n`Old Verification Level:` {args.VerificationLevel}\n{updatedByStr}");
            else if (args.SystemChannel?.Id != arsg2.SystemChannel?.Id)
                eb.WithOkColor().WithTitle("Server System Channel Updated").WithDescription(
                    $"`New System Channel:` {arsg2.SystemChannel?.Mention ?? "None"} | {arsg2.SystemChannel?.Id.ToString() ?? "N/A"}\n`Old System Channel:` {args.SystemChannel?.Mention ?? "None"} | {args.SystemChannel?.Id.ToString() ?? "N/A"}\n{updatedByStr}");
            else if (args.RulesChannel?.Id != arsg2.RulesChannel?.Id)
                eb.WithOkColor().WithTitle("Server Rules Channel Updated").WithDescription(
                    $"`New Rules Channel:` {arsg2.RulesChannel?.Mention ?? "None"} | {arsg2.RulesChannel?.Id.ToString() ?? "N/A"}\n`Old Rules Channel:` {args.RulesChannel?.Mention ?? "None"} | {args.RulesChannel?.Id.ToString() ?? "N/A"}\n{updatedByStr}");
            else if (args.PublicUpdatesChannel?.Id != arsg2.PublicUpdatesChannel?.Id)
                eb.WithOkColor().WithTitle("Server Public Updates Channel Updated").WithDescription(
                    $"`New Public Updates Channel:` {arsg2.PublicUpdatesChannel?.Mention ?? "None"} | {arsg2.PublicUpdatesChannel?.Id.ToString() ?? "N/A"}\n`Old Public Updates Channel:` {args.PublicUpdatesChannel?.Mention ?? "None"} | {args.PublicUpdatesChannel?.Id.ToString() ?? "N/A"}\n{updatedByStr}");
            else if (args.MaxVideoChannelUsers != arsg2.MaxVideoChannelUsers)
                eb.WithOkColor().WithTitle("Server Max Video Channel Users Updated").WithDescription(
                    $"`New Max Video Channel Users:` {arsg2.MaxVideoChannelUsers?.ToString() ?? "Unlimited"}\n`Old Max Video Channel Users:` {args.MaxVideoChannelUsers?.ToString() ?? "Unlimited"}\n{updatedByStr}");
            else if (args.MaxMembers != arsg2.MaxMembers)
                eb.WithOkColor().WithTitle("Server Max Members Updated").WithDescription(
                    $"`New Max Members:` {arsg2.MaxMembers?.ToString() ?? "N/A"}\n`Old Max Members:` {args.MaxMembers?.ToString() ?? "N/A"}\n{updatedByStr}");
            else
                return;

            if (!string.IsNullOrEmpty(eb.Title))
                await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a role is deleted in a guild.
    /// </summary>
    /// <param name="args">The role that was deleted.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnRoleDeleted(SocketRole args)
    {
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            if (logSetting.RoleDeletedId is null or 0)
                return;

            var channel = args.Guild.GetTextChannel(logSetting.RoleDeletedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await args.Guild.GetAuditLogsAsync(1, actionType: ActionType.RoleDeleted).FlattenAsync();
            var auditLog = auditLogs.FirstOrDefault();
            if (auditLog == null) return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Role Deleted")
                .WithDescription($"`Role:` {args.Name}\n" +
                                 $"`Id:` {args.Id}\n" +
                                 $"`Deleted By:` {auditLog.User.Mention} | {auditLog.User.Id}\n" +
                                 $"`Deleted At:` {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a role is updated in a guild.
    /// </summary>
    /// <param name="args">The original role before the update.</param>
    /// <param name="arsg2">The updated role.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnRoleUpdated(SocketRole args, SocketRole arsg2)
    {
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            if (logSetting.RoleUpdatedId is null or 0)
                return;

            var channel = args.Guild.GetTextChannel(logSetting.RoleUpdatedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await args.Guild.GetAuditLogsAsync(1, actionType: ActionType.RoleUpdated).FlattenAsync();
            var auditLog = auditLogs.FirstOrDefault();
            if (auditLog == null) return;

            var eb = new EmbedBuilder();
            var updatedByStr = $"`Updated By:` {auditLog.User.Mention} | {auditLog.User.Id}";
            var roleStr = $"`Role:` {args.Mention} | {args.Id}";

            if (args.Name != arsg2.Name)
                eb.WithOkColor().WithTitle("Role Name Updated")
                    .WithDescription($"`New Role Name:` {arsg2.Name}\n`Old Role Name:` {args.Name}\n{updatedByStr}");
            else if (args.Color != arsg2.Color)
                eb.WithColor(arsg2.Color).WithTitle("Role Color Updated").WithDescription(
                    $"{roleStr}\n`New Role Color:` {arsg2.Color}\n`Old Role Color:` {args.Color}\n{updatedByStr}");
            else if (args.IsHoisted != arsg2.IsHoisted)
                eb.WithOkColor().WithTitle("Role Property Hoisted Updated").WithDescription(
                    $"{roleStr}\n`New Role Hoisted:` {arsg2.IsHoisted}\n`Old Role Hoisted:` {args.IsHoisted}\n{updatedByStr}");
            else if (args.IsMentionable != arsg2.IsMentionable)
                eb.WithOkColor().WithTitle("Role Property Mentionable Updated").WithDescription(
                    $"{roleStr}\n`New Role Mentionable:` {arsg2.IsMentionable}\n`Old Role Mentionable:` {args.IsMentionable}\n{updatedByStr}");
            else if (args.IsManaged != arsg2.IsManaged)
                eb.WithOkColor().WithTitle("Role Property Managed Updated").WithDescription(
                    $"{roleStr}\n`New Role Managed:` {arsg2.IsManaged}\n`Old Role Managed:` {args.IsManaged}\n{updatedByStr}");
            else if (args.Position != arsg2.Position)
                eb.WithOkColor().WithTitle("Role Position Updated").WithDescription(
                    $"{roleStr}\n`New Role Position:` {arsg2.Position}\n`Old Role Position:` {args.Position}\n{updatedByStr}");
            else if (!arsg2.Permissions.Equals(args.Permissions))
                eb.WithOkColor().WithTitle("Role Permissions Updated").WithDescription(
                    $"{roleStr}\n`New Role Permissions:` {arsg2.Permissions}\n`Old Role Permissions:` {args.Permissions}\n{updatedByStr}");
            else if (args.Icon != arsg2.Icon || args.Emoji?.ToString() != arsg2.Emoji?.ToString())
                eb.WithOkColor().WithTitle("Role Icon Updated").WithDescription($"{roleStr}\n{updatedByStr}")
                    .WithThumbnailUrl(arsg2.GetIconUrl() ?? args.GetIconUrl());
            else
                return; // No detectable change handled

            if (!string.IsNullOrEmpty(eb.Title))
                await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a new event is created in a guild.
    /// </summary>
    /// <param name="args">The event that was created.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnEventCreated(SocketGuildEvent args)
    {
        if (GuildLogSettings.TryGetValue(args.Guild.Id, out var logSetting))
        {
            if (logSetting.EventCreatedId is null or 0)
                return;

            var channel = args.Guild.GetTextChannel(logSetting.EventCreatedId.Value);
            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Event Created")
                .WithDescription($"`Event:` {args.Name}\n" +
                                 $"`Created By:` {args.Creator?.Mention ?? "N/A"} | {args.Creator?.Id.ToString() ?? "N/A"}\n" +
                                 $"`Created At:` {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}\n" +
                                 $"`Description:` {args.Description}\n" +
                                 $"`Event Date:` {args.StartTime:dd/MM/yyyy HH:mm:ss}\n" +
                                 $"`End Date:` {args.EndTime:dd/MM/yyyy HH:mm:ss}\n" +
                                 $"`Event Location:` {args.Location ?? "N/A"}\n" +
                                 $"`Event Type:` {args.Type}\n" +
                                 $"`Event Id:` {args.Id}")
                .WithImageUrl(args.GetCoverImageUrl());

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles logging for when a thread is created.
    /// </summary>
    /// <param name="socketThreadChannel">The created thread channel.</param>
    private async Task OnThreadCreated(SocketThreadChannel socketThreadChannel)
    {
        if (GuildLogSettings.TryGetValue(socketThreadChannel.Guild.Id, out var logSetting))
        {
            if (logSetting.ThreadCreatedId is null or 0)
                return;

            if (socketThreadChannel.Guild.GetTextChannel(logSetting.ThreadCreatedId
                    .Value) is not IThreadChannel channel)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Thread Created")
                .WithDescription($"`Name:` {socketThreadChannel.Name}\n" +
                                 $"`Created By:` {socketThreadChannel.Owner?.Mention ?? "N/A"} | {socketThreadChannel.Owner?.Id.ToString() ?? "N/A"}\n" +
                                 $"`Created At:` {socketThreadChannel.CreatedAt:dd/MM/yyyy HH:mm:ss}\n" +
                                 $"`Thread Type:` {socketThreadChannel.Type}\n" +
                                 $"`Thread Tags:` {string.Join(", ", socketThreadChannel.AppliedTags)}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user has a role added to them.
    /// </summary>
    /// <param name="cacheable">The user before the event fired</param>
    /// <param name="arsg2">The user after the event was fired</param>
    private async Task OnUserRoleAdded(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser arsg2)
    {
        if (!GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting) || !cacheable.HasValue)
            return;

        if (logSetting.UserRoleAddedId is null or 0)
            return;

        var addedRoles = arsg2.Roles.Except(cacheable.Value.Roles).ToList();
        if (!addedRoles.Any()) return;

        var channel = arsg2.Guild.GetTextChannel(logSetting.UserRoleAddedId.Value);
        if (channel is null) return;

        await Task.Delay(500);
        var auditLogs = await arsg2.Guild.GetAuditLogsAsync(1, actionType: ActionType.MemberRoleUpdated).FlattenAsync();
        var auditLog = auditLogs.LastOrDefault();
        if (auditLog == null) return;

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle("User Role(s) Added")
            .WithDescription($"`Role(s):` {string.Join(", ", addedRoles.Select(x => x.Mention))}\n" +
                             $"`Added By:` {auditLog.User.Mention} | {auditLog.User.Id}" +
                             $"\n`Added To:` {arsg2.Mention} | {arsg2.Id}");

        await channel.SendMessageAsync(embed: eb.Build());
    }


    /// <summary>
    ///     Handles the event where a user has a role removed.
    /// </summary>
    /// <param name="cacheable">The user before the removal</param>
    /// <param name="arsg2">The user after the removal</param>
    private async Task OnUserRoleRemoved(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser arsg2)
    {
        if (!GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting) || !cacheable.HasValue)
            return;

        if (logSetting.UserRoleRemovedId is null or 0)
            return;

        var removedRoles = cacheable.Value.Roles.Except(arsg2.Roles).ToList();
        if (!removedRoles.Any()) return;

        var channel = arsg2.Guild.GetTextChannel(logSetting.UserRoleRemovedId.Value);
        if (channel is null) return;

        await Task.Delay(500);
        var auditLogs = await arsg2.Guild.GetAuditLogsAsync(1, actionType: ActionType.MemberRoleUpdated).FlattenAsync();
        var auditLog = auditLogs.LastOrDefault();
        if (auditLog == null) return;

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle("User Role(s) Removed")
            .WithDescription($"`Role(s):` {string.Join(", ", removedRoles.Select(x => x.Mention))}\n" +
                             $"`Removed By:` {auditLog.User.Mention} | {auditLog.User.Id}" +
                             $"\n`Removed From:` {arsg2.Mention} | {arsg2.Id}");

        await channel.SendMessageAsync(embed: eb.Build());
    }


    /// <summary>
    ///     Handles the event when a user updates their username.
    /// </summary>
    /// <param name="args">The user before they updated their username.</param>
    /// <param name="arsg2">The user after they updated their username.</param>
    private async Task OnUsernameUpdated(SocketUser args, SocketUser arsg2)
    {
        if (args.Username.Equals(arsg2.Username))
            return;

        // Find relevant guilds
        foreach (var guild in client.Guilds)
        {
            var user = guild.GetUser(args.Id);
            if (user != null && GuildLogSettings.TryGetValue(guild.Id, out var logSetting))
            {
                if (logSetting.UsernameUpdatedId is null or 0)
                    continue;

                var channel = guild.GetTextChannel(logSetting.UsernameUpdatedId.Value);
                if (channel is null)
                    continue;

                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Username Updated")
                    .WithDescription(
                        $"`User:` {user.Mention} | {user.Id}\n" +
                        $"`Old Username:` {args.Username}\n" +
                        $"`New Username:` {arsg2.Username}");

                await channel.SendMessageAsync(embed: eb.Build());
            }
        }
    }

    /// <summary>
    ///     Handles the event when a user updates their nickname.
    /// </summary>
    /// <param name="cacheable">The user before they updated their nickname</param>
    /// <param name="arsg2">The user after they updated their nickname</param>
    private async Task OnNicknameUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser arsg2)
    {
        if (!cacheable.HasValue || cacheable.Value.Nickname == arsg2.Nickname) return;

        if (GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting))
        {
            if (logSetting.NicknameUpdatedId is null or 0)
                return;

            var channel = arsg2.Guild.GetTextChannel(logSetting.NicknameUpdatedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await arsg2.Guild.GetAuditLogsAsync(1, actionType: ActionType.MemberUpdated).FlattenAsync();
            var entry = auditLogs.FirstOrDefault();
            if (entry == null) return; // Cannot determine who changed it without audit log

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Nickname Updated")
                .WithDescription(
                    $"`User:` {arsg2.Mention} | {arsg2.Id}\n" +
                    $"`Old Nickname:` {cacheable.Value.Nickname ?? cacheable.Value.Username}\n" +
                    $"`New Nickname:` {arsg2.Nickname ?? arsg2.Username}\n" +
                    $"`Updated By:` {entry.User.Mention} | {entry.User.Id}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a thread gets deleted.
    /// </summary>
    /// <param name="args">The cached thread. May return null. See <see cref="Cacheable{TEntity,TId}" /></param>
    private async Task OnThreadDeleted(Cacheable<SocketThreadChannel, ulong> args)
    {
        if (!args.HasValue) return;
        var deletedThread = args.Value;
        if (GuildLogSettings.TryGetValue(deletedThread.Guild.Id, out var logSetting))
        {
            if (logSetting.ThreadDeletedId is null or 0)
                return;

            var channel = deletedThread.Guild.GetTextChannel(logSetting.ThreadDeletedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await deletedThread.Guild.GetAuditLogsAsync(1, actionType: ActionType.ThreadDelete)
                .FlattenAsync();
            var entry = auditLogs.FirstOrDefault();
            if (entry == null) return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Thread Deleted")
                .WithDescription(
                    $"`Thread Name:` {deletedThread.Name}\n" +
                    $"`Thread Id:` {deletedThread.Id}\n" +
                    $"`Deleted By:` {entry.User.Mention} | {entry.User.Id}");

            await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a thread is updated.
    /// </summary>
    /// <param name="cacheable">The cached thread. May return null. See <see cref="Cacheable{TEntity,TId}" /></param>
    /// <param name="arsg2">The updated thread.</param>
    private async Task OnThreadUpdated(Cacheable<SocketThreadChannel, ulong> cacheable, SocketThreadChannel arsg2)
    {
        if (!cacheable.HasValue) return;
        var oldThread = cacheable.Value;
        if (GuildLogSettings.TryGetValue(arsg2.Guild.Id, out var logSetting))
        {
            if (logSetting.ThreadUpdatedId is null or 0)
                return;

            var channel = arsg2.Guild.GetTextChannel(logSetting.ThreadUpdatedId.Value);
            if (channel is null)
                return;

            await Task.Delay(500);
            var auditLogs = await arsg2.Guild.GetAuditLogsAsync(1, actionType: ActionType.ThreadUpdate).FlattenAsync();
            var entry = auditLogs.FirstOrDefault();
            if (entry == null) return;

            var eb = new EmbedBuilder();
            var updatedByStr = $"`Updated By:` {entry.User.Mention} | {entry.User.Id}";
            var threadIdStr = $"`Thread:` {arsg2.Mention} | {arsg2.Id}";

            if (oldThread.Name != arsg2.Name)
                eb.WithOkColor().WithTitle("Thread Name Updated").WithDescription(
                    $"{threadIdStr}\n`Old Thread Name:` {oldThread.Name}\n`New Thread Name:` {arsg2.Name}\n{updatedByStr}");
            else if (oldThread.IsArchived != arsg2.IsArchived)
                eb.WithOkColor().WithTitle("Thread Archival Status Updated").WithDescription(
                    $"{threadIdStr}\nBefore: {oldThread.IsArchived}\nAfter: {arsg2.IsArchived}\n{updatedByStr}");
            else if (oldThread.IsLocked != arsg2.IsLocked)
                eb.WithOkColor().WithTitle("Thread Lock Status Updated").WithDescription(
                    $"{threadIdStr}\nBefore: {oldThread.IsLocked}\nAfter: {arsg2.IsLocked}\n{updatedByStr}");
            else if (oldThread.SlowModeInterval != arsg2.SlowModeInterval)
                eb.WithOkColor().WithTitle("Thread Slow Mode Interval Updated").WithDescription(
                    $"{threadIdStr}\n`Old Interval:` {oldThread.SlowModeInterval}s\n`New Interval:` {arsg2.SlowModeInterval}s\n{updatedByStr}");
            else if (oldThread.AutoArchiveDuration != arsg2.AutoArchiveDuration)
                eb.WithOkColor().WithTitle("Thread Auto Archive Duration Updated").WithDescription(
                    $"{threadIdStr}\n`Old Duration:` {oldThread.AutoArchiveDuration}\n`New Duration:` {arsg2.AutoArchiveDuration}\n{updatedByStr}");
            else
                return; // No detectable change handled

            if (!string.IsNullOrEmpty(eb.Title))
                await channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a message is updated.
    /// </summary>
    /// <param name="cacheable">The cached message. May return null. See <see cref="Cacheable{TEntity,TId}" /></param>
    /// <param name="args2">The new message</param>
    /// <param name="args3">The channel where the message was updated</param>
    private async Task OnMessageUpdated(Cacheable<IMessage, ulong> cacheable, SocketMessage args2,
        ISocketMessageChannel args3)
    {
        if (!cacheable.HasValue || args2.Author.IsBot) return;
        var oldMessage = cacheable.Value;
        if (args3 is not SocketTextChannel guildChannel) return;
        if (oldMessage.Content == args2.Content) return; // Compare content directly

        if (GuildLogSettings.TryGetValue(guildChannel.Guild.Id, out var logSetting))
        {
            if (logSetting.MessageUpdatedId is null or 0)
                return;

            var channel = guildChannel.Guild.GetTextChannel(logSetting.MessageUpdatedId.Value);
            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Message Updated")
                .WithDescription(
                    $"`Message Author:` {oldMessage.Author.Mention} | {oldMessage.Author.Id}\n" +
                    $"`Message Channel:` {guildChannel.Mention} | {guildChannel.Id}\n" +
                    $"`Message Id:` {oldMessage.Id}\n" +
                    $"`Old Message Content:` {oldMessage.Content.TrimTo(500)}\n" + // Trim content
                    $"`Updated Message Content:` {args2.Content.TrimTo(500)}"); // Trim content

            var component = new ComponentBuilder()
                .WithButton("Jump to Message", style: ButtonStyle.Link, url: oldMessage.GetJumpUrl()).Build();

            await channel.SendMessageAsync(embed: eb.Build(), components: component);
        }
    }

    /// <summary>
    ///     Handles the event when a message is deleted.
    /// </summary>
    /// <param name="args">The cached message. May return null. See <see cref="Cacheable{TEntity,TId}" /></param>
    /// <param name="arsg2">The channel where the message was deleted</param>
    private async Task OnMessageDeleted(Cacheable<IMessage, ulong> args, Cacheable<IMessageChannel, ulong> arsg2)
    {
        // Get message from cache if available
        var message = args.HasValue ? args.Value : null;
        // If message not in cache, we cannot proceed as we don't have content/author
        if (message == null || message.Author.IsBot) return;
        // Get channel from cache if available
        var messageChannel = arsg2.HasValue ? arsg2.Value : null;
        if (messageChannel is not SocketTextChannel guildChannel) return; // Ensure it's a guild channel

        if (GuildLogSettings.TryGetValue(guildChannel.Guild.Id, out var logSetting))
        {
            if (logSetting.MessageDeletedId is null or 0)
                return;

            var logChannel = guildChannel.Guild.GetTextChannel(logSetting.MessageDeletedId.Value);
            if (logChannel is null)
                return;

            await Task.Delay(1000); // Delay slightly to increase chance of audit log availability

            var auditLogs = await guildChannel.Guild.GetAuditLogsAsync(5, actionType: ActionType.MessageDeleted)
                .FlattenAsync(); // Check last 5 deletes

            // Try to find who deleted the message
            var deleteUser = message.Author; // Assume self-delete initially
            var entry = auditLogs.FirstOrDefault(e =>
                e.Data is MessageDeleteAuditLogData data && data.Target.Id == message.Author.Id);

            if (entry != null &&
                (DateTimeOffset.UtcNow - entry.CreatedAt).TotalSeconds < 10) // Check if recent audit log matches
            {
                deleteUser = entry.User;
            }


            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Message Deleted")
                .WithDescription(
                    $"`Message Author:` {message.Author.Mention} | {message.Author.Id}\n" +
                    $"`Message Channel:` {guildChannel.Mention} | {guildChannel.Id}\n" +
                    $"`Message Content:` {message.Content.TrimTo(1000)}\n" + // Trim content
                    $"`Deleted By:` {deleteUser?.Mention ?? "Unknown"} | {deleteUser?.Id.ToString() ?? "N/A"}");


            if (message.Attachments.Count > 0)
            {
                eb.AddField("Attachments", $"{message.Attachments.Count} attachment(s) were included in this message.");
                foreach (var att in message.Attachments.Take(5))
                {
                    eb.AddField(att.Filename.TrimTo(50), $"Size: {att.Size} bytes", true);
                }
            }

            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

    private static bool IsImageAttachment(string filename)
    {
        var imageExtensions = new[]
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
        }; // Added webp
        return imageExtensions.Any(ext => filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Handles the event when a user joins a guild.
    /// </summary>
    /// <param name="guildUser">The user that joined the guild.</param>
    private async Task OnUserJoined(IGuildUser guildUser)
    {
        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.UserJoinedId is null or 0)
                return;

            var channel = await guildUser.Guild.GetTextChannelAsync(logSetting.UserJoinedId.Value);
            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("User Joined")
                .WithDescription(
                    $"`User:` {guildUser.Mention} | {guildUser.Id}\n" +
                    $"`Account Created:` {guildUser.CreatedAt:dd/MM/yyyy HH:mm:ss}\n" + // Added time
                    $"`Joined Server:` {guildUser.JoinedAt:dd/MM/yyyy HH:mm:ss}\n" + // Added time
                    $"`User Status:` {guildUser.Status}\n" +
                    $"`User Global Name:` {guildUser.GlobalName ?? guildUser.Username}")
                .WithThumbnailUrl(guildUser.RealAvatarUrl().ToString());

            var component = new ComponentBuilder()
                .WithButton("View User", style: ButtonStyle.Link, url: $"discord://-/users/{guildUser.Id}").Build();

            await channel.SendMessageAsync(components: component, embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user leaves a guild.
    /// </summary>
    /// <param name="guild">The guild the user left.</param>
    /// <param name="user">The user that left the guild.</param>
    private async Task OnUserLeft(IGuild guild, IUser user)
    {
        if (GuildLogSettings.TryGetValue(guild.Id, out var logSetting))
        {
            if (logSetting.UserLeftId is null or 0)
                return;

            var channel = await guild.GetTextChannelAsync(logSetting.UserLeftId.Value);
            if (channel is null)
                return;

            // Check if it was a kick or ban via audit log
            var title = "User Left";
            string footer = null;
            await Task.Delay(1000); // Delay for audit log
            var auditLogsKick = await guild.GetAuditLogsAsync(1, actionType: ActionType.Kick);
            var kickLog = auditLogsKick.FirstOrDefault(e =>
                e.Data is KickAuditLogData data && data.Target.Id == user.Id &&
                (DateTimeOffset.UtcNow - e.CreatedAt).TotalSeconds < 10);

            var auditLogsBan = await guild.GetAuditLogsAsync(1, actionType: ActionType.Ban);
            var banLog = auditLogsBan.FirstOrDefault(e =>
                e.Data is BanAuditLogData data && data.Target.Id == user.Id &&
                (DateTimeOffset.UtcNow - e.CreatedAt).TotalSeconds < 10);

            if (kickLog != null)
            {
                title = "User Kicked";
                footer = $"Kicked by {kickLog.User.Username} ({kickLog.User.Id})";
            }
            else if (banLog != null)
            {
                // Ban event is handled by OnUserBanned, so we might not want to log here again.
                // However, if the user left *before* the ban event fired, this might catch it.
                // Let's just stick to "User Left" unless kicked.
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(title)
                .WithDescription(
                    $"`User:` {user.Mention} | {user.Id}\n" +
                    $"`User Global Name:` {user.GlobalName ?? user.Username}\n" +
                    $"`Account Created:` {user.CreatedAt:dd/MM/yyyy HH:mm:ss}") // Added time
                .WithThumbnailUrl(user.RealAvatarUrl().ToString());

            if (footer != null)
                eb.WithFooter(footer);

            var component = new ComponentBuilder().WithButton("View User (May not work)", style: ButtonStyle.Link,
                url: $"discord://-/users/{user.Id}").Build();

            await channel.SendMessageAsync(components: component, embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user is banned from a guild.
    /// </summary>
    /// <param name="user">The user that was banned.</param>
    /// <param name="guild">The guild the user was banned from.</param>
    /// <param name="bannedBy">The user that banned the user.</param>
    private async Task OnUserBanned(IUser user, SocketGuild guild, SocketUser bannedBy)
    {
        if (GuildLogSettings.TryGetValue(guild.Id, out var logSetting))
        {
            if (logSetting.UserBannedId is null or 0)
                return;

            var channel = guild.GetTextChannel(logSetting.UserBannedId.Value);
            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithErrorColor() // Use error color for bans
                .WithTitle("User Banned")
                .WithDescription(
                    $"`User:` {user.Mention} | {user.Id}\n" +
                    $"`User Global Name:` {user.GlobalName ?? user.Username}\n" +
                    $"`Account Created:` {user.CreatedAt:dd/MM/yyyy HH:mm:ss}\n" + // Added time
                    $"`Banned By:` {bannedBy?.Mention ?? "Unknown"} | {bannedBy?.Id.ToString() ?? "N/A"}")
                .WithThumbnailUrl(user.RealAvatarUrl().ToString());

            var component = new ComponentBuilder().WithButton("View User (May not work)", style: ButtonStyle.Link,
                url: $"discord://-/users/{user.Id}").Build();

            await channel.SendMessageAsync(components: component, embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user is unbanned from a guild.
    /// </summary>
    /// <param name="user">The user that was unbanned.</param>
    /// <param name="guild">The guild the user was unbanned from.</param>
    /// <param name="unbannedBy">The user that unbanned the user.</param>
    private async Task OnUserUnbanned(IUser user, SocketGuild guild, SocketUser unbannedBy)
    {
        if (GuildLogSettings.TryGetValue(guild.Id, out var logSetting))
        {
            if (logSetting.UserUnbannedId is null or 0)
                return;

            var channel = guild.GetTextChannel(logSetting.UserUnbannedId.Value);
            if (channel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("User Unbanned")
                .WithDescription(
                    $"`User:` {user.Mention} | {user.Id}\n" +
                    $"`User Global Name:` {user.GlobalName ?? user.Username}\n" +
                    $"`Account Created:` {user.CreatedAt:dd/MM/yyyy HH:mm:ss}\n" + // Added time
                    $"`Unbanned By:` {unbannedBy?.Mention ?? "Unknown"} | {unbannedBy?.Id.ToString() ?? "N/A"}")
                .WithThumbnailUrl(user.RealAvatarUrl().ToString());

            var component = new ComponentBuilder().WithButton("View User (May not work)", style: ButtonStyle.Link,
                url: $"discord://-/users/{user.Id}").Build();

            await channel.SendMessageAsync(components: component, embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user is updated.
    /// </summary>
    /// <param name="args">The user before the update.</param>
    /// <param name="arsg2">The user after the update.</param>
    private async Task OnUserUpdated(SocketUser args, SocketUser arsg2)
    {
        if (args.IsBot) return;

        var hasAvatarChanged = args.AvatarId != arsg2.AvatarId;
        var hasGlobalNameChanged = args.GlobalName != arsg2.GlobalName;

        if (!hasAvatarChanged && !hasGlobalNameChanged) return;

        // Iterate through shared guilds
        foreach (var guild in client.Guilds)
        {
            var userInGuild = guild.GetUser(args.Id);
            if (userInGuild == null || !GuildLogSettings.TryGetValue(guild.Id, out var logSetting)) continue;
            if (logSetting.UserUpdatedId is null or 0)
                continue;

            var logChannel = guild.GetTextChannel(logSetting.UserUpdatedId.Value);
            if (logChannel is null)
                continue;

            var eb = new EmbedBuilder().WithOkColor();

            if (hasAvatarChanged)
            {
                if (logSetting.AvatarUpdatedId != null)
                    logChannel = guild.GetTextChannel(logSetting.AvatarUpdatedId.Value);
                if (logChannel is null)
                    return;
                eb.WithTitle("User Avatar Updated")
                    .WithDescription($"`User:` {userInGuild.Mention} | {userInGuild.Id}")
                    .WithThumbnailUrl(args.RealAvatarUrl().ToString()) // Old avatar
                    .WithImageUrl(arsg2.RealAvatarUrl().ToString()); // New avatar
                await logChannel.SendMessageAsync(embed: eb.Build());
            }

            if (!hasGlobalNameChanged) continue;
            if (logSetting.UsernameUpdatedId != null)
                logChannel = guild.GetTextChannel(logSetting.UsernameUpdatedId.Value);
            if (logChannel is null)
                return;
            eb = new EmbedBuilder().WithOkColor()
                .WithTitle("User Global Name Updated")
                .WithDescription(
                    $"`User:` {userInGuild.Mention} | {userInGuild.Id}\n" +
                    $"`Old Global Name:` {args.GlobalName ?? args.Username}\n" +
                    $"`New Global Name:` {arsg2.GlobalName ?? arsg2.Username}")
                .WithThumbnailUrl(arsg2.RealAvatarUrl().ToString()); // Show current avatar
            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a channel is created in a guild.
    /// </summary>
    /// <param name="args">The channel that was created.</param>
    private async Task OnChannelCreated(SocketChannel args)
    {
        if (args is not SocketGuildChannel channel) return;

        if (GuildLogSettings.TryGetValue(channel.Guild.Id, out var logSetting))
        {
            if (logSetting.ChannelCreatedId is null or 0)
                return;

            var logChannel = channel.Guild.GetTextChannel(logSetting.ChannelCreatedId.Value);
            if (logChannel is null)
                return;

            var createdType = channel switch
            {
                SocketStageChannel => "Stage",
                SocketVoiceChannel => "Voice",
                SocketNewsChannel => "News",
                SocketTextChannel => "Text",
                SocketCategoryChannel => "Category",
                _ => "Unknown"
            };

            await Task.Delay(500);
            var auditLogs = await channel.Guild.GetAuditLogsAsync(1, actionType: ActionType.ChannelCreated)
                .FlattenAsync();
            var entry = auditLogs.FirstOrDefault();

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Channel Created")
                .WithDescription(
                    $"`Channel:` <#{channel.Id}> ({channel.Name}) | {channel.Id}\n" +
                    $"`Created By:` {entry?.User.Mention ?? "Unknown"} | {entry?.User.Id.ToString() ?? "N/A"}\n" +
                    $"`Created At:` {channel.CreatedAt:dd/MM/yyyy HH:mm:ss}\n" +
                    $"`Channel Type:` {createdType}");

            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a channel is destroyed/deleted in a guild.
    /// </summary>
    /// <param name="args">The channel that was destroyed.</param>
    private async Task OnChannelDestroyed(SocketChannel args)
    {
        if (args is not SocketGuildChannel channel) return;

        if (GuildLogSettings.TryGetValue(channel.Guild.Id, out var logSetting))
        {
            if (logSetting.ChannelDestroyedId is null or 0)
                return;

            var logChannel = channel.Guild.GetTextChannel(logSetting.ChannelDestroyedId.Value);
            if (logChannel is null)
                return;

            var createdType = channel switch
            {
                SocketStageChannel => "Stage",
                SocketVoiceChannel => "Voice",
                SocketNewsChannel => "News",
                SocketTextChannel => "Text",
                SocketCategoryChannel => "Category",
                _ => "Unknown"
            };

            await Task.Delay(500);
            var auditLogs = await channel.Guild.GetAuditLogsAsync(1, actionType: ActionType.ChannelDeleted)
                .FlattenAsync();
            var entry = auditLogs.FirstOrDefault();

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Channel Destroyed")
                .WithDescription(
                    $"`Channel:` {channel.Name} | {channel.Id}\n" + // Cannot mention deleted channel
                    $"`Destroyed By:` {entry?.User.Mention ?? "Unknown"} | {entry?.User.Id.ToString() ?? "N/A"}\n" +
                    $"`Destroyed At:` {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}\n" + // Added time
                    $"`Channel Type:` {createdType}");

            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a channel is updated in a guild.
    /// </summary>
    /// <param name="args">The channel before the update.</param>
    /// <param name="arsg2">The channel after the update.</param>
    private async Task OnChannelUpdated(SocketChannel args, SocketChannel arsg2)
    {
        if (args is not SocketGuildChannel channel || arsg2 is not SocketGuildChannel channel2) return;
        if (channel.Id != channel2.Id) return; // Should be the same channel

        if (GuildLogSettings.TryGetValue(channel.Guild.Id, out var logSetting))
        {
            if (logSetting.ChannelUpdatedId is null or 0)
                return;

            var logChannel = channel.Guild.GetTextChannel(logSetting.ChannelUpdatedId.Value);
            if (logChannel is null)
                return;

            await Task.Delay(500);
            var audit = await channel.Guild.GetAuditLogsAsync(1, actionType: ActionType.ChannelUpdated).FlattenAsync();
            var entry = audit.FirstOrDefault();
            if (entry == null) return;

            var eb = new EmbedBuilder().WithOkColor();
            var updatedByStr = $"`Updated By:` {entry.User.Mention} | {entry.User.Id}";
            var channelIdStr =
                $"`Channel:` <#{channel2.Id}> ({channel2.Name}) | {channel2.Id}"; // Mention current state
            var changed = false;

            if (channel.Name != channel2.Name)
            {
                eb.WithTitle("Channel Name Updated").WithDescription(
                    $"{channelIdStr}\n`Old Name:` {channel.Name}\n`New Name:` {channel2.Name}\n{updatedByStr}");
                changed = true;
            }
            else if (channel.Position != channel2.Position)
            {
                // Position changes are often noisy due to category changes, maybe ignore?
                // eb.WithTitle("Channel Position Updated").WithDescription($"{channelIdStr}\n`Old Position:` {channel.Position}\n`New Position:` {channel2.Position}\n{updatedByStr}");
                // changed = true;
            }

            // Text Channel specific changes
            if (channel is SocketTextChannel textChannel && channel2 is SocketTextChannel textChannel2)
            {
                if (textChannel.Topic != textChannel2.Topic)
                {
                    eb.WithTitle("Channel Topic Updated").WithDescription(
                        $"{channelIdStr}\n`Old Topic:` {textChannel.Topic?.TrimTo(100) ?? "None"}\n`New Topic:` {textChannel2.Topic?.TrimTo(100) ?? "None"}\n{updatedByStr}");
                    changed = true;
                }
                else if (textChannel.IsNsfw != textChannel2.IsNsfw)
                {
                    eb.WithTitle("Channel NSFW Updated").WithDescription(
                        $"{channelIdStr}\n`Old NSFW:` {textChannel.IsNsfw}\n`New NSFW:` {textChannel2.IsNsfw}\n{updatedByStr}");
                    changed = true;
                }
                else if (textChannel.SlowModeInterval != textChannel2.SlowModeInterval)
                {
                    eb.WithTitle("Channel Slowmode Interval Updated").WithDescription(
                        $"{channelIdStr}\n`Old Slowmode Interval:` {textChannel.SlowModeInterval}s\n`New Slowmode Interval:` {textChannel2.SlowModeInterval}s\n{updatedByStr}");
                    changed = true;
                }
                else if (textChannel.CategoryId != textChannel2.CategoryId)
                {
                    eb.WithTitle("Channel Category Updated").WithDescription(
                        $"{channelIdStr}\n`Old Category:` {textChannel.Category?.Name ?? "None"}\n`New Category:` {textChannel2.Category?.Name ?? "None"}\n{updatedByStr}");
                    changed = true;
                }
            }
            // Voice Channel specific changes
            else if (channel is SocketVoiceChannel voiceChannel && channel2 is SocketVoiceChannel voiceChannel2)
            {
                if (voiceChannel.Bitrate != voiceChannel2.Bitrate)
                {
                    eb.WithTitle("Channel Bitrate Updated").WithDescription(
                        $"{channelIdStr}\n`Old Bitrate:` {voiceChannel.Bitrate / 1000}kbps\n`New Bitrate:` {voiceChannel2.Bitrate / 1000}kbps\n{updatedByStr}");
                    changed = true;
                }
                else if (voiceChannel.UserLimit != voiceChannel2.UserLimit)
                {
                    eb.WithTitle("Channel User Limit Updated").WithDescription(
                        $"{channelIdStr}\n`Old User Limit:` {voiceChannel.UserLimit?.ToString() ?? "Unlimited"}\n`New User Limit:` {voiceChannel2.UserLimit?.ToString() ?? "Unlimited"}\n{updatedByStr}");
                    changed = true;
                }
                else if (voiceChannel.CategoryId != voiceChannel2.CategoryId)
                {
                    eb.WithTitle("Channel Category Updated").WithDescription(
                        $"{channelIdStr}\n`Old Category:` {voiceChannel.Category?.Name ?? "None"}\n`New Category:` {voiceChannel2.Category?.Name ?? "None"}\n{updatedByStr}");
                    changed = true;
                }
                else if (voiceChannel.VideoQualityMode != voiceChannel2.VideoQualityMode)
                {
                    eb.WithTitle("Channel Video Quality Mode Updated").WithDescription(
                        $"{channelIdStr}\n`Old Video Quality Mode:` {voiceChannel.VideoQualityMode}\n`New Video Quality Mode:` {voiceChannel2.VideoQualityMode}\n{updatedByStr}");
                    changed = true;
                }
            }

            if (changed && !string.IsNullOrEmpty(eb.Title))
            {
                await logChannel.SendMessageAsync(embed: eb.Build());
            }
        }
    }

    /// <summary>
    ///     Handles the event when voice state is updated.
    /// </summary>
    /// <param name="user">The user that had their voice state updated.</param>
    /// <param name="oldState">The voice state before the update.</param>
    /// <param name="newState">The voice state after the update.</param>
    private async Task OnVoicePresence(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
    {
        if (user.IsBot || user is not IGuildUser guildUser)
            return;

        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.LogVoicePresenceId is null or 0)
                return;

            var logChannel = await guildUser.Guild.GetTextChannelAsync(logSetting.LogVoicePresenceId.Value);
            if (logChannel is null)
                return;

            var eb = new EmbedBuilder().WithOkColor();
            var stateChanged = false;
            var userInfo = $"`User:` {user.Mention} | {user.Id}";

            // Channel Changes
            if (oldState.VoiceChannel?.Id != newState.VoiceChannel?.Id)
            {
                if (oldState.VoiceChannel != null && newState.VoiceChannel != null)
                    eb.WithTitle("User Moved Voice Channels").WithDescription(
                        $"{userInfo}\n`Old Channel:` {oldState.VoiceChannel.Name}\n`New Channel:` {newState.VoiceChannel.Name}");
                else if (oldState.VoiceChannel == null && newState.VoiceChannel != null)
                    eb.WithTitle("User Joined Voice Channel")
                        .WithDescription($"{userInfo}\n`Channel:` {newState.VoiceChannel.Name}");
                else if (oldState.VoiceChannel != null && newState.VoiceChannel == null)
                    eb.WithTitle("User Left Voice Channel")
                        .WithDescription($"{userInfo}\n`Channel:` {oldState.VoiceChannel.Name}");
                stateChanged = true;
            }
            // Mute/Deafen Status Changes (only log if channel didn't change, to avoid spam)
            else if (newState.VoiceChannel != null) // Only log these if user is still in a channel
            {
                if (oldState.IsDeafened != newState.IsDeafened)
                {
                    eb.WithTitle(newState.IsDeafened
                            ? $"User {(!newState.IsSelfDeafened ? "Server Voice Deafened" : "Self Voice Deafened")}"
                            : "User Voice UnDeafened")
                        .WithDescription($"{userInfo}\n`Channel:` {newState.VoiceChannel.Name}");
                    stateChanged = true;
                }
                else if (oldState.IsMuted != newState.IsMuted)
                {
                    eb.WithTitle(newState.IsMuted
                            ? $"User {(!newState.IsSelfMuted ? "Server Voice Muted" : "Self Voice Muted")}"
                            : "User Voice UnMuted")
                        .WithDescription($"{userInfo}\n`Channel:` {newState.VoiceChannel.Name}");
                    stateChanged = true;
                }
                else if (oldState.IsStreaming != newState.IsStreaming)
                {
                    eb.WithTitle(newState.IsStreaming ? "User Started Streaming" : "User Stopped Streaming")
                        .WithDescription($"{userInfo}\n`Channel:` {newState.VoiceChannel.Name}");
                    stateChanged = true;
                }
                else if (oldState.IsVideoing != newState.IsVideoing)
                {
                    eb.WithTitle(newState.IsVideoing ? "User Started Video" : "User Stopped Video")
                        .WithDescription($"{userInfo}\n`Channel:` {newState.VoiceChannel.Name}");
                    stateChanged = true;
                }
            }

            if (stateChanged && !string.IsNullOrEmpty(eb.Title))
            {
                await logChannel.SendMessageAsync(embed: eb.Build());
            }
        }
    }

    /// <summary>
    ///     Handles the event when a user says something using tts in a channel.
    /// </summary>
    /// <param name="user">The user that used tts.</param>
    /// <param name="oldState">The voice state before the update.</param>
    /// <param name="newState">The voice state after the update.</param>
    private async Task OnVoicePresenceTts(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
    {
        if (user.IsBot || user is not IGuildUser guildUser)
            return;

        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.LogVoicePresenceTtsId is null or 0)
                return;

            var logChannel = await guildUser.Guild.GetTextChannelAsync(logSetting.LogVoicePresenceTtsId.Value);
            if (logChannel is null)
                return;
            await Task.CompletedTask;
        }
    }

    /// <summary>
    ///     Handles the event when a user is muted in a guild.
    /// </summary>
    /// <param name="guildUser">The user that was muted.</param>
    /// <param name="muter">The user that muted the user.</param>
    /// <param name="muteType">Type of mute. <see cref="MuteType" /></param>
    /// <param name="reason">The reason the user was muted.</param>
    private async Task OnUserMuted(IGuildUser guildUser, IUser muter, MuteType muteType, string reason)
    {
        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.UserMutedId is null or 0)
                return;

            var logChannel = await guildUser.Guild.GetTextChannelAsync(logSetting.UserMutedId.Value);
            if (logChannel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"User {muteType} Muted")
                .WithDescription(
                    $"`User Muted:` {guildUser.Mention} | {guildUser.Id}\n" +
                    $"`Muted By:` {muter.Mention} | {muter.Id}\n" +
                    $"`Reason:` {reason ?? "N/A"}")
                .WithThumbnailUrl(guildUser.RealAvatarUrl().ToString());

            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Handles the event when a user is unmuted in a guild.
    /// </summary>
    /// <param name="guildUser">The user that was unmuted.</param>
    /// <param name="unmuter">The user that unmuted the user.</param>
    /// <param name="muteType">Type of mute. <see cref="MuteType" /></param>
    /// <param name="reason">The reason the user was unmuted.</param>
    private async Task OnUserUnmuted(IGuildUser guildUser, IUser unmuter, MuteType muteType, string reason)
    {
        if (GuildLogSettings.TryGetValue(guildUser.Guild.Id, out var logSetting))
        {
            if (logSetting.UserMutedId is null or 0)
                return;

            var logChannel = await guildUser.Guild.GetTextChannelAsync(logSetting.UserMutedId.Value);
            if (logChannel is null)
                return;

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"User {muteType} Unmuted")
                .WithDescription(
                    $"`User Unmuted:` {guildUser.Mention} | {guildUser.Id}\n" +
                    $"`Unmuted By:` {unmuter.Mention} | {unmuter.Id}\n" +
                    $"`Reason:` {reason ?? "N/A"}") // Reason might not apply
                .WithThumbnailUrl(guildUser.RealAvatarUrl().ToString());

            await logChannel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Sets the log channel for a specific type of log.
    /// </summary>
    /// <param name="guildId">The guildId to set the log setting for.</param>
    /// <param name="channelId">The channelId to set the log channel to.</param>
    /// <param name="type">The type of log to set the channel for.</param>
    public async Task SetLogChannel(ulong guildId, ulong channelId, LogType type)
    {
        LoggingV2 logSetting;
        var isNew = false;
        await using var db = await dbFactory.CreateConnectionAsync();

        logSetting = await db.LoggingV2.FirstOrDefaultAsync(l => l.GuildId == guildId).ConfigureAwait(false);

        if (logSetting == null)
        {
            isNew = true;
            logSetting = new LoggingV2
            {
                GuildId = guildId
            };
        }

        switch (type)
        {
            case LogType.Other: logSetting.LogOtherId = channelId; break;
            case LogType.EventCreated: logSetting.EventCreatedId = channelId; break;
            case LogType.RoleUpdated: logSetting.RoleUpdatedId = channelId; break;
            case LogType.RoleCreated: logSetting.RoleCreatedId = channelId; break;
            case LogType.ServerUpdated: logSetting.ServerUpdatedId = channelId; break;
            case LogType.ThreadCreated: logSetting.ThreadCreatedId = channelId; break;
            case LogType.UserRoleAdded: logSetting.UserRoleAddedId = channelId; break;
            case LogType.UserRoleRemoved: logSetting.UserRoleRemovedId = channelId; break;
            case LogType.UsernameUpdated: logSetting.UsernameUpdatedId = channelId; break;
            case LogType.NicknameUpdated: logSetting.NicknameUpdatedId = channelId; break;
            case LogType.ThreadDeleted: logSetting.ThreadDeletedId = channelId; break;
            case LogType.ThreadUpdated: logSetting.ThreadUpdatedId = channelId; break;
            case LogType.MessageUpdated: logSetting.MessageUpdatedId = channelId; break;
            case LogType.MessageDeleted: logSetting.MessageDeletedId = channelId; break;
            case LogType.UserJoined: logSetting.UserJoinedId = channelId; break;
            case LogType.UserLeft: logSetting.UserLeftId = channelId; break;
            case LogType.UserBanned: logSetting.UserBannedId = channelId; break;
            case LogType.UserUnbanned: logSetting.UserUnbannedId = channelId; break;
            case LogType.UserUpdated: logSetting.UserUpdatedId = channelId; break; // Assuming UserUpdatedId exists
            case LogType.ChannelCreated: logSetting.ChannelCreatedId = channelId; break;
            case LogType.ChannelDestroyed: logSetting.ChannelDestroyedId = channelId; break;
            case LogType.ChannelUpdated: logSetting.ChannelUpdatedId = channelId; break;
            case LogType.VoicePresence: logSetting.LogVoicePresenceId = channelId; break;
            case LogType.VoicePresenceTts: logSetting.LogVoicePresenceTtsId = channelId; break;
            case LogType.UserMuted: logSetting.UserMutedId = channelId; break;
            case LogType.RoleDeleted: logSetting.RoleDeletedId = channelId; break;
        }

        try
        {
            if (isNew)
                await db.InsertAsync(logSetting).ConfigureAwait(false);
            else
                await db.UpdateAsync(logSetting).ConfigureAwait(false);

            GuildLogSettings.AddOrUpdate(guildId, logSetting, (_, _) => logSetting);
        }
        catch (Exception e)
        {
            Log.Error(e, "There was an issue setting log settings for Guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Allows you to set the log channel for a specific category of logs.
    /// </summary>
    /// <param name="guildId">The guildId to set the logs for.</param>
    /// <param name="channelId">The channelId to set the logs to.</param>
    /// <param name="categoryTypes">The category of logs to set the channel for.</param>
    public async Task LogSetByType(ulong guildId, ulong channelId, LogCategoryTypes categoryTypes)
    {
        LoggingV2 logSetting;
        var isNew = false;
        await using var db = await dbFactory.CreateConnectionAsync();

        logSetting = await db.LoggingV2.FirstOrDefaultAsync(l => l.GuildId == guildId).ConfigureAwait(false);

        if (logSetting == null)
        {
            isNew = true;
            logSetting = new LoggingV2
            {
                GuildId = guildId
            };
        }

        switch (categoryTypes)
        {
            case LogCategoryTypes.All:
                logSetting.AvatarUpdatedId = channelId;
                logSetting.ChannelCreatedId = channelId;
                logSetting.ChannelDestroyedId = channelId;
                logSetting.ChannelUpdatedId = channelId;
                logSetting.EventCreatedId = channelId;
                logSetting.LogOtherId = channelId;
                logSetting.MessageDeletedId = channelId;
                logSetting.MessageUpdatedId = channelId;
                logSetting.NicknameUpdatedId = channelId;
                logSetting.RoleCreatedId = channelId;
                logSetting.RoleDeletedId = channelId;
                logSetting.RoleUpdatedId = channelId;
                logSetting.ServerUpdatedId = channelId;
                logSetting.ThreadCreatedId = channelId;
                logSetting.ThreadDeletedId = channelId;
                logSetting.ThreadUpdatedId = channelId;
                logSetting.UserBannedId = channelId;
                logSetting.UserJoinedId = channelId;
                logSetting.UserLeftId = channelId;
                logSetting.UserMutedId = channelId;
                logSetting.UsernameUpdatedId = channelId;
                logSetting.UserUnbannedId = channelId;
                logSetting.UserUpdatedId = channelId;
                logSetting.LogUserPresenceId = channelId;
                logSetting.LogVoicePresenceId = channelId;
                logSetting.UserRoleAddedId = channelId;
                logSetting.UserRoleRemovedId = channelId;
                logSetting.LogVoicePresenceTtsId = channelId;
                break;
            case LogCategoryTypes.Users:
                logSetting.NicknameUpdatedId = channelId;
                logSetting.AvatarUpdatedId = channelId; // Check model
                logSetting.UsernameUpdatedId = channelId;
                logSetting.UserRoleAddedId = channelId;
                logSetting.UserRoleRemovedId = channelId;
                logSetting.LogVoicePresenceId = channelId;
                logSetting.UserJoinedId = channelId;
                logSetting.UserLeftId = channelId;
                logSetting.UserUpdatedId = channelId; // General user update
                break;
            case LogCategoryTypes.Threads:
                logSetting.ThreadCreatedId = channelId;
                logSetting.ThreadDeletedId = channelId;
                logSetting.ThreadUpdatedId = channelId;
                break;
            case LogCategoryTypes.Roles:
                logSetting.RoleCreatedId = channelId;
                logSetting.RoleDeletedId = channelId;
                logSetting.RoleUpdatedId = channelId;
                break;
            case LogCategoryTypes.Server:
                logSetting.ServerUpdatedId = channelId;
                logSetting.EventCreatedId = channelId;
                break;
            case LogCategoryTypes.Channel:
                logSetting.ChannelUpdatedId = channelId;
                logSetting.ChannelCreatedId = channelId;
                logSetting.ChannelDestroyedId = channelId;
                break;
            case LogCategoryTypes.Messages:
                logSetting.MessageDeletedId = channelId;
                logSetting.MessageUpdatedId = channelId;
                break;
            case LogCategoryTypes.Moderation:
                logSetting.UserMutedId = channelId;
                logSetting.UserBannedId = channelId;
                logSetting.UserUnbannedId = channelId;
                logSetting.LogOtherId = channelId;
                break;
            case LogCategoryTypes.None:
                ulong? noneValue = null; // Use null for nullable ulongs, 0 otherwise if non-nullable
                logSetting.AvatarUpdatedId = noneValue; // Check model property types
                logSetting.ChannelCreatedId = noneValue;
                logSetting.ChannelDestroyedId = noneValue;
                logSetting.ChannelUpdatedId = noneValue;
                logSetting.EventCreatedId = noneValue;
                logSetting.LogOtherId = noneValue;
                logSetting.MessageDeletedId = noneValue;
                logSetting.MessageUpdatedId = noneValue;
                logSetting.NicknameUpdatedId = noneValue;
                logSetting.RoleCreatedId = noneValue;
                logSetting.RoleDeletedId = noneValue;
                logSetting.RoleUpdatedId = noneValue;
                logSetting.ServerUpdatedId = noneValue;
                logSetting.ThreadCreatedId = noneValue;
                logSetting.ThreadDeletedId = noneValue;
                logSetting.ThreadUpdatedId = noneValue;
                logSetting.UserBannedId = noneValue;
                logSetting.UserJoinedId = noneValue;
                logSetting.UserLeftId = noneValue;
                logSetting.UserMutedId = noneValue;
                logSetting.UsernameUpdatedId = noneValue;
                logSetting.UserUnbannedId = noneValue;
                logSetting.UserUpdatedId = noneValue;
                logSetting.LogUserPresenceId = noneValue;
                logSetting.LogVoicePresenceId = noneValue;
                logSetting.UserRoleAddedId = noneValue;
                logSetting.UserRoleRemovedId = noneValue;
                logSetting.LogVoicePresenceTtsId = noneValue;
                break;
        }

        try
        {
            if (isNew)
                await db.InsertAsync(logSetting).ConfigureAwait(false);
            else
                await db.UpdateAsync(logSetting).ConfigureAwait(false);

            GuildLogSettings.AddOrUpdate(guildId, logSetting, (_, _) => logSetting);
        }
        catch (Exception e)
        {
            Log.Error(e, "There was an issue setting log settings by type for Guild {GuildId}", guildId);
        }
    }
}