using System.IO;
using System.Net.Http;
using System.Text;
using Discord.Commands;
using Discord.Net;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Server_Management.Services;
using Mewdeko.Services.Settings;
using Swan;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement
{
    /// <summary>
    ///     Provides commands for managing roles within a guild, including creation, deletion, synchronization, and user
    ///     assignment.
    /// </summary>
    [Group]
    public class RoleCommands(GuildSettingsService guildSettings, BotConfigService config)
        : MewdekoSubmodule<RoleCommandsService>
    {
        /// <summary>
        ///     Creates multiple roles within the guild based on a provided list of role names.
        /// </summary>
        /// <param name="roles">A space-separated list of role names to create.</param>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(ChannelPermission.ManageRoles)]
        public async Task CreateRoles([Remainder] string roles)
        {
            var roleList = roles.Split(" ");
            if (await PromptUserConfirmAsync(
                    $"Are you sure you want to create {roleList.Length} roles with these names?\n{string.Join("\n", roleList)}",
                    ctx.User.Id))
            {
                var msg = await ctx.Channel.SendConfirmAsync(
                    Strings.CreatingRoles(ctx.Guild.Id, config.Data.LoadingEmote, roleList.Length));
                foreach (var i in roleList)
                {
                    await ctx.Guild.CreateRoleAsync(i, null, null, false, false);
                }

                await msg.ModifyAsync(x =>
                {
                    x.Embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(Strings.RolesCreated(ctx.Guild.Id, config.Data.SuccessEmote, roleList.Length))
                        .Build();
                });
            }
        }

        /// <summary>
        ///     Synchronizes a role's permissions to all text channels and categories within the guild.
        /// </summary>
        /// <param name="role">The role to synchronize across the guild.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public async Task SyncRoleToAll(IRole role)
        {
            var ch = ctx.Channel as ITextChannel;
            var perms = ch.GetPermissionOverwrite(role);
            if (perms is null)
            {
                await ctx.Channel.SendErrorAsync(Strings.RoleNoPermsInChannel(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var msg = await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.LoadingEmote} Syncing permissions from {role.Mention} to {(await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count(x => x is not SocketThreadChannel)} Channels and {(await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count(x => x is not SocketThreadChannel)} Categories.....")
                .ConfigureAwait(false);
            foreach (var i in (await ctx.Guild.GetChannelsAsync().ConfigureAwait(false)).Where(x =>
                         x is not SocketThreadChannel or SocketVoiceChannel))
            {
                if (perms != null)
                    await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms).ConfigureAwait(false);
            }

            foreach (var i in await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false))
            {
                if (perms != null)
                    await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms).ConfigureAwait(false);
            }

            var eb = new EmbedBuilder
            {
                Color = Mewdeko.OkColor,
                Description =
                    Strings.SuccessfullySyncedPermsChannelsCategories(ctx.Guild.Id, role.Mention,
                        (await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count(x =>
                            x is not SocketThreadChannel),
                        (await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false)).Count)
            };
            await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Synchronizes a role's permissions to all text channels within the guild.
        /// </summary>
        /// <param name="role">The role to synchronize across text channels.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public async Task SyncRoleToAllChannels(IRole role)
        {
            var ch = ctx.Channel as ITextChannel;
            var perms = ch.GetPermissionOverwrite(role);
            if (perms is null)
            {
                await ctx.Channel.SendErrorAsync(Strings.RoleNoPermsInChannel(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var msg = await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.LoadingEmote} Syncing permissions from {role.Mention} to {(await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count(x => x is not SocketThreadChannel)} Channels.....")
                .ConfigureAwait(false);
            foreach (var i in (await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Where(x =>
                         x is not SocketThreadChannel))
            {
                if (perms != null)
                    await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms).ConfigureAwait(false);
            }

            var eb = new EmbedBuilder
            {
                Color = Mewdeko.OkColor,
                Description =
                    Strings.SuccessfullySyncedPermsChannels(ctx.Guild.Id, role.Mention,
                        (await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count(x =>
                            x is not SocketThreadChannel))
            };
            await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Synchronizes a role's permissions to all categories within the guild.
        /// </summary>
        /// <param name="role">The role to synchronize across categories.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public async Task SyncRoleToAllCategories(IRole role)
        {
            var ch = ctx.Channel as ITextChannel;
            var perms = ch.GetPermissionOverwrite(role);
            if (perms is null)
            {
                await ctx.Channel.SendErrorAsync(Strings.RoleNoPermsInChannel(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var msg = await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.LoadingEmote} Syncing permissions from {role.Mention} to {(await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false)).Count} Categories.....")
                .ConfigureAwait(false);
            foreach (var i in await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false))
            {
                if (perms != null)
                    await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms).ConfigureAwait(false);
            }

            var eb = new EmbedBuilder
            {
                Color = Mewdeko.OkColor,
                Description =
                    Strings.SuccessfullySyncedPermsCategories(ctx.Guild.Id, role.Mention,
                        (await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false)).Count)
            };
            await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes a list of roles from the guild.
        /// </summary>
        /// <param name="roles">An array of roles to delete.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task DeleteRoles(params IRole[] roles)
        {
            if (roles.Count(x => !x.IsManaged) is 0)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotDeleteManagedRoles(ctx.Guild.Id), Config
                    )
                    .ConfigureAwait(false);
                return;
            }

            var secondlist = new List<string>();
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            foreach (var i in roles.Where(x => !x.IsManaged))
            {
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= i.Position)
                {
                    await ctx.Channel.SendErrorAsync(Strings.CannotManageUser(ctx.Guild.Id, i.Mention), Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= i.Position)
                {
                    await ctx.Channel.SendErrorAsync(Strings.CannotManageMention(ctx.Guild.Id, i.Mention), Config)
                        .ConfigureAwait(false);
                    return;
                }

                secondlist.Add(
                    $"{i.Mention} - {(await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => x.RoleIds.Contains(i.Id))} Users");
            }

            var embed = new EmbedBuilder
            {
                Title = "Are you sure you want to delete these roles?", Description = $"{string.Join("\n", secondlist)}"
            };
            if (await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false))
            {
                var msg = await ctx.Channel
                    .SendConfirmAsync(Strings.DeletingRolesProgress(ctx.Guild.Id, config.Data.LoadingEmote,
                        roles.Length))
                    .ConfigureAwait(false);
                foreach (var i in roles) await i.DeleteAsync().ConfigureAwait(false);
                var newemb = new EmbedBuilder
                {
                    Description = $"Succesfully deleted {roles.Length} roles!", Color = Mewdeko.OkColor
                };
                await msg.ModifyAsync(x => x.Embed = newemb.Build()).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Stops a mass role operation job by its job number.
        /// </summary>
        /// <param name="jobnum">The job number of the mass role operation to stop.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task StopJob(int jobnum)
        {
            var list = Service.Jobslist
                .Find(x => x.JobId == jobnum && x.GuildId == ctx.Guild.Id);
            if (list == null)
            {
                await ctx.Channel.SendErrorAsync(
                        $"No job with that ID exists, please check the list again with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder
            {
                Color = Mewdeko.OkColor, Description = "Are you sure you want to stop this job?"
            };
            eb.AddField(list.JobType,
                $"Started by {list.StartedBy.Mention}\nProgress: {list.AddedTo}/{list.TotalUsers}");
            if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
            {
                var msg = await ctx.Channel.SendConfirmAsync(Strings.JobStopCancelled(ctx.Guild.Id));
                msg.DeleteAfter(5);
                return;
            }

            await Service.StopJob(ctx.Channel as ITextChannel, jobnum, ctx.Guild).ConfigureAwait(false);
        }

        /// <summary>
        ///     Modifies the roles of a user by adding new roles and/or removing existing ones.
        /// </summary>
        /// <param name="user">The user whose roles will be modified.</param>
        /// <param name="roles">The roles to be added to the user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task SetRoles(IGuildUser user, params IRole[] roles)
        {
            foreach (var i in roles)
            {
                if (ctx.User.Id != ctx.Guild.OwnerId &&
                    ((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= i.Position)
                {
                    await ctx.Channel.SendErrorAsync(Strings.CannotManageUser(ctx.Guild.Id, i.Mention), Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) > i.Position) continue;
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRoleMention(ctx.Guild.Id, i.Mention), Config)
                    .ConfigureAwait(false);
                return;
            }

            await user.AddRolesAsync(roles).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                    Strings.UserGivenRoles(ctx.Guild.Id, user, string.Join<string>("|", roles.Select(x => x.Mention))))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Assigns a specific role to a list of users, adding the role if they don't already have it.
        /// </summary>
        /// <param name="role">The role to be added to the users.</param>
        /// <param name="users">The users to whom the role will be added.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task AddUsersToRole(IRole role, params IUser[] users)
        {
            if (ctx.User.Id != ctx.Guild.OwnerId &&
                ((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            if (((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            foreach (var i in users.Select(x => x as IGuildUser))
            {
                await i.AddRoleAsync(role).ConfigureAwait(false);
            }

            await ctx.Channel.SendConfirmAsync(
                    Strings.RoleUsersAdded(ctx.Guild.Id, role.Mention,
                        string.Join<string>("|", users.Select(x => x.Mention))))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a specific role from a list of users, if they have the role.
        /// </summary>
        /// <param name="role">The role to be removed from the users.</param>
        /// <param name="users">The users from whom the role will be removed.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveUsersFromRole(IRole role, params IUser[] users)
        {
            if (ctx.User.Id != ctx.Guild.OwnerId &&
                ((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            if (((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            foreach (var i in users.Select(x => x as IGuildUser))
            {
                await i.AddRoleAsync(role).ConfigureAwait(false);
            }

            await ctx.Channel.SendConfirmAsync(
                    Strings.RoleUsersRemoved(ctx.Guild.Id, role.Mention,
                        string.Join<string>("|", users.Select(x => x.Mention))))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes specified roles from a user.
        /// </summary>
        /// <param name="user">The user from whom the roles will be removed.</param>
        /// <param name="roles">The roles to be removed from the user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveRoles(IGuildUser user, params IRole[] roles)
        {
            foreach (var i in roles)
            {
                if (ctx.User.Id != ctx.Guild.OwnerId &&
                    ((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= i.Position)
                {
                    await ctx.Channel.SendErrorAsync(Strings.CannotManageUser(ctx.Guild.Id, i.Mention), Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) > i.Position) continue;
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRoleMention(ctx.Guild.Id, i.Mention), Config)
                    .ConfigureAwait(false);
                return;
            }

            await user.RemoveRolesAsync(roles).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                    $"{user} {Strings.RemoveRoles(ctx.Guild.Id)}:\n{string.Join<string>("|", roles.Select(x => x.Mention))}")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all ongoing mass role operations within the server, providing details about each.
        /// </summary>
        /// <remarks>
        ///     This command helps in monitoring mass role operations, offering insights into the progress, type, and initiator of
        ///     each operation.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RoleJobs()
        {
            var list = Service.Jobslist;
            if (list.Count == 0)
            {
                await ctx.Channel.SendErrorAsync(Strings.NoMassOperations(ctx.Guild.Id), Config);
                return;
            }

            var eb = new EmbedBuilder
            {
                Title = $"{list.Count} Mass Role Operations Running", Color = Mewdeko.OkColor
            };
            foreach (var i in list)
            {
                if (i.Role2 is not null && i.JobType != "Adding then Removing a Role")
                {
                    eb.AddField($"Job {i.JobId}",
                        $"Job Type: {i.JobType}\nStarted By: {i.StartedBy.Mention}\nProgress: {i.AddedTo}/{i.TotalUsers}\nFirst Role:{i.Role1.Mention}\nSecond Role:{i.Role2.Mention}");
                }

                if (i.Role2 is not null && i.JobType == "Adding then Removing a Role")
                {
                    eb.AddField($"Job {i.JobId}",
                        $"Job Type: {i.JobType}\nStarted By: {i.StartedBy.Mention}\nProgress: {i.AddedTo}/{i.TotalUsers}\nRemoving Role:{i.Role2.Mention}\nAdding Role:{i.Role1.Mention}");
                }
                else
                {
                    eb.AddField($"Job {i.JobId}",
                        $"Job Type: {i.JobType}\nStarted By: {i.StartedBy.Mention}\nProgress: {i.AddedTo}/{i.TotalUsers}\nRole:{i.Role1.Mention}");
                }
            }

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a specified role to all server members.
        /// </summary>
        /// <param name="role">The role to be added to all server members.</param>
        /// <remarks>
        ///     This command initiates a mass role application process, applying the specified role to every member in the server.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task AddToAll(IRole role)
        {
            await Task.Delay(500).ConfigureAwait(false);
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => !c.Roles.Contains(role));
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.AllUsersHaveRole(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.Count + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Users and Bots",
                role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} Members.\nThis will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role.Mention, count2, count))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.AppliedRoleToMembers(ctx.Guild.Id, role.Mention, count2, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a specified role to all bots in the server.
        /// </summary>
        /// <param name="role">The role to be added to all bots.</param>
        /// <remarks>
        ///     This command targets only bot accounts within the server, applying the specified role to them.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task AddToAllBots(IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => !c.Roles.Contains(role) && c.IsBot);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.AllBotsHaveRole(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Bots Only", role)
                .ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} Members.\nThis will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role.Mention, count2.ToString("N0"),
                                        count.ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.AppliedRoleToBots(ctx.Guild.Id, role.Mention, count2, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a specified role to all human users in the server, excluding bots.
        /// </summary>
        /// <param name="role">The role to be added to all human users.</param>
        /// <remarks>
        ///     This command excludes bot accounts and applies the specified role only to human members of the server.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task AddToAllUsers(IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => !c.Roles.Contains(role) && !c.IsBot);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.AllUsersHaveRole(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Users Only", role)
                .ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} users.\n + This will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role.Mention, count2.ToString("N0"),
                                        count.ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.AppliedRoleToUsers(ctx.Guild.Id, role.Mention, count2, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Exports a list of roles and their associated users to a text file, allowing for easy backup and transfer.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task ExportRoleList()
        {
            var roles = ctx.Guild.Roles.ToList();
            roles = roles.Where(x => !x.IsManaged && x.Id != ctx.Guild.Id).ToList();
            if (!roles.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.NoManageableRolesFound(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            await ctx.Channel.SendConfirmAsync(Strings.ExportingRoleUserList(ctx.Guild.Id, config.Data.LoadingEmote))
                .ConfigureAwait(false);
            var pair = new List<ExportedRoles>();
            foreach (var i in roles.OrderByDescending(x => x.Position))
            {
                var role = i as SocketRole;
                if (role.Members.Any())
                    role.Members.ForEach(x => pair.Add(new ExportedRoles
                    {
                        RoleId = i.Id, UserId = x.Id, RoleName = i.Name
                    }));
                else
                    pair.Add(new ExportedRoles
                    {
                        RoleId = i.Id, UserId = 0, RoleName = i.Name
                    });
            }

            var toExport = string.Join("\n", pair.Select(x => $"{x.RoleId},{x.UserId},{x.RoleName}"));
            var toSend = new MemoryStream(Encoding.UTF8.GetBytes(toExport));
            await ctx.Channel.SendFileAsync(toSend, "rolelist.txt").ConfigureAwait(false);
            await toSend.DisposeAsync();
        }

        /// <summary>
        ///     Imports roles and user associations from a text file, applying the specified roles to the mentioned users.
        /// </summary>
        /// <param name="newRoles">Indicates whether new roles should be created based on the import data.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task ImportRoleList(bool newRoles = false)
        {
            if (!ctx.Message.Attachments.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.AttachRoleListFile(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var client = new HttpClient();
            var guildUsers = (await ctx.Guild.GetUsersAsync()).ToList();
            var roles = ctx.Guild.Roles.ToList();
            var file = await client.GetStringAsync(ctx.Message.Attachments.First().Url);
            var lines = file.ToLines();
            var toProcess = new List<KeyValuePair<IRole, IGuildUser>>();
            var addedRoles = new List<ulong>();
            if (!newRoles)
                toProcess = (from i in lines
                    select i.Split(",")
                    into split
                    let role = roles.FirstOrDefault(x => x.Id == Convert.ToUInt64(split[0]))
                    where role is not null
                    let user = guildUsers.FirstOrDefault(x => x.Id == Convert.ToUInt64(split[1]))
                    select new KeyValuePair<IRole, IGuildUser>(role, user)).ToList();
            else
            {
                foreach (var i in lines)
                {
                    var split = i.Split(",");
                    var roleId = Convert.ToUInt64(split[0]);
                    IRole role;
                    if (!addedRoles.Contains(roleId))
                        role = await ctx.Guild.CreateRoleAsync(split[2], null, null, false, null).ConfigureAwait(false);
                    else
                        role = roles.FirstOrDefault(x => x.Name == split[2]);
                    addedRoles.Add(Convert.ToUInt64(split[0]));
                    var user = guildUsers.FirstOrDefault(x => x.Id == Convert.ToUInt64(split[1]));
                    if (user is not null)
                        toProcess.Add(new KeyValuePair<IRole, IGuildUser>(role, user));
                }
            }

            if (!toProcess.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.NoRolesUsersInFile(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            // Remove items more efficiently by iterating backwards
            for (var index = toProcess.Count - 1; index >= 0; index--)
            {
                var i = toProcess[index];
                if (i.Value.RoleIds.Any() && i.Value.RoleIds.Contains(i.Key.Id))
                {
                    toProcess.RemoveAt(index);
                }
            }

            if (!toProcess.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.AllRolesAlreadyApplied(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            var count = toProcess.Count;
            var addedCount = 0;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Importing User Roles", null)
                .ConfigureAwait(false);
            await ctx.Channel
                .SendConfirmAsync(
                    $" {config.Data.LoadingEmote} Importing {count} roles/users\nThis will take about {TimeSpan.FromSeconds(count).Humanize()}")
                .ConfigureAwait(false);
            foreach (var i in toProcess)
            {
                try
                {
                    var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                    var t = e == "Stopped";
                    if (t)
                    {
                        await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                        await ctx.Channel.SendConfirmAsync(
                                Strings.MassroleStopped(ctx.Guild.Id, addedCount.ToString("N0"),
                                    addedCount.ToString("N0"), toProcess.Count.ToString("N0")))
                            .ConfigureAwait(false);
                        return;
                    }

                    await i.Value.AddRoleAsync(i.Key).ConfigureAwait(false);
                    await Service.UpdateCount(ctx.Guild, jobId, count).ConfigureAwait(false);
                    addedCount++;
                }
                catch (HttpException)
                {
                    //ignored
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel
                .SendConfirmAsync(Strings.AppliedRoleUserImports(ctx.Guild.Id, addedCount, toProcess.Count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a specified role to a list of users defined in an attached file.
        /// </summary>
        /// <param name="role">The role to be added to the users listed in the attached file.</param>
        /// <remarks>
        ///     This command adds a specified role to users listed in a text file attached to the command message. It streamlines
        ///     the process of applying a role to a specific subset of users.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task AddRoleToList(IRole role)
        {
            if (!ctx.Message.Attachments.Any())
            {
                await ctx.Channel
                    .SendErrorAsync(Strings.AttachUserListFile(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var client = new HttpClient();
            var guildUsers = (await ctx.Guild.GetUsersAsync()).ToList();
            var actualUsers = new List<IGuildUser>();
            var file = await client.GetStringAsync(ctx.Message.Attachments.First().Url);
            var fileUsers = file.ToLines();
            var ulongIds = new List<ulong>();
            var stringUsers = new List<string>();
            foreach (var i in fileUsers)
            {
                if (ulong.TryParse(i, out var id))
                    ulongIds.Add(id);
                else
                    stringUsers.Add(i);
            }

            foreach (var i in stringUsers)
            {
                var user = guildUsers.FirstOrDefault(x =>
                    x.Username.Equals(i, StringComparison.OrdinalIgnoreCase) ||
                    x.DisplayName.Equals(i, StringComparison.OrdinalIgnoreCase) ||
                    (x.Nickname != null && x.Nickname.Equals(i, StringComparison.OrdinalIgnoreCase)));
                if (user is null)
                    continue;
                actualUsers.Add(user);
            }

            foreach (var i in ulongIds)
            {
                var user = guildUsers.FirstOrDefault(x => x.Id == i);
                if (user is null)
                    continue;
                actualUsers.Add(user);
            }

            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            actualUsers = actualUsers.Where(x => !x.GetRoles().Contains(role)).ToList();
            var count = actualUsers.Count;
            if (!actualUsers.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.AllUsersHaveRole(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Users in List", role)
                .ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} users.\nThis will take about {TimeSpan.FromSeconds(actualUsers.Count).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in actualUsers)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role.Mention, count2.ToString("N0"),
                                        count.ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.AppliedRoleToUsers(ctx.Guild.Id, role.Mention, count2, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a specified role to all users who have been a server member for longer than a specified duration.
        /// </summary>
        /// <param name="time">The minimum duration a user must have been a member of the server to receive the role.</param>
        /// <param name="role">The role to be added to qualifying users.</param>
        /// <remarks>
        ///     This command targets users based on their membership duration, applying a role to those who have been part of the
        ///     server for longer than the specified time.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task AddToUsersOver(StoopidTime time, IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c =>
                !c.Roles.Contains(role) && !c.IsBot &&
                DateTimeOffset.Now.Subtract(c.JoinedAt.Value) >= time.Time);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.UsersAtAgeHaveRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                    $"Adding a role to server members that have been here for {time.Time.Humanize()}", role)
                .ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} users who have acounts that are equal to or older than {time.Time.Humanize()} old..\nThis will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role.Mention, count2.ToString("N0"),
                                        count.ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.AppliedRoleToUsers(ctx.Guild.Id, role.Mention, count2, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a specified role to all users who have been a server member for shorter than a specified duration.
        /// </summary>
        /// <param name="time">The maximum duration a user can have been a member of the server to receive the role.</param>
        /// <param name="role">The role to be added to qualifying users.</param>
        /// <remarks>
        ///     Similar to 'AddToUsersOver', but targets new members by applying a role to those who have been part of the server
        ///     for less than the specified time.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task AddToUsersUnder(StoopidTime time, IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c =>
                !c.Roles.Contains(role) && !c.IsBot &&
                DateTimeOffset.Now.Subtract(c.JoinedAt.Value) < time.Time);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.UsersAtAgeHaveRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                    $"Adding a role to server members that have been here for {time.Time.Humanize()} or less", role)
                .ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} users who have acounts that are less than {time.Time.Humanize()} old..\nThis will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role.Mention, count2.ToString("N0"),
                                        count.ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await ctx.Channel.SendConfirmAsync(Strings.AppliedRoleToUsers(ctx.Guild.Id, role.Mention, count2, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a specified role from all server members.
        /// </summary>
        /// <param name="role">The role to be removed from all server members.</param>
        /// <remarks>
        ///     This command initiates a mass role removal process, removing the specified role from every member in the server.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveFromAll(IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => c.Roles.Contains(role));
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.NoUsersHaveRole(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                "Removing a role from all server members", role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Removing {role.Mention} from {count} Members.\n + This will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role.Mention, count2.ToString("N0"),
                                        count.ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.RemoveRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel
                .SendConfirmAsync(Strings.RemovedRoleFromMembers(ctx.Guild.Id, role.Mention, count2, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a specified role from all human users, excluding bots.
        /// </summary>
        /// <param name="role">The role to be removed from all human members.</param>
        /// <remarks>
        ///     This command targets only human members for role removal, allowing bots to retain their assigned roles. It's useful
        ///     for adjusting roles among human users without affecting bots' configurations.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveFromAllUsers(IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => c.Roles.Contains(role) && !c.IsBot);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.NoUsersHaveRole(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                "Removing a role from only users", role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Removing {role.Mention} from {count} users.\n + This will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role.Mention, count2.ToString("N0"),
                                        count.ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.RemoveRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel
                .SendConfirmAsync(Strings.RemovedRoleFromRoleForUsers(ctx.Guild.Id, role.Mention, count2, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a specified role from all bots in the server.
        /// </summary>
        /// <param name="role">The role to be removed from all bots.</param>
        /// <remarks>
        ///     This command is specifically designed to remove a role from bot accounts only, leaving human users' roles intact.
        ///     Ideal for managing bot permissions collectively.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveFromAllBots(IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => c.Roles.Contains(role) && c.IsBot);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.NoBotsHaveRole(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                "Removing a role from all bots", role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Removing {role.Mention} from {count} bots.\n + This will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role.Mention, count2.ToString("N0"),
                                        count.ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.RemoveRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.RemovedRoleFromBots(ctx.Guild.Id, role.Mention, count2, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a specified role to all users who currently have another specified role.
        /// </summary>
        /// <param name="role">The role to add.</param>
        /// <param name="role2">The role whose members will receive the new role.</param>
        /// <remarks>
        ///     This command allows for the targeted application of a role based on existing role memberships, enabling
        ///     administrators to dynamically adjust role assignments based on evolving server roles and user groups.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task AddRoleToRole(IRole role, IRole role2)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var client = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role2.Position ||
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRoles(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            if (client.GetRoles().Max(x => x.Position) <= role2.Position ||
                client.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRoles(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
            var inrole = users.Where(x => x.GetRoles().Contains(role));
            var inrole2 = users.Where(x => x.GetRoles().Contains(role2));
            if (inrole.Count() == inrole2.Count())
            {
                await ctx.Channel
                    .SendErrorAsync(Strings.AllUsersAlreadyHaveRole(ctx.Guild.Id, role.Mention, role2.Mention), Config)
                    .ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, inrole.Count(),
                "Adding a role to users within a role", role, role2).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                    $"Adding {role2.Mention} to users in {role.Mention}.\nThis will take about {inrole.Count()}s.")
                .ConfigureAwait(false);
            var count2 = 0;
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in inrole)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role2.Mention, count2.ToString("N0"),
                                        inrole.Count().ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role2).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.AddedRoleToUsers(ctx.Guild.Id, role2.Mention, count2))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a specified role from all users who currently have another specified role.
        /// </summary>
        /// <param name="role">The role to remove from users.</param>
        /// <param name="role2">The role whose members will have the first role removed.</param>
        /// <remarks>
        ///     Facilitates the cleaning or reorganization of roles within the server by removing a role from users based on their
        ///     membership in another role. This is particularly useful in situations where roles need to be reassigned or
        ///     permissions updated en masse.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveFromRole(IRole role, IRole role2)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var client = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role2.Position ||
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRoles(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            if (client.GetRoles().Max(x => x.Position) <= role2.Position ||
                client.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRoles(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
            var inrole = users.Where(x => x.GetRoles().Contains(role));
            var inrole2 = users.Where(x => x.GetRoles().Contains(role2));
            if (!inrole2.Any())
            {
                await ctx.Channel
                    .SendErrorAsync(Strings.NoUsersInRoleHaveRole(ctx.Guild.Id, role.Mention, role2.Mention), Config)
                    .ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, inrole.Count(),
                "Removing a role from users within a role", role, role2).ConfigureAwait(false);
            var guildUsers = inrole as IGuildUser[] ?? inrole.ToArray();
            await ctx.Channel.SendConfirmAsync(
                    Strings.RemovingRoleFromUsersInRole(ctx.Guild.Id, role2.Mention, role.Mention, guildUsers.Length))
                .ConfigureAwait(false);
            var count2 = 0;
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in inrole)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, role2.Mention, count2.ToString("N0"),
                                        inrole.Count().ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.RemoveRoleAsync(role2).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.RemovedRoleFromUsers(ctx.Guild.Id, role2.Mention, count2))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a role to users and then removes a different role from them in one operation.
        /// </summary>
        /// <param name="role">The role to add to the users.</param>
        /// <param name="role2">The role to remove from the same users.</param>
        /// <remarks>
        ///     This command combines role addition and removal into a single step for users who have a specific role, streamlining
        ///     the process of updating user roles and permissions. It's especially useful during role transitions or server
        ///     restructurings.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task AddThenRemove(IRole role, IRole role2)
        {
            await Task.Delay(500).ConfigureAwait(false);
            var client = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            var runnerUser = (IGuildUser)ctx.User;
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role2.Position ||
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.CannotManageRoles(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            if (client.GetRoles().Max(x => x.Position) <= role2.Position ||
                client.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync(Strings.BotCannotManageRoles(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
            var inrole = users.Where(x => x.GetRoles().Contains(role2));
            if (!inrole.Any())
            {
                await ctx.Channel.SendErrorAsync(Strings.NoUsersHaveRole(ctx.Guild.Id), Config).ConfigureAwait(false);

                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, inrole.Count(),
                "Adding then Removing a Role", role, role2).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to users in {role2.Mention} and removing {role2.Mention}.\nThis will take about {inrole.Count() * 2}s.")
                .ConfigureAwait(false);
            var count2 = 0;
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in inrole)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    Strings.MassroleStopped(ctx.Guild.Id, $"{role2.Mention} and removed {role.Mention}",
                                        count2.ToString("N0"), inrole.Count().ToString("N0")))
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await i.RemoveRoleAsync(role2).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                Strings.RoleAddedRemovedUsers(ctx.Guild.Id, role2.Mention, count2, role.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Represents a structure to hold exported role information.
        /// </summary>
        public record ExportedRoles
        {
            /// <summary>
            ///     Gets or sets the unique identifier for the role.
            /// </summary>
            public ulong RoleId { get; set; }

            /// <summary>
            ///     Gets or sets the unique identifier for the user.
            /// </summary>
            public ulong UserId { get; set; }

            /// <summary>
            ///     Gets or sets the name of the role.
            /// </summary>
            public string RoleName { get; set; }
        }
    }
}