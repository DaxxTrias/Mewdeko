﻿using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Confessions.Services;

namespace Mewdeko.Modules.Confessions;

/// <summary>
///     Module for managing confessions.
/// </summary>
/// <param name="guildSettings">The guild settings service</param>
public class Confessions(GuildSettingsService guildSettings) : MewdekoModuleBase<ConfessionService>
{
    /// <summary>
    ///     Allows users to confess anonymously.
    /// </summary>
    /// <param name="serverId">The ID of the server.</param>
    /// <param name="confession">The confession message</param>
    /// <example>.confess 1234567890 falafel.</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.DM)]
    public async Task Confess(ulong serverId, string confession)
    {
        var gc = await guildSettings.GetGuildConfig(serverId);
        var attachment = ctx.Message.Attachments.FirstOrDefault()?.Url;
        var user = ctx.User as SocketUser;
        if (user!.MutualGuilds.Select(x => x.Id).Contains(serverId))
        {
            if (gc.ConfessionChannel is 0)
            {
                await ErrorAsync(Strings.ConfessionsNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (gc.ConfessionBlacklist.Split(" ").Length > 0)
            {
                if (gc.ConfessionBlacklist.Split(" ").Contains(ctx.User.Id.ToString()))
                {
                    await ErrorAsync(Strings.ConfessionsBlacklisted(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await Service.SendConfession(serverId, ctx.User, confession, ctx.Channel, null, attachment)
                    .ConfigureAwait(false);
            }
            else
            {
                await Service.SendConfession(serverId, ctx.User, confession, ctx.Channel, null, attachment)
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await ErrorAsync(Strings.ConfessionsNoneAny(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the confession channel for anonymous confessions.
    /// </summary>
    /// <param name="channel">The confession channel (optional).</param>
    /// <example>.confessionchannel #confessions</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    [RequireContext(ContextType.Guild)]
    public async Task ConfessionChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionChannel(ctx.Guild, 0).ConfigureAwait(false);
            await ConfirmAsync(Strings.ConfessionsDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ErrorAsync(Strings.ConfessionsInvalidPerms(ctx.Guild.Id)).ConfigureAwait(false);
        }

        await Service.SetConfessionChannel(ctx.Guild, channel.Id).ConfigureAwait(false);
        await ConfirmAsync(Strings.ConfessionsChannelSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }


    /// <summary>
    ///     Sets the confession log channel for logging confessions. Misuse of this feature will end up in me being 2m away
    ///     from your house.
    /// </summary>
    /// <param name="channel">The confession log channel (optional).</param>
    /// <example>.confessionlogchannel #confessions</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task ConfessionLogChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionChannel(ctx.Guild, 0).ConfigureAwait(false);
            await ConfirmAsync(Strings.ConfessionsLoggingDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ErrorAsync(Strings.ConfessionsInvalidPerms(ctx.Guild.Id)).ConfigureAwait(false);
        }

        await Service.SetConfessionLogChannel(ctx.Guild, channel.Id).ConfigureAwait(false);
        await ErrorAsync(Strings.ConfessionsSpleen(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Adds or removes a user from the confession blacklist.
    /// </summary>
    /// <param name="user">The user to add or remove from the blacklist.</param>
    /// <example>.confessionblacklist @user</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    [RequireContext(ContextType.Guild)]
    public async Task ConfessionBlacklist(IUser user)
    {
        var blacklists = (await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionBlacklist.Split(" ");
        if (blacklists.Length > 0)
        {
            if (blacklists.Contains(user.Id.ToString()))
            {
                await ErrorAsync(Strings.ConfessionsBlacklistedAlready(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.ConfessionsBlacklistedAdded(ctx.Guild.Id, user.Mention));
        }
    }

    /// <summary>
    ///     Removes a user from the confession blacklist.
    /// </summary>
    /// <param name="user">The user to remove from the blacklist.</param>
    /// <example>.confessionunblacklist @user</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    [RequireContext(ContextType.Guild)]
    public async Task ConfessionUnblacklist(IUser user)
    {
        var blacklists = (await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionBlacklist.Split(" ");
        if (blacklists.Length > 0)
        {
            if (!blacklists.Contains(user.Id.ToString()))
            {
                await ErrorAsync(Strings.ConfessionsBlacklistedNot(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.ConfessionsBlacklistedRemoved(ctx.Guild.Id, user.Mention));
        }
    }
}