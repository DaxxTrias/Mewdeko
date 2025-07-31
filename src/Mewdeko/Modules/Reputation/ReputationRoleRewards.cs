using System.Text;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Reputation.Services;

namespace Mewdeko.Modules.Reputation;

public partial class Reputation
{
    /// <summary>
    ///     Commands for managing reputation-based role rewards and milestones.
    /// </summary>
    public class ReputationRoleRewards : MewdekoSubmodule<RepService>
    {
        /// <summary>
        ///     Adds a role reward for reaching a specific reputation milestone.
        /// </summary>
        /// <param name="role">The role to award.</param>
        /// <param name="reputation">The reputation required to earn the role.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RepRoleAdd(IRole role, int reputation)
        {
            if (reputation < 0)
            {
                await ReplyErrorAsync(Strings.RepAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var currentUser = await ctx.Guild.GetCurrentUserAsync();
            if (role.Position >= currentUser.Hierarchy)
            {
                await ReplyErrorAsync(Strings.RepRoleHierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var isNew = await Service.AddOrUpdateRoleRewardAsync(ctx.Guild.Id, role.Id, reputation);

            if (isNew)
            {
                await SuccessAsync(Strings.RepRoleAdded(ctx.Guild.Id, role.Name, reputation)).ConfigureAwait(false);
            }
            else
            {
                await SuccessAsync(Strings.RepRoleUpdated(ctx.Guild.Id, role.Name, reputation)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Adds a role reward with advanced configuration options.
        /// </summary>
        /// <param name="role">The role to award.</param>
        /// <param name="reputation">The reputation required to earn the role.</param>
        /// <param name="removeOnDrop">Whether to remove the role if reputation drops.</param>
        /// <param name="announceChannel">Optional channel to announce role awards.</param>
        /// <param name="announceDm">Whether to send DM notifications.</param>
        /// <param name="xpReward">Optional XP reward for reaching milestone.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RepRoleAdvanced(IRole role, int reputation, bool removeOnDrop = true,
            ITextChannel? announceChannel = null, bool announceDm = false, int? xpReward = null)
        {
            if (reputation < 0)
            {
                await ReplyErrorAsync(Strings.RepAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var currentUser = await ctx.Guild.GetCurrentUserAsync();
            if (role.Position >= currentUser.Hierarchy)
            {
                await ReplyErrorAsync(Strings.RepRoleHierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var isNew = await Service.AddOrUpdateRoleRewardAsync(ctx.Guild.Id, role.Id, reputation,
                removeOnDrop, announceChannel?.Id, announceDm, xpReward);

            if (isNew)
            {
                await SuccessAsync(Strings.RepRoleAdded(ctx.Guild.Id, role.Name, reputation)).ConfigureAwait(false);
            }
            else
            {
                await SuccessAsync(Strings.RepRoleUpdated(ctx.Guild.Id, role.Name, reputation)).ConfigureAwait(false);
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
        ///     Lists all configured role rewards for the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task RepRoleList()
        {
            var roleRewards = await Service.GetRoleRewardsAsync(ctx.Guild.Id);

            if (!roleRewards.Any())
            {
                await ReplyConfirmAsync(Strings.RepNoRoleRewards(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepRoleRewardsList(ctx.Guild.Id));

            var description = new StringBuilder();

            foreach (var reward in roleRewards)
            {
                var role = ctx.Guild.GetRole(reward.RoleId);
                var roleName = role?.Name ?? $"Unknown Role ({reward.RoleId})";

                description.AppendLine($"**{roleName}** - {reward.RepRequired} rep");

                if (reward.RemoveOnDrop)
                    description.AppendLine($"  └ {Strings.RepRemoveOnDrop(ctx.Guild.Id)}");

                if (reward.AnnounceChannel.HasValue)
                {
                    var channel = await ctx.Guild.GetTextChannelAsync(reward.AnnounceChannel.Value);
                    var channelName = channel?.Name ?? "Unknown Channel";
                    description.AppendLine($"  └ {Strings.RepRoleAnnounceChannel(ctx.Guild.Id, channelName)}");
                }

                if (reward.AnnounceDM)
                    description.AppendLine($"  └ {Strings.RepAnnounceDm(ctx.Guild.Id)}");

                if (reward.XPReward is > 0)
                    description.AppendLine($"  └ {Strings.RepRoleXpReward(ctx.Guild.Id, reward.XPReward.Value)}");

                description.AppendLine();
            }

            eb.WithDescription(description.ToString());
            await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows detailed information about a specific role reward.
        /// </summary>
        /// <param name="role">The role to show information for.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task RepRoleInfo(IRole role)
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
                .AddField(Strings.RepRoleRequiredRep(ctx.Guild.Id), roleReward.RepRequired, true)
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
                eb.AddField(Strings.RepDateAdded(ctx.Guild.Id),
                    $"{roleReward.DateAdded.Value:yyyy-MM-dd HH:mm} UTC", true);
            }

            await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
        }
    }
}