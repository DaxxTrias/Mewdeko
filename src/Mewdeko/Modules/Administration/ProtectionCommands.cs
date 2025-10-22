﻿using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using LinqToDB;
using LinqToDB.Async;
using DataModel;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for managing the Anti-Alt, Anti-Raid, and Anti-Spam protection settings.
    /// </summary>
    [Group]
    public class ProtectionCommands(IDataConnectionFactory dbFactory) : MewdekoSubmodule<ProtectionService>
    {
        /// <summary>
        ///     Disables the Anti-Alt protection for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiAlt()
        {
            if (await Service.TryStopAntiAlt(ctx.Guild.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Alt")).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Alt")).ConfigureAwait(false);
        }


        /// <summary>
        ///     Configures the Anti-Alt protection for the guild, setting the minimum account age and punishment action.
        /// </summary>
        /// <param name="minAge">The minimum age (in minutes) for accounts to be considered as alts.</param>
        /// <param name="action">The punishment action to be taken against detected alts. <see cref="PunishmentAction" /></param>
        /// <param name="punishTime">Optional: The duration of the punishment, if applicable.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiAlt(StoopidTime minAge, PunishmentAction action,
            [Remainder] StoopidTime? punishTime = null)
        {
            var minAgeMinutes = (int)minAge.Time.TotalMinutes;
            var punishTimeMinutes = (int?)punishTime?.Time.TotalMinutes ?? 0;

            if (minAgeMinutes < 1 || punishTimeMinutes < 0)
                return;
            switch (action)
            {
                case PunishmentAction.Timeout when punishTime.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            await Service.StartAntiAltAsync(ctx.Guild.Id, minAgeMinutes, action, punishTimeMinutes)
                .ConfigureAwait(false);

            await ctx.OkAsync().ConfigureAwait(false);
        }


        /// <summary>
        ///     Configures the Anti-Alt protection for the guild, setting the minimum account age and punishment action with a
        ///     role-based punishment.
        /// </summary>
        /// <param name="minAge">The minimum age (in minutes) for accounts to be considered as alts.</param>
        /// <param name="action">The punishment action to be taken against detected alts. <see cref="PunishmentAction" /></param>
        /// <param name="role">The role to be assigned to detected alts as punishment.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiAlt(StoopidTime minAge, PunishmentAction action, [Remainder] IRole role)
        {
            var minAgeMinutes = (int)minAge.Time.TotalMinutes;

            if (minAgeMinutes < 1)
                return;

            await Service.StartAntiAltAsync(ctx.Guild.Id, minAgeMinutes, action, roleId: role.Id).ConfigureAwait(false);

            await ctx.OkAsync().ConfigureAwait(false);
        }


        /// <summary>
        ///     Disables the Anti-Raid protection for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiRaid()
        {
            if (await Service.TryStopAntiRaid(ctx.Guild.Id))
                await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Raid"));
            else
                await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Raid"));
        }

        /// <summary>
        ///     Configures the Anti-Raid protection for the guild, setting the user threshold, detection time window, punishment
        ///     action, and optional punishment duration.
        /// </summary>
        /// <param name="userThreshold">The threshold of users that triggers the detection of a raid.</param>
        /// <param name="seconds">The time window (in seconds) to observe user joins.</param>
        /// <param name="action">The punishment action to be taken against detected raids. <see cref="PunishmentAction" /></param>
        /// <param name="punishTime">The duration of punishment for the raiders (optional).</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(1)]
        public Task AntiRaid(int userThreshold, int seconds, PunishmentAction action,
            [Remainder] StoopidTime punishTime)
        {
            return InternalAntiRaid(userThreshold, seconds, action, punishTime);
        }

        /// <summary>
        ///     Configures the Anti-Raid protection for the guild, setting the user threshold, detection time window, and
        ///     punishment action.
        /// </summary>
        /// <param name="userThreshold">The threshold of users that triggers the detection of a raid.</param>
        /// <param name="seconds">The time window (in seconds) to observe user joins.</param>
        /// <param name="action">The punishment action to be taken against detected raids. <see cref="PunishmentAction" /></param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(2)]
        public Task AntiRaid(int userThreshold, int seconds, PunishmentAction action)
        {
            return InternalAntiRaid(userThreshold, seconds, action);
        }


        private async Task InternalAntiRaid(int userThreshold, int seconds = 10,
            PunishmentAction action = PunishmentAction.Mute, StoopidTime? punishTime = null)
        {
            switch (action)
            {
                case PunishmentAction.Timeout when punishTime.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            if (action == PunishmentAction.AddRole)
            {
                await ReplyErrorAsync(Strings.PunishmentUnsupported(ctx.Guild.Id, action)).ConfigureAwait(false);
                return;
            }

            if (userThreshold is < 2 or > 30)
            {
                await ReplyErrorAsync(Strings.RaidCnt(ctx.Guild.Id, 2, 30)).ConfigureAwait(false);
                return;
            }

            if (seconds is < 2 or > 300)
            {
                await ReplyErrorAsync(Strings.RaidTime(ctx.Guild.Id, 2, 300)).ConfigureAwait(false);
                return;
            }

            if (punishTime is not null)
            {
                if (!ProtectionService.IsDurationAllowed(action))
                    await ReplyErrorAsync(Strings.ProtCantUseTime(ctx.Guild.Id)).ConfigureAwait(false);
            }

            var time = (int?)punishTime?.Time.TotalMinutes ?? 0;
            if (time is < 0 or > 60 * 24)
                return;

            var stats = await Service.StartAntiRaidAsync(ctx.Guild.Id, userThreshold, seconds,
                action, time).ConfigureAwait(false);

            if (stats == null) return;

            await ctx.Channel.SendConfirmAsync(Strings.ProtEnable(ctx.Guild.Id, "Anti-Raid"),
                    $"{ctx.User.Mention} {GetAntiRaidString(stats)}")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Disables the Anti-Spam protection for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiSpam()
        {
            if (await Service.TryStopAntiSpam(ctx.Guild.Id))
                await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Spam"));
            else
                await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Spam"));
        }

        /// <summary>
        ///     Configures the Anti-Spam protection for the guild, setting the message count threshold, punishment action, and
        ///     optional punishment duration.
        /// </summary>
        /// <param name="messageCount">The threshold of messages that triggers the detection of spam.</param>
        /// <param name="action">
        ///     The punishment action to be taken against detected spammers. <see cref="PunishmentAction" />
        /// </param>
        /// <param name="punishTime">The duration of punishment for the spammers (optional).</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(1)]
        public Task AntiSpam(int messageCount, PunishmentAction action, [Remainder] StoopidTime punishTime)
        {
            return InternalAntiSpam(messageCount, action, punishTime);
        }

        /// <summary>
        ///     Configures the Anti-Spam protection for the guild, setting the message count threshold, punishment action, and the
        ///     role to add to spammers.
        /// </summary>
        /// <param name="messageCount">The threshold of messages that triggers the detection of spam.</param>
        /// <param name="action">
        ///     The punishment action to be taken against detected spammers. <see cref="PunishmentAction" />
        /// </param>
        /// <param name="role">The role to add to the spammers.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(0)]
        public Task AntiSpam(int messageCount, PunishmentAction action, [Remainder] IRole role)
        {
            if (action != PunishmentAction.AddRole)
                return Task.CompletedTask;

            return InternalAntiSpam(messageCount, action, null, role);
        }

        /// <summary>
        ///     Configures the Anti-Spam protection for the guild, setting the message count threshold and punishment action.
        /// </summary>
        /// <param name="messageCount">The threshold of messages that triggers the detection of spam.</param>
        /// <param name="action">
        ///     The punishment action to be taken against detected spammers. <see cref="PunishmentAction" />
        /// </param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(2)]
        public Task AntiSpam(int messageCount, PunishmentAction action)
        {
            return InternalAntiSpam(messageCount, action);
        }


        /// <summary>
        ///     Configures the Anti-Spam protection for the guild, setting the message count threshold, punishment action, and
        ///     optional punishment duration.
        /// </summary>
        /// <param name="messageCount">The threshold of messages that triggers the detection of spam.</param>
        /// <param name="action">The punishment action to be taken against detected spammers.</param>
        /// <param name="timeData">The duration of punishment for the spammers (optional).</param>
        /// <param name="role">The role to add to the spammers (optional).</param>
        /// <remarks>
        ///     This method is internally used by the AntiSpam command and is restricted to users with Administrator permissions.
        /// </remarks>
        private async Task InternalAntiSpam(int messageCount, PunishmentAction action,
            StoopidTime? timeData = null, IRole? role = null)
        {
            if (messageCount is < 2 or > 10)
                return;

            if (timeData is not null)
            {
                if (!ProtectionService.IsDurationAllowed(action))
                {
                    await ReplyErrorAsync(Strings.ProtCantUseTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            var time = (int?)timeData?.Time.TotalMinutes ?? 0;
            if (time is < 0 or > 60 * 24)
                return;

            switch (action)
            {
                case PunishmentAction.Timeout when timeData.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when timeData.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var stats = await Service.StartAntiSpamAsync(ctx.Guild.Id, messageCount, action, time, role?.Id)
                .ConfigureAwait(false);

            await ctx.Channel.SendConfirmAsync(Strings.ProtEnable(ctx.Guild.Id, "Anti-Spam"),
                $"{ctx.User.Mention} {GetAntiSpamString(stats)}").ConfigureAwait(false);
        }


        /// <summary>
        ///     Ignores the current text channel from Anti-Spam protection.
        /// </summary>
        /// <remarks>
        ///     This command adds the current text channel to the list of ignored channels for Anti-Spam protection.
        ///     It is restricted to users with Administrator permissions and is used to exclude specific channels from Anti-Spam
        ///     checks.
        /// </remarks>
        public async Task AntispamIgnore()
        {
            var added = await Service.AntiSpamIgnoreAsync(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

            if (added is null)
            {
                await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Spam")).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(added.Value
                    ? Strings.SpamIgnore(ctx.Guild.Id, "Anti-Spam")
                    : Strings.SpamNotIgnore(ctx.Guild.Id, "Anti-Spam"))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Disables the Anti-Mass-Mention protection for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiMassMention()
        {
            if (await Service.TryStopAntiMassMention(ctx.Guild.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Mass-Mention")).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Mass-Mention"))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Mass-Mention protection for the guild, setting the mention threshold for a single message,
        ///     the time window for mention tracking, the maximum allowed mentions in the time window, and the punishment action.
        /// </summary>
        /// <param name="mentionThreshold">The number of mentions allowed in a single message before triggering protection.</param>
        /// <param name="timeWindowSeconds">The time window (in seconds) to observe mentions.</param>
        /// <param name="maxMentionsInTimeWindow">The maximum allowed mentions in the specified time window.</param>
        /// <param name="ignoreBots">Whether to ignore bot accounts when tracking mentions.</param>
        /// <param name="action">
        ///     The punishment action to be taken against users who exceed the mention limits.
        ///     <see cref="PunishmentAction" />
        /// </param>
        /// <param name="punishTime">Optional: The duration of the punishment (if applicable).</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiMassMention(int mentionThreshold, int timeWindowSeconds, int maxMentionsInTimeWindow,
            bool ignoreBots,
            PunishmentAction action, [Remainder] StoopidTime? punishTime = null)
        {
            var punishTimeMinutes = (int?)punishTime?.Time.TotalMinutes ?? 0;

            if (punishTimeMinutes < 0 || mentionThreshold < 1 || timeWindowSeconds < 1 || maxMentionsInTimeWindow < 1)
                return;

            switch (action)
            {
                case PunishmentAction.Timeout when punishTime.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            await Service.StartAntiMassMentionAsync(ctx.Guild.Id, mentionThreshold, timeWindowSeconds,
                maxMentionsInTimeWindow, ignoreBots, action, punishTimeMinutes, null).ConfigureAwait(false);

            await ctx.OkAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Mass-Mention protection for the guild, setting the mention threshold for a single message,
        ///     the time window for mention tracking, the maximum allowed mentions in the time window, and the punishment action
        ///     with a role-based punishment.
        /// </summary>
        /// <param name="mentionThreshold">The number of mentions allowed in a single message before triggering protection.</param>
        /// <param name="timeWindowSeconds">The time window (in seconds) to observe mentions.</param>
        /// <param name="maxMentionsInTimeWindow">The maximum allowed mentions in the specified time window.</param>
        /// <param name="ignoreBots">Whether to ignore bot accounts when tracking mentions.</param>
        /// <param name="action">
        ///     The punishment action to be taken against users who exceed the mention limits.
        ///     <see cref="PunishmentAction" />
        /// </param>
        /// <param name="role">The role to be assigned to punished users as punishment.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiMassMention(int mentionThreshold, int timeWindowSeconds, int maxMentionsInTimeWindow,
            bool ignoreBots,
            PunishmentAction action, [Remainder] IRole role)
        {
            if (mentionThreshold < 1 || timeWindowSeconds < 1 || maxMentionsInTimeWindow < 1)
                return;

            await Service.StartAntiMassMentionAsync(ctx.Guild.Id, mentionThreshold, timeWindowSeconds,
                maxMentionsInTimeWindow, ignoreBots, action, 0, role.Id).ConfigureAwait(false);

            await ctx.OkAsync().ConfigureAwait(false);
        }


        /// <summary>
        ///     Displays the current status of anti-protection settings, including Anti-Spam, Anti-Raid, Anti-Alt, and
        ///     Anti-Mass-Mention.
        /// </summary>
        /// <remarks>
        ///     This command provides information about the active anti-protection settings in the server, including Anti-Spam,
        ///     Anti-Raid, Anti-Alt, and Anti-Mass-Mention.
        ///     It does not require any specific permissions to use.
        /// </remarks>
        public async Task AntiList()
        {
            var (spam, raid, alt, massMention, pattern) = Service.GetAntiStats(ctx.Guild.Id);

            if (spam is null && raid is null && alt is null && massMention is null && pattern is null)
            {
                await ReplyConfirmAsync(Strings.ProtNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.ProtActive(ctx.Guild.Id));

            if (spam != null)
            {
                embed.AddField(efb => efb.WithName("Anti-Spam")
                    .WithValue(GetAntiSpamString(spam).TrimTo(1024))
                    .WithIsInline(true));
            }

            if (raid != null)
            {
                embed.AddField(efb => efb.WithName("Anti-Raid")
                    .WithValue(GetAntiRaidString(raid).TrimTo(1024))
                    .WithIsInline(true));
            }

            if (alt is not null)
                embed.AddField("Anti-Alt", GetAntiAltString(alt), true);

            if (massMention != null)
            {
                embed.AddField("Anti-Mass-Mention", GetAntiMassMentionString(massMention).TrimTo(1024), true);
            }

            if (pattern != null)
            {
                embed.AddField("Anti-Pattern", GetAntiPatternString(pattern).TrimTo(1024), true);
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Builds the string for the Anti-Mass-Mention settings display.
        /// </summary>
        /// <param name="stats">The AntiMassMentionStats object.</param>
        /// <returns>A formatted string showing the current Anti-Mass-Mention settings.</returns>
        private string GetAntiMassMentionString(AntiMassMentionStats stats)
        {
            var settings = stats.AntiMassMentionSettings;

            var ignoreBots = settings.IgnoreBots ? "Yes" : "No";
            var add = "";
            if (settings.MuteTime > 0)
                add = $" ({TimeSpan.FromMinutes(settings.MuteTime).Humanize()})";

            return Strings.MassMentionStats(ctx.Guild.Id,
                Format.Bold(settings.MentionThreshold.ToString()),
                Format.Bold(settings.MaxMentionsInTimeWindow.ToString()),
                Format.Bold(settings.TimeWindowSeconds.ToString()),
                Format.Bold(settings.Action + add),
                Format.Bold(ignoreBots));
        }


        private string? GetAntiAltString(AntiAltStats alt)
        {
            return Strings.AntiAltStatus(ctx.Guild.Id,
                Format.Bold(TimeSpan.Parse(alt.MinAge).ToString(@"dd\d\ hh\h\ mm\m\ ")),
                Format.Bold(alt.Action.ToString()),
                Format.Bold(alt.Counter.ToString()));
        }

        private string? GetAntiSpamString(AntiSpamStats stats)
        {
            var settings = stats.AntiSpamSettings;
            var ignoredString = string.Join(", ", settings.AntiSpamIgnores.Select(c => $"<#{c.ChannelId}>"));

            if (string.IsNullOrWhiteSpace(ignoredString))
                ignoredString = "none";

            var add = "";
            if (settings.MuteTime > 0) add = $" ({TimeSpan.FromMinutes(settings.MuteTime).Humanize()})";

            return Strings.SpamStats(ctx.Guild.Id,
                Format.Bold(settings.MessageThreshold.ToString()),
                Format.Bold(settings.Action + add),
                ignoredString);
        }

        private string? GetAntiRaidString(AntiRaidStats stats)
        {
            var actionString = Format.Bold(stats.AntiRaidSettings.Action.ToString());

            if (stats.AntiRaidSettings.PunishDuration > 0)
                actionString += $" **({TimeSpan.FromMinutes(stats.AntiRaidSettings.PunishDuration).Humanize()})**";

            return Strings.RaidStats(ctx.Guild.Id,
                Format.Bold(stats.AntiRaidSettings.UserThreshold.ToString()),
                Format.Bold(stats.AntiRaidSettings.Seconds.ToString()),
                actionString);
        }

        private string? GetAntiPatternString(AntiPatternStats stats)
        {
            var settings = stats.AntiPatternSettings;
            var patterns = settings.AntiPatternPatterns?.ToList();
            var patternCount = patterns?.Count ?? 0;

            var add = "";
            if (settings.PunishDuration > 0)
                add = $" ({TimeSpan.FromMinutes(settings.PunishDuration).Humanize()})";

            return Strings.AntiPatternStats(ctx.Guild.Id,
                Format.Bold(settings.Action + add),
                Format.Bold(patternCount.ToString()),
                Format.Bold(stats.Counter.ToString()));
        }

        /// <summary>
        ///     Adds a user to the protection ignore list so anti-spam rules will skip them.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ProtIgnoreAdd(IGuildUser user, [Remainder] string? note = null)
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var exists = await db.GetTable<DataModel.ProtectionIgnoredUser>()
                .AnyAsync(x => x.GuildId == ctx.Guild.Id && x.UserId == user.Id);
            if (!exists)
            {
                await db.InsertAsync(new DataModel.ProtectionIgnoredUser
                {
                    GuildId = ctx.Guild.Id,
                    UserId = user.Id,
                    DateAdded = DateTime.UtcNow,
                    Note = note
                });
            }

            await ReplyConfirmAsync($"Added {user.Mention} to protection ignore list.");
        }

        /// <summary>
        ///     Removes a user from the protection ignore list.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ProtIgnoreRemove(IGuildUser user)
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            await db.GetTable<DataModel.ProtectionIgnoredUser>()
                .Where(x => x.GuildId == ctx.Guild.Id && x.UserId == user.Id)
                .DeleteAsync();
            await ReplyConfirmAsync($"Removed {user.Mention} from protection ignore list.");
        }

        /// <summary>
        ///     Lists users on the protection ignore list.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ProtIgnoreList()
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var list = await db.GetTable<DataModel.ProtectionIgnoredUser>()
                .Where(x => x.GuildId == ctx.Guild.Id)
                .ToListAsync();

            if (list.Count == 0)
            {
                await ReplyConfirmAsync("Protection ignore list is empty.");
                return;
            }

            var lines = list.Select(x => $"<@{x.UserId}> {(string.IsNullOrWhiteSpace(x.Note) ? string.Empty : $"- {x.Note}")}");
            await ReplyConfirmAsync(string.Join("\n", lines));
        }

        /// <summary>
        ///     Disables the Anti-Pattern protection for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiPattern()
        {
            if (await Service.TryStopAntiPattern(ctx.Guild.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.AntiPatternDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Pattern protection for the guild, setting the punishment action and optional duration.
        /// </summary>
        /// <param name="action">The punishment action to be taken against detected pattern matches.</param>
        /// <param name="punishTime">Optional: The duration of the punishment, if applicable.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiPattern(PunishmentAction action, [Remainder] StoopidTime? punishTime = null)
        {
            var punishTimeMinutes = (int?)punishTime?.Time.TotalMinutes ?? 0;

            if (punishTimeMinutes < 0)
                return;

            switch (action)
            {
                case PunishmentAction.Timeout when punishTime?.Time.Days > 28:
                    await ReplyErrorAsync("Timeout length cannot be longer than 28 days.").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime?.Time == TimeSpan.Zero:
                    await ReplyErrorAsync("Timeout punishment requires a duration.").ConfigureAwait(false);
                    return;
            }

            var stats = await Service.StartAntiPatternAsync(ctx.Guild.Id, action, punishTimeMinutes)
                .ConfigureAwait(false);

            if (stats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var durationText = punishTimeMinutes > 0
                ? $" for **{TimeSpan.FromMinutes(punishTimeMinutes).Humanize()}**"
                : "";
            await ReplyConfirmAsync(Strings.AntiPatternEnabled(ctx.Guild.Id, action.ToString(), durationText))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Pattern protection for the guild, setting the punishment action with a role-based punishment.
        /// </summary>
        /// <param name="action">The punishment action to be taken against detected pattern matches.</param>
        /// <param name="role">The role to be assigned to users who match patterns.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiPattern(PunishmentAction action, [Remainder] IRole role)
        {
            var stats = await Service.StartAntiPatternAsync(ctx.Guild.Id, action, roleId: role.Id)
                .ConfigureAwait(false);

            if (stats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.AntiPatternEnabledRole(ctx.Guild.Id, action.ToString(), role.Mention))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a regex pattern to the Anti-Pattern protection.
        /// </summary>
        /// <param name="pattern">The regex pattern to match against usernames/display names.</param>
        /// <param name="name">Optional name for the pattern.</param>
        /// <param name="checkUsername">Whether to check usernames against this pattern (default: true).</param>
        /// <param name="checkDisplayName">Whether to check display names against this pattern (default: true).</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PatternAdd(string pattern, string? name = null, bool checkUsername = true,
            bool checkDisplayName = true)
        {
            if (await Service.AddPatternAsync(ctx.Guild.Id, pattern, name, checkUsername, checkDisplayName)
                    .ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.PatternAdded(ctx.Guild.Id, name ?? "Unnamed", pattern, checkUsername,
                    checkDisplayName)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.PatternAddFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a pattern from the Anti-Pattern protection.
        /// </summary>
        /// <param name="patternId">The ID of the pattern to remove.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PatternRemove(int patternId)
        {
            if (await Service.RemovePatternAsync(ctx.Guild.Id, patternId).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.PatternRemoved(ctx.Guild.Id, patternId)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.PatternRemoveFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all patterns configured for the Anti-Pattern protection.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PatternList()
        {
            var (_, _, _, _, patternStats) = Service.GetAntiStats(ctx.Guild.Id);

            if (patternStats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var patterns = patternStats.AntiPatternSettings.AntiPatternPatterns?.ToList();
            if (patterns == null || patterns.Count == 0)
            {
                await ReplyConfirmAsync(Strings.PatternListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.PatternListTitle(ctx.Guild.Id))
                .WithDescription($"**Action:** {patternStats.Action}\n**Triggered:** {patternStats.Counter} times");

            foreach (var pattern in patterns.Take(10)) // Limit to 10 patterns to avoid embed limits
            {
                var fieldName = $"ID: {pattern.Id} - {pattern.Name ?? "Unnamed"}";
                var fieldValue = $"**Pattern:** `{pattern.Pattern}`\n" +
                                 $"**Username:** {(pattern.CheckUsername ? "✅" : "❌")}\n" +
                                 $"**Display Name:** {(pattern.CheckDisplayName ? "✅" : "❌")}";
                embed.AddField(fieldName, fieldValue, true);
            }

            if (patterns.Count > 10)
            {
                embed.WithFooter($"Showing first 10 of {patterns.Count} patterns");
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures advanced anti-pattern settings.
        /// </summary>
        /// <param name="setting">The setting to configure.</param>
        /// <param name="value">The value to set.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PatternConfig(string setting, string value)
        {
            var (_, _, _, _, patternStats) = Service.GetAntiStats(ctx.Guild.Id);

            if (patternStats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = false;
            var settingLower = setting.ToLower();

            switch (settingLower)
            {
                case "accountage":
                    if (bool.TryParse(value, out var checkAccountAge))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkAccountAge);
                    }

                    break;
                case "maxaccountage":
                    if (int.TryParse(value, out var maxAccountAgeMonths) && maxAccountAgeMonths > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            maxAccountAgeMonths: maxAccountAgeMonths);
                    }

                    break;
                case "jointiming":
                    if (bool.TryParse(value, out var checkJoinTiming))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkJoinTiming: checkJoinTiming);
                    }

                    break;
                case "maxjoinhours":
                    if (double.TryParse(value, out var maxJoinHours) && maxJoinHours > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id, maxJoinHours: maxJoinHours);
                    }

                    break;
                case "batchcreation":
                    if (bool.TryParse(value, out var checkBatchCreation))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkBatchCreation: checkBatchCreation);
                    }

                    break;
                case "offlinestatus":
                    if (bool.TryParse(value, out var checkOfflineStatus))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkOfflineStatus: checkOfflineStatus);
                    }

                    break;
                case "newaccounts":
                    if (bool.TryParse(value, out var checkNewAccounts))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkNewAccounts: checkNewAccounts);
                    }

                    break;
                case "newaccountdays":
                    if (int.TryParse(value, out var newAccountDays) && newAccountDays > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            newAccountDays: newAccountDays);
                    }

                    break;
                case "minimumscore":
                    if (int.TryParse(value, out var minimumScore) && minimumScore > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id, minimumScore: minimumScore);
                    }

                    break;
                default:
                    await ReplyErrorAsync(Strings.PatternConfigUnknownSetting(ctx.Guild.Id, setting))
                        .ConfigureAwait(false);
                    return;
            }

            if (success)
            {
                await ReplyConfirmAsync(Strings.PatternConfigUpdated(ctx.Guild.Id, setting, value))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.PatternConfigUpdateFailed(ctx.Guild.Id, setting)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Shows the current anti-pattern configuration.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PatternConfig()
        {
            var (_, _, _, _, patternStats) = Service.GetAntiStats(ctx.Guild.Id);

            if (patternStats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var settings = patternStats.AntiPatternSettings;
            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.PatternConfigTitle(ctx.Guild.Id))
                .WithDescription($"**Action:** {settings.Action}\n**Minimum Score:** {settings.MinimumScore}")
                .AddField("Account Age Check",
                    $"**Enabled:** {settings.CheckAccountAge}\n**Max Age:** {settings.MaxAccountAgeMonths} months",
                    true)
                .AddField("Join Timing Check",
                    $"**Enabled:** {settings.CheckJoinTiming}\n**Max Hours:** {settings.MaxJoinHours}h", true)
                .AddField("Batch Creation Check", $"**Enabled:** {settings.CheckBatchCreation}", true)
                .AddField("Offline Status Check", $"**Enabled:** {settings.CheckOfflineStatus}", true)
                .AddField("New Account Check",
                    $"**Enabled:** {settings.CheckNewAccounts}\n**Days:** {settings.NewAccountDays}", true)
                .AddField("Statistics",
                    $"**Patterns:** {settings.AntiPatternPatterns?.Count() ?? 0}\n**Triggered:** {patternStats.Counter} times",
                    true);

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}