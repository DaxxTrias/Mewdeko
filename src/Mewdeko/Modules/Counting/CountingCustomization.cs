using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Counting.Services;

namespace Mewdeko.Modules.Counting;

public partial class Counting
{
    /// <summary>
    ///     Commands for customizing counting behavior and messages.
    /// </summary>
    public class CountingCustomization : MewdekoModuleBase<CountingService>
    {
        /// <summary>
        ///     Sets a custom message for successful counts.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        /// <param name="embed">The message template. Use "-" to reset to default.</param>
        /// <example>.counting successmessage Good job! You counted {number}!</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CountingSuccessMessage(ITextChannel? channel = null, [Remainder] string? embed = null)
        {
            channel ??= (ITextChannel)ctx.Channel;

            var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
            if (countingChannel == null)
            {
                await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
                return;
            }

            if (embed == "-")
            {
                await Service.ResetSuccessMessageAsync(channel.Id);
                await ConfirmAsync(Strings.CountingSuccessMessageReset(ctx.Guild.Id, channel.Mention));
                return;
            }

            if (string.IsNullOrWhiteSpace(embed))
            {
                var current = await Service.GetSuccessMessageAsync(channel.Id);
                await ConfirmAsync(
                    Strings.CountingCurrentSuccessMessage(ctx.Guild.Id, channel.Mention, current ?? "Default"));
                return;
            }

            await Service.SetSuccessMessageAsync(channel.Id, embed);
            await ConfirmAsync(Strings.CountingSuccessMessageSet(ctx.Guild.Id, channel.Mention));
        }

        /// <summary>
        ///     Sets a custom message for failed counts.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        /// <param name="embed">The message template. Use "-" to reset to default.</param>
        /// <example>.counting failuremessage Wrong! Expected {expected}, got {actual}</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CountingFailureMessage(ITextChannel? channel = null, [Remainder] string? embed = null)
        {
            channel ??= (ITextChannel)ctx.Channel;

            var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
            if (countingChannel == null)
            {
                await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
                return;
            }

            if (embed == "-")
            {
                await Service.ResetFailureMessageAsync(channel.Id);
                await ConfirmAsync(Strings.CountingFailureMessageReset(ctx.Guild.Id, channel.Mention));
                return;
            }

            if (string.IsNullOrWhiteSpace(embed))
            {
                var current = await Service.GetFailureMessageAsync(channel.Id);
                await ConfirmAsync(
                    Strings.CountingCurrentFailureMessage(ctx.Guild.Id, channel.Mention, current ?? "Default"));
                return;
            }

            await Service.SetFailureMessageAsync(channel.Id, embed);
            await ConfirmAsync(Strings.CountingFailureMessageSet(ctx.Guild.Id, channel.Mention));
        }

        /// <summary>
        ///     Sets a custom message for milestone achievements.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        /// <param name="embed">The message template. Use "-" to reset to default.</param>
        /// <example>.counting milestonemessage 🎉 Milestone reached: {number}!</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CountingMilestoneMessage(ITextChannel? channel = null, [Remainder] string? embed = null)
        {
            channel ??= (ITextChannel)ctx.Channel;

            var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
            if (countingChannel == null)
            {
                await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
                return;
            }

            if (embed == "-")
            {
                await Service.ResetMilestoneMessageAsync(channel.Id);
                await ConfirmAsync(Strings.CountingMilestoneMessageReset(ctx.Guild.Id, channel.Mention));
                return;
            }

            if (string.IsNullOrWhiteSpace(embed))
            {
                var current = await Service.GetMilestoneMessageAsync(channel.Id);
                await ConfirmAsync(
                    Strings.CountingCurrentMilestoneMessage(ctx.Guild.Id, channel.Mention, current ?? "Default"));
                return;
            }

            await Service.SetMilestoneMessageAsync(channel.Id, embed);
            await ConfirmAsync(Strings.CountingMilestoneMessageSet(ctx.Guild.Id, channel.Mention));
        }

        /// <summary>
        ///     Sets the channel where milestone achievements are announced.
        /// </summary>
        /// <param name="countingChannel">The counting channel.</param>
        /// <param name="milestoneChannel">The channel for milestone announcements. Use null to disable.</param>
        /// <example>.counting milestonechannel #counting #announcements</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CountingMilestoneChannel(ITextChannel? countingChannel = null,
            ITextChannel? milestoneChannel = null)
        {
            countingChannel ??= (ITextChannel)ctx.Channel;

            var channel = await Service.GetCountingChannelAsync(countingChannel.Id);
            if (channel == null)
            {
                await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, countingChannel.Mention));
                return;
            }

            if (milestoneChannel == null)
            {
                await Service.SetMilestoneChannelAsync(countingChannel.Id, null);
                await ConfirmAsync(Strings.CountingMilestoneChannelDisabled(ctx.Guild.Id, countingChannel.Mention));
                return;
            }

            await Service.SetMilestoneChannelAsync(countingChannel.Id, milestoneChannel.Id);
            await ConfirmAsync(Strings.CountingMilestoneChannelSet(ctx.Guild.Id, countingChannel.Mention,
                milestoneChannel.Mention));
        }

        /// <summary>
        ///     Sets the channel where counting failures are logged.
        /// </summary>
        /// <param name="countingChannel">The counting channel.</param>
        /// <param name="failureChannel">The channel for failure logs. Use null to disable.</param>
        /// <example>.counting failurechannel #counting #mod-logs</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CountingFailureChannel(ITextChannel? countingChannel = null,
            ITextChannel? failureChannel = null)
        {
            countingChannel ??= (ITextChannel)ctx.Channel;

            var channel = await Service.GetCountingChannelAsync(countingChannel.Id);
            if (channel == null)
            {
                await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, countingChannel.Mention));
                return;
            }

            if (failureChannel == null)
            {
                await Service.SetFailureChannelAsync(countingChannel.Id, null);
                await ConfirmAsync(Strings.CountingFailureChannelDisabled(ctx.Guild.Id, countingChannel.Mention));
                return;
            }

            await Service.SetFailureChannelAsync(countingChannel.Id, failureChannel.Id);
            await ConfirmAsync(Strings.CountingFailureChannelSet(ctx.Guild.Id, countingChannel.Mention,
                failureChannel.Mention));
        }

        /// <summary>
        ///     Sets custom milestone numbers.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        /// <param name="milestones">Comma-separated milestone numbers (e.g., 50,100,250,500,1000).</param>
        /// <example>.counting milestones 50,100,250,500,1000,2500</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CountingMilestones(ITextChannel? channel = null, [Remainder] string? milestones = null)
        {
            channel ??= (ITextChannel)ctx.Channel;

            var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
            if (countingChannel == null)
            {
                await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
                return;
            }

            if (string.IsNullOrWhiteSpace(milestones))
            {
                var current = await Service.GetMilestonesAsync(channel.Id);
                await ConfirmAsync(Strings.CountingCurrentMilestones(ctx.Guild.Id, channel.Mention,
                    string.Join(", ", current)));
                return;
            }

            if (milestones == "-")
            {
                await Service.ResetMilestonesAsync(channel.Id);
                await ConfirmAsync(Strings.CountingMilestonesReset(ctx.Guild.Id, channel.Mention));
                return;
            }

            var milestoneNumbers = milestones.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .Where(m => long.TryParse(m, out _))
                .Select(long.Parse)
                .Where(m => m > 0)
                .OrderBy(m => m)
                .ToList();

            if (!milestoneNumbers.Any())
            {
                await ErrorAsync(Strings.CountingInvalidMilestones(ctx.Guild.Id));
                return;
            }

            await Service.SetMilestonesAsync(channel.Id, milestoneNumbers);
            await ConfirmAsync(Strings.CountingMilestonesSet(ctx.Guild.Id, channel.Mention,
                string.Join(", ", milestoneNumbers)));
        }

        /// <summary>
        ///     Sets the failure threshold before consequences.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        /// <param name="threshold">Number of failures before action (1-10).</param>
        /// <example>.counting failurethreshold 3</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CountingFailureThreshold(ITextChannel? channel = null, int threshold = 0)
        {
            channel ??= (ITextChannel)ctx.Channel;

            var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
            if (countingChannel == null)
            {
                await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
                return;
            }

            if (threshold == 0)
            {
                var current = await Service.GetFailureThresholdAsync(channel.Id);
                await ConfirmAsync(Strings.CountingCurrentFailureThreshold(ctx.Guild.Id, channel.Mention, current));
                return;
            }

            if (threshold < 1 || threshold > 10)
            {
                await ErrorAsync(Strings.CountingInvalidFailureThreshold(ctx.Guild.Id));
                return;
            }

            await Service.SetFailureThresholdAsync(channel.Id, threshold);
            await ConfirmAsync(Strings.CountingFailureThresholdSet(ctx.Guild.Id, channel.Mention, threshold));
        }

        /// <summary>
        ///     Sets the cooldown between user counts.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        /// <param name="seconds">Cooldown in seconds (0-300).</param>
        /// <example>.counting cooldown 30</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CountingCooldown(ITextChannel? channel = null, int seconds = -1)
        {
            channel ??= (ITextChannel)ctx.Channel;

            var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
            if (countingChannel == null)
            {
                await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
                return;
            }

            if (seconds == -1)
            {
                var current = await Service.GetCooldownAsync(channel.Id);
                await ConfirmAsync(Strings.CountingCurrentCooldown(ctx.Guild.Id, channel.Mention, current));
                return;
            }

            if (seconds < 0 || seconds > 300)
            {
                await ErrorAsync(Strings.CountingInvalidCooldown(ctx.Guild.Id));
                return;
            }

            await Service.SetCooldownAsync(channel.Id, seconds);
            await ConfirmAsync(Strings.CountingCooldownSet(ctx.Guild.Id, channel.Mention, seconds));
        }
    }
}