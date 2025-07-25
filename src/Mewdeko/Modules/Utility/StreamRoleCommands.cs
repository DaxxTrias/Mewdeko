﻿using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Contains commands related to stream roles in Discord servers.
    /// </summary>
    public class StreamRoleCommands : MewdekoSubmodule<StreamRoleService>
    {
        /// <summary>
        ///     Sets a stream role for users in the server. When users start streaming, they are automatically assigned a specific
        ///     role.
        /// </summary>
        /// <param name="fromRole">The role to monitor for streaming activity.</param>
        /// <param name="addRole">The role to assign to users who start streaming.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [BotPerm(GuildPermission.ManageRoles)]
        [UserPerm(GuildPermission.ManageRoles)]
        [RequireContext(ContextType.Guild)]
        public async Task StreamRole(IRole fromRole, IRole addRole)
        {
            await Service.SetStreamRole(fromRole, addRole).ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.StreamRoleEnabled(ctx.Guild.Id, Format.Bold(fromRole.ToString()),
                Format.Bold(addRole.ToString()))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Disables the stream role feature in the server.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [BotPerm(GuildPermission.ManageRoles)]
        [UserPerm(GuildPermission.ManageRoles)]
        [RequireContext(ContextType.Guild)]
        public async Task StreamRole()
        {
            await Service.StopStreamRole(ctx.Guild).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.StreamRoleDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a keyword for the stream role feature. Only users with streams containing this keyword will receive the stream
        ///     role.
        /// </summary>
        /// <param name="keyword">The keyword to set for the stream role feature.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [BotPerm(GuildPermission.ManageRoles)]
        [UserPerm(GuildPermission.ManageRoles)]
        [RequireContext(ContextType.Guild)]
        public async Task StreamRoleKeyword([Remainder] string? keyword = null)
        {
            var kw = await Service.SetKeyword(ctx.Guild, keyword).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(keyword))
                await ReplyConfirmAsync(Strings.StreamRoleKwReset(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.StreamRoleKwSet(ctx.Guild.Id, Format.Bold(kw))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds or removes a user to/from the blacklist for the stream role feature.
        /// </summary>
        /// <param name="action">The action to perform (add or remove).</param>
        /// <param name="user">The user to add or remove from the list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [BotPerm(GuildPermission.ManageRoles)]
        [UserPerm(GuildPermission.ManageRoles)]
        [RequireContext(ContextType.Guild)]
        public async Task StreamRoleBlacklist(AddRemove action, [Remainder] IGuildUser user)
        {
            var success = await Service
                .ApplyListAction(StreamRoleListType.Blacklist, ctx.Guild, action, user.Id, user.ToString())
                .ConfigureAwait(false);

            if (action == AddRemove.Add)
            {
                if (success)
                {
                    await ReplyConfirmAsync(Strings.StreamRoleBlAdd(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmAsync(Strings.StreamRoleBlAddFail(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                }
            }
            else if (success)
            {
                await ReplyConfirmAsync(Strings.StreamRoleBlRem(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.StreamRoleBlRemFail(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Adds or removes a user to/from the whitelist for the stream role feature.
        /// </summary>
        /// <param name="action">The action to perform (add or remove).</param>
        /// <param name="user">The user to add or remove from the list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [BotPerm(GuildPermission.ManageRoles)]
        [UserPerm(GuildPermission.ManageRoles)]
        [RequireContext(ContextType.Guild)]
        public async Task StreamRoleWhitelist(AddRemove action, [Remainder] IGuildUser user)
        {
            var success = await Service
                .ApplyListAction(StreamRoleListType.Whitelist, ctx.Guild, action, user.Id, user.ToString())
                .ConfigureAwait(false);

            if (action == AddRemove.Add)
            {
                if (success)
                {
                    await ReplyConfirmAsync(Strings.StreamRoleWlAdd(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmAsync(Strings.StreamRoleWlAddFail(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                }
            }
            else if (success)
            {
                await ReplyConfirmAsync(Strings.StreamRoleWlRem(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.StreamRoleWlRemFail(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
        }
    }
}