﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Collections;
using Mewdeko.Common.Replacements;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Mewdeko.Modules.Administration.Services;

public class AdministrationService : INService
{
    private readonly DbService _db;
    private readonly LogCommandService _logService;
    private readonly Mewdeko _bot;

    public AdministrationService(Mewdeko bot, CommandHandler cmdHandler, DbService db,
        LogCommandService logService)
    {
        using var uow = db.GetDbContext();
        var gc = uow.GuildConfigs.All().Where(x => bot.GetCurrentGuildIds().Contains(x.GuildId));
        _bot = bot;
        _db = db;
        _logService = logService;

        DeleteMessagesOnCommand = new ConcurrentHashSet<ulong>(gc
            .Where(g => g.DeleteMessageOnCommand)
            .Select(g => g.GuildId));

        DeleteMessagesOnCommandChannels = new ConcurrentDictionary<ulong, bool>(gc
            .SelectMany(x => x.DelMsgOnCmdChannels)
            .ToDictionary(x => x.ChannelId, x => x.State)
            .ToConcurrent());
        cmdHandler.CommandExecuted += DelMsgOnCmd_Handler;
    }
    
    public ConcurrentHashSet<ulong> DeleteMessagesOnCommand { get; }
    public ConcurrentDictionary<ulong, bool> DeleteMessagesOnCommandChannels { get; }
    
    

    public async Task StaffRoleSet(IGuild guild, ulong role)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.StaffRole = role;
        await uow.SaveChangesAsync().ConfigureAwait(false);;
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task MemberRoleSet(IGuild guild, ulong role)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.MemberRole = role;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public ulong GetStaffRole(ulong id) => _bot.GetGuildConfig(id).StaffRole;

    public ulong GetMemberRole(ulong id) => _bot.GetGuildConfig(id).MemberRole;

    public (bool DelMsgOnCmd, IEnumerable<DelMsgOnCmdChannel> channels) GetDelMsgOnCmdData(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId,
            set => set.Include(x => x.DelMsgOnCmdChannels));

        return (conf.DeleteMessageOnCommand, conf.DelMsgOnCmdChannels);
    }

    private Task DelMsgOnCmd_Handler(IUserMessage msg, CommandInfo cmd)
    {
        var _ = Task.Run(async () =>
        {
            if (msg.Channel is SocketTextChannel channel)
            {
                if (DeleteMessagesOnCommandChannels.TryGetValue(channel.Id, out var state))
                {
                    if (state && cmd.Name != "Purge" && cmd.Name != "pick")
                    {
                        _logService.AddDeleteIgnore(msg.Id);
                        try
                        {
                            await msg.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    //if state is false, that means do not do it
                }
                else if (DeleteMessagesOnCommand.Contains(channel.Guild.Id) && cmd.Name != "Purge" &&
                         cmd.Name != "pick")
                {
                    _logService.AddDeleteIgnore(msg.Id);
                    try
                    {
                        await msg.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            //wat ?!
        });
        return Task.CompletedTask;
    }

    public bool ToggleDeleteMessageOnCommand(ulong guildId)
    {
        bool enabled;
        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        enabled = conf.DeleteMessageOnCommand = !conf.DeleteMessageOnCommand;
        _bot.UpdateGuildConfig(guildId, conf);
        uow.SaveChanges();

        return enabled;
    }

    public async Task SetDelMsgOnCmdState(ulong guildId, ulong chId, Administration.State newState)
    {
        await using (var uow = _db.GetDbContext())
        {
            var conf = uow.ForGuildId(guildId,
                set => set.Include(x => x.DelMsgOnCmdChannels));

            var old = conf.DelMsgOnCmdChannels.FirstOrDefault(x => x.ChannelId == chId);
            if (newState == Administration.State.Inherit)
            {
                if (old is not null)
                {
                    conf.DelMsgOnCmdChannels.Remove(old);
                    uow.Remove(old);
                }
            }
            else
            {
                if (old is null)
                {
                    old = new DelMsgOnCmdChannel { ChannelId = chId };
                    conf.DelMsgOnCmdChannels.Add(old);
                }

                old.State = newState == Administration.State.Enable;
                DeleteMessagesOnCommandChannels[chId] = newState == Administration.State.Enable;
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        switch (newState)
        {
            case Administration.State.Disable:
                break;
            case Administration.State.Enable:
                DeleteMessagesOnCommandChannels[chId] = true;
                break;
            default:
                DeleteMessagesOnCommandChannels.TryRemove(chId, out var _);
                break;
        }
    }

    public static async Task DeafenUsers(bool value, params IGuildUser[] users)
    {
        if (!users.Any())
            return;
        foreach (var u in users)
            try
            {
                await u.ModifyAsync(usr => usr.Deaf = value).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
    }

    public static async Task EditMessage(ICommandContext context, ITextChannel chanl, ulong messageId, string text)
    {
        var msg = await chanl.GetMessageAsync(messageId);

        if (msg is not IUserMessage umsg || msg.Author.Id != context.Client.CurrentUser.Id)
            return;

        var rep = new ReplacementBuilder()
            .WithDefault(context)
            .Build();

        if (SmartEmbed.TryParse(rep.Replace(text), out var embed, out var plainText))
        {
            await umsg.ModifyAsync(x =>
            {
                x.Embed = embed?.Build();
                x.Content = plainText?.SanitizeMentions();
            }).ConfigureAwait(false);
        }
        else
        {
            await umsg.ModifyAsync(x =>
            {
                x.Content = text.SanitizeMentions();
                x.Embed = null;
            }).ConfigureAwait(false);
        }
    }
}