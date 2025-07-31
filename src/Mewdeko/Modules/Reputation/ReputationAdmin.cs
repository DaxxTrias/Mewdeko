using DataModel;
using Discord.Commands;
using LinqToDB;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Reputation.Services;

namespace Mewdeko.Modules.Reputation;

public partial class Reputation
{
    /// <summary>
    ///     Administrative commands for managing the reputation system.
    /// </summary>
    public class ReputationAdmin : MewdekoSubmodule<RepService>
    {
        private readonly IDataConnectionFactory dbFactory;
        //private readonly RepEventService eventService;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ReputationAdmin" /> class.
        /// </summary>
        /// <param name="dbFactory">The database connection factory.</param>
        public ReputationAdmin(IDataConnectionFactory dbFactory)
        {
            this.dbFactory = dbFactory;
        }

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

            await using var db = await dbFactory.CreateConnectionAsync();
            var userRep = await db.UserReputations
                .Where(x => x.UserId == user.Id && x.GuildId == ctx.Guild.Id)
                .FirstOrDefaultAsync();

            if (userRep == null)
            {
                await ReplyErrorAsync(Strings.RepNoRepToRemove(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            userRep.TotalRep = Math.Max(0, userRep.TotalRep - amount);
            await db.UpdateAsync(userRep);

            // Add to history
            var historyEntry = new RepHistory
            {
                GiverId = ctx.User.Id,
                ReceiverId = user.Id,
                GuildId = ctx.Guild.Id,
                ChannelId = ctx.Channel.Id,
                Amount = -amount,
                RepType = "admin_take",
                Reason = $"Admin removal by {ctx.User}",
                IsAnonymous = false,
                Timestamp = DateTime.UtcNow
            };
            await db.InsertAsync(historyEntry);

            await SuccessAsync(Strings.RepTaken(ctx.Guild.Id, amount, user.Mention, userRep.TotalRep))
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

            await using var db = await dbFactory.CreateConnectionAsync();
            var userRep = await db.UserReputations
                .Where(x => x.UserId == user.Id && x.GuildId == ctx.Guild.Id)
                .FirstOrDefaultAsync();

            if (userRep == null)
            {
                userRep = new UserReputation
                {
                    UserId = user.Id, GuildId = ctx.Guild.Id, TotalRep = amount, DateAdded = DateTime.UtcNow
                };
                await db.InsertAsync(userRep);
            }
            else
            {
                userRep.TotalRep = amount;
                await db.UpdateAsync(userRep);
            }

            // Add to history
            var historyEntry = new RepHistory
            {
                GiverId = ctx.User.Id,
                ReceiverId = user.Id,
                GuildId = ctx.Guild.Id,
                ChannelId = ctx.Channel.Id,
                Amount = amount,
                RepType = "admin_set",
                Reason = $"Admin set by {ctx.User}",
                IsAnonymous = false,
                Timestamp = DateTime.UtcNow
            };
            await db.InsertAsync(historyEntry);

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
            await using var db = await dbFactory.CreateConnectionAsync();

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

                await db.UserReputations.Where(x => x.GuildId == ctx.Guild.Id).DeleteAsync();
                await db.RepHistories.Where(x => x.GuildId == ctx.Guild.Id).DeleteAsync();
                await db.RepCooldowns.Where(x => x.GuildId == ctx.Guild.Id).DeleteAsync();
                await db.RepBadges.Where(x => x.GuildId == ctx.Guild.Id).DeleteAsync();

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

                await db.UserReputations.Where(x => x.UserId == user.Id && x.GuildId == ctx.Guild.Id).DeleteAsync();
                await db.RepHistories.Where(x => x.ReceiverId == user.Id && x.GuildId == ctx.Guild.Id).DeleteAsync();
                await db.RepCooldowns.Where(x => x.ReceiverId == user.Id && x.GuildId == ctx.Guild.Id).DeleteAsync();
                await db.RepBadges.Where(x => x.UserId == user.Id && x.GuildId == ctx.Guild.Id).DeleteAsync();

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
            await using var db = await dbFactory.CreateConnectionAsync();
            var userRep = await db.UserReputations
                .Where(x => x.UserId == user.Id && x.GuildId == ctx.Guild.Id)
                .FirstOrDefaultAsync();

            if (userRep == null)
            {
                userRep = new UserReputation
                {
                    UserId = user.Id, GuildId = ctx.Guild.Id, IsFrozen = true, DateAdded = DateTime.UtcNow
                };
                await db.InsertAsync(userRep);
            }
            else
            {
                userRep.IsFrozen = true;
                await db.UpdateAsync(userRep);
            }

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
            await using var db = await dbFactory.CreateConnectionAsync();
            var userRep = await db.UserReputations
                .Where(x => x.UserId == user.Id && x.GuildId == ctx.Guild.Id)
                .FirstOrDefaultAsync();

            if (userRep == null)
            {
                await ReplyErrorAsync(Strings.RepNoData(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            userRep.IsFrozen = false;
            await db.UpdateAsync(userRep);

            await SuccessAsync(Strings.RepUnfrozenUser(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows the current reputation configuration for the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepConfig()
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var config = await db.RepConfigs.FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id) ?? new RepConfig
            {
                GuildId = ctx.Guild.Id
            };

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepConfigTitle(ctx.Guild.Id))
                .AddField(Strings.RepConfigEnabled(ctx.Guild.Id, config.Enabled), config.Enabled ? "✅" : "❌", true)
                .AddField(Strings.RepConfigCooldown(ctx.Guild.Id, config.DefaultCooldownMinutes),
                    config.DefaultCooldownMinutes, true)
                .AddField(Strings.RepConfigDailyLimit(ctx.Guild.Id, config.DailyLimit), config.DailyLimit, true)
                .AddField(Strings.RepConfigAccountAge(ctx.Guild.Id, config.MinAccountAgeDays), config.MinAccountAgeDays,
                    true)
                .AddField(Strings.RepConfigMembership(ctx.Guild.Id, config.MinServerMembershipHours),
                    config.MinServerMembershipHours, true)
                .AddField(Strings.RepConfigMessages(ctx.Guild.Id, config.MinMessageCount), config.MinMessageCount,
                    true);

            await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
        }

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

            await using var db = await dbFactory.CreateConnectionAsync();
            var channelConfig = await db.RepChannelConfigs
                .Where(x => x.GuildId == ctx.Guild.Id && x.ChannelId == channel.Id)
                .FirstOrDefaultAsync();

            if (channelConfig == null)
            {
                channelConfig = new RepChannelConfig
                {
                    GuildId = ctx.Guild.Id,
                    ChannelId = channel.Id,
                    State = state.ToLower(),
                    Multiplier = multiplier,
                    DateAdded = DateTime.UtcNow
                };
                await db.InsertAsync(channelConfig);
            }
            else
            {
                channelConfig.State = state.ToLower();
                channelConfig.Multiplier = multiplier;
                await db.UpdateAsync(channelConfig);
            }

            var message = state.ToLower() switch
            {
                "enabled" => Strings.RepChannelEnabled(ctx.Guild.Id, channel.Mention),
                "disabled" => Strings.RepChannelDisabledSet(ctx.Guild.Id, channel.Mention),
                "readonly" => Strings.RepChannelReadonly(ctx.Guild.Id, channel.Mention),
                _ => "Configuration updated."
            };

            if (multiplier != 1.0m)
            {
                message += $"\n{Strings.RepChannelMultiplier(ctx.Guild.Id, channel.Mention, multiplier)}";
            }

            await SuccessAsync(message).ConfigureAwait(false);
        }

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

            await using var db = await dbFactory.CreateConnectionAsync();

            // Parse emoji
            ulong? emojiId = null;
            var emojiName = emoji;

            if (Emote.TryParse(emoji, out var customEmote))
            {
                emojiId = customEmote.Id;
                emojiName = customEmote.Name;
            }

            var existingConfig = await db.RepReactionConfigs
                .Where(x => x.GuildId == ctx.Guild.Id && x.EmojiName == emojiName && x.EmojiId == emojiId)
                .FirstOrDefaultAsync();

            if (existingConfig != null)
            {
                existingConfig.RepAmount = amount;
                existingConfig.RepType = repType.ToLower();
                existingConfig.IsEnabled = true;
                await db.UpdateAsync(existingConfig);
                await SuccessAsync(Strings.RepReactionUpdated(ctx.Guild.Id, emoji, amount, repType))
                    .ConfigureAwait(false);
            }
            else
            {
                var reactionConfig = new RepReactionConfig
                {
                    GuildId = ctx.Guild.Id,
                    EmojiName = emojiName,
                    EmojiId = emojiId,
                    RepAmount = amount,
                    RepType = repType.ToLower()
                };
                await db.InsertAsync(reactionConfig);
                await SuccessAsync(Strings.RepReactionAdded(ctx.Guild.Id, emoji, amount, repType))
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
            await using var db = await dbFactory.CreateConnectionAsync();

            // Parse emoji
            ulong? emojiId = null;
            var emojiName = emoji;

            if (Emote.TryParse(emoji, out var customEmote))
            {
                emojiId = customEmote.Id;
                emojiName = customEmote.Name;
            }

            var deleted = await db.RepReactionConfigs
                .Where(x => x.GuildId == ctx.Guild.Id && x.EmojiName == emojiName && x.EmojiId == emojiId)
                .DeleteAsync();

            if (deleted > 0)
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
            await using var db = await dbFactory.CreateConnectionAsync();
            var reactionConfigs = await db.RepReactionConfigs
                .Where(x => x.GuildId == ctx.Guild.Id)
                .ToListAsync();

            if (!reactionConfigs.Any())
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

        /// <summary>
        ///     Configures role rewards for reputation milestones.
        /// </summary>
        /// <param name="role">The role to reward.</param>
        /// <param name="reputation">The reputation required to earn this role.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepRole(IRole role, int reputation)
        {
            if (reputation < 0)
            {
                await ReplyErrorAsync(Strings.RepNegativeAmount(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await using var db = await dbFactory.CreateConnectionAsync();
            var existing = await db.RepRoleRewards
                .Where(x => x.GuildId == ctx.Guild.Id && x.RoleId == role.Id)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.RepRequired = reputation;
                await db.UpdateAsync(existing);
                await SuccessAsync(Strings.RepRoleRewardUpdated(ctx.Guild.Id, role.Mention, reputation))
                    .ConfigureAwait(false);
            }
            else
            {
                var roleReward = new RepRoleRewards
                {
                    GuildId = ctx.Guild.Id, RoleId = role.Id, RepRequired = reputation, DateAdded = DateTime.UtcNow
                };
                await db.InsertAsync(roleReward);
                await SuccessAsync(Strings.RepRoleRewardAdded(ctx.Guild.Id, role.Mention, reputation))
                    .ConfigureAwait(false);
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
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepRoleRemove(IRole role)
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var deleted = await db.RepRoleRewards
                .Where(x => x.GuildId == ctx.Guild.Id && x.RoleId == role.Id)
                .DeleteAsync();

            if (deleted > 0)
            {
                await SuccessAsync(Strings.RepRoleRewardRemoved(ctx.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }
}