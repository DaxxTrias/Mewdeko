using System.Text;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;
using Serilog;

namespace Mewdeko.Modules.Xp;

/// <summary>
///     Module for managing XP system functionality.
/// </summary>
public class Xp(InteractiveService serv, ICurrencyService currencyService) : MewdekoModuleBase<XpService>
{
    /// <summary>
    ///     Shows a user's XP card with their rank, level, and progress.
    /// </summary>
    /// <param name="user">The user to show the XP card for. If not specified, shows the caller's card.</param>
    /// <example>.rank</example>
    /// <example>.rank @user</example>
    [Cmd]
    [Aliases]
    public async Task Rank([Remainder] IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;

        // Prevent showing ranks for bots
        if (user.IsBot)
        {
            await ReplyErrorAsync(Strings.XpBotsNoRank(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            var stats = await Service.GetUserXpStatsAsync(ctx.Guild.Id, user.Id);

            // Check if user has any XP
            if (stats.TotalXp == 0 && user.Id == ctx.User.Id)
            {
                await ReplyErrorAsync(Strings.XpYouNoXp(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (stats.TotalXp == 0)
            {
                await ReplyErrorAsync(Strings.XpUserNoXp(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
                return;
            }

            // Generate XP card
            var cardPath = await Service.GenerateXpCardAsync(ctx.Guild.Id, user.Id);
            await ctx.Channel.SendFileAsync(cardPath, "xp.png", Strings.XpCardFor(ctx.Guild.Id, user.ToString()));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting XP card for {UserId} in {GuildId}", user.Id, ctx.Guild.Id);
            await ReplyErrorAsync(Strings.XpErrorGettingCard(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Shows a user's XP information in a text format that's accessible for screen readers.
    /// </summary>
    /// <param name="user">The user to show XP information for. If not specified, shows the caller's stats.</param>
    /// <example>.textrank</example>
    /// <example>.textrank @user</example>
    [Cmd]
    [Aliases]
    public async Task TextRank([Remainder] IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;

        // Prevent showing ranks for bots
        if (user.IsBot)
        {
            await ReplyErrorAsync(Strings.XpBotsNoRank(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            var stats = await Service.GetUserXpStatsAsync(ctx.Guild.Id, user.Id);

            // Check if user has any XP
            if (stats.TotalXp == 0 && user.Id == ctx.User.Id)
            {
                await ReplyErrorAsync(Strings.XpYouNoXp(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (stats.TotalXp == 0)
            {
                await ReplyErrorAsync(Strings.XpUserNoXp(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
                return;
            }

            // Calculate progress percentage
            var progressPercent = (int)((double)stats.LevelXp / stats.RequiredXp * 100);

            // Format the text response in an accessible way
            var response = new StringBuilder();
            response.AppendLine(Strings.TextRankHeader(ctx.Guild.Id, user.ToString()));
            response.AppendLine(Strings.TextRankLevel(ctx.Guild.Id, stats.Level));
            response.AppendLine(Strings.TextRankXp(ctx.Guild.Id, stats.TotalXp));
            response.AppendLine(
                Strings.TextRankProgress(ctx.Guild.Id, stats.LevelXp, stats.RequiredXp, progressPercent));
            response.AppendLine(Strings.TextRankServerPosition(ctx.Guild.Id, stats.Rank));

            // Check if user has bonus XP
            if (stats.BonusXp > 0)
            {
                response.AppendLine(Strings.TextRankBonusXp(ctx.Guild.Id, stats.BonusXp));
            }

            await ReplyAsync(response.ToString()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting text XP stats for {UserId} in {GuildId}", user.Id, ctx.Guild.Id);
            await ReplyErrorAsync(Strings.XpErrorGettingStats(ctx.Guild.Id));
        }
    }

    /// <summary>
///     Shows the XP leaderboard for the server.
/// </summary>
/// <param name="page">Page number to display (starts at 1).</param>
/// <example>.leaderboard</example>
/// <example>.leaderboard 2</example>
[Cmd]
[Aliases]
public async Task Leaderboard(int page = 1)
{
    if (page < 1)
        page = 1;

    var serverLb = await Service.GetLeaderboardAsync(ctx.Guild.Id, page);

    if (serverLb.Count == 0)
    {
        await ReplyErrorAsync(Strings.XpLeaderboardEmpty(ctx.Guild.Id)).ConfigureAwait(false);
        return;
    }

    var users = await ctx.Guild.GetUsersAsync();

    var userDict = users.ToDictionary(u => u.Id, u => u);

    var paginator = new LazyPaginatorBuilder()
        .AddUser(ctx.User)
        .WithPageFactory(pageNum => BuildPage(pageNum, userDict))
        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
        .WithDefaultEmotes()
        .WithActionOnCancellation(ActionOnStop.DeleteMessage)
        .Build();

    await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);
    return;

    async Task<PageBuilder> BuildPage(int pageNum, Dictionary<ulong, IGuildUser> userLookup)
    {
        var pageUsers = await Service.GetLeaderboardAsync(ctx.Guild.Id, pageNum + 1);

        var embed = new PageBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpLeaderboardTitle(ctx.Guild.Id));

        var lb = new List<string>();

        foreach (var user in pageUsers)
        {
            var username = userLookup.TryGetValue(user.UserId, out var guildUser) ? guildUser.ToString() : user.UserId.ToString();

            lb.Add(Strings.XpLeaderboardLine(
                ctx.Guild.Id,
                user.Rank,
                username,
                user.Level,
                user.TotalXp));
        }

        embed.WithDescription(string.Join("\n", lb));
        return embed;
    }
}

    /// <summary>
    ///     Adds XP to a user.
    /// </summary>
    /// <param name="user">The user to add XP to.</param>
    /// <param name="amount">The amount of XP to add.</param>
    /// <example>.addxp @user 100</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AddXp(IGuildUser user, int amount)
    {
        if (amount <= 0)
        {
            await ReplyErrorAsync(Strings.XpAmountPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (user.IsBot)
        {
            await ReplyErrorAsync(Strings.XpCantAddToBot(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.AddXpAsync(ctx.Guild.Id, user.Id, amount);
        await ReplyConfirmAsync(Strings.XpAdded(ctx.Guild.Id, amount, user.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a user's XP to a specific amount.
    /// </summary>
    /// <param name="user">The user to set XP for.</param>
    /// <param name="amount">The amount of XP to set.</param>
    /// <example>.setxp @user 1000</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetXp(IGuildUser user, long amount)
    {
        if (amount < 0)
        {
            await ReplyErrorAsync(Strings.XpAmountNotNegative(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (user.IsBot)
        {
            await ReplyErrorAsync(Strings.XpCantAddToBot(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetUserXpAsync(ctx.Guild.Id, user.Id, amount);
        await ReplyConfirmAsync(Strings.XpSet(ctx.Guild.Id, user.ToString(), amount)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Resets a user's XP to zero.
    /// </summary>
    /// <param name="user">The user to reset XP for.</param>
    /// <param name="resetBonus">Whether to also reset bonus XP. Defaults to false.</param>
    /// <example>.resetxp @user</example>
    /// <example>.resetxp @user true</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task ResetXp(IGuildUser user, bool resetBonus = false)
    {
        await Service.ResetUserXpAsync(ctx.Guild.Id, user.Id, resetBonus);

        if (resetBonus)
            await ReplyConfirmAsync(Strings.XpResetWithBonus(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.XpReset(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Resets a user's XP to zero.
    /// </summary>
    /// <example>.resetxp @user</example>
    /// <example>.resetxp @user true</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ResetXp()
    {
        await Service.ResetGuildXp(ctx.Guild.Id, true);
        await ReplyConfirmAsync(Strings.XpResetGuild(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets how users are notified when they level up.
    /// </summary>
    /// <param name="type">The notification type: None, Channel, or DM.</param>
    /// <example>.levelnotif Channel</example>
    [Cmd]
    [Aliases]
    public async Task LevelNotif(XpNotificationType type)
    {
        await Service.SetUserNotificationPreferenceAsync(ctx.Guild.Id, ctx.User.Id, type);
        await ReplyConfirmAsync(Strings.XpNotificationSet(ctx.Guild.Id, type)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the server's XP leaderboard settings.
    /// </summary>
    /// <example>.xpsettings</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpSettings()
    {
        var settings = await Service.GetGuildXpSettingsAsync(ctx.Guild.Id);
        var exclusions = new List<string>();

        // Get exclusion information
        var excludedUsers = await Service.GetExcludedItemsAsync(ctx.Guild.Id, ExcludedItemType.User);
        if (excludedUsers.Count > 0)
            exclusions.Add(Strings.XpExcludedUsers(ctx.Guild.Id, excludedUsers.Count));

        var excludedRoles = await Service.GetExcludedItemsAsync(ctx.Guild.Id, ExcludedItemType.Role);
        if (excludedRoles.Count > 0)
            exclusions.Add(Strings.XpExcludedRoles(ctx.Guild.Id, excludedRoles.Count));

        var excludedChannels = await Service.GetExcludedItemsAsync(ctx.Guild.Id, ExcludedItemType.Channel);
        if (excludedChannels.Count > 0)
            exclusions.Add(Strings.XpExcludedChannels(ctx.Guild.Id, excludedChannels.Count));

        // Get boost events
        var boostEvents = await Service.GetActiveBoostEventsAsync(ctx.Guild.Id);
        var boostInfo = boostEvents.Count > 0
            ? Strings.XpActiveBoostEvents(ctx.Guild.Id, boostEvents.Count)
            : Strings.XpNoActiveBoostEvents(ctx.Guild.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpSettingsTitle(ctx.Guild.Id))
            .AddField(Strings.XpBasicSettings(ctx.Guild.Id), Strings.XpSettingsBasicInfo(
                ctx.Guild.Id,
                settings.XpPerMessage,
                settings.MessageXpCooldown,
                settings.VoiceXpPerMinute,
                settings.VoiceXpTimeout,
                settings.XpMultiplier,
                settings.XpCurveType
            ))
            .AddField(Strings.XpExclusions(ctx.Guild.Id),
                exclusions.Count > 0 ? string.Join("\n", exclusions) : Strings.XpNoExclusions(ctx.Guild.Id))
            .AddField(Strings.XpBoosts(ctx.Guild.Id), boostInfo);

        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Sets the amount of XP gained per message.
    /// </summary>
    /// <param name="amount">The amount of XP to award per message.</param>
    /// <example>.setmsgxp 5</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetMessageXp(int amount)
    {
        if (amount <= 0)
        {
            await ReplyErrorAsync(Strings.XpAmountPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (amount > XpService.MaxXpPerMessage)
        {
            await ReplyErrorAsync(Strings.XpMessageTooHigh(ctx.Guild.Id, XpService.MaxXpPerMessage))
                .ConfigureAwait(false);
            return;
        }

        await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.XpPerMessage = amount);
        await ReplyConfirmAsync(Strings.XpMessageSet(ctx.Guild.Id, amount)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the cooldown between message XP gains.
    /// </summary>
    /// <param name="seconds">The cooldown in seconds.</param>
    /// <example>.setxpcooldown 60</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetXpCooldown(int seconds)
    {
        if (seconds < 0)
        {
            await ReplyErrorAsync(Strings.XpCooldownNotNegative(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.MessageXpCooldown = seconds);

        if (seconds == 0)
            await ReplyConfirmAsync(Strings.XpCooldownDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.XpCooldownSet(ctx.Guild.Id, seconds)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the amount of XP gained per minute in voice channels.
    /// </summary>
    /// <param name="amount">The amount of XP to award per minute in voice.</param>
    /// <example>.setvoicexp 2</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetVoiceXp(int amount)
    {
        if (amount < 0)
        {
            await ReplyErrorAsync(Strings.XpAmountNotNegative(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (amount > XpService.MaxVoiceXpPerMinute)
        {
            await ReplyErrorAsync(Strings.XpVoiceTooHigh(ctx.Guild.Id, XpService.MaxVoiceXpPerMinute))
                .ConfigureAwait(false);
            return;
        }

        await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.VoiceXpPerMinute = amount);

        if (amount == 0)
            await ReplyConfirmAsync(Strings.XpVoiceDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.XpVoiceSet(ctx.Guild.Id, amount)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the voice XP timeout (how long a user must be in voice to gain XP).
    /// </summary>
    /// <param name="minutes">The timeout in minutes.</param>
    /// <example>.setvoicetimeout 5</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetVoiceTimeout(int minutes)
    {
        if (minutes <= 0)
        {
            await ReplyErrorAsync(Strings.XpTimeoutPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.VoiceXpTimeout = minutes);
        await ReplyConfirmAsync(Strings.XpVoiceTimeoutSet(ctx.Guild.Id, minutes)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the server-wide XP multiplier.
    /// </summary>
    /// <param name="multiplier">The XP multiplier value.</param>
    /// <example>.setxpmultiplier 1.5</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetXpMultiplier(double multiplier)
    {
        if (multiplier <= 0)
        {
            await ReplyErrorAsync(Strings.XpMultiplierPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.XpMultiplier = multiplier);
        await ReplyConfirmAsync(Strings.XpMultiplierSet(ctx.Guild.Id, multiplier)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the XP curve type used for level calculations.
    /// </summary>
    /// <param name="type">The XP curve type: Default, Flat, or Steep.</param>
    /// <example>.setxpcurve Flat</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetXpCurve(XpCurveType type)
    {
        await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.XpCurveType = (int)type);
        await ReplyConfirmAsync(Strings.XpCurveSet(ctx.Guild.Id, type)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets an XP multiplier for a channel.
    /// </summary>
    /// <param name="channel">The channel to set the multiplier for.</param>
    /// <param name="multiplier">The multiplier value.</param>
    /// <example>.setchannelxp #general 2.0</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetChannelXp(ITextChannel channel, double multiplier)
    {
        if (multiplier <= 0)
        {
            await ReplyErrorAsync(Strings.XpMultiplierPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetChannelMultiplierAsync(ctx.Guild.Id, channel.Id, multiplier);

        if (multiplier == 1.0)
            await ReplyConfirmAsync(Strings.XpChannelMultiplierReset(ctx.Guild.Id, channel.Mention))
                .ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.XpChannelMultiplierSet(ctx.Guild.Id, channel.Mention, multiplier))
                .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets an XP multiplier for a role.
    /// </summary>
    /// <param name="role">The role to set the multiplier for.</param>
    /// <param name="multiplier">The multiplier value.</param>
    /// <example>.setrolexp "VIP Members" 1.5</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetRoleXp(IRole role, double multiplier)
    {
        if (multiplier <= 0)
        {
            await ReplyErrorAsync(Strings.XpMultiplierPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetRoleMultiplierAsync(ctx.Guild.Id, role.Id, multiplier);

        if (multiplier == 1.0)
            await ReplyConfirmAsync(Strings.XpRoleMultiplierReset(ctx.Guild.Id, role.Mention)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.XpRoleMultiplierSet(ctx.Guild.Id, role.Mention, multiplier))
                .ConfigureAwait(false);
    }

    /// <summary>
    ///     Creates a temporary XP boost event.
    /// </summary>
    /// <param name="time">How long the boost should last.</param>
    /// <param name="multiplier">The XP multiplier for the boost.</param>
    /// <param name="name">The name of the boost event.</param>
    /// <example>.xpboost 2h 2.0 Weekend XP Event</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpBoost(StoopidTime time, double multiplier, [Remainder] string name)
    {
        if (time.Time.TotalMinutes < 5)
        {
            await ReplyErrorAsync(Strings.XpBoostTooShort(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (multiplier <= 1.0)
        {
            await ReplyErrorAsync(Strings.XpBoostMultiplierTooLow(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            await ReplyErrorAsync(Strings.XpBoostNeedsName(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var startTime = DateTime.UtcNow;
        var endTime = startTime + time.Time;

        var boostEvent = await Service.CreateXpBoostEventAsync(
            ctx.Guild.Id,
            name,
            multiplier,
            startTime,
            endTime
        );

        await ReplyConfirmAsync(Strings.XpBoostCreated(
            ctx.Guild.Id,
            boostEvent.Name,
            boostEvent.Multiplier,
            time.Time.Humanize()
        )).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists active XP boost events.
    /// </summary>
    /// <example>.xpboosts</example>
    [Cmd]
    [Aliases]
    public async Task XpBoosts()
    {
        var boosts = await Service.GetActiveBoostEventsAsync(ctx.Guild.Id);

        if (boosts.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpNoActiveBoosts(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpActiveBoostsTitle(ctx.Guild.Id));

        foreach (var boost in boosts)
        {
            var timeLeft = boost.EndTime - DateTime.UtcNow;
            embed.AddField(
                $"{boost.Name} ({boost.Multiplier}x)",
                Strings.XpBoostTimeLeft(ctx.Guild.Id, timeLeft.Humanize())
            );
        }

        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Cancels an XP boost event.
    /// </summary>
    /// <param name="eventId">ID of the event to cancel.</param>
    /// <example>.cancelboost 3</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task CancelBoost(int eventId)
    {
        var success = await Service.CancelXpBoostEventAsync(eventId);

        if (success)
            await ReplyConfirmAsync(Strings.XpBoostCancelled(ctx.Guild.Id, eventId)).ConfigureAwait(false);
        else
            await ReplyErrorAsync(Strings.XpBoostNotFound(ctx.Guild.Id, eventId)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Excludes a user from gaining XP.
    /// </summary>
    /// <param name="user">The user to exclude.</param>
    /// <example>.xpexclude @user</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpExclude(IGuildUser user)
    {
        await Service.ExcludeItemAsync(ctx.Guild.Id, user.Id, ExcludedItemType.User);
        await ReplyConfirmAsync(Strings.XpUserExcluded(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Excludes a role from gaining XP.
    /// </summary>
    /// <param name="role">The role to exclude.</param>
    /// <example>.xpexclude @role</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpExclude(IRole role)
    {
        await Service.ExcludeItemAsync(ctx.Guild.Id, role.Id, ExcludedItemType.Role);
        await ReplyConfirmAsync(Strings.XpRoleExcluded(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Excludes a channel from gaining XP.
    /// </summary>
    /// <param name="channel">The channel to exclude.</param>
    /// <example>.xpexclude #channel</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpExclude(ITextChannel channel)
    {
        await Service.ExcludeItemAsync(ctx.Guild.Id, channel.Id, ExcludedItemType.Channel);
        await ReplyConfirmAsync(Strings.XpChannelExcluded(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Includes a previously excluded user for XP gain.
    /// </summary>
    /// <param name="user">The user to include.</param>
    /// <example>.xpinclude @user</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpInclude(IGuildUser user)
    {
        await Service.IncludeItemAsync(ctx.Guild.Id, user.Id, ExcludedItemType.User);
        await ReplyConfirmAsync(Strings.XpUserIncluded(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Includes a previously excluded role for XP gain.
    /// </summary>
    /// <param name="role">The role to include.</param>
    /// <example>.xpinclude @role</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpInclude(IRole role)
    {
        await Service.IncludeItemAsync(ctx.Guild.Id, role.Id, ExcludedItemType.Role);
        await ReplyConfirmAsync(Strings.XpRoleIncluded(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Includes a previously excluded channel for XP gain.
    /// </summary>
    /// <param name="channel">The channel to include.</param>
    /// <example>.xpinclude #channel</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpInclude(ITextChannel channel)
    {
        await Service.IncludeItemAsync(ctx.Guild.Id, channel.Id, ExcludedItemType.Channel);
        await ReplyConfirmAsync(Strings.XpChannelIncluded(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists excluded users, roles, or channels.
    /// </summary>
    /// <param name="type">The type of exclusions to list: users, roles, or channels.</param>
    /// <example>.xpexcludelist users</example>
    [Cmd]
    [Aliases]
    public async Task XpExcludeList(string type = null)
    {
        type = type?.ToLower();

        if (string.IsNullOrWhiteSpace(type) || type != "users" && type != "roles" && type != "channels")
        {
            await ReplyErrorAsync(Strings.XpExcludeListInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        ExcludedItemType itemType;
        string title;

        switch (type)
        {
            case "users":
                itemType = ExcludedItemType.User;
                title = Strings.XpExcludedUsersTitle(ctx.Guild.Id);
                break;
            case "roles":
                itemType = ExcludedItemType.Role;
                title = Strings.XpExcludedRolesTitle(ctx.Guild.Id);
                break;
            case "channels":
                itemType = ExcludedItemType.Channel;
                title = Strings.XpExcludedChannelsTitle(ctx.Guild.Id);
                break;
            default:
                return;
        }

        var items = await Service.GetExcludedItemsAsync(ctx.Guild.Id, itemType);

        if (items.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpNoExcludedItems(ctx.Guild.Id, type)).ConfigureAwait(false);
            return;
        }

        var names = new List<string>();

        foreach (var id in items)
        {
            switch (itemType)
            {
                case ExcludedItemType.User:
                    var user = await ctx.Guild.GetUserAsync(id);
                    names.Add(user != null ? user.ToString() : id.ToString());
                    break;
                case ExcludedItemType.Role:
                    var role = ctx.Guild.GetRole(id);
                    names.Add(role != null ? role.Name : id.ToString());
                    break;
                case ExcludedItemType.Channel:
                    var channel = await ctx.Guild.GetTextChannelAsync(id);
                    names.Add(channel != null ? channel.Mention : id.ToString());
                    break;
            }
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(names.Count / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder()
                .WithOkColor()
                .WithTitle($"{title} ({names.Count})")
                .WithDescription(string.Join("\n", names.Skip(page * 20).Take(20)));
        }
    }

    /// <summary>
    ///     Sets a role reward for a specific level.
    /// </summary>
    /// <param name="level">The level that triggers the reward.</param>
    /// <param name="role">The role to award.</param>
    /// <example>.rolereward 10 @VIP</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task RoleReward(int level, IRole role)
    {
        if (level <= 0)
        {
            await ReplyErrorAsync(Strings.XpLevelPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        // Check if the bot can assign this role
        if (role.Position >= ((IGuildUser)ctx.User).GetRoles().Max(r => r.Position))
        {
            await ReplyErrorAsync(Strings.XpRoleHierarchy(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetRoleRewardAsync(ctx.Guild.Id, level, role.Id);
        await ReplyConfirmAsync(Strings.XpRoleRewardSet(ctx.Guild.Id, level, role.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a role reward for a specific level.
    /// </summary>
    /// <param name="level">The level to remove the reward from.</param>
    /// <example>.removerr 10</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task RemoveRoleReward(int level)
    {
        if (level <= 0)
        {
            await ReplyErrorAsync(Strings.XpLevelPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetRoleRewardAsync(ctx.Guild.Id, level, null);
        await ReplyConfirmAsync(Strings.XpRoleRewardRemoved(ctx.Guild.Id, level)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all role rewards.
    /// </summary>
    /// <example>.rolerewards</example>
    [Cmd]
    [Aliases]
    public async Task RoleRewards()
    {
        var rewards = await Service.GetRoleRewardsAsync(ctx.Guild.Id);

        if (rewards.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpNoRoleRewards(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpRoleRewardsTitle(ctx.Guild.Id));

        var rewardLines = new List<string>();

        foreach (var reward in rewards.OrderBy(r => r.Level))
        {
            var role = ctx.Guild.GetRole(reward.RoleId);
            if (role != null)
                rewardLines.Add(Strings.XpRoleRewardLine(ctx.Guild.Id, reward.Level, role.Mention));
        }

        embed.WithDescription(string.Join("\n", rewardLines));
        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Sets a currency reward for a specific level.
    /// </summary>
    /// <param name="level">The level that triggers the reward.</param>
    /// <param name="amount">The amount of currency to award.</param>
    /// <example>.currencyreward 5 100</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task CurrencyReward(int level, long amount)
    {
        if (level <= 0)
        {
            await ReplyErrorAsync(Strings.XpLevelPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (amount <= 0)
        {
            await ReplyErrorAsync(Strings.XpAmountPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetCurrencyRewardAsync(ctx.Guild.Id, level, amount);

        await ReplyConfirmAsync(Strings.XpCurrencyRewardSet(
            ctx.Guild.Id,
            level,
            amount,
            await currencyService.GetCurrencyEmote(ctx.Guild.Id)
        )).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a currency reward for a specific level.
    /// </summary>
    /// <param name="level">The level to remove the reward from.</param>
    /// <example>.removecr 5</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveCurrencyReward(int level)
    {
        if (level <= 0)
        {
            await ReplyErrorAsync(Strings.XpLevelPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetCurrencyRewardAsync(ctx.Guild.Id, level, 0);
        await ReplyConfirmAsync(Strings.XpCurrencyRewardRemoved(ctx.Guild.Id, level)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all currency rewards.
    /// </summary>
    /// <example>.currencyrewards</example>
    [Cmd]
    [Aliases]
    public async Task CurrencyRewards()
    {
        var rewards = await Service.GetCurrencyRewardsAsync(ctx.Guild.Id);

        if (rewards.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpNoCurrencyRewards(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpCurrencyRewardsTitle(ctx.Guild.Id));

        var rewardLines = new List<string>();
        var currencyName = await currencyService.GetCurrencyEmote(ctx.Guild.Id);

        foreach (var reward in rewards.OrderBy(r => r.Level))
        {
            rewardLines.Add(Strings.XpCurrencyRewardLine(ctx.Guild.Id, reward.Level, reward.Amount, currencyName));
        }

        embed.WithDescription(string.Join("\n", rewardLines));
        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Creates a new XP competition.
    /// </summary>
    /// <param name="time">How long the competition should last.</param>
    /// <param name="type">The competition type: MostGained, ReachLevel, or HighestTotal.</param>
    /// <param name="targetLevel">For ReachLevel competitions, the target level.</param>
    /// <param name="name">The name of the competition.</param>
    /// <example>.xpcompetition 1d MostGained Weekly XP Race</example>
    /// <example>.xpcompetition 3d ReachLevel 20 First to Level 20</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpCompetition(StoopidTime time, XpCompetitionType type, int targetLevel = 0,
        [Remainder] string name = null)
    {
        if (time.Time.TotalMinutes < 60)
        {
            await ReplyErrorAsync(Strings.XpCompetitionTooShort(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            await ReplyErrorAsync(Strings.XpCompetitionNeedsName(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (type == XpCompetitionType.ReachLevel && targetLevel <= 0)
        {
            await ReplyErrorAsync(Strings.XpCompetitionNeedsLevel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var startTime = DateTime.UtcNow;
        var endTime = startTime + time.Time;

        var competition = await Service.CreateCompetitionAsync(
            ctx.Guild.Id,
            name,
            type,
            startTime,
            endTime,
            targetLevel,
            ctx.Channel.Id // Use current channel for announcements
        );

        // Start the competition
        await Service.StartCompetitionAsync(competition.Id);

        await ReplyConfirmAsync(Strings.XpCompetitionCreated(
            ctx.Guild.Id,
            competition.Name,
            competition.Type.ToString(),
            time.Time.Humanize(),
            competition.Type == (int)XpCompetitionType.ReachLevel ? competition.TargetLevel.ToString() : ""
        )).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a reward for a competition placement.
    /// </summary>
    /// <param name="competitionId">The ID of the competition.</param>
    /// <param name="position">The position to reward (1 for first place, etc.).</param>
    /// <param name="type">The type of reward: Role, XP, or Currency.</param>
    /// <param name="amount">For XP or Currency rewards, the amount to award.</param>
    /// <param name="role">For Role rewards, the role to award.</param>
    /// <example>.addcompetitionreward 1 1 Role @Winner</example>
    /// <example>.addcompetitionreward 1 2 XP 1000</example>
    /// <example>.addcompetitionreward 1 3 Currency 500</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AddCompetitionReward(int competitionId, int position, string type, [Remainder] string reward)
    {
        if (position <= 0)
        {
            await ReplyErrorAsync(Strings.XpCompetitionPositionInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        type = type.ToLower();

        if (type != "role" && type != "xp" && type != "currency")
        {
            await ReplyErrorAsync(Strings.XpCompetitionRewardInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        switch (type)
        {
            case "role":
                var roleMatch = await ctx.Guild.GetRoleAsync(MentionUtils.ParseRole(reward));
                if (roleMatch == null)
                {
                    await ReplyErrorAsync(Strings.XpCompetitionRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await Service.AddCompetitionRewardAsync(competitionId, position, roleMatch.Id);
                await ReplyConfirmAsync(Strings.XpCompetitionRoleRewardAdded(
                    ctx.Guild.Id,
                    position,
                    roleMatch.Mention,
                    competitionId
                )).ConfigureAwait(false);
                break;

            case "xp":
                if (!int.TryParse(reward, out var xpAmount) || xpAmount <= 0)
                {
                    await ReplyErrorAsync(Strings.XpCompetitionAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await Service.AddCompetitionRewardAsync(competitionId, position, 0, xpAmount);
                await ReplyConfirmAsync(Strings.XpCompetitionXpRewardAdded(
                    ctx.Guild.Id,
                    position,
                    xpAmount,
                    competitionId
                )).ConfigureAwait(false);
                break;

            case "currency":
                if (!long.TryParse(reward, out var currencyAmount) || currencyAmount <= 0)
                {
                    await ReplyErrorAsync(Strings.XpCompetitionAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await Service.AddCompetitionRewardAsync(competitionId, position, 0, 0, currencyAmount);
                await ReplyConfirmAsync(Strings.XpCompetitionCurrencyRewardAdded(
                    ctx.Guild.Id,
                    position,
                    currencyAmount,
                    await currencyService.GetCurrencyEmote(ctx.Guild.Id),
                    competitionId
                )).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Lists active XP competitions.
    /// </summary>
    /// <example>.xpcompetitions</example>
    [Cmd]
    [Aliases]
    public async Task XpCompetitions()
    {
        var competitions = await Service.GetActiveCompetitionsAsync(ctx.Guild.Id);

        if (competitions.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpNoActiveCompetitions(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpActiveCompetitionsTitle(ctx.Guild.Id));

        foreach (var comp in competitions)
        {
            var timeLeft = comp.EndTime - DateTime.UtcNow;
            var description = Strings.XpCompetitionInfo(
                ctx.Guild.Id,
                comp.Type.ToString(),
                timeLeft.Humanize(),
                comp.Type == (int)XpCompetitionType.ReachLevel ? comp.TargetLevel.ToString() : ""
            );

            embed.AddField(comp.Name, description);
        }

        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Shows the leaderboard for an active competition.
    /// </summary>
    /// <param name="competitionId">ID of the competition.</param>
    /// <example>.competitionlb 1</example>
    [Cmd]
    [Aliases]
    public async Task CompetitionLeaderboard(int competitionId)
    {
        var competition = await Service.GetCompetitionAsync(competitionId);

        if (competition == null || competition.GuildId != ctx.Guild.Id)
        {
            await ReplyErrorAsync(Strings.XpCompetitionNotFound(ctx.Guild.Id, competitionId)).ConfigureAwait(false);
            return;
        }

        var entries = await Service.GetCompetitionEntriesAsync(competitionId);

        if (entries.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpCompetitionNoEntries(ctx.Guild.Id, competition.Name)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpCompetitionLeaderboardTitle(ctx.Guild.Id, competition.Name));

        // Sort entries based on competition type
        List<(string Username, string Value, int Position)> leaderboard = new();

        switch ((XpCompetitionType)competition.Type)
        {
            case XpCompetitionType.MostGained:
                var gainedSorted = entries.OrderByDescending(e => e.CurrentXp - e.StartingXp).ToList();
                for (var i = 0; i < Math.Min(10, gainedSorted.Count); i++)
                {
                    var entry = gainedSorted[i];
                    var user = await ctx.Guild.GetUserAsync(entry.UserId);
                    var username = user?.ToString() ?? entry.UserId.ToString();
                    var gained = entry.CurrentXp - entry.StartingXp;
                    leaderboard.Add((username, gained.ToString("N0"), i + 1));
                }

                break;

            case XpCompetitionType.ReachLevel:
                var levelSorted = entries
                    .Where(e => e.AchievedTargetAt != null)
                    .OrderBy(e => e.AchievedTargetAt)
                    .ToList();

                for (var i = 0; i < Math.Min(10, levelSorted.Count); i++)
                {
                    var entry = levelSorted[i];
                    var user = await ctx.Guild.GetUserAsync(entry.UserId);
                    var username = user?.ToString() ?? entry.UserId.ToString();
                    var achievedAt = (entry.AchievedTargetAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm:ss");
                    leaderboard.Add((username, achievedAt, i + 1));
                }

                break;

            case XpCompetitionType.HighestTotal:
                var totalSorted = entries.OrderByDescending(e => e.CurrentXp).ToList();
                for (var i = 0; i < Math.Min(10, totalSorted.Count); i++)
                {
                    var entry = totalSorted[i];
                    var user = await ctx.Guild.GetUserAsync(entry.UserId);
                    var username = user?.ToString() ?? entry.UserId.ToString();
                    leaderboard.Add((username, entry.CurrentXp.ToString("N0"), i + 1));
                }

                break;
        }

        // Build description from leaderboard entries
        var description = new List<string>();
        foreach (var entry in leaderboard)
        {
            description.Add(Strings.XpCompetitionLeaderboardLine(
                ctx.Guild.Id,
                entry.Position,
                entry.Username,
                entry.Value
            ));
        }

        if (description.Count == 0)
        {
            description.Add(Strings.XpCompetitionNoQualifiedEntries(ctx.Guild.Id));
        }

        embed.WithDescription(string.Join("\n", description));

        // Add time information
        var timeLeft = competition.EndTime - DateTime.UtcNow;
        if (timeLeft.TotalSeconds > 0)
        {
            embed.WithFooter(Strings.XpCompetitionTimeLeft(ctx.Guild.Id, timeLeft.Humanize()));
        }
        else
        {
            embed.WithFooter(Strings.XpCompetitionEnded(ctx.Guild.Id));
        }

        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Ends a competition early and distributes rewards.
    /// </summary>
    /// <param name="competitionId">ID of the competition to end.</param>
    /// <example>.endcompetition 1</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task EndCompetition(int competitionId)
    {
        var competition = await Service.GetCompetitionAsync(competitionId);

        if (competition == null || competition.GuildId != ctx.Guild.Id)
        {
            await ReplyErrorAsync(Strings.XpCompetitionNotFound(ctx.Guild.Id, competitionId)).ConfigureAwait(false);
            return;
        }

        if (competition.EndTime < DateTime.UtcNow)
        {
            await ReplyErrorAsync(Strings.XpCompetitionAlreadyEnded(ctx.Guild.Id, competition.Name))
                .ConfigureAwait(false);
            return;
        }

        await Service.FinalizeCompetitionAsync(competitionId);
        await ReplyConfirmAsync(Strings.XpCompetitionManuallyEnded(ctx.Guild.Id, competition.Name))
            .ConfigureAwait(false);
    }
}