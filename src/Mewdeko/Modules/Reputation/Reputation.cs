using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Reputation.Common;
using Mewdeko.Modules.Reputation.Services;

namespace Mewdeko.Modules.Reputation;

/// <summary>
///     Module for managing user reputation system including giving, checking, and viewing reputation.
/// </summary>
public partial class Reputation : MewdekoModuleBase<RepService>
{
    /// <summary>
    ///     Gives reputation to a specified user.
    /// </summary>
    /// <param name="target">The user to give reputation to.</param>
    /// <param name="repType">The type of reputation to give.</param>
    /// <param name="reason">Optional reason or comment for giving reputation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task Rep(IGuildUser? target = null, string repType = "standard", [Remainder] string? reason = null)
    {
        if (target == null)
        {
            await ReplyErrorAsync(Strings.RepNoTarget(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        // Validate reputation type
        if (!await Service.IsValidReputationTypeAsync(ctx.Guild.Id, repType))
        {
            await ReplyErrorAsync(Strings.RepInvalidType(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await GiveRep(target, repType, reason).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gives reputation to a specified user.
    /// </summary>
    /// <param name="target">The user to give reputation to.</param>
    /// <param name="repType">The type of reputation to give.</param>
    /// <param name="reason">Optional reason or comment for giving reputation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task RepGive(IGuildUser target, string repType = "standard", [Remainder] string? reason = null)
    {
        await GiveRep(target, repType, reason).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gives reputation to a specified user.
    /// </summary>
    /// <param name="target">The user to give reputation to.</param>
    /// <param name="repType">The type of reputation to give.</param>
    /// <param name="reason">Optional reason or comment for giving reputation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task GiveRep(IGuildUser target, string repType = "standard", [Remainder] string? reason = null)
    {
        await GiveRepInternal(target, repType, reason).ConfigureAwait(false);
    }

    private async Task GiveRepInternal(IGuildUser target, string repType = "standard", string? reason = null,
        bool anonymous = false)
    {
        if (target.Id == ctx.User.Id)
        {
            await ReplyErrorAsync(Strings.RepSelf(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (target.IsBot)
        {
            await ReplyErrorAsync(Strings.RepBot(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var result = await Service.GiveReputationAsync(ctx.Guild.Id, ctx.User.Id, target.Id, ctx.Channel.Id, repType,
            reason, null, anonymous);

        switch (result.Result)
        {
            case GiveRepResultType.Success:
                await SuccessAsync(Strings.RepGiven(ctx.Guild.Id, result.Amount, target.Mention, result.NewTotal))
                    .ConfigureAwait(false);
                break;
            case GiveRepResultType.Cooldown:
                var remaining = result.CooldownRemaining?.ToString(@"hh\:mm\:ss") ?? "unknown";
                await ReplyErrorAsync(Strings.RepCooldown(ctx.Guild.Id, remaining)).ConfigureAwait(false);
                break;
            case GiveRepResultType.DailyLimit:
                await ReplyErrorAsync(Strings.RepDailyLimit(ctx.Guild.Id, result.DailyLimit)).ConfigureAwait(false);
                break;
            case GiveRepResultType.WeeklyLimit:
                await ReplyErrorAsync(Strings.RepWeeklyLimit(ctx.Guild.Id, result.WeeklyLimit)).ConfigureAwait(false);
                break;
            case GiveRepResultType.ChannelDisabled:
                await ReplyErrorAsync(Strings.RepChannelDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                break;
            case GiveRepResultType.UserFrozen:
                await ReplyErrorAsync(Strings.RepUserFrozen(ctx.Guild.Id)).ConfigureAwait(false);
                break;
            case GiveRepResultType.MinimumAccountAge:
                await ReplyErrorAsync(Strings.RepMinAccountAge(ctx.Guild.Id, result.RequiredDays))
                    .ConfigureAwait(false);
                break;
            case GiveRepResultType.MinimumServerMembership:
                await ReplyErrorAsync(Strings.RepMinMembership(ctx.Guild.Id, result.RequiredHours))
                    .ConfigureAwait(false);
                break;
            case GiveRepResultType.MinimumMessages:
                await ReplyErrorAsync(Strings.RepMinMessages(ctx.Guild.Id, result.RequiredMessages))
                    .ConfigureAwait(false);
                break;
            case GiveRepResultType.Disabled:
                await ReplyErrorAsync(Strings.RepDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Checks the reputation of a user.
    /// </summary>
    /// <param name="target">The user to check reputation for. If null, checks the command invoker's reputation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task RepCheck([Remainder] IGuildUser? target = null)
    {
        target ??= (IGuildUser)ctx.User;

        var (total, rank) = await Service.GetUserReputationAsync(ctx.Guild.Id, target.Id);

        if (total == 0)
        {
            await ReplyConfirmAsync(Strings.RepCheckNone(ctx.Guild.Id, target.DisplayName)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.RepCheck(ctx.Guild.Id, target.DisplayName, total, rank)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Checks the current user's reputation.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task MyRep()
    {
        await RepCheck().ConfigureAwait(false);
    }

    /// <summary>
    ///     Checks the reputation of a user.
    /// </summary>
    /// <param name="target">The user to check reputation for.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task CheckRep([Remainder] IGuildUser? target = null)
    {
        await RepCheck(target).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the reputation leaderboard for the server.
    /// </summary>
    /// <param name="page">The page number to display.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task RepLeaderboard(int page = 1)
    {
        if (page < 1)
            page = 1;

        var leaderboard = await Service.GetLeaderboardAsync(ctx.Guild.Id, page);

        if (!leaderboard.Any())
        {
            await ReplyErrorAsync(Strings.RepLeaderboardEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.RepLeaderboardTitle(ctx.Guild.Id));

        var description = string.Empty;
        var i = (page - 1) * 25 + 1;

        foreach (var (userId, rep) in leaderboard)
        {
            var user = await ctx.Guild.GetUserAsync(userId) ?? await ctx.Client.GetUserAsync(userId);
            var username = user?.ToString() ?? $"Unknown User ({userId})";
            description += $"`{i++}.` **{username}** - {rep} rep\n";
        }

        eb.WithDescription(description);
        eb.WithFooter(Strings.PageNum(ctx.Guild.Id, page));

        await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the reputation leaderboard for the server.
    /// </summary>
    /// <param name="page">The page number to display.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task RepLb(int page = 1)
    {
        await RepLeaderboard(page).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the reputation leaderboard for the server.
    /// </summary>
    /// <param name="page">The page number to display.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task RepTop(int page = 1)
    {
        await RepLeaderboard(page).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the reputation leaderboard for the server.
    /// </summary>
    /// <param name="page">The page number to display.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task TopRep(int page = 1)
    {
        await RepLeaderboard(page).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the reputation history for a user.
    /// </summary>
    /// <param name="target">The user to show history for. If null, shows the command invoker's history.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task RepHistory([Remainder] IGuildUser? target = null)
    {
        target ??= (IGuildUser)ctx.User;

        var history = await Service.GetReputationHistoryAsync(ctx.Guild.Id, target.Id);

        if (!history.Any())
        {
            await ReplyConfirmAsync(Strings.RepHistoryEmpty(ctx.Guild.Id, target.DisplayName)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.RepHistoryTitle(ctx.Guild.Id, target.DisplayName));

        var description = string.Empty;

        foreach (var entry in history.Take(10))
        {
            var giver = await ctx.Guild.GetUserAsync(entry.GiverId) ?? await ctx.Client.GetUserAsync(entry.GiverId);
            var giverName = giver?.ToString() ?? "Unknown User";

            var sign = entry.Amount > 0 ? "+" : "";
            description += $"{entry.Timestamp:MM/dd HH:mm} - **{giverName}** {sign}{entry.Amount}\n";

            if (!string.IsNullOrEmpty(entry.Reason))
                description += $"└ {entry.Reason}\n";
        }

        eb.WithDescription(description);
        eb.WithFooter(Strings.RepHistoryFooter(ctx.Guild.Id, history.Count));

        await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows detailed reputation statistics for a user.
    /// </summary>
    /// <param name="target">The user to show stats for. If null, shows the command invoker's stats.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task RepStats([Remainder] IGuildUser? target = null)
    {
        target ??= (IGuildUser)ctx.User;

        var stats = await Service.GetUserStatsAsync(ctx.Guild.Id, target.Id);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.RepStatsTitle(ctx.Guild.Id, target.ToString()))
            .AddField(Strings.RepTotal(ctx.Guild.Id), stats.TotalRep, true)
            .AddField(Strings.RepRank(ctx.Guild.Id), $"#{stats.Rank}", true)
            .AddField(Strings.RepGivenTotal(ctx.Guild.Id), stats.TotalGiven, true)
            .AddField(Strings.RepReceivedTotal(ctx.Guild.Id), stats.TotalReceived, true)
            .AddField(Strings.RepStreakCurrent(ctx.Guild.Id), stats.CurrentStreak, true)
            .AddField(Strings.RepStreakLongest(ctx.Guild.Id), stats.LongestStreak, true);

        if (stats.LastGivenAt.HasValue)
            eb.AddField(Strings.RepLastGiven(ctx.Guild.Id),
                $"{stats.LastGivenAt.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC", true);

        if (stats.LastReceivedAt.HasValue)
            eb.AddField(Strings.RepLastReceived(ctx.Guild.Id),
                $"{stats.LastReceivedAt.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC", true);

        await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gives anonymous reputation to a specified user.
    /// </summary>
    /// <param name="target">The user to give reputation to.</param>
    /// <param name="repType">The type of reputation to give.</param>
    /// <param name="reason">Optional reason or comment for giving reputation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task AnonRep(IGuildUser target, string repType = "standard", [Remainder] string? reason = null)
    {
        await GiveRepInternal(target, repType, reason, true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gives anonymous reputation to a specified user.
    /// </summary>
    /// <param name="target">The user to give reputation to.</param>
    /// <param name="repType">The type of reputation to give.</param>
    /// <param name="reason">Optional reason or comment for giving reputation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task RepAnon(IGuildUser target, string repType = "standard", [Remainder] string? reason = null)
    {
        await GiveRepInternal(target, repType, reason, true).ConfigureAwait(false);
    }
}