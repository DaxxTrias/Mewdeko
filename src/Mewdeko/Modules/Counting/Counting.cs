using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Counting.Common;
using Mewdeko.Modules.Counting.Services;

namespace Mewdeko.Modules.Counting;

/// <summary>
/// Module for managing counting channels and functionality.
/// </summary>
/// <param name="interactive">The interactive service for handling user interactions.</param>
/// <param name="countingService">The main counting service.</param>
/// <param name="statsService">The counting statistics service.</param>
/// <param name="moderationService">The counting moderation service.</param>
public partial class Counting(
    InteractiveService interactive,
    CountingService countingService,
    CountingStatsService statsService,
    CountingModerationService moderationService)
    : MewdekoModuleBase<CountingService>
{
    /// <summary>
    /// Sets up counting in the current channel or a specified channel.
    /// </summary>
    /// <param name="channel">The channel to set up counting in. Defaults to current channel.</param>
    /// <param name="startNumber">The number to start counting from. Defaults to 1.</param>
    /// <param name="increment">The increment for each count. Defaults to 1.</param>
    /// <example>.counting setup</example>
    /// <example>.counting setup #counting-channel 0 1</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountingSetup(ITextChannel? channel = null, long startNumber = 1, int increment = 1)
    {
        channel ??= (ITextChannel)ctx.Channel;

        if (increment <= 0)
        {
            await ErrorAsync(Strings.CountingInvalidIncrement(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var result = await countingService.SetupCountingChannelAsync(ctx.Guild.Id, channel.Id, startNumber, increment);

        if (!result.Success)
        {
            await ErrorAsync(Strings.CountingSetupFailed(ctx.Guild.Id, result.ErrorMessage ?? "Unknown error")).ConfigureAwait(false);
            return;
        }

        await ConfirmAsync(Strings.CountingSetupSuccess(ctx.Guild.Id, channel.Mention, startNumber, increment)).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows the current status of counting in a channel.
    /// </summary>
    /// <param name="channel">The channel to check. Defaults to current channel.</param>
    /// <example>.counting status</example>
    /// <example>.counting status #counting-channel</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task CountingStatus(ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var config = await countingService.GetCountingConfigAsync(channel.Id);
        var stats = await countingService.GetChannelStatsAsync(channel.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CountingStatusTitle(ctx.Guild.Id, channel.Name))
            .AddField(Strings.CountingCurrentNumber(ctx.Guild.Id), countingChannel.CurrentNumber.ToString("N0"), true)
            .AddField(Strings.CountingHighestNumber(ctx.Guild.Id), $"{countingChannel.HighestNumber:N0}", true)
            .AddField(Strings.CountingTotalCounts(ctx.Guild.Id), countingChannel.TotalCounts.ToString("N0"), true)
            .AddField(Strings.CountingParticipants(ctx.Guild.Id), stats?.TotalParticipants.ToString("N0") ?? "0", true)
            .AddField(Strings.CountingPattern(ctx.Guild.Id), ((CountingPattern)(config?.Pattern ?? 0)).ToString(), true)
            .AddField(Strings.CountingIncrement(ctx.Guild.Id), countingChannel.Increment.ToString(), true);

        if (countingChannel.LastUserId > 0)
        {
            embed.AddField(Strings.CountingLastUser(ctx.Guild.Id), $"<@{countingChannel.LastUserId}>", true);
        }

        if (config != null)
        {
            var configText = new List<string>();
            if (!config.AllowRepeatedUsers) configText.Add(Strings.CountingNoRepeats(ctx.Guild.Id));
            if (config.Cooldown > 0) configText.Add(Strings.CountingCooldown(ctx.Guild.Id, config.Cooldown));
            if (config.MaxNumber > 0) configText.Add(Strings.CountingMaxNumber(ctx.Guild.Id, config.MaxNumber.ToString("N0")));
            if (config.ResetOnError) configText.Add(Strings.CountingResetOnError(ctx.Guild.Id));
            if (config.DeleteWrongMessages) configText.Add(Strings.CountingDeleteWrongMessages(ctx.Guild.Id));

            if (configText.Any())
            {
                embed.AddField(Strings.CountingConfiguration(ctx.Guild.Id), string.Join("\n", configText), false);
            }
        }

        await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Configures settings for a counting channel.
    /// </summary>
    /// <param name="channel">The channel to configure.</param>
    /// <example>.counting config</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountingConfig(ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var config = await countingService.GetCountingConfigAsync(channel.Id);
        if (config == null)
        {
            await ErrorAsync(Strings.CountingConfigNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CountingConfigTitle(ctx.Guild.Id, channel.Name))
            .AddField(Strings.CountingAllowRepeats(ctx.Guild.Id), config.AllowRepeatedUsers ? "✅" : "❌", true)
            .AddField(Strings.CountingCooldownSeconds(ctx.Guild.Id), config.Cooldown.ToString(), true)
            .AddField(Strings.CountingMaxNumberLimit(ctx.Guild.Id), config.MaxNumber > 0 ? config.MaxNumber.ToString("N0") : Strings.CountingUnlimited(ctx.Guild.Id), true)
            .AddField(Strings.CountingResetOnErrorSetting(ctx.Guild.Id), config.ResetOnError ? "✅" : "❌", true)
            .AddField(Strings.CountingDeleteWrongSetting(ctx.Guild.Id), config.DeleteWrongMessages ? "✅" : "❌", true)
            .AddField(Strings.CountingPatternType(ctx.Guild.Id), ((CountingPattern)config.Pattern).ToString(), true)
            .AddField(Strings.CountingNumberBaseSetting(ctx.Guild.Id), config.NumberBase.ToString(), true)
            .AddField(Strings.CountingAchievementsSetting(ctx.Guild.Id), config.EnableAchievements ? "✅" : "❌", true)
            .AddField(Strings.CountingCompetitionsSetting(ctx.Guild.Id), config.EnableCompetitions ? "✅" : "❌", true);

        if (!string.IsNullOrEmpty(config.SuccessEmote))
        {
            embed.AddField(Strings.CountingSuccessEmote(ctx.Guild.Id), config.SuccessEmote, true);
        }

        if (!string.IsNullOrEmpty(config.ErrorEmote))
        {
            embed.AddField(Strings.CountingErrorEmote(ctx.Guild.Id), config.ErrorEmote, true);
        }

        await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the counting in a channel to a specific number.
    /// </summary>
    /// <param name="channel">The channel to reset.</param>
    /// <param name="newNumber">The number to reset to. Defaults to 0.</param>
    /// <param name="reason">The reason for the reset.</param>
    /// <example>.counting reset</example>
    /// <example>.counting reset #counting-channel 100 "Milestone celebration"</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task CountingReset(ITextChannel? channel = null, long newNumber = 0, [Remainder] string? reason = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var confirmed = await PromptUserConfirmAsync(
            Strings.CountingResetConfirm(ctx.Guild.Id, channel.Mention, countingChannel.CurrentNumber, newNumber),
            ctx.User.Id).ConfigureAwait(false);

        if (!confirmed)
        {
            await ErrorAsync(Strings.CountingResetCancelled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var success = await countingService.ResetCountingChannelAsync(channel.Id, newNumber, ctx.User.Id, reason);
        if (success)
        {
            await ConfirmAsync(Strings.CountingResetSuccess(ctx.Guild.Id, channel.Mention, newNumber)).ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingResetFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Shows counting statistics for a user or channel.
    /// </summary>
    /// <param name="user">The user to show stats for. Defaults to the command invoker.</param>
    /// <param name="channel">The channel to check stats in. Defaults to current channel.</param>
    /// <example>.counting stats</example>
    /// <example>.counting stats @user</example>
    /// <example>.counting stats @user #counting-channel</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task CountingStats(IGuildUser? user = null, ITextChannel? channel = null)
    {
        user ??= (IGuildUser)ctx.User;
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var userStats = await statsService.GetUserStatsAsync(channel.Id, user.Id);
        if (userStats == null)
        {
            await ErrorAsync(Strings.CountingNoUserStats(ctx.Guild.Id, user.DisplayName, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var rank = await statsService.GetUserRankAsync(channel.Id, user.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CountingUserStatsTitle(ctx.Guild.Id, user.DisplayName, channel.Name))
            .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
            .AddField(Strings.CountingRank(ctx.Guild.Id), rank?.ToString() ?? "N/A", true)
            .AddField(Strings.CountingContributions(ctx.Guild.Id), userStats.ContributionsCount.ToString("N0"), true)
            .AddField(Strings.CountingCurrentStreak(ctx.Guild.Id), userStats.CurrentStreak.ToString("N0"), true)
            .AddField(Strings.CountingHighestStreak(ctx.Guild.Id), userStats.HighestStreak.ToString("N0"), true)
            .AddField(Strings.CountingAccuracy(ctx.Guild.Id), $"{userStats.Accuracy:F1}%", true)
            .AddField(Strings.CountingErrors(ctx.Guild.Id), userStats.ErrorsCount.ToString("N0"), true)
            .AddField(Strings.CountingTotalNumbersCounted(ctx.Guild.Id), userStats.TotalNumbersCounted.ToString("N0"), true);

        if (userStats.LastContribution.HasValue)
        {
            embed.AddField(Strings.CountingLastContribution(ctx.Guild.Id),
                $"<t:{((DateTimeOffset)userStats.LastContribution.Value).ToUnixTimeSeconds()}:R>", true);
        }

        await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows the leaderboard for a counting channel.
    /// </summary>
    /// <param name="type">The type of leaderboard to show.</param>
    /// <param name="channel">The channel to show the leaderboard for.</param>
    /// <example>.counting leaderboard</example>
    /// <example>.counting leaderboard streak</example>
    /// <example>.counting leaderboard accuracy #counting-channel</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task CountingLeaderboard(string type = "contributions", ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        if (!Enum.TryParse<LeaderboardType>(type, true, out var leaderboardType))
        {
            await ErrorAsync(Strings.CountingInvalidLeaderboardType(ctx.Guild.Id,
                string.Join(", ", Enum.GetNames<LeaderboardType>()))).ConfigureAwait(false);
            return;
        }

        var leaderboard = await statsService.GetLeaderboardAsync(channel.Id, leaderboardType, 20);
        if (!leaderboard.Any())
        {
            await ErrorAsync(Strings.CountingNoLeaderboardData(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var pages = leaderboard
            .Chunk(10)
            .Select<CountingLeaderboardEntry[], PageBuilder>((chunk, pageIndex) =>
            {
                var description = new List<string>();
                foreach (var entry in chunk)
                {
                    var value = leaderboardType switch
                    {
                        LeaderboardType.Contributions => entry.ContributionsCount.ToString("N0"),
                        LeaderboardType.Streak => entry.HighestStreak.ToString("N0"),
                        LeaderboardType.Accuracy => $"{entry.Accuracy:F1}%",
                        LeaderboardType.TotalNumbers => entry.TotalNumbersCounted.ToString("N0"),
                        _ => entry.ContributionsCount.ToString("N0")
                    };

                    var rankEmoji = entry.Rank switch
                    {
                        1 => "🥇",
                        2 => "🥈",
                        3 => "🥉",
                        _ => $"`{entry.Rank}.`"
                    };

                    description.Add($"{rankEmoji} <@{entry.UserId}> - **{value}**");
                }

                return new PageBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.CountingLeaderboardTitle(ctx.Guild.Id, channel.Name, leaderboardType.ToString()))
                    .WithDescription(string.Join("\n", description))
                    .WithFooter(Strings.CountingLeaderboardPage(ctx.Guild.Id, pageIndex + 1, (leaderboard.Count + 9) / 10));
            })
            .ToList();

        var paginator = new StaticPaginatorBuilder()
            .WithUsers(ctx.User)
            .WithPages(pages)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, ctx.Channel).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a save point for the current counting progress.
    /// </summary>
    /// <param name="channel">The channel to save.</param>
    /// <param name="reason">The reason for creating the save point.</param>
    /// <example>.counting save "Before reset for event"</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task CountingSave(ITextChannel? channel = null, [Remainder] string? reason = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var saveId = await countingService.CreateSavePointAsync(channel.Id, ctx.User.Id, reason);
        if (saveId > 0)
        {
            await ConfirmAsync(Strings.CountingSaveSuccess(ctx.Guild.Id, channel.Mention, countingChannel.CurrentNumber, saveId)).ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingSaveFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Restores counting from a previous save point.
    /// </summary>
    /// <param name="saveId">The ID of the save point to restore from.</param>
    /// <param name="channel">The channel to restore.</param>
    /// <example>.counting restore 1</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task CountingRestore(int saveId, ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var confirmed = await PromptUserConfirmAsync(
            Strings.CountingRestoreConfirm(ctx.Guild.Id, channel.Mention, saveId, countingChannel.CurrentNumber),
            ctx.User.Id).ConfigureAwait(false);

        if (!confirmed)
        {
            await ErrorAsync(Strings.CountingRestoreCancelled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var success = await countingService.RestoreFromSaveAsync(channel.Id, saveId, ctx.User.Id);
        if (success)
        {
            await ConfirmAsync(Strings.CountingRestoreSuccess(ctx.Guild.Id, channel.Mention, saveId)).ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingRestoreFailed(ctx.Guild.Id, saveId)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Lists all counting channels in the server.
    /// </summary>
    /// <example>.counting list</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task CountingList()
    {
        var channels = await countingService.GetGuildCountingChannelsAsync(ctx.Guild.Id);
        if (!channels.Any())
        {
            await ErrorAsync(Strings.CountingNoChannelsInGuild(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CountingChannelsTitle(ctx.Guild.Id, ctx.Guild.Name))
            .WithDescription(Strings.CountingChannelsCount(ctx.Guild.Id, channels.Count));

        foreach (var channel in channels.Take(10))
        {
            var channelObj = await ctx.Guild.GetTextChannelAsync(channel.ChannelId);
            var channelName = channelObj?.Name ?? Strings.CountingDeletedChannel(ctx.Guild.Id);
            var channelMention = channelObj?.Mention ?? $"#{channelName}";

            embed.AddField($"{channelMention}",
                Strings.CountingChannelInfo(ctx.Guild.Id, channel.CurrentNumber.ToString("N0"), channel.TotalCounts.ToString("N0"), channel.HighestNumber.ToString("N0")),
                true);
        }

        if (channels.Count > 10)
        {
            embed.WithFooter(Strings.CountingMoreChannels(ctx.Guild.Id, channels.Count - 10));
        }

        await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
    }
}