using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for configuring critical security audit alerts.
    /// </summary>
    [Group]
    public class SecurityAuditCommands : MewdekoSubmodule<SecurityAuditMonitorService>
    {
        /// <summary>
        ///     Shows security audit monitor settings.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SecurityAuditStatus()
        {
            var settings = await Service.GetOrCreateSettingsAsync(ctx.Guild.Id).ConfigureAwait(false);
            var channel = settings.AlertChannelId is null or 0
                ? "not set"
                : $"<#{settings.AlertChannelId.Value}>";

            await ConfirmAsync(
                    $"Security audit monitor is **{(settings.Enabled ? "enabled" : "disabled")}**.\n" +
                    $"Alert channel: {channel}\n" +
                    $"Owner DMs: **{(settings.DmBotOwnersOnCritical ? "enabled" : "disabled")}**")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the dedicated security audit alert channel.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SecurityAuditSetChannel(ITextChannel channel)
        {
            await Service.SetAlertChannelAsync(ctx.Guild, channel).ConfigureAwait(false);
            await ConfirmAsync($"Critical security audit alerts will be sent to {channel.Mention}.")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Clears the dedicated security audit alert channel.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SecurityAuditDisableChannel()
        {
            await Service.SetAlertChannelAsync(ctx.Guild, null).ConfigureAwait(false);
            await ConfirmAsync("Critical security audit alerts will fall back to warnlog or logging channels.")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables or disables critical security audit monitoring.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SecurityAuditEnabled(bool enabled)
        {
            await Service.SetEnabledAsync(ctx.Guild, enabled).ConfigureAwait(false);
            await ConfirmAsync($"Security audit monitor is now **{(enabled ? "enabled" : "disabled")}**.")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables or disables bot-owner DMs for critical security audit alerts.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SecurityAuditOwnerDms(bool enabled)
        {
            await Service.SetOwnerDmAsync(ctx.Guild, enabled).ConfigureAwait(false);
            await ConfirmAsync($"Security audit owner DMs are now **{(enabled ? "enabled" : "disabled")}**.")
                .ConfigureAwait(false);
        }
    }
}
