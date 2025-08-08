using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Reputation.Common;
using Mewdeko.Modules.Reputation.Services;

namespace Mewdeko.Modules.Reputation;

/// <summary>
///     Slash command module for managing user reputation system.
/// </summary>
[Group("rep", "Reputation system commands")]
public class SlashReputation : MewdekoSlashModuleBase<RepService>
{
    private readonly RepConfigService configService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SlashReputation" /> class.
    /// </summary>
    /// <param name="configService">The reputation configuration service.</param>
    public SlashReputation(RepConfigService configService)
    {
        this.configService = configService;
    }

    /// <summary>
    ///     Gives reputation to a specified user.
    /// </summary>
    /// <param name="user">The user to give reputation to.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("give", "Give reputation to a user")]
    [CheckPermissions]
    public async Task RepGive(IGuildUser user)
    {
        if (user.Id == ctx.User.Id)
        {
            await ReplyAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.RepSelf(ctx.Guild.Id))
                .Build()).ConfigureAwait(false);
            return;
        }

        if (user.IsBot)
        {
            await RespondAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.RepBot(ctx.Guild.Id))
                .Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        var result = await Service.GiveReputationAsync(ctx.Guild.Id, ctx.User.Id, user.Id, ctx.Channel.Id);

        var eb = new EmbedBuilder();

        switch (result.Result)
        {
            case GiveRepResultType.Success:
                eb.WithOkColor()
                    .WithDescription(
                        $"{Config.SuccessEmote} {Strings.RepGiven(ctx.Guild.Id, result.Amount, user.Mention, result.NewTotal)}");
                break;
            case GiveRepResultType.Cooldown:
                var remaining = result.CooldownRemaining?.ToString(@"hh\:mm\:ss") ?? "unknown";
                eb.WithErrorColor()
                    .WithDescription(Strings.RepCooldown(ctx.Guild.Id, remaining));
                break;
            case GiveRepResultType.DailyLimit:
                eb.WithErrorColor()
                    .WithDescription(Strings.RepDailyLimit(ctx.Guild.Id, result.DailyLimit));
                break;
            case GiveRepResultType.WeeklyLimit:
                eb.WithErrorColor()
                    .WithDescription(Strings.RepWeeklyLimit(ctx.Guild.Id, result.DailyLimit));
                break;
            case GiveRepResultType.ChannelDisabled:
                eb.WithErrorColor()
                    .WithDescription(Strings.RepChannelDisabled(ctx.Guild.Id));
                break;
            case GiveRepResultType.UserFrozen:
                eb.WithErrorColor()
                    .WithDescription(Strings.RepUserFrozen(ctx.Guild.Id));
                break;
            case GiveRepResultType.MinimumAccountAge:
                eb.WithErrorColor()
                    .WithDescription(Strings.RepMinAccountAge(ctx.Guild.Id, result.RequiredDays));
                break;
            case GiveRepResultType.MinimumServerMembership:
                eb.WithErrorColor()
                    .WithDescription(Strings.RepMinMembership(ctx.Guild.Id, result.RequiredHours));
                break;
            case GiveRepResultType.MinimumMessages:
                eb.WithErrorColor()
                    .WithDescription(Strings.RepMinMessages(ctx.Guild.Id, result.RequiredDays));
                break;
            case GiveRepResultType.Disabled:
                eb.WithErrorColor()
                    .WithDescription(Strings.RepDisabled(ctx.Guild.Id));
                break;
        }

        await ReplyAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Checks the reputation of a user.
    /// </summary>
    /// <param name="user">The user to check reputation for. If null, checks your own reputation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("check", "Check a user's reputation")]
    [CheckPermissions]
    public async Task RepCheck(IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;

        var (total, rank) = await Service.GetUserReputationAsync(ctx.Guild.Id, user.Id);

        var eb = new EmbedBuilder().WithOkColor();

        eb.WithDescription(total == 0
            ? Strings.RepCheckNone(ctx.Guild.Id, user.DisplayName)
            : Strings.RepCheck(ctx.Guild.Id, user.DisplayName, total, rank));

        await ReplyAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the reputation leaderboard for the server.
    /// </summary>
    /// <param name="page">The page number to display.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("leaderboard", "View the reputation leaderboard")]
    [CheckPermissions]
    public async Task RepLeaderboard(int page = 1)
    {
        if (page < 1)
            page = 1;

        var leaderboard = await Service.GetLeaderboardAsync(ctx.Guild.Id, page);

        if (!leaderboard.Any())
        {
            await ReplyAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.RepLeaderboardEmpty(ctx.Guild.Id))
                .Build()).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.RepLeaderboardTitle(ctx.Guild.Id));

        var description = string.Empty;
        var i = (page - 1) * 25 + 1;

        foreach (var (userId, rep) in leaderboard)
        {
            var guildUser = await ctx.Guild.GetUserAsync(userId) ?? await ctx.Client.GetUserAsync(userId);
            var username = guildUser?.ToString() ?? $"Unknown User ({userId})";
            description += $"`{i++}.` **{username}** - {rep} rep\n";
        }

        eb.WithDescription(description);
        eb.WithFooter(Strings.PageNum(ctx.Guild.Id, page));

        await ReplyAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the reputation history for a user.
    /// </summary>
    /// <param name="user">The user to show history for. If null, shows your own history.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("history", "View reputation history for a user")]
    [CheckPermissions]
    public async Task RepHistory(IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;

        var history = await Service.GetReputationHistoryAsync(ctx.Guild.Id, user.Id);

        if (!history.Any())
        {
            await ReplyAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.RepHistoryEmpty(ctx.Guild.Id, user.DisplayName))
                .Build()).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.RepHistoryTitle(ctx.Guild.Id, user.DisplayName));

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

        await ReplyAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows detailed reputation statistics for a user.
    /// </summary>
    /// <param name="user">The user to show stats for. If null, shows your own stats.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("stats", "View detailed reputation statistics")]
    [CheckPermissions]
    public async Task RepStats(IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;

        var stats = await Service.GetUserStatsAsync(ctx.Guild.Id, user.Id);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.RepStatsTitle(ctx.Guild.Id, user.ToString()))
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

        await ReplyAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    #region Component Interactions

    /// <summary>
    ///     Handles reputation configuration category selection.
    /// </summary>
    /// <param name="category">The selected configuration category.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_config_category", true)]
    public async Task RepConfigCategory(string category)
    {
        await configService.HandleCategorySelectionAsync(ctx, category);
    }

    /// <summary>
    ///     Handles saving reputation configuration changes.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_config_save", true)]
    public async Task RepConfigSave()
    {
        await configService.HandleSaveAsync(ctx);
    }

    /// <summary>
    ///     Handles resetting reputation configuration to defaults.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_config_reset", true)]
    public async Task RepConfigReset()
    {
        await configService.HandleResetAsync(ctx);
    }

    /// <summary>
    ///     Handles exporting reputation configuration.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_config_export", true)]
    public async Task RepConfigExport()
    {
        await configService.HandleExportAsync(ctx);
    }

    /// <summary>
    ///     Handles closing the reputation configuration menu.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_config_close", true)]
    public async Task RepConfigClose()
    {
        await ((SocketMessageComponent)ctx.Interaction).Message.DeleteAsync();
    }

    /// <summary>
    ///     Handles toggling the reputation system enabled/disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_toggle_enabled", true)]
    public async Task RepToggleEnabled()
    {
        await configService.HandleToggleAsync(ctx, "enabled");
    }

    /// <summary>
    ///     Handles toggling anonymous reputation.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_toggle_anonymous", true)]
    public async Task RepToggleAnonymous()
    {
        await configService.HandleToggleAsync(ctx, "anonymous");
    }

    /// <summary>
    ///     Handles toggling negative reputation.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_toggle_negative", true)]
    public async Task RepToggleNegative()
    {
        await configService.HandleToggleAsync(ctx, "negative");
    }

    /// <summary>
    ///     Handles toggling reputation decay.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_toggle_decay", true)]
    public async Task RepToggleDecay()
    {
        await configService.HandleToggleAsync(ctx, "decay");
    }

    /// <summary>
    ///     Handles editing cooldown settings.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_edit_cooldown", true)]
    public async Task RepEditCooldown()
    {
        await configService.HandleEditAsync(ctx, "cooldown");
    }

    /// <summary>
    ///     Handles editing daily limit settings.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_edit_daily_limit", true)]
    public async Task RepEditDailyLimit()
    {
        await configService.HandleEditAsync(ctx, "daily_limit");
    }

    /// <summary>
    ///     Handles editing weekly limit settings.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_edit_weekly_limit", true)]
    public async Task RepEditWeeklyLimit()
    {
        await configService.HandleEditAsync(ctx, "weekly_limit");
    }

    /// <summary>
    ///     Handles editing account age requirements.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_edit_account_age", true)]
    public async Task RepEditAccountAge()
    {
        await configService.HandleEditAsync(ctx, "account_age");
    }

    /// <summary>
    ///     Handles editing membership requirements.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_edit_membership", true)]
    public async Task RepEditMembership()
    {
        await configService.HandleEditAsync(ctx, "membership");
    }

    /// <summary>
    ///     Handles editing message requirements.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_edit_messages", true)]
    public async Task RepEditMessages()
    {
        await configService.HandleEditAsync(ctx, "messages");
    }

    /// <summary>
    ///     Handles editing decay amount.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_edit_decay_amount", true)]
    public async Task RepEditDecayAmount()
    {
        await configService.HandleEditAsync(ctx, "decay_amount");
    }

    /// <summary>
    ///     Handles editing inactive days.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_edit_inactive_days", true)]
    public async Task RepEditInactiveDays()
    {
        await configService.HandleEditAsync(ctx, "inactive_days");
    }

    /// <summary>
    ///     Handles notification channel selection.
    /// </summary>
    /// <param name="channels">The selected channels.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_select_notification_channel", true)]
    public async Task RepSelectNotificationChannel(IChannel[] channels)
    {
        var channel = channels.FirstOrDefault();
        await configService.HandleChannelSelectAsync(ctx, channel?.Id);
    }

    /// <summary>
    ///     Handles decay type selection.
    /// </summary>
    /// <param name="decayTypes">The selected decay types.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_select_decay_type", true)]
    public async Task RepSelectDecayType(string[] decayTypes)
    {
        var decayType = decayTypes.FirstOrDefault();
        await configService.HandleDecayTypeSelectAsync(ctx, decayType);
    }

    /// <summary>
    ///     Handles clearing the notification channel.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("rep_clear_notification_channel", true)]
    public async Task RepClearNotificationChannel()
    {
        await configService.HandleChannelSelectAsync(ctx, null);
    }

    /// <summary>
    ///     Handles modal submissions for editing configuration values.
    /// </summary>
    /// <param name="setting">The setting being edited.</param>
    /// <param name="modal">The modal data.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ModalInteraction("rep_modal_*", true)]
    public async Task RepModalSubmit(string setting, RepEditModal modal)
    {
        await configService.HandleModalSubmitAsync(ctx, setting, modal.Value);
    }

    #endregion
}