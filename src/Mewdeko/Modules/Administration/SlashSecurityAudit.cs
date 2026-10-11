using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

/// <summary>
///     Slash commands for configuring critical security audit alerts.
/// </summary>
[Group("securityaudit", "Configure critical security audit alerts")]
public class SlashSecurityAudit : MewdekoSlashModuleBase<SecurityAuditMonitorService>
{
    /// <summary>
    ///     Shows security audit monitor settings.
    /// </summary>
    [SlashCommand("status", "Show security audit monitor settings")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Status()
    {
        var settings = await Service.GetOrCreateSettingsAsync(ctx.Guild.Id).ConfigureAwait(false);
        var channel = settings.AlertChannelId is null or 0
            ? "not set"
            : $"<#{settings.AlertChannelId.Value}>";

        await ReplyConfirmAsync(
                $"Security audit monitor is **{(settings.Enabled ? "enabled" : "disabled")}**.\n" +
                $"Alert channel: {channel}\n" +
                $"Owner DMs: **{(settings.DmBotOwnersOnCritical ? "enabled" : "disabled")}**")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the dedicated security audit alert channel.
    /// </summary>
    [SlashCommand("channel", "Set the dedicated security audit alert channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Channel(ITextChannel channel)
    {
        await Service.SetAlertChannelAsync(ctx.Guild, channel).ConfigureAwait(false);
        await ReplyConfirmAsync($"Critical security audit alerts will be sent to {channel.Mention}.")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears the dedicated security audit alert channel.
    /// </summary>
    [SlashCommand("disable-channel", "Clear the dedicated security audit alert channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task DisableChannel()
    {
        await Service.SetAlertChannelAsync(ctx.Guild, null).ConfigureAwait(false);
        await ReplyConfirmAsync("Critical security audit alerts will fall back to warnlog or logging channels.")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Enables or disables critical security audit monitoring.
    /// </summary>
    [SlashCommand("enabled", "Enable or disable critical security audit monitoring")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Enabled(bool enabled)
    {
        await Service.SetEnabledAsync(ctx.Guild, enabled).ConfigureAwait(false);
        await ReplyConfirmAsync($"Security audit monitor is now **{(enabled ? "enabled" : "disabled")}**.")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Enables or disables bot-owner DMs for critical security audit alerts.
    /// </summary>
    [SlashCommand("owner-dms", "Enable or disable bot-owner DMs for critical security audit alerts")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task OwnerDms(bool enabled)
    {
        await Service.SetOwnerDmAsync(ctx.Guild, enabled).ConfigureAwait(false);
        await ReplyConfirmAsync($"Security audit owner DMs are now **{(enabled ? "enabled" : "disabled")}**.")
            .ConfigureAwait(false);
    }
}
