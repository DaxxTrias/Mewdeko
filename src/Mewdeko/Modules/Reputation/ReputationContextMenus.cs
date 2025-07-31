// using Discord.Interactions;
// using Mewdeko.Common.Attributes.InteractionCommands;
// using Mewdeko.Modules.Reputation.Common;
// using Mewdeko.Modules.Reputation.Services;
//
// namespace Mewdeko.Modules.Reputation;
//
// /// <summary>
// /// Context menu commands for the reputation system.
// /// </summary>
// public class ReputationContextMenus : MewdekoSlashModuleBase<RepService>
// {
//     /// <summary>
//     /// Gives reputation to a user via user context menu (right-click user).
//     /// </summary>
//     /// <param name="user">The user to give reputation to.</param>
//     /// <returns>A task that represents the asynchronous operation.</returns>
//     [UserCommand("Give Reputation")]
//     [CheckPermissions]
//     public async Task GiveReputationContextMenu(IGuildUser user)
//     {
//         if (user.Id == ctx.User.Id)
//         {
//             await RespondAsync(embed: new EmbedBuilder()
//                 .WithErrorColor()
//                 .WithDescription(Strings.RepSelf(ctx.Guild.Id))
//                 .Build(), ephemeral: true).ConfigureAwait(false);
//             return;
//         }
//
//         if (user.IsBot)
//         {
//             await RespondAsync(embed: new EmbedBuilder()
//                 .WithErrorColor()
//                 .WithDescription(Strings.RepBot(ctx.Guild.Id))
//                 .Build(), ephemeral: true).ConfigureAwait(false);
//             return;
//         }
//
//         var result = await Service.GiveReputationAsync(ctx.Guild.Id, ctx.User.Id, user.Id, ctx.Channel.Id);
//
//         var eb = new EmbedBuilder();
//
//         switch (result.Result)
//         {
//             case GiveRepResultType.Success:
//                 eb.WithOkColor()
//                     .WithDescription($"{Config.SuccessEmote} {Strings.RepGiven(ctx.Guild.Id, result.Amount, user.Mention, result.NewTotal)}");
//                 break;
//             case GiveRepResultType.Cooldown:
//                 var remaining = result.CooldownRemaining?.ToString(@"hh\:mm\:ss") ?? "unknown";
//                 eb.WithErrorColor()
//                     .WithDescription(Strings.RepCooldown(ctx.Guild.Id, remaining));
//                 break;
//             case GiveRepResultType.DailyLimit:
//                 eb.WithErrorColor()
//                     .WithDescription(Strings.RepDailyLimit(ctx.Guild.Id, result.DailyLimit));
//                 break;
//             case GiveRepResultType.WeeklyLimit:
//                 eb.WithErrorColor()
//                     .WithDescription(Strings.RepWeeklyLimit(ctx.Guild.Id, result.DailyLimit));
//                 break;
//             case GiveRepResultType.ChannelDisabled:
//                 eb.WithErrorColor()
//                     .WithDescription(Strings.RepChannelDisabled(ctx.Guild.Id));
//                 break;
//             case GiveRepResultType.UserFrozen:
//                 eb.WithErrorColor()
//                     .WithDescription(Strings.RepUserFrozen(ctx.Guild.Id));
//                 break;
//             case GiveRepResultType.MinimumAccountAge:
//                 eb.WithErrorColor()
//                     .WithDescription(Strings.RepMinAccountAge(ctx.Guild.Id, result.RequiredDays));
//                 break;
//             case GiveRepResultType.MinimumServerMembership:
//                 eb.WithErrorColor()
//                     .WithDescription(Strings.RepMinMembership(ctx.Guild.Id, result.RequiredHours));
//                 break;
//             case GiveRepResultType.MinimumMessages:
//                 eb.WithErrorColor()
//                     .WithDescription(Strings.RepMinMessages(ctx.Guild.Id, result.RequiredDays));
//                 break;
//             case GiveRepResultType.Disabled:
//                 eb.WithErrorColor()
//                     .WithDescription(Strings.RepDisabled(ctx.Guild.Id));
//                 break;
//         }
//
//         await RespondAsync(embed: eb.Build()).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     /// Checks a user's reputation via user context menu (right-click user).
//     /// </summary>
//     /// <param name="user">The user to check reputation for.</param>
//     /// <returns>A task that represents the asynchronous operation.</returns>
//     [UserCommand("Check Reputation")]
//     [CheckPermissions]
//     public async Task CheckReputationContextMenu(IGuildUser user)
//     {
//         var (total, rank) = await Service.GetUserReputationAsync(ctx.Guild.Id, user.Id);
//
//         var eb = new EmbedBuilder().WithOkColor();
//
//         eb.WithDescription(total == 0
//             ? Strings.RepCheckNone(ctx.Guild.Id, user.DisplayName)
//             : Strings.RepCheck(ctx.Guild.Id, user.DisplayName, total, rank));
//
//         await RespondAsync(embed: eb.Build()).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     /// Shows detailed reputation statistics for a user via context menu.
//     /// </summary>
//     /// <param name="user">The user to show stats for.</param>
//     /// <returns>A task that represents the asynchronous operation.</returns>
//     [UserCommand("Reputation Stats")]
//     [CheckPermissions]
//     public async Task ReputationStatsContextMenu(IGuildUser user)
//     {
//         var stats = await Service.GetUserStatsAsync(ctx.Guild.Id, user.Id);
//
//         var eb = new EmbedBuilder()
//             .WithOkColor()
//             .WithTitle(Strings.RepStatsTitle(ctx.Guild.Id, user.ToString()))
//             .AddField(Strings.RepTotal(ctx.Guild.Id), stats.TotalRep, true)
//             .AddField(Strings.RepRank(ctx.Guild.Id), $"#{stats.Rank}", true)
//             .AddField(Strings.RepGivenTotal(ctx.Guild.Id), stats.TotalGiven, true)
//             .AddField(Strings.RepReceivedTotal(ctx.Guild.Id), stats.TotalReceived, true)
//             .AddField(Strings.RepStreakCurrent(ctx.Guild.Id), stats.CurrentStreak, true)
//             .AddField(Strings.RepStreakLongest(ctx.Guild.Id), stats.LongestStreak, true);
//
//         if (stats.LastGivenAt.HasValue)
//             eb.AddField(Strings.RepLastGiven(ctx.Guild.Id), $"{stats.LastGivenAt.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC", true);
//
//         if (stats.LastReceivedAt.HasValue)
//             eb.AddField(Strings.RepLastReceived(ctx.Guild.Id), $"{stats.LastReceivedAt.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC", true);
//
//         await RespondAsync(embed: eb.Build()).ConfigureAwait(false);
//     }
// }

