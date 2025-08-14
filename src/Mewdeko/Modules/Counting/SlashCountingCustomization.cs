using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Counting.Services;

namespace Mewdeko.Modules.Counting;

/// <summary>
///     Slash commands for customizing counting behavior and messages.
/// </summary>
[Group("counting-config", "Configure counting customization")]
public class SlashCountingCustomization : MewdekoSlashModuleBase<CountingService>
{
    /// <summary>
    ///     Sets a custom message for successful counts.
    /// </summary>
    /// <param name="channel">The counting channel. Defaults to current channel.</param>
    /// <param name="message">The message template. Use "-" to reset to default.</param>
    [SlashCommand("success-message", "Set custom message for successful counts")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingSuccessMessage(ITextChannel? channel = null, string? message = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
            return;
        }

        if (message == "-")
        {
            await Service.ResetSuccessMessageAsync(channel.Id);
            await ConfirmAsync(Strings.CountingSuccessMessageReset(ctx.Guild.Id, channel.Mention));
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            var current = await Service.GetSuccessMessageAsync(channel.Id);
            await ConfirmAsync(
                Strings.CountingCurrentSuccessMessage(ctx.Guild.Id, channel.Mention, current ?? "Default"));
            return;
        }

        await Service.SetSuccessMessageAsync(channel.Id, message);
        await ConfirmAsync(Strings.CountingSuccessMessageSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Sets a custom message for failed counts.
    /// </summary>
    /// <param name="channel">The counting channel. Defaults to current channel.</param>
    /// <param name="message">The message template. Use "-" to reset to default.</param>
    [SlashCommand("failure-message", "Set custom message for failed counts")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingFailureMessage(ITextChannel? channel = null, string? message = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
            return;
        }

        if (message == "-")
        {
            await Service.ResetFailureMessageAsync(channel.Id);
            await ConfirmAsync(Strings.CountingFailureMessageReset(ctx.Guild.Id, channel.Mention));
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            var current = await Service.GetFailureMessageAsync(channel.Id);
            await ConfirmAsync(
                Strings.CountingCurrentFailureMessage(ctx.Guild.Id, channel.Mention, current ?? "Default"));
            return;
        }

        await Service.SetFailureMessageAsync(channel.Id, message);
        await ConfirmAsync(Strings.CountingFailureMessageSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Sets a custom message for milestone achievements.
    /// </summary>
    /// <param name="channel">The counting channel. Defaults to current channel.</param>
    /// <param name="message">The message template. Use "-" to reset to default.</param>
    [SlashCommand("milestone-message", "Set custom message for milestones")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingMilestoneMessage(ITextChannel? channel = null, string? message = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
            return;
        }

        if (message == "-")
        {
            await Service.ResetMilestoneMessageAsync(channel.Id);
            await ConfirmAsync(Strings.CountingMilestoneMessageReset(ctx.Guild.Id, channel.Mention));
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            var current = await Service.GetMilestoneMessageAsync(channel.Id);
            await ConfirmAsync(
                Strings.CountingCurrentMilestoneMessage(ctx.Guild.Id, channel.Mention, current ?? "Default"));
            return;
        }

        await Service.SetMilestoneMessageAsync(channel.Id, message);
        await ConfirmAsync(Strings.CountingMilestoneMessageSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Sets the channel where milestone achievements are announced.
    /// </summary>
    /// <param name="countingChannel">The counting channel.</param>
    /// <param name="milestoneChannel">The channel for milestone announcements.</param>
    [SlashCommand("milestone-channel", "Set channel for milestone announcements")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
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
    /// <param name="failureChannel">The channel for failure logs.</param>
    [SlashCommand("failure-channel", "Set channel for failure logs")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingFailureChannel(ITextChannel? countingChannel = null, ITextChannel? failureChannel = null)
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
        await ConfirmAsync(
            Strings.CountingFailureChannelSet(ctx.Guild.Id, countingChannel.Mention, failureChannel.Mention));
    }

    /// <summary>
    ///     Sets custom milestone numbers.
    /// </summary>
    /// <param name="channel">The counting channel.</param>
    /// <param name="milestones">Comma-separated milestone numbers.</param>
    [SlashCommand("milestones", "Set custom milestone numbers")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingMilestones(ITextChannel? channel = null, string? milestones = null)
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
            await ConfirmAsync(
                Strings.CountingCurrentMilestones(ctx.Guild.Id, channel.Mention, string.Join(", ", current)));
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
    /// <param name="channel">The counting channel.</param>
    /// <param name="threshold">Number of failures before action (1-10).</param>
    [SlashCommand("failure-threshold", "Set failure threshold before consequences")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingFailureThreshold(ITextChannel? channel = null, int threshold = 3)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
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
    /// <param name="channel">The counting channel.</param>
    /// <param name="seconds">Cooldown in seconds (0-300).</param>
    [SlashCommand("cooldown", "Set cooldown between user counts")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingCooldown(ITextChannel? channel = null, int seconds = 0)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
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