using System.Text;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Patreon.Services;

namespace Mewdeko.Modules.Patreon;

/// <summary>
///     Slash commands for managing Patreon integration and announcements.
/// </summary>
[Group("patreon", "Manage Patreon settings")]
public class SlashPatreon(IBotCredentials creds, PatreonApiClient patreonApiClient, ILogger<SlashPatreon> logger)
    : MewdekoSlashModuleBase<PatreonService>
{
    /// <summary>
    ///     Sets the Patreon announcement channel.
    /// </summary>
    /// <param name="channel">The channel to set for Patreon announcements.</param>
    [SlashCommand("channel", "Set the Patreon announcement channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonChannel(ITextChannel channel)
    {
        // Check bot permissions in the target channel
        var socketGuild = ctx.Guild as SocketGuild;
        var perms = socketGuild.CurrentUser.GetPermissions(channel);
        if (!perms.SendMessages)
        {
            await EphemeralReplyErrorAsync(Strings.PatreonNoPermissions(ctx.Guild.Id, channel.Mention));
            return;
        }

        await Service.SetPatreonChannel(ctx.Guild.Id, channel.Id);
        await EphemeralReplyConfirmAsync(Strings.PatreonChannelSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Disables Patreon announcements for this server.
    /// </summary>
    [SlashCommand("disable", "Disable Patreon announcements")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonDisable()
    {
        await Service.SetPatreonChannel(ctx.Guild.Id, 0);
        await EphemeralReplyConfirmAsync(Strings.PatreonDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets a custom message for Patreon announcements.
    /// </summary>
    /// <param name="message">The custom message. Use placeholders like %server.name%, %month%, etc.</param>
    [SlashCommand("message", "Set a custom message for Patreon announcements")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonMessage(string message)
    {
        await Service.SetPatreonMessage(ctx.Guild.Id, message);

        if (message == "-")
        {
            await EphemeralReplyConfirmAsync(Strings.PatreonMessageReset(ctx.Guild.Id));
        }
        else
        {
            await EphemeralReplyConfirmAsync(Strings.PatreonMessageSet(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Resets the Patreon message to default.
    /// </summary>
    [SlashCommand("reset-message", "Reset the Patreon message to default")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonResetMessage()
    {
        await Service.SetPatreonMessage(ctx.Guild.Id, "-");
        await EphemeralReplyConfirmAsync(Strings.PatreonMessageReset(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets the day of the month for Patreon announcements.
    /// </summary>
    /// <param name="day">The day of the month (1-28) to send announcements.</param>
    [SlashCommand("day", "Set the day of the month for Patreon announcements")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonDay([MinValue(1)] [MaxValue(28)] int day)
    {
        if (!await Service.SetAnnouncementDay(ctx.Guild.Id, day))
        {
            await EphemeralReplyErrorAsync(Strings.PatreonInvalidDay(ctx.Guild.Id));
            return;
        }

        await EphemeralReplyConfirmAsync(Strings.PatreonDaySet(ctx.Guild.Id, day));
    }

    /// <summary>
    ///     Toggles Patreon announcements on or off.
    /// </summary>
    [SlashCommand("toggle", "Toggle Patreon announcements on or off")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonToggle()
    {
        var enabled = await Service.TogglePatreonAnnouncements(ctx.Guild.Id);

        if (enabled)
        {
            await EphemeralReplyConfirmAsync(Strings.PatreonEnabled(ctx.Guild.Id));
        }
        else
        {
            await EphemeralReplyConfirmAsync(Strings.PatreonDisabled(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Shows the current Patreon configuration for this server.
    /// </summary>
    [SlashCommand("config", "Show current Patreon configuration")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonConfig()
    {
        var config = await Service.GetPatreonConfig(ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.PatreonConfigTitle(ctx.Guild.Id))
            .WithColor(Mewdeko.OkColor);

        if (config.channelId == 0)
        {
            eb.AddField(Strings.PatreonConfigStatus(ctx.Guild.Id), Strings.PatreonConfigDisabled(ctx.Guild.Id), true);
        }
        else
        {
            var socketGuild = ctx.Guild as SocketGuild;
            var channel = socketGuild.GetTextChannel(config.channelId);
            var channelText = channel?.Mention ?? Strings.PatreonChannelNotFound(ctx.Guild.Id);

            eb.AddField(Strings.PatreonConfigStatus(ctx.Guild.Id),
                    config.enabled
                        ? Strings.PatreonConfigEnabled(ctx.Guild.Id)
                        : Strings.PatreonConfigDisabled(ctx.Guild.Id), true)
                .AddField(Strings.PatreonConfigChannel(ctx.Guild.Id), channelText, true)
                .AddField(Strings.PatreonConfigDay(ctx.Guild.Id), config.day.ToString(), true)
                .AddField(Strings.PatreonConfigMessage(ctx.Guild.Id),
                    string.IsNullOrWhiteSpace(config.message) ? Strings.PatreonConfigDefault(ctx.Guild.Id) : "Custom",
                    true);

            if (config.lastAnnouncement.HasValue)
            {
                eb.AddField(Strings.PatreonConfigLast(ctx.Guild.Id),
                    $"<t:{((DateTimeOffset)config.lastAnnouncement.Value).ToUnixTimeSeconds()}:R>", true);
            }
        }

        await FollowupAsync(embed: eb.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Manually triggers a Patreon announcement.
    /// </summary>
    [SlashCommand("announce", "Manually trigger a Patreon announcement")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonAnnounce()
    {
        var success = await Service.TriggerManualAnnouncement(ctx.Guild.Id);

        if (success)
        {
            await EphemeralReplyConfirmAsync(Strings.PatreonAnnouncementSent(ctx.Guild.Id));
        }
        else
        {
            await EphemeralReplyErrorAsync(Strings.PatreonAnnouncementFailed(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Shows Patreon module information and available placeholders.
    /// </summary>
    [SlashCommand("help", "Show Patreon module help and available placeholders")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task PatreonHelp()
    {
        var eb = new EmbedBuilder()
            .WithTitle(Strings.PatreonHelpTitle(ctx.Guild.Id))
            .WithDescription(Strings.PatreonHelpDescription(ctx.Guild.Id))
            .WithColor(Mewdeko.OkColor)
            .AddField(Strings.PatreonHelpPlaceholders(ctx.Guild.Id),
                "`%server.name%` - Server name\n" +
                "`%server.id%` - Server ID\n" +
                "`%channel.name%` - Channel name\n" +
                "`%channel.mention%` - Channel mention\n" +
                "`%bot.name%` - Bot display name\n" +
                "`%bot.mention%` - Bot mention\n" +
                "`%month%` - Current month name\n" +
                "`%year%` - Current year\n" +
                "`%patreon.link%` - Patreon link")
            .AddField(Strings.PatreonHelpCommands(ctx.Guild.Id),
                "`/patreon channel` - Set announcement channel\n" +
                "`/patreon message` - Set custom message\n" +
                "`/patreon day` - Set announcement day\n" +
                "`/patreon toggle` - Enable/disable announcements\n" +
                "`/patreon config` - Show current settings\n" +
                "`/patreon announce` - Send manual announcement");

        await FollowupAsync(embed: eb.Build(), ephemeral: true);
    }

    /// <summary>
    ///     View the current Patreon announcement message.
    /// </summary>
    [SlashCommand("view", "View current Patreon announcement message")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonView()
    {
        var config = await Service.GetPatreonConfig(ctx.Guild.Id);

        if (string.IsNullOrWhiteSpace(config.message))
        {
            await EphemeralReplyConfirmAsync(Strings.PatreonUsingDefaultMessage(ctx.Guild.Id));
            return;
        }

        await EphemeralReplyConfirmAsync(Strings.PatreonCurrentMessage(ctx.Guild.Id, config.message));
    }

    /// <summary>
    ///     Securely sets up Patreon OAuth integration
    /// </summary>
    [SlashCommand("sync", "Set up Patreon OAuth integration")]
    [RequireContext(ContextType.Guild)]
    [SlashOwnerOnly]
    [CheckPermissions]
    public async Task PatreonSync()
    {
        await DeferAsync(true);

        try
        {
            // Check if Patreon integration is configured
            if (string.IsNullOrEmpty(creds?.PatreonClientId))
            {
                await FollowupAsync(
                    "❌ Patreon integration is not configured on this bot instance. Please contact the bot owner.",
                    ephemeral: true);
                return;
            }

            // Generate OAuth URL for this guild
            if (string.IsNullOrEmpty(creds.PatreonBaseUrl) || creds.PatreonBaseUrl == "https://yourdomain.com")
            {
                await FollowupAsync(
                    "❌ Patreon base URL is not configured. Please contact the bot owner to set PatreonBaseUrl in credentials.",
                    ephemeral: true);
                return;
            }

            var redirectUri = $"{creds.PatreonBaseUrl}/dashboard/patreon";
            var state = $"{ctx.Guild.Id}:{Guid.NewGuid()}";

            var authUrl = patreonApiClient?.GetAuthorizationUrl(
                creds.PatreonClientId,
                redirectUri,
                state);

            if (string.IsNullOrEmpty(authUrl))
            {
                await FollowupAsync("❌ Failed to generate authorization URL.", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.PatreonOauthTitle(ctx.Guild.Id))
                .WithDescription(Strings.PatreonAuthDescription(ctx.Guild.Id))
                .WithColor(0xF96854)
                .AddField("⚠️ Important Requirements",
                    "• You must be the **owner** of a Patreon campaign\n" +
                    "• This will give the bot access to your supporter data\n" +
                    "• You can revoke access anytime from your Patreon settings")
                .AddField("📋 Setup Process",
                    "1. Click the \"Authorize with Patreon\" button below\n" +
                    "2. Sign in to Patreon if prompted\n" +
                    "3. Review and approve the requested permissions\n" +
                    "4. You'll be redirected back automatically\n" +
                    "5. Your supporters will be synced automatically")
                .WithFooter(Strings.PatreonSetupFooter(ctx.Guild.Id));

            var component = new ComponentBuilder()
                .WithButton("Authorize with Patreon", style: ButtonStyle.Link, url: authUrl, emote: new Emoji("🔗"));

            await FollowupAsync(embed: embed.Build(), components: component.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting up Patreon OAuth for guild {GuildId}", ctx.Guild.Id);
            await FollowupAsync("❌ An error occurred while setting up Patreon OAuth.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Lists active Patreon supporters
    /// </summary>
    [SlashCommand("supporters", "List active Patreon supporters")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonSupporters()
    {
        await DeferAsync(true);

        var supporters = await Service.GetActiveSupportersAsync(ctx.Guild.Id);

        if (!supporters.Any())
        {
            await FollowupAsync(Strings.PatreonNoSupporters(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.PatreonSupportersTitle(ctx.Guild.Id, supporters.Count))
            .WithColor(0xF96854);

        var description = new StringBuilder();
        var count = 0;

        foreach (var supporter in supporters.Take(15))
        {
            count++;
            var discordMention = supporter.DiscordUserId != 0
                ? $"<@{supporter.DiscordUserId}>"
                : Strings.PatreonNotLinked(ctx.Guild.Id);

            var amount = supporter.AmountCents / 100.0;
            description.AppendLine(Strings.PatreonSupporterEntry(ctx.Guild.Id, count, supporter.FullName,
                amount.ToString("F2")));
            description.AppendLine(Strings.PatreonSupporterDiscord(ctx.Guild.Id, discordMention));
            description.AppendLine();
        }

        if (supporters.Count > 15)
        {
            description.AppendLine(Strings.PatreonSupportersMore(ctx.Guild.Id, supporters.Count - 15));
        }

        embed.WithDescription(description.ToString());
        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Links a Discord user to a Patreon supporter
    /// </summary>
    /// <param name="user">Discord user to link</param>
    /// <param name="patreonUserId">Patreon user ID</param>
    [SlashCommand("link", "Link a Discord user to a Patreon supporter")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonLink(IGuildUser user, string patreonUserId)
    {
        if (await Service.LinkUserAsync(ctx.Guild.Id, user.Id, patreonUserId))
        {
            await EphemeralReplyConfirmAsync(Strings.PatreonUserLinked(ctx.Guild.Id, user.Mention, patreonUserId));
        }
        else
        {
            await EphemeralReplyErrorAsync(Strings.PatreonLinkFailed(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Toggles automatic role synchronization
    /// </summary>
    [SlashCommand("rolesync", "Toggle automatic role synchronization")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task PatreonRoleSync()
    {
        var enabled = await Service.ToggleRoleSyncAsync(ctx.Guild.Id);

        if (enabled)
        {
            await EphemeralReplyConfirmAsync(Strings.PatreonRoleSyncEnabled(ctx.Guild.Id));
        }
        else
        {
            await EphemeralReplyConfirmAsync(Strings.PatreonRoleSyncDisabled(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Maps a Patreon tier to a Discord role
    /// </summary>
    /// <param name="tierId">Patreon tier ID</param>
    /// <param name="role">Discord role to assign</param>
    [SlashCommand("tiermap", "Map a Patreon tier to a Discord role")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task PatreonTierMap(string tierId, IRole role)
    {
        if (await Service.MapTierToRoleAsync(ctx.Guild.Id, tierId, role.Id))
        {
            await EphemeralReplyConfirmAsync(Strings.PatreonTierMapped(ctx.Guild.Id, tierId, role.Mention));
        }
        else
        {
            await EphemeralReplyErrorAsync(Strings.PatreonTierMapFailed(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Syncs roles for all linked supporters
    /// </summary>
    [SlashCommand("syncall", "Sync roles for all linked supporters")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task PatreonSyncAll()
    {
        await DeferAsync(true);

        var result = await Service.SyncAllRolesAsync(ctx.Guild.Id);

        await FollowupAsync(Strings.PatreonSyncAllComplete(ctx.Guild.Id, result), ephemeral: true);
    }

    /// <summary>
    ///     Shows Patreon supporter statistics
    /// </summary>
    [SlashCommand("stats", "Show Patreon supporter statistics")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonStats()
    {
        await DeferAsync(true);

        var supporters = await Service.GetActiveSupportersAsync(ctx.Guild.Id);
        var tiers = await Service.GetTiersAsync(ctx.Guild.Id);
        var goals = await Service.GetGoalsAsync(ctx.Guild.Id);

        var totalAmount = supporters.Sum(s => s.AmountCents) / 100.0;
        var linkedCount = supporters.Count(s => s.DiscordUserId != 0);
        var averageAmount = supporters.Any() ? supporters.Average(s => s.AmountCents) / 100.0 : 0;

        var embed = new EmbedBuilder()
            .WithTitle(Strings.PatreonStatsTitle(ctx.Guild.Id))
            .WithColor(0xF96854)
            .AddField(Strings.PatreonStatsSupporters(ctx.Guild.Id), supporters.Count, true)
            .AddField(Strings.PatreonStatsLinked(ctx.Guild.Id), linkedCount, true)
            .AddField(Strings.PatreonStatsRevenue(ctx.Guild.Id), $"${totalAmount:F2}", true)
            .AddField(Strings.PatreonStatsAverage(ctx.Guild.Id), $"${averageAmount:F2}", true)
            .AddField(Strings.PatreonStatsTiers(ctx.Guild.Id), tiers.Count, true)
            .AddField(Strings.PatreonStatsGoals(ctx.Guild.Id), goals.Count, true);

        if (supporters.Any())
        {
            var topSupporter = supporters.First();
            embed.AddField(Strings.PatreonStatsTop(ctx.Guild.Id),
                Strings.PatreonStatsTopSupporter(ctx.Guild.Id,
                    $"{topSupporter.FullName} - ${(topSupporter.AmountCents / 100.0):F2}"));
        }

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Shows current Patreon goals
    /// </summary>
    [SlashCommand("goals", "Show current Patreon goals")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task PatreonGoals()
    {
        await DeferAsync(true);

        var goals = await Service.GetGoalsAsync(ctx.Guild.Id);

        if (!goals.Any())
        {
            await FollowupAsync(Strings.PatreonNoGoals(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.PatreonGoalsTitle(ctx.Guild.Id))
            .WithColor(0xF96854);

        foreach (var goal in goals.Take(5))
        {
            var amount = goal.AmountCents / 100.0;
            var status = goal.ReachedAt.HasValue
                ? Strings.PatreonGoalReached(ctx.Guild.Id)
                : Strings.PatreonGoalProgress(ctx.Guild.Id, goal.CompletedPercentage);

            embed.AddField(Strings.PatreonGoalField(ctx.Guild.Id, $"{goal.Title} (${amount:F2})"),
                $"{goal.Description}\n**{Strings.PatreonGoalStatus(ctx.Guild.Id)}:** {status}");
        }

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Shows top Patreon supporters
    /// </summary>
    /// <param name="count">Number of top supporters to show (1-20)</param>
    [SlashCommand("top", "Show top Patreon supporters")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task PatreonTop([MinValue(1)] [MaxValue(20)] int count = 10)
    {
        await DeferAsync(true);

        var supporters = await Service.GetActiveSupportersAsync(ctx.Guild.Id);

        if (!supporters.Any())
        {
            await FollowupAsync(Strings.PatreonNoSupporters(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.PatreonTopTitle(ctx.Guild.Id, Math.Min(count, supporters.Count)))
            .WithColor(0xF96854);

        var description = new StringBuilder();

        for (var i = 0; i < Math.Min(count, supporters.Count); i++)
        {
            var supporter = supporters[i];
            var amount = supporter.AmountCents / 100.0;
            var medal = i < 3
                ? new[]
                {
                    "🥇", "🥈", "🥉"
                }[i]
                : $"{i + 1}.";

            description.AppendLine(Strings.PatreonTopEntry(ctx.Guild.Id, medal, supporter.FullName,
                amount.ToString("F2")));
        }

        embed.WithDescription(description.ToString());
        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Lists Patreon tier-role mappings
    /// </summary>
    [SlashCommand("roles", "List Patreon tier-role mappings")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonRoles()
    {
        await DeferAsync(true);

        var tiers = await Service.GetTiersAsync(ctx.Guild.Id);

        if (!tiers.Any())
        {
            await FollowupAsync(Strings.PatreonNoTiers(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.PatreonRolesTitle(ctx.Guild.Id))
            .WithColor(0xF96854);

        var description = new StringBuilder();

        foreach (var tier in tiers)
        {
            var role = tier.DiscordRoleId != 0
                ? ctx.Guild.GetRole(tier.DiscordRoleId)?.Mention ?? Strings.PatreonRoleDeleted(ctx.Guild.Id)
                : Strings.PatreonRoleNotMapped(ctx.Guild.Id);

            var amount = tier.AmountCents / 100.0;
            description.AppendLine(Strings.PatreonTierInfo(ctx.Guild.Id, $"**{tier.TierTitle}** (${amount:F2}/month)"));
            description.AppendLine(Strings.PatreonTierRole(ctx.Guild.Id, role));
            description.AppendLine(Strings.PatreonTierId(ctx.Guild.Id, tier.TierId));
            description.AppendLine();
        }

        embed.WithDescription(description.ToString());
        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Shows detailed Patreon analytics
    /// </summary>
    [SlashCommand("analytics", "Show detailed Patreon analytics")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task PatreonAnalytics()
    {
        await DeferAsync(true);

        var analytics = await Service.GetAnalyticsAsync(ctx.Guild.Id);

        if (analytics.TotalSupporters == 0)
        {
            await FollowupAsync(Strings.PatreonNoData(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.PatreonAnalyticsTitle(ctx.Guild.Id))
            .WithColor(0xF96854)
            .AddField(Strings.PatreonAnalyticsOverview(ctx.Guild.Id),
                Strings.PatreonAnalyticsOverviewData(ctx.Guild.Id,
                    analytics.TotalSupporters,
                    analytics.ActiveSupporters,
                    analytics.FormerSupporters,
                    analytics.LinkedSupporters), true)
            .AddField(Strings.PatreonAnalyticsRevenue(ctx.Guild.Id),
                Strings.PatreonAnalyticsRevenueData(ctx.Guild.Id,
                    analytics.TotalMonthlyRevenue,
                    analytics.AverageSupport,
                    analytics.LifetimeRevenue,
                    analytics.NewSupportersThisMonth), true);

        if (analytics.TierDistribution.Any())
        {
            var tierText = string.Join("\n",
                analytics.TierDistribution.Select(kvp =>
                    Strings.PatreonAnalyticsTierEntry(ctx.Guild.Id, kvp.Key, kvp.Value)));
            embed.AddField(Strings.PatreonAnalyticsDistribution(ctx.Guild.Id), tierText, true);
        }

        if (analytics.TopSupporters.Any())
        {
            var topText = string.Join("\n", analytics.TopSupporters.Select((s, i) =>
                Strings.PatreonAnalyticsTopEntry(ctx.Guild.Id, i + 1, s.Name, s.Amount, s.IsLinked ? "✅" : "❌")));
            embed.AddField(Strings.PatreonAnalyticsTop(ctx.Guild.Id), topText);
        }

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Sends recognition messages for new supporters
    /// </summary>
    /// <param name="channel">Channel to send recognition messages</param>
    [SlashCommand("recognize", "Send recognition messages for new supporters")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.SendMessages)]
    [CheckPermissions]
    public async Task PatreonRecognize(ITextChannel? channel = null)
    {
        await DeferAsync(true);

        var targetChannel = channel ?? ctx.Channel as ITextChannel;
        if (targetChannel == null)
        {
            await FollowupAsync(Strings.PatreonRecognizeInvalidChannel(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var count = await Service.SendSupporterRecognitionAsync(ctx.Guild.Id, targetChannel.Id);

        if (count > 0)
        {
            await FollowupAsync(Strings.PatreonRecognizeSuccess(ctx.Guild.Id, count, targetChannel.Mention),
                ephemeral: true);
        }
        else
        {
            await FollowupAsync(Strings.PatreonRecognizeNone(ctx.Guild.Id), ephemeral: true);
        }
    }
}