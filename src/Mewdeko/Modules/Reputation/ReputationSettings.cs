using System.Text;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Reputation.Services;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Reputation;

public partial class Reputation
{
    /// <summary>
    ///     Comprehensive administrative commands for reputation system configuration and management.
    /// </summary>
    public class ReputationSettings : MewdekoSubmodule<RepService>
    {
        private readonly RepCommandRequirementsService commandRequirementsService;
        private readonly RepConfigService configService;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ReputationSettings" /> class.
        /// </summary>
        /// <param name="commandRequirementsService">The command requirements service.</param>
        /// <param name="configService">The reputation configuration service.</param>
        public ReputationSettings(RepCommandRequirementsService commandRequirementsService,
            RepConfigService configService)
        {
            this.commandRequirementsService = commandRequirementsService;
            this.configService = configService;
        }

        #region Channel Configuration

        /// <summary>
        ///     Configures channel-specific reputation settings.
        /// </summary>
        /// <param name="channel">The channel to configure.</param>
        /// <param name="state">The state to set (enabled/disabled/readonly).</param>
        /// <param name="multiplier">The reputation multiplier for this channel.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepChannel(ITextChannel channel, string state = "enabled", decimal multiplier = 1.0m)
        {
            var validStates = new[]
            {
                "enabled", "disabled", "readonly"
            };
            if (!validStates.Contains(state.ToLower()))
            {
                await ReplyErrorAsync(Strings.RepChannelInvalidState(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (multiplier < 0)
            {
                await ReplyErrorAsync(Strings.RepMultiplierNegative(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetChannelConfigAsync(ctx.Guild.Id, channel.Id, state, multiplier);

            var message = state.ToLower() switch
            {
                "enabled" => Strings.RepChannelEnabled(ctx.Guild.Id, channel.Mention),
                "disabled" => Strings.RepChannelDisabledSet(ctx.Guild.Id, channel.Mention),
                "readonly" => Strings.RepChannelReadonly(ctx.Guild.Id, channel.Mention),
                _ => Strings.RepConfigUpdated(ctx.Guild.Id)
            };

            if (multiplier != 1.0m)
            {
                message += $"\n{Strings.RepChannelMultiplier(ctx.Guild.Id, channel.Mention, multiplier)}";
            }

            await SuccessAsync(message).ConfigureAwait(false);
        }

        #endregion

        #region Milestone Management

        /// <summary>
        ///     Manually triggers milestone lost event for testing.
        /// </summary>
        /// <param name="user">The user who lost the milestone.</param>
        /// <param name="role">The role that was lost.</param>
        /// <param name="threshold">The reputation threshold.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepMilestoneLost(IGuildUser user, IRole role, int threshold)
        {
            await SuccessAsync(Strings.RepMilestoneLost(ctx.Guild.Id, user.Mention, role.Name, threshold))
                .ConfigureAwait(false);
        }

        #endregion

        #region Core Configuration Commands

        /// <summary>
        ///     Shows the current reputation configuration in a detailed embed.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepStatus()
        {
            await configService.ShowConfigurationStatusAsync(ctx).ConfigureAwait(false);
        }

        /// <summary>
        ///     Opens the interactive reputation configuration interface.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepConfig()
        {
            await configService.ShowConfigurationMenuAsync(ctx).ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables or disables the reputation system for this server.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepEnable(bool enabled = true)
        {
            await Service.SetEnabledAsync(ctx.Guild.Id, enabled);

            await SuccessAsync(Strings.RepConfigEnabled(ctx.Guild.Id, enabled)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the default cooldown between giving reputation.
        /// </summary>
        /// <param name="minutes">Cooldown in minutes (1-1440).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepCooldown(int minutes)
        {
            if (minutes is < 1 or > 1440)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetDefaultCooldownAsync(ctx.Guild.Id, minutes);
            await SuccessAsync(Strings.RepConfigCooldown(ctx.Guild.Id, minutes)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the daily limit for giving reputation.
        /// </summary>
        /// <param name="limit">Daily limit (1-100).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepDailyLimit(int limit)
        {
            if (limit is < 1 or > 100)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetDailyLimitAsync(ctx.Guild.Id, limit);
            await SuccessAsync(Strings.RepConfigDailyLimit(ctx.Guild.Id, limit)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the weekly limit for giving reputation (optional).
        /// </summary>
        /// <param name="limit">Weekly limit (null to disable, 1-500).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepWeeklyLimit(int? limit = null)
        {
            if (limit.HasValue && limit is < 1 or > 500)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetWeeklyLimitAsync(ctx.Guild.Id, limit);

            await SuccessAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the minimum account age required to give reputation.
        /// </summary>
        /// <param name="days">Minimum account age in days (0-365).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepMinAge(int days)
        {
            if (days is < 0 or > 365)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetMinAccountAgeAsync(ctx.Guild.Id, days);
            await SuccessAsync(Strings.RepConfigAccountAge(ctx.Guild.Id, days)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the minimum server membership time required to give reputation.
        /// </summary>
        /// <param name="hours">Minimum membership time in hours (0-720).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepMinMembership(int hours)
        {
            if (hours is < 0 or > 720)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetMinServerMembershipAsync(ctx.Guild.Id, hours);
            await SuccessAsync(Strings.RepConfigMembership(ctx.Guild.Id, hours)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the minimum message count required to give reputation.
        /// </summary>
        /// <param name="count">Minimum message count (0-1000).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepMinMessages(int count)
        {
            if (count is < 0 or > 1000)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetMinMessageCountAsync(ctx.Guild.Id, count);
            await SuccessAsync(Strings.RepConfigMessages(ctx.Guild.Id, count)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables or disables negative reputation.
        /// </summary>
        /// <param name="enabled">True to enable negative reputation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepNegative(bool enabled)
        {
            await Service.SetNegativeReputationAsync(ctx.Guild.Id, enabled);

            await SuccessAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables or disables anonymous reputation giving.
        /// </summary>
        /// <param name="enabled">True to enable anonymous reputation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepAnonymous(bool enabled)
        {
            await Service.SetAnonymousReputationAsync(ctx.Guild.Id, enabled);

            await SuccessAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the notification channel for reputation events.
        /// </summary>
        /// <param name="channel">The channel to send notifications to (null to disable).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepNotificationChannel(ITextChannel? channel = null)
        {
            await Service.SetNotificationChannelAsync(ctx.Guild.Id, channel?.Id);

            await SuccessAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        #endregion

        #region Decay Configuration

        /// <summary>
        ///     Enables or disables reputation decay for inactive users.
        /// </summary>
        /// <param name="enabled">True to enable decay.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepDecay(bool enabled)
        {
            await Service.SetDecaySettingsAsync(ctx.Guild.Id, enabled);

            var message = enabled
                ? Strings.RepDecayEnabled(ctx.Guild.Id, "1", "daily")
                : Strings.RepDecayDisabled(ctx.Guild.Id);
            await SuccessAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the decay type (daily, weekly, monthly).
        /// </summary>
        /// <param name="type">The decay type.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepDecayType(string type)
        {
            var validTypes = new[]
            {
                "daily", "weekly", "monthly", "fixed", "percentage"
            };
            if (!validTypes.Contains(type.ToLower()))
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetDecaySettingsAsync(ctx.Guild.Id, true, type.ToLower());
            await SuccessAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the decay amount.
        /// </summary>
        /// <param name="amount">The decay amount (1-100).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepDecayAmount(int amount)
        {
            if (amount is < 1 or > 100)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetDecaySettingsAsync(ctx.Guild.Id, true, amount: amount);
            await SuccessAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the number of inactive days before decay starts.
        /// </summary>
        /// <param name="days">Days of inactivity (1-365).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepDecayInactive(int days)
        {
            if (days is < 1 or > 365)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetDecaySettingsAsync(ctx.Guild.Id, true, inactiveDays: days);
            await SuccessAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        #endregion

        #region Import/Export

        /// <summary>
        ///     Exports the current reputation configuration to JSON.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepExport()
        {
            await configService.ExportConfigurationAsync(ctx).ConfigureAwait(false);
        }

        /// <summary>
        ///     Imports reputation configuration from uploaded JSON file.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepImport()
        {
            await configService.ImportConfigurationAsync(ctx).ConfigureAwait(false);
        }

        #endregion

        #region User Management

        /// <summary>
        ///     Removes reputation from a user.
        /// </summary>
        /// <param name="user">The user to remove reputation from.</param>
        /// <param name="amount">The amount of reputation to remove.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepTake(IGuildUser user, int amount = 1)
        {
            if (amount <= 0)
            {
                await ReplyErrorAsync(Strings.RepAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var newTotal = await Service.TakeReputationAsync(ctx.Guild.Id, user.Id, amount, ctx.User.Id);

            if (newTotal == 0)
            {
                await ReplyErrorAsync(Strings.RepNoRepToRemove(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await SuccessAsync(Strings.RepTaken(ctx.Guild.Id, amount, user.Mention, newTotal))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a user's reputation to a specific value.
        /// </summary>
        /// <param name="user">The user to set reputation for.</param>
        /// <param name="amount">The amount to set reputation to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepSet(IGuildUser user, int amount)
        {
            if (amount < 0)
            {
                await ReplyErrorAsync(Strings.RepNegativeAmount(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetReputationAsync(ctx.Guild.Id, user.Id, amount, ctx.User.Id);
            await SuccessAsync(Strings.RepSet(ctx.Guild.Id, user.Mention, amount)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Resets reputation for a user or all users in the server.
        /// </summary>
        /// <param name="target">The user to reset, or "all" to reset everyone.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepReset([Remainder] string target)
        {
            if (target.ToLower() == "all")
            {
                var confirmed = await PromptUserConfirmAsync(
                    "Are you sure you want to reset ALL reputation data for this server? This cannot be undone!",
                    ctx.User.Id);
                if (!confirmed)
                {
                    await ReplyErrorAsync(Strings.RepOperationCancelled(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await Service.ResetAllReputationAsync(ctx.Guild.Id);
                await SuccessAsync(Strings.RepResetAll(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                var user = ulong.TryParse(target, out var userId)
                    ? await ctx.Guild.GetUserAsync(userId)
                    : (await ctx.Guild.GetUsersAsync()).FirstOrDefault(x =>
                        x.Username.Contains(target, StringComparison.OrdinalIgnoreCase));

                if (user == null)
                {
                    await ReplyErrorAsync(Strings.UserNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var wasReset = await Service.ResetUserReputationAsync(ctx.Guild.Id, user.Id);
                if (!wasReset)
                {
                    await ReplyErrorAsync(Strings.RepNoData(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await SuccessAsync(Strings.RepResetUser(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Freezes a user's reputation, preventing them from gaining or losing reputation.
        /// </summary>
        /// <param name="user">The user to freeze.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepFreeze(IGuildUser user)
        {
            await Service.FreezeUserAsync(ctx.Guild.Id, user.Id);
            await SuccessAsync(Strings.RepFrozenUser(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Unfreezes a user's reputation, allowing them to gain and lose reputation again.
        /// </summary>
        /// <param name="user">The user to unfreeze.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepUnfreeze(IGuildUser user)
        {
            var wasUnfrozen = await Service.UnfreezeUserAsync(ctx.Guild.Id, user.Id);
            if (!wasUnfrozen)
            {
                await ReplyErrorAsync(Strings.RepNoData(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await SuccessAsync(Strings.RepUnfrozenUser(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
        }

        #endregion

        #region Reaction Configuration

        /// <summary>
        ///     Configures reaction-based reputation giving.
        /// </summary>
        /// <param name="emoji">The emoji to use for reputation.</param>
        /// <param name="amount">The amount of reputation to give.</param>
        /// <param name="repType">The type of reputation (standard, helper, artist, memer).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepReaction(string emoji, int amount = 1, string repType = "standard")
        {
            if (!await Service.IsValidReputationTypeAsync(ctx.Guild.Id, repType))
            {
                await ReplyErrorAsync(Strings.RepInvalidType(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var isNew = await Service.AddOrUpdateReactionConfigAsync(ctx.Guild.Id, emoji, amount, repType);

            if (isNew)
            {
                await SuccessAsync(Strings.RepReactionAdded(ctx.Guild.Id, emoji, amount, repType))
                    .ConfigureAwait(false);
            }
            else
            {
                await SuccessAsync(Strings.RepReactionUpdated(ctx.Guild.Id, emoji, amount, repType))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a reaction-based reputation configuration.
        /// </summary>
        /// <param name="emoji">The emoji to remove.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepReactionRemove(string emoji)
        {
            var removed = await Service.RemoveReactionConfigAsync(ctx.Guild.Id, emoji);

            if (removed)
            {
                await SuccessAsync(Strings.RepReactionRemoved(ctx.Guild.Id, emoji)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepReactionNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all configured reaction-based reputation settings.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepReactionList()
        {
            var reactionConfigs = await Service.GetReactionConfigsAsync(ctx.Guild.Id);

            if (reactionConfigs.Count == 0)
            {
                await ReplyErrorAsync(Strings.RepReactionListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepReactionListTitle(ctx.Guild.Id));

            var description = string.Empty;
            foreach (var config in reactionConfigs)
            {
                var emoji = config.EmojiId.HasValue
                    ? $"<:{config.EmojiName}:{config.EmojiId}>"
                    : config.EmojiName;
                var status = config.IsEnabled ? "✅" : "❌";
                description += $"{status} {emoji} - {config.RepAmount} {config.RepType} rep\n";
            }

            eb.WithDescription(description);
            await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
        }

        #endregion

        #region Role Rewards

        /// <summary>
        ///     Manages role rewards for reputation milestones. Use without parameters to list all rewards,
        ///     or provide a role and reputation to add/update a reward with optional advanced settings.
        /// </summary>
        /// <param name="role">The role to award (optional for listing).</param>
        /// <param name="reputation">The reputation required to earn the role (optional for listing).</param>
        /// <param name="removeOnDrop">Whether to remove the role if reputation drops.</param>
        /// <param name="announceChannel">Optional channel to announce role awards.</param>
        /// <param name="announceDm">Whether to send DM notifications.</param>
        /// <param name="xpReward">Optional XP reward for reaching milestone.</param>
        /// <param name="detailed">Show detailed information when listing (use 'detailed' as first parameter).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RepRole(IRole? role = null, int? reputation = null, bool removeOnDrop = true,
            ITextChannel? announceChannel = null, bool announceDm = false, int? xpReward = null,
            string? detailed = null)
        {
            // If no role specified or "detailed" is the first parameter, show list
            if (role == null || (reputation == null && role.Name?.ToLower() == "detailed"))
            {
                var showDetailed = role?.Name?.ToLower() == "detailed" || detailed?.ToLower() == "detailed";
                await ShowRoleRewardsList(showDetailed).ConfigureAwait(false);
                return;
            }

            switch (reputation)
            {
                // If reputation not specified, show info for specific role
                case null:
                    await ShowRoleRewardInfo(role).ConfigureAwait(false);
                    return;
                // Add/update role reward
                case < 0:
                    await ReplyErrorAsync(Strings.RepAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var currentUser = await ctx.Guild.GetCurrentUserAsync();
            if (role.Position >= currentUser.Hierarchy)
            {
                await ReplyErrorAsync(Strings.RepRoleHierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var isNew = await Service.AddOrUpdateRoleRewardAsync(ctx.Guild.Id, role.Id, reputation.Value,
                removeOnDrop, announceChannel?.Id, announceDm, xpReward);

            // Check if advanced settings were used
            var hasAdvancedSettings = !removeOnDrop || announceChannel != null || announceDm || xpReward.HasValue;

            if (isNew)
            {
                if (hasAdvancedSettings)
                {
                    await SuccessAsync(Strings.RepRoleAddedAdvanced(ctx.Guild.Id, role.Name, reputation.Value,
                        removeOnDrop, announceDm, xpReward ?? 0)).ConfigureAwait(false);
                }
                else
                {
                    await SuccessAsync(Strings.RepRoleAdded(ctx.Guild.Id, role.Name, reputation.Value))
                        .ConfigureAwait(false);
                }
            }
            else
            {
                if (hasAdvancedSettings)
                {
                    await SuccessAsync(Strings.RepRoleUpdatedAdvanced(ctx.Guild.Id, role.Name, reputation.Value,
                        removeOnDrop, announceDm, xpReward ?? 0)).ConfigureAwait(false);
                }
                else
                {
                    await SuccessAsync(Strings.RepRoleUpdated(ctx.Guild.Id, role.Name, reputation.Value))
                        .ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        ///     Removes a role reward configuration.
        /// </summary>
        /// <param name="role">The role to remove from rewards.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        public async Task RepRoleRemove(IRole role)
        {
            var removed = await Service.RemoveRoleRewardAsync(ctx.Guild.Id, role.Id);

            if (removed)
            {
                await SuccessAsync(Strings.RepRoleRewardRemoved(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Helper method to show the role rewards list.
        /// </summary>
        /// <param name="detailed">Whether to show detailed information.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task ShowRoleRewardsList(bool detailed)
        {
            var roleRewards = await Service.GetRoleRewardsAsync(ctx.Guild.Id);

            if (!roleRewards.Any())
            {
                await ReplyConfirmAsync(Strings.RepNoRoleRewards(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepRoleRewardsList(ctx.Guild.Id))
                .WithDescription(Strings.RepRoleRewardsDesc(ctx.Guild.Id));

            var content = new StringBuilder();

            foreach (var reward in roleRewards.Take(detailed ? 25 : 10))
            {
                var role = ctx.Guild.GetRole(reward.RoleId);
                var roleName = role?.Name ?? $"Unknown Role ({reward.RoleId})";

                if (detailed)
                {
                    content.AppendLine($"**{roleName}** - {reward.RepRequired} rep");

                    if (reward.RemoveOnDrop)
                        content.AppendLine($"  └ Remove when rep drops below threshold");

                    if (reward.AnnounceChannel.HasValue)
                    {
                        var channel = await ctx.Guild.GetTextChannelAsync(reward.AnnounceChannel.Value);
                        var channelName = channel?.Name ?? "Unknown Channel";
                        content.AppendLine($"  └ Announce in #{channelName}");
                    }

                    if (reward.AnnounceDM)
                        content.AppendLine($"  └ Send DM notifications");

                    if (reward.XPReward is > 0)
                        content.AppendLine($"  └ XP Reward: {reward.XPReward.Value}");

                    content.AppendLine();
                }
                else
                {
                    content.AppendLine(
                        $"**{roleName}** - {Strings.RepRoleRequiredRep(ctx.Guild.Id)}: {reward.RepRequired}");

                    if (reward.XPReward is > 0)
                        content.AppendLine($"  └ {Strings.RepRoleXpReward(ctx.Guild.Id, reward.XPReward.Value)}");

                    if (reward.AnnounceChannel.HasValue)
                    {
                        var channel = await ctx.Guild.GetTextChannelAsync(reward.AnnounceChannel.Value);
                        var channelName = channel?.Name ?? "Unknown Channel";
                        content.AppendLine($"  └ {Strings.RepRoleAnnounceChannel(ctx.Guild.Id, channelName)}");
                    }

                    content.AppendLine();
                }
            }


            eb.AddField("Role Rewards", content.ToString());
            await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
        }

        /// <summary>
        ///     Helper method to show detailed information about a specific role reward.
        /// </summary>
        /// <param name="role">The role to show information for.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task ShowRoleRewardInfo(IRole role)
        {
            var roleReward = await Service.GetRoleRewardAsync(ctx.Guild.Id, role.Id);

            if (roleReward == null)
            {
                await ReplyErrorAsync(Strings.RepRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepRoleInfoTitle(ctx.Guild.Id, role.Name))
                .AddField(Strings.RepRequired(ctx.Guild.Id), roleReward.RepRequired, true)
                .AddField(Strings.RepRemoveOnDrop(ctx.Guild.Id), roleReward.RemoveOnDrop ? "Yes" : "No", true);

            if (roleReward.AnnounceChannel.HasValue)
            {
                var channel = await ctx.Guild.GetTextChannelAsync(roleReward.AnnounceChannel.Value);
                var channelName = channel?.Mention ?? "Unknown Channel";
                eb.AddField(Strings.RepAnnounceChannel(ctx.Guild.Id), channelName, true);
            }

            eb.AddField(Strings.RepAnnounceDm(ctx.Guild.Id), roleReward.AnnounceDM ? "Yes" : "No", true);

            if (roleReward.XPReward is > 0)
            {
                eb.AddField(Strings.RepXpReward(ctx.Guild.Id), roleReward.XPReward.Value, true);
            }

            if (roleReward.DateAdded.HasValue)
            {
                eb.AddField(Strings.RepDateAdded(ctx.Guild.Id), $"{roleReward.DateAdded.Value:yyyy-MM-dd HH:mm} UTC",
                    true);
            }

            await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
        }

        #endregion

        #region Command Requirements

        /// <summary>
        ///     Sets reputation requirements for a command, optionally restricted to specific channels.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <param name="minReputation">The minimum reputation required.</param>
        /// <param name="repType">The specific reputation type required (optional, defaults to total).</param>
        /// <param name="channels">Optional channels where this requirement applies (if none specified, applies globally).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepCommandReq(string commandName, int minReputation, string? repType = null,
            params ITextChannel[] channels)
        {
            if (minReputation < 0)
            {
                await ReplyErrorAsync(Strings.RepAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            // Validate reputation type if provided
            if (!string.IsNullOrEmpty(repType) && repType != "total" &&
                !await Service.IsValidReputationTypeAsync(ctx.Guild.Id, repType))
            {
                await ReplyErrorAsync(Strings.RepInvalidType(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            string? channelIdJson = null;
            var actualRepType = repType == "total" ? null : repType;

            // Handle channel restrictions if provided
            if (channels.Length > 0)
            {
                var channelIds = channels.Select(c => c.Id).ToList();
                channelIdJson = JsonConvert.SerializeObject(channelIds);
            }

            await commandRequirementsService.AddCommandRequirementAsync(ctx.Guild.Id, commandName.ToLowerInvariant(),
                minReputation, actualRepType, channelIdJson).ConfigureAwait(false);

            if (channels.Length > 0)
            {
                var channelNames = string.Join(", ", channels.Select(c => $"#{c.Name}"));
                await SuccessAsync(Strings.RepCommandRequirementAddedChannels(ctx.Guild.Id,
                    commandName, minReputation, repType ?? "total", channelNames)).ConfigureAwait(false);
            }
            else
            {
                await SuccessAsync(Strings.RepCommandRequirementAdded(ctx.Guild.Id,
                    commandName, minReputation, repType ?? "total")).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Sets bypass roles for a command requirement.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <param name="roles">The roles that can bypass the requirement.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepCommandBypass(string commandName, params IRole[] roles)
        {
            if (roles.Length == 0)
            {
                await ReplyErrorAsync(Strings.RepNoRolesSpecified(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var roleIds = roles.Select(r => r.Id).ToList();
            var roleIdJson = JsonConvert.SerializeObject(roleIds);

            // Get existing requirement
            var requirement =
                await commandRequirementsService.GetCommandRequirementAsync(ctx.Guild.Id,
                    commandName.ToLowerInvariant());

            if (requirement == null)
            {
                await ReplyErrorAsync(Strings.RepCommandRequirementNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            // Update with bypass roles
            await commandRequirementsService.AddCommandRequirementAsync(ctx.Guild.Id, commandName.ToLowerInvariant(),
                requirement.MinReputation, requirement.RequiredRepType, requirement.RestrictedChannels,
                requirement.DenialMessage, roleIdJson, requirement.ShowInHelp);

            var roleNames = string.Join(", ", roles.Select(r => r.Name));
            await SuccessAsync(Strings.RepCommandBypassAdded(ctx.Guild.Id, commandName, roleNames))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes reputation requirements from a command.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepCommandReqRemove(string commandName)
        {
            var deleted = await commandRequirementsService
                .RemoveCommandRequirementAsync(ctx.Guild.Id, commandName.ToLowerInvariant())
                .ConfigureAwait(false);

            if (deleted > 0)
                await SuccessAsync(Strings.RepCommandRequirementRemoved(ctx.Guild.Id, commandName))
                    .ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.RepCommandRequirementNotFound(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all command requirements for the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepCommandReqList()
        {
            var requirements = await commandRequirementsService.GetCommandRequirementsAsync(ctx.Guild.Id)
                .ConfigureAwait(false);

            if (!requirements.Any())
            {
                await ReplyConfirmAsync(Strings.RepNoCommandRequirements(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepCommandRequirementsList(ctx.Guild.Id))
                .WithDescription(Strings.RepCommandRequirementsDesc(ctx.Guild.Id));

            foreach (var req in requirements.Take(15))
            {
                var value = $"**Min Rep:** {req.MinReputation} {req.RequiredRepType ?? "total"}";

                if (!string.IsNullOrEmpty(req.RestrictedChannels))
                {
                    try
                    {
                        var channelIds = JsonConvert.DeserializeObject<List<ulong>>(req.RestrictedChannels);
                        if (channelIds?.Any() == true)
                        {
                            var channelNames = new List<string>();
                            foreach (var channelId in channelIds)
                            {
                                var channel = await ctx.Guild.GetChannelAsync(channelId);
                                if (channel != null) channelNames.Add($"#{channel.Name}");
                            }

                            if (channelNames.Any())
                                value += $"\n**Channels:** {string.Join(", ", channelNames)}";
                        }
                    }
                    catch
                    {
                        // Ignore JSON parsing errors
                    }
                }

                embed.AddField($".{req.CommandName}", value, true);
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows reputation requirements for a specific command.
        /// </summary>
        /// <param name="commandName">The command to check.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task RepCommandInfo(string commandName)
        {
            var requirement =
                await commandRequirementsService.GetCommandRequirementAsync(ctx.Guild.Id,
                    commandName.ToLowerInvariant());

            if (requirement == null)
            {
                await ReplyConfirmAsync(Strings.RepCommandNoRequirements(ctx.Guild.Id, commandName))
                    .ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepCommandRequirementInfo(ctx.Guild.Id, commandName))
                .AddField(Strings.RepMinReputation(ctx.Guild.Id),
                    $"{requirement.MinReputation} {requirement.RequiredRepType ?? "total"}", true)
                .AddField(Strings.RepShowInHelp(ctx.Guild.Id), requirement.ShowInHelp ? "✅" : "❌", true);

            if (!string.IsNullOrEmpty(requirement.RestrictedChannels))
            {
                try
                {
                    var channelIds = JsonConvert.DeserializeObject<List<ulong>>(requirement.RestrictedChannels);
                    var channelNames = new List<string>();

                    foreach (var channelId in channelIds ?? [])
                    {
                        var channel = await ctx.Guild.GetChannelAsync(channelId);
                        if (channel != null) channelNames.Add($"#{channel.Name}");
                    }

                    if (channelNames.Any())
                        embed.AddField(Strings.RepRestrictedChannels(ctx.Guild.Id), string.Join(", ", channelNames));
                }
                catch
                {
                    // Ignore JSON parsing errors
                }
            }

            if (!string.IsNullOrEmpty(requirement.DenialMessage))
                embed.AddField(Strings.RepDenialMessage(ctx.Guild.Id), requirement.DenialMessage);

            if (!string.IsNullOrEmpty(requirement.BypassRoles))
            {
                try
                {
                    var roleIds = JsonConvert.DeserializeObject<List<ulong>>(requirement.BypassRoles);
                    var roleNames = new List<string>();

                    foreach (var roleId in roleIds ?? [])
                    {
                        var role = ctx.Guild.GetRole(roleId);
                        if (role != null) roleNames.Add(role.Name);
                    }

                    if (roleNames.Any())
                        embed.AddField(Strings.RepBypassRoles(ctx.Guild.Id), string.Join(", ", roleNames));
                }
                catch
                {
                    // Ignore JSON parsing errors
                }
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        #endregion

        #region Custom Reputation Types

        /// <summary>
        ///     Adds a custom reputation type.
        /// </summary>
        /// <param name="typeName">The name of the reputation type.</param>
        /// <param name="displayName">The display name for the type.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepTypeAdd(string typeName, [Remainder] string displayName)
        {
            var added = await Service.AddCustomTypeAsync(ctx.Guild.Id, typeName, displayName);

            if (added)
            {
                await SuccessAsync(Strings.RepTypeAdded(ctx.Guild.Id, typeName, displayName)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepTypeExists(ctx.Guild.Id, typeName)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a custom reputation type.
        /// </summary>
        /// <param name="typeName">The name of the reputation type to remove.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepTypeRemove(string typeName)
        {
            var removed = await Service.RemoveCustomTypeAsync(ctx.Guild.Id, typeName);

            if (removed)
            {
                await SuccessAsync(Strings.RepTypeRemoved(ctx.Guild.Id, typeName)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepTypeNotFound(ctx.Guild.Id, typeName)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all custom reputation types for this server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepTypeList()
        {
            var customTypes = await Service.GetCustomTypesAsync(ctx.Guild.Id);

            if (!customTypes.Any())
            {
                await ReplyErrorAsync(Strings.RepTypeListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepTypeListTitle(ctx.Guild.Id));

            var description = string.Join("\n", customTypes.Select(x => $"**{x.TypeName}** - {x.DisplayName}"));
            eb.WithDescription(description);

            await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the announcement channel for a role reward.
        /// </summary>
        /// <param name="role">The role to configure.</param>
        /// <param name="channel">The channel for announcements (null to disable).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepRoleChannel(IRole role, ITextChannel? channel = null)
        {
            var updated = await Service.UpdateRoleRewardChannelAsync(ctx.Guild.Id, role.Id, channel?.Id);

            if (updated)
            {
                var message = channel != null
                    ? Strings.RepRoleChannelSet(ctx.Guild.Id, role.Name, channel.Name)
                    : Strings.RepRoleChannelDisabled(ctx.Guild.Id, role.Name);

                await SuccessAsync(message).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        #endregion

        #region Reputation Events

        /// <summary>
        ///     Creates a reputation multiplier event.
        /// </summary>
        /// <param name="name">Name of the event.</param>
        /// <param name="multiplier">Reputation multiplier.</param>
        /// <param name="duration">Duration in hours.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepEventCreate(string name, decimal multiplier, int duration)
        {
            var endTime = await Service.CreateEventAsync(ctx.Guild.Id, name, multiplier, duration);
            await SuccessAsync(Strings.RepEventCreated(ctx.Guild.Id, name, multiplier, endTime)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Ends a reputation event early.
        /// </summary>
        /// <param name="name">Name of the event to end.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepEventEnd(string name)
        {
            var ended = await Service.EndEventAsync(ctx.Guild.Id, name);

            if (ended)
            {
                await SuccessAsync(Strings.RepEventEnded(ctx.Guild.Id, name)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        #endregion
    }
}