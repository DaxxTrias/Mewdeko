﻿using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Afk.Services;

namespace Mewdeko.Modules.Afk;

/// <summary>
///     Slash commands for setting and managing AFK messages.
/// </summary>
[Group("afk", "Set or Manage AFK")]
public class SlashAfk : MewdekoSlashModuleBase<AfkService>
{
    private readonly InteractiveService interactivity;

    /// <summary>
    ///     Initializes a new instance of <see cref="SlashAfk" />.
    /// </summary>
    /// <param name="serv">The interactivity service used for embed pagination.</param>
    public SlashAfk(InteractiveService serv)
    {
        interactivity = serv;
    }

    /// <summary>
    ///     Sets the user's AFK status with an optional message.
    /// </summary>
    /// <param name="message">The AFK message. If not provided, the user's AFK status will be toggled.</param>
    /// <example>.afk</example>
    /// <example>.afk I'm AFK</example>
    [SlashCommand("set", "Set your afk with an optional message")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.SendMessages)]
    public async Task Afk(string? message = null)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id));
            return;
        }

        if (message == null)
        {
            var afkmsg = (await Service.GetAfk(ctx.Guild.Id, ctx.User.Id))?.Message;
            if (string.IsNullOrEmpty(afkmsg))
            {
                await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, "_ _").ConfigureAwait(false);
                await EphemeralReplyErrorAsync(Strings.AfkMsgEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                try
                {
                    var user = await ctx.Guild.GetUserAsync(ctx.User.Id).ConfigureAwait(false);
                    var toset = user.Nickname is null
                        ? $"[AFK] {user.Username.TrimTo(26)}"
                        : $"[AFK] {user.Nickname.TrimTo(26)}";
                    await user.ModifyAsync(x => x.Nickname = toset).ConfigureAwait(false);
                }
                catch
                {
                    //ignored
                }

                await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
                return;
            }

            await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, "").ConfigureAwait(false);
            await EphemeralReplyConfirmAsync(Strings.AfkMsgDisabled(ctx.Guild.Id));
            try
            {
                var user = await ctx.Guild.GetUserAsync(ctx.User.Id).ConfigureAwait(false);
                await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
            }
            catch
            {
                //ignored
            }

            await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
            return;
        }

        if (message.Length != 0 && message.Length > await Service.GetAfkLength(ctx.Guild.Id))
        {
            await ReplyErrorAsync(Strings.AfkMessageTooLong(ctx.Guild.Id, Service.GetAfkLength(ctx.Guild.Id)))
                .ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, message.EscapeWeirdStuff()).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.AfkEnabled(ctx.Guild.Id, message)).ConfigureAwait(false);
        await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a timed AFK status with a custom message.
    /// </summary>
    /// <param name="time">The duration for the AFK status.</param>
    /// <param name="message">The custom message for the AFK status.</param>
    /// <example>.afk 1h I'm AFK</example>
    [SlashCommand("timed", "Sets a timed afk that auto removes itself and pings you when it.")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.SendMessages)]
    public async Task TimedAfk(string time, string message)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await EphemeralReplyErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var parsedTime = StoopidTime.FromInput(time);
        if (parsedTime.Time.Equals(default))
        {
            await EphemeralReplyErrorAsync(Strings.AfkTimeInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (message.Length != 0 && message.Length > await Service.GetAfkLength(ctx.Guild.Id))
        {
            await ReplyErrorAsync(Strings.AfkMessageTooLong(ctx.Guild.Id, Service.GetAfkLength(ctx.Guild.Id)))
                .ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, message, true, DateTime.UtcNow + parsedTime.Time);
        await ConfirmAsync(Strings.AfkTimedSet(ctx.Guild.Id,
            TimestampTag.FromDateTimeOffset(DateTimeOffset.UtcNow + parsedTime.Time, TimestampTagStyles.Relative),
            message));
    }

    /// <summary>
    ///     Sets a custom AFK embed that will be displayed when a user is AFK. Use "-" to reset to the default embed. Check
    ///     https://eb.mewdeko.tech for the embed builder and http://mewdeko.tech/placeholders for placeholders.
    /// </summary>
    /// <param name="embedCode">The custom message to set. Use "-" to reset to the default message.</param>
    /// <example>/afk message -</example>
    /// <example>/afk message embedcode</example>
    [SlashCommand("message", "Allows you to set a custom embed for AFK messages.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task CustomAfkMessage(string embedCode)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (embedCode == "-")
        {
            await Service.SetCustomAfkMessage(ctx.Guild, "-").ConfigureAwait(false);
            await ConfirmAsync(Strings.AfkMessageDefault(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetCustomAfkMessage(ctx.Guild, embedCode).ConfigureAwait(false);
        await ConfirmAsync(Strings.AfkMessageUpdated(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays a list of active users who are currently AFK.
    /// </summary>
    /// <example>./afk listactive</example>
    [SlashCommand("listactive", "Sends a list of active afk users")]
    [CheckPermissions]
    public async Task GetActiveAfks()
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var afks = await Service.GetAfkUsers(ctx.Guild).ConfigureAwait(false);
        if (afks.Count == 0)
        {
            await ErrorAsync(Strings.AfkUserNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(afks.ToArray().Length / 20).WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();

        await interactivity
            .SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle(Strings.ActiveAfksTitle(ctx.Guild.Id, afks.ToArray().Length))
                .WithDescription(string.Join("\n", afks.ToArray().Skip(page * 20).Take(20)));
        }
    }

    /// <summary>
    ///     Displays the AFK status of a specific user.
    /// </summary>
    /// <param name="user">The user to check the AFK status for.</param>
    /// <example>/afk view @user</example>
    [SlashCommand("view", "View another user's afk message")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task AfkView(IGuildUser user)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!await Service.IsAfk(user.Guild.Id, user.Id))
        {
            await ErrorAsync(Strings.AfkUserNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var msg = await Service.GetAfk(user.Guild.Id, user.Id);
        await ctx.Interaction.SendConfirmAsync(Strings.AfkViewMessage(ctx.Guild.Id, user, msg.Message))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the text channels where the AFK message doesnt display.
    /// </summary>
    /// <example>/afk disabledlist</example>
    [SlashCommand("disabledlist", "Shows a list of channels where afk messages are not allowed to display")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task AfkDisabledList()
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var mentions = new List<string>();
        var chans = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
        if (string.IsNullOrEmpty(chans) || chans.Contains('0'))
        {
            await ErrorAsync(Strings.AfkDisabledChannelsNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.SendConfirmAsync(Strings.Loading(ctx.Guild.Id)).ConfigureAwait(false);
        foreach (var i in chans.Split(","))
        {
            var role = await ctx.Guild.GetTextChannelAsync(Convert.ToUInt64(i)).ConfigureAwait(false);
            mentions.Add(role.Mention);
        }

        var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(mentions.ToArray().Length / 20).WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
        await ctx.Interaction.DeleteOriginalResponseAsync().ConfigureAwait(false);
        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle(Strings.DisabledAfkChannels(ctx.Guild.Id, mentions.ToArray().Length))
                .WithDescription(string.Join("\n", mentions.ToArray().Skip(page * 20).Take(20)));
        }
    }

    /// <summary>
    ///     Sets the maximum length of all AFK messages.
    /// </summary>
    /// <param name="num">The maximum length you want to set.</param>
    /// <example>/afk maxlength 100</example>
    [SlashCommand("maxlength", "Sets the maximum length of afk messages.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task AfkLength(int num)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (num > 4096)
        {
            await ErrorAsync(Strings.AfkMaxLengthTooLarge(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await Service.AfkLengthSet(ctx.Guild, num).ConfigureAwait(false);
            await ConfirmAsync(Strings.AfkMaxLengthSet(ctx.Guild.Id, num));
        }
    }

    /// <summary>
    ///     Sets the type of AFK behavior for the guild.
    /// </summary>
    /// <param name="afkTypeEnum">The type of AFK behavior to set. <see cref="Afk.AfkTypeEnum" /></param>
    /// <example>.afktype 1</example>
    [SlashCommand("type", "Sets how afk messages are removed. Do @Mewdeko help afktype to see more.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task AfkType(Afk.AfkTypeEnum afkTypeEnum)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ReplyErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.AfkTypeSet(ctx.Guild, (int)afkTypeEnum).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.AfkTypeSet(ctx.Guild.Id, afkTypeEnum)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the timeout duration before a user is no longer considered afk. Triggers when a user sends a message or types
    ///     in a channel.
    /// </summary>
    /// <param name="input">The timeout duration for the AFK status.</param>
    /// <example>/afk timeout 1h</example>
    [SlashCommand("timeout", "Sets after how long mewdeko no longer ignores a user's typing/messages.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task AfkTimeout(string input)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var time = StoopidTime.FromInput(input);
        if (time.Time.Equals(default))
        {
            await ErrorAsync(Strings.AfkTimeInvalid(ctx.Guild.Id));
            return;
        }

        if (time.Time < TimeSpan.FromSeconds(1) || time.Time > TimeSpan.FromHours(2))
        {
            await ErrorAsync(Strings.AfkTimeoutInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.AfkTimeoutSet(ctx.Guild, Convert.ToInt32(time.Time.TotalSeconds)).ConfigureAwait(false);
        await ConfirmAsync(Strings.AfkTimeoutSet(ctx.Guild.Id, time.Time.Humanize())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes the specified channel from the afk message blacklist.
    /// </summary>
    /// <param name="channel">The text channel for which to remove from the afk message blacklist.</param>
    /// <example>/afk undisable #channel</example>
    [SlashCommand("undisable", "Allows afk messages to be shown in a channel again.")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task AfkUndisable(ITextChannel channel)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ctx.Interaction
                .SendErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        var chan = new[]
        {
            channel
        };
        var mentions = new List<string>();
        var toremove = new List<string>();
        var chans = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
        if (string.IsNullOrWhiteSpace(chans) || chans == "0")
        {
            await ErrorAsync(Strings.AfkDisabledChannelsNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var e = chans.Split(",");
        var list = e.ToList();
        foreach (var i in chan)
        {
            if (e.Contains(i.Id.ToString()))
            {
                toremove.Add(i.Id.ToString());
                mentions.Add(i.Mention);
            }
        }

        if (mentions.Count == 0)
        {
            await ErrorAsync(Strings.AfkDisabledChannelsNoneset(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!list.Except(toremove).Any())
        {
            await Service.AfkDisabledSet(ctx.Guild, "0").ConfigureAwait(false);
            await ConfirmAsync(Strings.AfkIgnoringNoLongerDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list.Except(toremove))).ConfigureAwait(false);
        await ConfirmAsync(Strings.AfkDisabledChannelsRemoved(ctx.Guild.Id, string.Join(",", mentions)));
    }

    /// <summary>
    ///     Sets the channel where the AFK message will not display.
    /// </summary>
    /// <param name="channel">Channel you want to add to the afk message blacklist.</param>
    /// <example>/afk disable #channel</example>
    [SlashCommand("disable", "Disables afk messages to be shown in channels you specify.")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task AfkDisable(ITextChannel channel)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ctx.Interaction
                .SendErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        var chan = new[]
        {
            channel
        };
        var list = new HashSet<string>();
        // ReSharper disable once CollectionNeverQueried.Local
        var newchans = new HashSet<string>();
        var mentions = new HashSet<string>();
        if (await Service.GetDisabledAfkChannels(ctx.Guild.Id) == "0"
            || string.IsNullOrWhiteSpace(await Service.GetDisabledAfkChannels(ctx.Guild.Id)))
        {
            foreach (var i in chan)
            {
                list.Add(i.Id.ToString());
                mentions.Add(i.Mention);
            }

            await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list)).ConfigureAwait(false);
            await ConfirmAsync(Strings.AfkDisabledIn(ctx.Guild.Id, string.Join(",", list))).ConfigureAwait(false);
        }
        else
        {
            var e = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
            var w = e.Split(",");
            foreach (var i in w)
                list.Add(i);

            foreach (var i in chan)
            {
                if (!w.Contains(i.Id.ToString()))
                {
                    list.Add(i.Id.ToString());
                    mentions.Add(i.Mention);
                }

                newchans.Add(i.Id.ToString());
            }

            if (mentions.Count > 0)
            {
                await ErrorAsync(Strings.AfkAlreadyInList(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list)).ConfigureAwait(false);
            await ConfirmAsync(Strings.AfkChannelsUpdated(ctx.Guild.Id, string.Join(",", mentions)))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes the AFK status for a user.
    /// </summary>
    /// <param name="user">The user you want to remove afk from.</param>
    /// <example>/afk remove @user</example>
    [SlashCommand("remove", "Removes afk from a user")]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task AfkRemove(IGuildUser user)
    {
        if (!await CheckRoleHierarchy(user))
            return;
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorAsync(Strings.AfkStillStarting(ctx.Guild.Id));
            return;
        }

        var msg = await Service.GetAfk(ctx.Guild.Id, user.Id);
        if (msg is null)
        {
            await EphemeralReplyErrorAsync(Strings.AfkNotLBozo(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild.Id, user.Id, "").ConfigureAwait(false);
        await EphemeralReplyErrorAsync(Strings.AfkNoted(ctx.Guild.Id, user.Mention));
    }
}