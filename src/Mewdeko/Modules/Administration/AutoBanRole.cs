using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Command group for managing the AutoBanRole feature.
    /// </summary>
    public class AutoBanRole : MewdekoSubmodule<AutoBanRoleService>
    {
        /// <summary>
        ///     Lists all roles in the AutoBanRole list for the current guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutoBanRoleList()
        {
            var roles = await Service.GetAutoBanRoles(Context.Guild.Id);

            if (!roles.Any())
            {
                await ReplyErrorAsync("No auto-ban roles configured for this server.").ConfigureAwait(false);
                return;
            }

            var roleList = new List<string>();
            foreach (var roleId in roles)
            {
                var role = Context.Guild.GetRole(roleId);
                if (role != null)
                {
                    roleList.Add($"• {role.Mention} (`{role.Id}`)");
                }
                else
                {
                    roleList.Add($"• Deleted Role (`{roleId}`)");
                }
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.AutoBanRolesTitle(Context.Guild.Id))
                .WithDescription(string.Join("\n", roleList))
                .WithColor(Mewdeko.ErrorColor)
                .WithFooter($"Total: {roleList.Count} role(s)")
                .Build();

            await Context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a role to the list of AutoBanRoles.
        /// </summary>
        /// <param name="role">The role to add</param>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutoBanRoleAdd(IRole role)
        {
            var success = await Service.AddAutoBanRole(Context.Guild.Id, role.Id);
            if (success)
            {
                await ReplyConfirmAsync(Strings.AbroleAdd(Context.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AbroleExists(Context.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a role from the list of AutoBanRoles.
        /// </summary>
        /// <param name="role">The role to remove</param>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutoBanRoleRemove(IRole role)
        {
            var success = await Service.RemoveAutoBanRole(Context.Guild.Id, role.Id);
            if (success)
            {
                await ReplyConfirmAsync(Strings.AbroleRemove(Context.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AbroleNotexists(Context.Guild.Id, role.Mention)).ConfigureAwait(false);
            }
        }
    }
}