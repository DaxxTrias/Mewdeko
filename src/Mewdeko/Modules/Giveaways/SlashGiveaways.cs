﻿using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Giveaways.Services;
using SkiaSharp;

namespace Mewdeko.Modules.Giveaways;

/// <summary>
///     Slash commands for giveaways.
/// </summary>
/// <param name="dbFactory">The database service</param>
/// <param name="interactiveService">The service used to make paginated embeds</param>
/// <param name="guildSettings">Service for getting guild configs</param>
[Group("giveaways", "Create or manage giveaways!")]
public class SlashGiveaways(
    IDataConnectionFactory dbFactory,
    InteractiveService interactiveService,
    GuildSettingsService guildSettings)
    : MewdekoSlashModuleBase<GiveawayService>
{
    /// <summary>
    ///     Enters a giveaway via a button
    /// </summary>
    /// <param name="giveawayId">The giveaway id</param>
    [ComponentInteraction("entergiveaway:*", true)]
    public async Task EnterGiveaway(int giveawayId)
    {
        var (successful, reason) = await Service.AddUserToGiveaway(ctx.User.Id, giveawayId);
        if (!successful)
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.GiveawayEntryFailed(ctx.Guild.Id, reason), Config);
        }

        await ctx.Interaction.SendEphemeralConfirmAsync(Strings.GiveawayEntrySuccessful(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets the giveaway emote
    /// </summary>
    /// <param name="maybeEmote">The emote to set</param>
    [SlashCommand("emote", "Set the giveaway emote!")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task GEmote(string maybeEmote)
    {
        await DeferAsync().ConfigureAwait(false);
        var emote = maybeEmote.ToIEmote();
        if (emote.Name == null)
        {
            await ctx.Interaction.SendErrorFollowupAsync("That emote is invalid!", Config).ConfigureAwait(false);
            return;
        }

        try
        {
            var message = await ctx.Interaction.SendConfirmFollowupAsync("Checking emote...").ConfigureAwait(false);
            await message.AddReactionAsync(emote).ConfigureAwait(false);
        }
        catch
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                    "I'm unable to use that emote for giveaways! Most likely because I'm not in a server with it.",
                    Config)
                .ConfigureAwait(false);
            return;
        }

        await Service.SetGiveawayEmote(ctx.Guild, emote.ToString()).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmFollowupAsync(
                $"Giveaway emote set to {emote}! Just keep in mind this doesn't update until the next giveaway.")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the giveaway banner
    /// </summary>
    /// <param name="banner">The url of the banner to set</param>
    [SlashCommand("banner", "Allows you to set a banner for giveaways!")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task GBanner(string banner)
    {
        var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
        if (!Uri.IsWellFormedUriString(banner, UriKind.Absolute) && banner != "none")
        {
            await ctx.Interaction.SendErrorAsync(Strings.GiveawayInvalidUrl(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        gc.GiveawayBanner = banner == "none" ? "" : banner;
        await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
        await ctx.Interaction.SendConfirmAsync(Strings.GiveawayBannerSet(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets the giveaway embed color for winning users
    /// </summary>
    /// <param name="color">The color in hex</param>
    [SlashCommand("winembedcolor", "Allows you to set the win embed color!")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task GWinEmbedColor(string color)
    {
        var colorVal = StringExtensions.GetHexFromColorName(color);
        if (color.StartsWith("#"))
        {
            if (SKColor.TryParse(color, out _))
                colorVal = color;
        }

        if (colorVal is not null)
        {
            var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
            gc.GiveawayEmbedColor = colorVal;
            await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
            await ctx.Interaction.SendConfirmAsync(
                    Strings.GiveawayWinEmbedColorSet(ctx.Guild.Id))
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction
                .SendErrorAsync(
                    Strings.GiveawayInvalidColor(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the giveaway embed color
    /// </summary>
    /// <param name="color">The color in hex</param>
    [SlashCommand("embedcolor", "Allows you to set the regular embed color!")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task GEmbedColor(string color)
    {
        var colorVal = StringExtensions.GetHexFromColorName(color);
        if (color.StartsWith("#"))
        {
            if (SKColor.TryParse(color, out _))
                colorVal = color;
        }

        if (colorVal is not null)
        {
            var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
            gc.GiveawayEmbedColor = colorVal;
            await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
            await ctx.Interaction.SendConfirmAsync(
                    Strings.GiveawayEmbedColorSet(ctx.Guild.Id))
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction
                .SendErrorAsync(
                    Strings.GiveawayInvalidColor(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Toggles whether winners get dmed
    /// </summary>
    [SlashCommand("dm", "Toggles whether winners get dmed!")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task GDm()
    {
        var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
        gc.DmOnGiveawayWin = !gc.DmOnGiveawayWin;
        await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
        await ctx.Interaction.SendConfirmAsync(
                Strings.GiveawayDmStatus(ctx.Guild.Id, gc.DmOnGiveawayWin))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Rerolls a giveaway
    /// </summary>
    /// <param name="messageid">The messageid of the giveaway to reroll</param>
    [SlashCommand("reroll", "Rerolls a giveaway!")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task GReroll(ulong messageid)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var gway = dbContext.Giveaways
            .Where(x => x.ServerId == ctx.Guild.Id).ToList().Find(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Interaction.SendErrorAsync(Strings.GiveawayNotFound(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        if (gway.Ended != 1)
        {
            await ctx.Interaction.SendErrorAsync(Strings.GiveawayNotEnded(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        await Service.GiveawayTimerAction(gway).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync(Strings.GiveawayRerolled(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     View giveaway stats
    /// </summary>
    [SlashCommand("stats", "View giveaway stats!")]
    [CheckPermissions]
    public async Task GStats()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var eb = new EmbedBuilder().WithOkColor();
        var gways = dbContext.Giveaways.Where(x => x.ServerId == ctx.Guild.Id);
        if (!gways.Any())
        {
            await ctx.Interaction.SendErrorAsync(Strings.GiveawayNoStatsAvailable(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
        }
        else
        {
            List<ITextChannel> gchans = [];
            foreach (var i in gways)
            {
                var chan = await ctx.Guild.GetTextChannelAsync(i.ChannelId).ConfigureAwait(false);
                if (!gchans.Contains(chan))
                    gchans.Add(chan);
            }

            var amount = gways.Distinct(x => x.UserId).Count();
            eb.WithTitle(Strings.GiveawayStatsTitle(ctx.Guild.Id));
            eb.AddField(Strings.GiveawayStatsUsers(ctx.Guild.Id), amount, true);
            eb.AddField(Strings.GiveawayStatsTotal(ctx.Guild.Id), gways.Count(), true);
            eb.AddField(Strings.GiveawayStatsActive(ctx.Guild.Id), gways.Count(x => x.Ended == 0), true);
            eb.AddField(Strings.GiveawayStatsEnded(ctx.Guild.Id), gways.Count(x => x.Ended == 1), true);
            eb.AddField(Strings.GiveawayStatsChannels(ctx.Guild.Id),
                string.Join("\n", gchans.Select(x =>
                    Strings.GiveawayStatsChannelEntry(ctx.Guild.Id, x.Mention, gways.Count(s => s.ChannelId == x.Id)))),
                true);

            await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Starts a giveaway faster than just .gstart
    /// </summary>
    /// <param name="chan">The channel to start the giveaway in</param>
    /// <param name="time">The amount of time the giveaway should last</param>
    /// <param name="winners">The number of winners</param>
    /// <param name="what">The item being given away</param>
    /// <param name="pingRole">The role to ping when starting the giveaway</param>
    /// <param name="attachment">The banner to use for the giveaway</param>
    /// <param name="host">The host of the giveaway</param>
    /// <param name="useButton">Whether to use a button for joining the giveaway</param>
    /// <param name="useCaptcha">Whether to require captcha verification for joining</param>
    /// <param name="requiredRoles">Array of roles required to join the giveaway</param>
    /// <param name="requiredMessages">Number of messages required to join the giveaway</param>
    [SlashCommand("start", "Start a giveaway!")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task GStart(ITextChannel chan, TimeSpan time, int winners, string what, IRole pingRole = null,
        IAttachment attachment = null, IUser host = null, bool useButton = false, bool useCaptcha = false,
        IRole[] requiredRoles = null, ulong requiredMessages = 0)
    {
        host ??= ctx.User;
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        if (useButton && useCaptcha)
        {
            await ReplyAsync(Strings.GiveawayButtonCaptchaError(ctx.Guild.Id));
            return;
        }

        string reqs;
        if (requiredRoles is null || requiredRoles.Length == 0)
            reqs = "";
        else
            reqs = string.Join("\n", requiredRoles.Select(x => x.Id));

        var emote = (await Service.GetGiveawayEmote(ctx.Guild.Id)).ToIEmote();
        if (!useButton && !useCaptcha)
        {
            try
            {
                var message = await ctx.Interaction.SendConfirmFollowupAsync(
                    Strings.GiveawayCheckingEmote(ctx.Guild.Id)).ConfigureAwait(false);
                await message.AddReactionAsync(emote).ConfigureAwait(false);
            }
            catch
            {
                await ctx.Interaction.SendErrorFollowupAsync(
                        Strings.GiveawayEmoteInvalidInteraction(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }
        }

        var user = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = user.GetPermissions(chan);
        if (!useButton && !useCaptcha)
        {
            if (!perms.Has(ChannelPermission.AddReactions))
            {
                await ctx.Interaction.SendErrorFollowupAsync(
                        Strings.GiveawayNoReactionPermissionInteraction(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }
        }

        if (!perms.Has(ChannelPermission.UseExternalEmojis) && !ctx.Guild.Emotes.Contains(emote))
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                    Strings.GiveawayNoExternalEmotesInteraction(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        await Service.GiveawaysInternal(chan, time, what, winners, host.Id, ctx.Guild.Id,
            ctx.Channel as ITextChannel, ctx.Guild, banner: attachment?.Url, pingRole: pingRole, useButton: useButton,
            useCaptcha: useCaptcha, reqroles: reqs, messageCount: requiredMessages).ConfigureAwait(false);
    }

    /// <summary>
    ///     View current giveaways
    /// </summary>
    [SlashCommand("list", "View current giveaways!")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task GList()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var gways = dbContext.Giveaways.Where(x => x.ServerId == ctx.Guild.Id).Where(x => x.Ended == 0);
        if (!gways.Any())
        {
            await ctx.Interaction.SendErrorAsync(Strings.GiveawayNoActive(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(gways.Count() / 5)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            return new PageBuilder().WithOkColor()
                .WithTitle(Strings.GiveawayListTitle(ctx.Guild.Id, gways.Count()))
                .WithDescription(string.Join("\n\n",
                    await gways.Skip(page * 5).Take(5).AsEnumerable().Select(async x =>
                            Strings.GiveawayListEntry(ctx.Guild.Id, x.MessageId, x.Item, x.Winners,
                                await GetJumpUrl(x.ChannelId, x.MessageId).ConfigureAwait(false)))
                        .GetResults()
                        .ConfigureAwait(false)));
        }
    }

    private async Task<string> GetJumpUrl(ulong channelId, ulong messageId)
    {
        var channel = await ctx.Guild.GetTextChannelAsync(channelId).ConfigureAwait(false);
        var message = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
        return message.GetJumpUrl();
    }

    /// <summary>
    ///     End a giveaway
    /// </summary>
    /// <param name="messageid"></param>
    [SlashCommand("end", "End a giveaway!")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task GEnd(ulong messageid)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var gway = dbContext.Giveaways
            .Where(x => x.ServerId == ctx.Guild.Id).ToList().Find(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Interaction.SendErrorAsync(Strings.GiveawayNotFound(ctx.Guild.Id), Config).ConfigureAwait(false);
        }

        if (gway.Ended == 1)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.GiveawayAlreadyEnded(ctx.Guild.Id,
                    await guildSettings.GetPrefix(ctx.Guild),
                    messageid),
                Config).ConfigureAwait(false);
        }
        else
        {
            await Service.GiveawayTimerAction(gway).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.GiveawayEndedSuccess(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }
}