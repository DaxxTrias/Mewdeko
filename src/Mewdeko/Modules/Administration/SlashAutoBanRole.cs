using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class SlashAdministration
{
    /// <summary>
    ///     Slash command group for managing the AutoBanRole feature.
    /// </summary>
    [Group("autobanrole", "Allows you to set or remove a role from autobanning a user when they add it.")]
    public class SlashAutoBanRole : MewdekoSlashSubmodule<AutoBanRoleService>
    {
        /// <summary>
        ///     Lists all roles in the AutoBanRole list for the current guild.
        /// </summary>
        [SlashCommand("list", "List all roles that trigger auto-ban")]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AutoBanRoleList()
        {
            var roles = await Service.GetAutoBanRoles(Context.Guild.Id);

            if (roles.Count == 0)
            {
                await ReplyErrorAsync("No auto-ban roles configured for this server.").ConfigureAwait(false);
                return;
            }

            var roleList = (from roleId in roles
                let role = Context.Guild.GetRole(roleId)
                select role != null ? $"• {role.Mention} (`{role.Id}`)" : $"• Deleted Role (`{roleId}`)").ToList();

            var embed = new EmbedBuilder()
                .WithTitle(Strings.AutoBanRolesTitle(Context.Guild.Id))
                .WithDescription(string.Join("\n", roleList))
                .WithColor(Mewdeko.ErrorColor)
                .WithFooter($"Total: {roleList.Count} role(s)")
                .Build();

            await RespondAsync(embed: embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a role to the list of AutoBanRoles.
        /// </summary>
        /// <param name="role">The role to add</param>
        [SlashCommand("add", "Add a role to the list of AutoBanRoles")]
        [SlashUserPerm(GuildPermission.Administrator)]
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
        [SlashCommand("remove", "Remove a role from the list of AutoBanRoles")]
        [SlashUserPerm(GuildPermission.Administrator)]
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