using System.Text;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Patreon.Services;

namespace Mewdeko.Modules.Patreon;

/// <summary>
///     Commands for managing Patreon integration and announcements.
/// </summary>
public class Patreon(IBotCredentials creds, PatreonApiClient patreonApiClient, ILogger<Patreon> logger)
    : MewdekoModuleBase<PatreonService>
{
    /// <summary>
    ///     Sets or shows the Patreon announcement channel.
    /// </summary>
    /// <param name="channel">The channel to set for Patreon announcements. If null, shows current channel.</param>
    /// <example>.patreon channel #announcements</example>
    /// <example>.patreon channel</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.SendMessages)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonChannel(ITextChannel? channel = null)
    {
        if (channel == null)
        {
            var config = await Service.GetPatreonConfig(ctx.Guild.Id);
            if (config.channelId == 0)
            {
                await ReplyConfirmAsync(Strings.PatreonNoChannel(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var currentChannel = guild.GetTextChannel(config.channelId);
            if (currentChannel == null)
            {
                await ReplyErrorAsync(Strings.PatreonChannelNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.PatreonCurrentChannel(ctx.Guild.Id, currentChannel.Mention))
                .ConfigureAwait(false);
            return;
        }

        // Check bot permissions in the target channel
        var socketGuild = ctx.Guild as SocketGuild;
        var perms = socketGuild.CurrentUser.GetPermissions(channel);
        if (!perms.SendMessages)
        {
            await ReplyErrorAsync(Strings.PatreonNoPermissions(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        await Service.SetPatreonChannel(ctx.Guild.Id, channel.Id);
        await ReplyConfirmAsync(Strings.PatreonChannelSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Disables Patreon announcements for this server.
    /// </summary>
    /// <example>.patreon disable</example>
    [Cmd]
    [Aliases]
    [Priority(1)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonDisable()
    {
        await Service.SetPatreonChannel(ctx.Guild.Id, 0);
        await ReplyConfirmAsync(Strings.PatreonDisabled(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a custom message for Patreon announcements.
    /// </summary>
    /// <param name="message">
    ///     The custom message. Use placeholders like %server.name%, %month%, etc. Use "-" to reset to
    ///     default.
    /// </param>
    /// <example>.patreon message 🎉 Support us on Patreon! %patreon.link%</example>
    /// <example>.patreon message -</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonMessage([Remainder] string? message = null)
    {
        if (message == null)
        {
            var config = await Service.GetPatreonConfig(ctx.Guild.Id);
            if (string.IsNullOrWhiteSpace(config.message))
            {
                await ReplyConfirmAsync(Strings.PatreonUsingDefaultMessage(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.PatreonCurrentMessage(ctx.Guild.Id, config.message)).ConfigureAwait(false);
            return;
        }

        await Service.SetPatreonMessage(ctx.Guild.Id, message);

        if (message == "-")
        {
            await ReplyConfirmAsync(Strings.PatreonMessageReset(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.PatreonMessageSet(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the day of the month for Patreon announcements.
    /// </summary>
    /// <param name="day">The day of the month (1-28) to send announcements.</param>
    /// <example>.patreon day 1</example>
    /// <example>.patreon day 15</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonDay(int? day = null)
    {
        if (day == null)
        {
            var config = await Service.GetPatreonConfig(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.PatreonCurrentDay(ctx.Guild.Id, config.day)).ConfigureAwait(false);
            return;
        }

        if (!await Service.SetAnnouncementDay(ctx.Guild.Id, day.Value))
        {
            await ReplyErrorAsync(Strings.PatreonInvalidDay(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.PatreonDaySet(ctx.Guild.Id, day.Value)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles Patreon announcements on or off.
    /// </summary>
    /// <example>.patreon toggle</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonToggle()
    {
        var enabled = await Service.TogglePatreonAnnouncements(ctx.Guild.Id);

        if (enabled)
        {
            await ReplyConfirmAsync(Strings.PatreonEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.PatreonDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Shows the current Patreon configuration for this server.
    /// </summary>
    /// <example>.patreon config</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
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

        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Manually triggers a Patreon announcement.
    /// </summary>
    /// <example>.patreon announce</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonAnnounce()
    {
        var success = await Service.TriggerManualAnnouncement(ctx.Guild.Id);

        if (success)
        {
            await ReplyConfirmAsync(Strings.PatreonAnnouncementSent(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.PatreonAnnouncementFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Shows Patreon module information and available placeholders.
    /// </summary>
    /// <example>.patreon help</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonHelp()
    {
        var eb = new EmbedBuilder()
            .WithTitle(Strings.PatreonHelpTitle(ctx.Guild.Id))
            .WithDescription(Strings.PatreonHelpDescription(ctx.Guild.Id))
            .WithColor(Mewdeko.OkColor)
            .AddField(Strings.PatreonHelpPlaceholders(ctx.Guild.Id),
                "**Basic Placeholders:**\n" +
                "`%server.name%` - Server name\n" +
                "`%channel.name%` - Channel name\n" +
                "`%month%` - Current month name\n" +
                "`%year%` - Current year\n" +
                "`%patreon.link%` - Patreon link\n\n" +
                "**Supporter Data:**\n" +
                "`%supporter.count%` - Active supporters\n" +
                "`%supporter.new%` - New supporters this month\n" +
                "`%revenue.monthly%` - Monthly revenue\n" +
                "`%supporters.summary%` - Formatted supporter text\n" +
                "`%revenue.summary%` - Revenue summary\n" +
                "`%growth.summary%` - Growth summary")
            .AddField(Strings.PatreonHelpBasicCommands(ctx.Guild.Id),
                Strings.PatreonHelpBasicCommandsList(ctx.Guild.Id))
            .AddField(Strings.PatreonHelpAdvancedCommands(ctx.Guild.Id),
                Strings.PatreonHelpAdvancedCommandsList(ctx.Guild.Id))
            .AddField("💡 Example Message with Data",
                "```🎉 It's %month%! Thanks to %supporters.summary%%revenue.summary%!%growth.summary%```\n" +
                "This becomes: *🎉 It's December! Thanks to our 25 incredible supporters who help us raise $150/month! We gained 3 new supporters this month!*");

        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Securely sets up Patreon OAuth integration
    /// </summary>
    /// <example>.patreon sync</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [OwnerOnly]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonSync()
    {
        try
        {
            // Check if Patreon integration is configured
            if (string.IsNullOrEmpty(creds.PatreonClientId))
            {
                await ReplyErrorAsync(
                        "❌ Patreon integration is not configured on this bot instance. Please contact the bot owner.")
                    .ConfigureAwait(false);
                return;
            }

            // Generate OAuth URL for this guild
            if (string.IsNullOrEmpty(creds.PatreonBaseUrl) || creds.PatreonBaseUrl == "https://yourdomain.com")
            {
                await ReplyErrorAsync(
                        "❌ Patreon base URL is not configured. Please contact the bot owner to set PatreonBaseUrl in credentials.")
                    .ConfigureAwait(false);
                return;
            }

            var redirectUri = $"{creds.PatreonBaseUrl}/dashboard/patreon";
            var state = $"{ctx.Guild.Id}:{Guid.NewGuid()}";

            var authUrl = patreonApiClient.GetAuthorizationUrl(
                creds.PatreonClientId,
                redirectUri,
                state);

            if (string.IsNullOrEmpty(authUrl))
            {
                await ReplyErrorAsync("❌ Failed to generate authorization URL.").ConfigureAwait(false);
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

            await ReplyAsync(embed: embed.Build(), components: component.Build()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting up Patreon OAuth for guild {GuildId}", ctx.Guild.Id);
            await ReplyErrorAsync("❌ An error occurred while setting up Patreon OAuth.").ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Lists active Patreon supporters
    /// </summary>
    /// <example>.patreon supporters</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonSupporters()
    {
        var supporters = await Service.GetActiveSupportersAsync(ctx.Guild.Id);

        if (!supporters.Any())
        {
            await ReplyErrorAsync(Strings.PatreonNoSupporters(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.PatreonSupportersTitle(ctx.Guild.Id, supporters.Count))
            .WithColor(0xF96854);

        var description = new StringBuilder();
        var count = 0;

        foreach (var supporter in supporters.Take(15)) // Limit to 15 for embed space
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
        await ctx.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Links a Discord user to a Patreon supporter
    /// </summary>
    /// <param name="user">Discord user to link</param>
    /// <param name="patreonUserId">Patreon user ID</param>
    /// <example>.patreon link @User 12345678</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonLink(IGuildUser user, string patreonUserId)
    {
        if (await Service.LinkUserAsync(ctx.Guild.Id, user.Id, patreonUserId))
        {
            await ReplyConfirmAsync(Strings.PatreonUserLinked(ctx.Guild.Id, user.Mention, patreonUserId))
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.PatreonLinkFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Toggles automatic role synchronization
    /// </summary>
    /// <example>.patreon rolesync</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonRoleSync()
    {
        var enabled = await Service.ToggleRoleSyncAsync(ctx.Guild.Id);

        if (enabled)
        {
            await ReplyConfirmAsync(Strings.PatreonRoleSyncEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.PatreonRoleSyncDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Maps a Patreon tier to a Discord role
    /// </summary>
    /// <param name="tierId">Patreon tier ID</param>
    /// <param name="role">Discord role to assign</param>
    /// <example>.patreon tiermap 1234567 @PatreonSupporter</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonTierMap(string tierId, IRole role)
    {
        if (await Service.MapTierToRoleAsync(ctx.Guild.Id, tierId, role.Id))
        {
            await ReplyConfirmAsync(Strings.PatreonTierMapped(ctx.Guild.Id, tierId, role.Mention))
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.PatreonTierMapFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Syncs roles for all linked supporters
    /// </summary>
    /// <example>.patreon syncall</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonSyncAll()
    {
        var msg = await ReplyAsync(Strings.PatreonSyncAllStarted(ctx.Guild.Id)).ConfigureAwait(false);

        var result = await Service.SyncAllRolesAsync(ctx.Guild.Id);

        await msg.ModifyAsync(m => m.Content = Strings.PatreonSyncAllComplete(ctx.Guild.Id, result))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows Patreon supporter statistics
    /// </summary>
    /// <example>.patreon stats</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonStats()
    {
        var supporters = await Service.GetActiveSupportersAsync(ctx.Guild.Id);
        var tiers = await Service.GetTiersAsync(ctx.Guild.Id);
        var creator = await Service.GetCreatorIdentity(ctx.Guild.Id);

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
            .AddField(Strings.PatreonStatsTiers(ctx.Guild.Id), tiers.Count, true);

        // Add creator information if available
        if (creator != null)
        {
            var creatorName = creator.Attributes.FullName ?? Strings.PatreonCreatorUnknown(ctx.Guild.Id);
            var creatorInfo = !string.IsNullOrEmpty(creator.Attributes.Url)
                ? $"[{creatorName}]({creator.Attributes.Url})"
                : creatorName;

            embed.AddField(Strings.PatreonStatsCreator(ctx.Guild.Id), creatorInfo, true);
        }

        if (supporters.Any())
        {
            var topSupporter = supporters.First();
            embed.AddField(Strings.PatreonStatsTop(ctx.Guild.Id),
                Strings.PatreonStatsTopSupporter(ctx.Guild.Id,
                    $"{topSupporter.FullName} - ${topSupporter.AmountCents / 100.0:F2}/month"));
        }

        await ctx.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows top Patreon supporters
    /// </summary>
    /// <param name="count">Number of top supporters to show (1-20)</param>
    /// <example>.patreon top</example>
    /// <example>.patreon top 15</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonTop(int count = 10)
    {
        if (count < 1 || count > 20) count = 10;

        var supporters = await Service.GetActiveSupportersAsync(ctx.Guild.Id);

        if (!supporters.Any())
        {
            await ReplyErrorAsync(Strings.PatreonNoSupporters(ctx.Guild.Id)).ConfigureAwait(false);
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
        await ctx.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists Patreon tier-role mappings
    /// </summary>
    /// <example>.patreon roles</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonRoles()
    {
        var tiers = await Service.GetTiersAsync(ctx.Guild.Id);

        if (!tiers.Any())
        {
            await ReplyErrorAsync(Strings.PatreonNoTiers(ctx.Guild.Id)).ConfigureAwait(false);
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
        await ctx.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows detailed Patreon analytics
    /// </summary>
    /// <example>.patreon analytics</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonAnalytics()
    {
        var analytics = await Service.GetAnalyticsAsync(ctx.Guild.Id);

        if (analytics.TotalSupporters == 0)
        {
            await ReplyErrorAsync(Strings.PatreonNoData(ctx.Guild.Id)).ConfigureAwait(false);
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

        await ctx.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sends recognition messages for new supporters
    /// </summary>
    /// <param name="channel">Channel to send recognition messages</param>
    /// <example>.patreon recognize #general</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.SendMessages)]
    [RequireContext(ContextType.Guild)]
    public async Task PatreonRecognize(ITextChannel? channel = null)
    {
        var targetChannel = channel ?? ctx.Channel as ITextChannel;
        if (targetChannel == null)
        {
            await ReplyErrorAsync(Strings.PatreonRecognizeInvalidChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var count = await Service.SendSupporterRecognitionAsync(ctx.Guild.Id, targetChannel.Id);

        if (count > 0)
        {
            await ReplyConfirmAsync(Strings.PatreonRecognizeSuccess(ctx.Guild.Id, count, targetChannel.Mention))
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyAsync(Strings.PatreonRecognizeNone(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }
}