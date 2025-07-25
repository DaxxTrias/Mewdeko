﻿using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for managing auto-assign roles.
    /// </summary>
    [Group]
    public class AutoAssignRoleCommands : MewdekoSubmodule<AutoAssignRoleService>
    {
        /// <summary>
        ///     Enables or disables auto-assigning the specified role to users when they join the guild.
        /// </summary>
        /// <param name="role">The role to enable or disable auto-assigning</param>
        /// <remarks>
        ///     This command requires the caller to have GuildPermission.ManageRoles.
        /// </remarks>
        /// <example>.autoassignrole RoleName</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        public async Task AutoAssignRole([Remainder] IRole role)
        {
            var guser = (IGuildUser)ctx.User;
            if (role.Id == ctx.Guild.EveryoneRole.Id)
                return;

            // The user can't auto-assign the role which is higher or equal to their highest role.
            if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var roles = await Service.ToggleAarAsync(ctx.Guild.Id, role.Id).ConfigureAwait(false);
            if (roles.Count == 0)
                await ReplyConfirmAsync(Strings.AarDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            else if (roles.Contains(role.Id))
                await AutoAssignRole().ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.AarRoleRemoved(ctx.Guild.Id, Format.Bold(role.Mention)))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Displays the list of roles eligible for auto-assigning when users join the guild.
        /// </summary>
        /// <remarks>
        ///     This command requires the caller to have GuildPermission.ManageRoles.
        /// </remarks>
        /// <example>.autoassignrole</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        public async Task AutoAssignRole()
        {
            var roles = await Service.TryGetNormalRoles(ctx.Guild.Id);
            if (!roles.Any())
            {
                await ReplyConfirmAsync(Strings.AarNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var existing = roles.Select(rid => ctx.Guild.GetRole(rid)).Where(r => r is not null)
                .ToList();

            if (existing.Count != roles.Count())
                await Service.SetAarRolesAsync(ctx.Guild.Id, existing.Select(x => x.Id)).ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.AarRoles(ctx.Guild.Id,
                $"\n{existing.Select(x => Format.Bold(x.Mention)).JoinWith("\n")}")).ConfigureAwait(false);
        }


        /// <summary>
        ///     Enables or disables auto-assigning the specified role to bots when they join the guild.
        /// </summary>
        /// <param name="role">The role to enable or disable auto-assigning</param>
        /// <remarks>
        ///     This command requires the caller to have GuildPermission.ManageRoles.
        /// </remarks>
        /// <example>.autoassignbotrole RoleName</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        public async Task AutoAssignBotRole([Remainder] IRole role)
        {
            var guser = (IGuildUser)ctx.User;
            if (role.Id == ctx.Guild.EveryoneRole.Id)
                return;

            // The user can't auto-assign the role which is higher or equal to their highest role.
            if (ctx.User.Id != guser.Guild.OwnerId && guser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var roles = await Service.ToggleAabrAsync(ctx.Guild.Id, role.Id).ConfigureAwait(false);
            if (roles.Count == 0)
                await ReplyConfirmAsync(Strings.AabrDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            else if (roles.Contains(role.Id))
                await AutoAssignBotRole().ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.AabrRoleRemoved(ctx.Guild.Id, Format.Bold(role.Mention)))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Displays the list of roles eligible for auto-assigning to bots when they join the guild.
        /// </summary>
        /// <remarks>
        ///     This command requires the caller to have GuildPermission.ManageRoles.
        /// </remarks>
        /// <example>.autoassignbotrole</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        public async Task AutoAssignBotRole()
        {
            var roles = await Service.TryGetBotRoles(ctx.Guild.Id);
            if (!roles.Any())
            {
                await ReplyConfirmAsync(Strings.AabrNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var existing = roles.Select(rid => ctx.Guild.GetRole(rid)).Where(r => r is not null)
                .ToList();

            if (existing.Count != roles.Count())
                await Service.SetAabrRolesAsync(ctx.Guild.Id, existing.Select(x => x.Id)).ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.AabrRoles(ctx.Guild.Id,
                $"\n{existing.Select(x => Format.Bold(x.Mention)).JoinWith("\n")}")).ConfigureAwait(false);
        }
    }
}