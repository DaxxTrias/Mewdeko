﻿using Mewdeko.Common.ModuleBehaviors;
using Serilog;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.StatusRoles.Services;

public class StatusRolesService : INService, IReadyExecutor
{
    private readonly DiscordSocketClient client;
    private readonly DbService db;
    private readonly IDataCache cache;

    public StatusRolesService(DiscordSocketClient client, DbService db, EventHandler eventHandler, IDataCache cache)
    {
        this.client = client;
        this.db = db;
        this.cache = cache;
        eventHandler.PresenceUpdated += EventHandlerOnPresenceUpdated;
    }

    public async Task OnReadyAsync()
    {
        await using var uow = db.GetDbContext();
        var statusRoles = uow.StatusRoles.ToList();
        await cache.SetStatusRoleCache(statusRoles);
        Log.Information("StatusRoles cached");
    }

    private async Task EventHandlerOnPresenceUpdated(SocketUser args, SocketPresence args2, SocketPresence args3)
    {
        try
        {
            if (!await cache.AddProcessingUser(args.Id))
                return;
            var beforeStatus = args2?.Activities?.FirstOrDefault() as CustomStatusGame;
            if (args3.Activities?.FirstOrDefault() is not CustomStatusGame status)
            {
                await cache.RemoveProcessingUser(args.Id);
                return;
            }
            if (status.State is null && beforeStatus?.State is null || status.State == beforeStatus?.State)
            {
                await cache.RemoveProcessingUser(args.Id);
                return;
            }

            if (!await cache.SetUserStatusCache(args.Id, status.State?.ToBase64() is null ? "none" : status.State.ToBase64()))
            {
                await cache.RemoveProcessingUser(args.Id);
                return;
            }

            await using var uow = db.GetDbContext();
            var statusRolesConfigs = await cache.GetStatusRoleCache();
            if (statusRolesConfigs is null || !statusRolesConfigs.Any())
            {
                await cache.RemoveProcessingUser(args.Id);
                return;
            }

            foreach (var i in statusRolesConfigs)
            {
                if (client.GetGuild(i.GuildId) is not IGuild guild)
                    continue;
                var curUser = await guild.GetUserAsync(args.Id);
                if (curUser is null)
                    continue;
                var toAdd = new List<ulong>();
                var toRemove = new List<ulong>();
                if (!string.IsNullOrWhiteSpace(i.ToAdd))
                    toAdd = i.ToAdd.Split(" ").Select(ulong.Parse).ToList();
                if (!string.IsNullOrWhiteSpace(i.ToRemove))
                    toRemove = i.ToRemove.Split(" ").Select(ulong.Parse).ToList();
                if (status.State is null || !status.State.Contains(i.Status))
                {
                    if (beforeStatus is not null && beforeStatus.State.Contains(i.Status))
                    {
                        if (i.RemoveAdded)
                        {
                            if (toAdd.Any())
                            {
                                foreach (var role in toAdd.Where(socketRole => curUser.RoleIds.Contains(socketRole)))
                                {
                                    try
                                    {
                                        await curUser.RemoveRoleAsync(role);
                                    }
                                    catch
                                    {
                                        Log.Error($"Unable to remove added role {role} for {curUser} in {guild} due to permission issues.");
                                        await Task.Delay(TimeSpan.FromSeconds(3));
                                        await cache.RemoveProcessingUser(args.Id);
                                    }
                                }
                            }
                        }

                        if (i.ReaddRemoved)
                        {
                            if (toRemove.Any())
                            {
                                foreach (var role in toRemove.Where(socketRole => !curUser.RoleIds.Contains(socketRole)))
                                {
                                    try
                                    {
                                        await curUser.AddRoleAsync(role);
                                    }
                                    catch
                                    {
                                        Log.Error($"Unable to add removed role {role} for {curUser} in {guild} due to permission issues.");
                                        await Task.Delay(TimeSpan.FromSeconds(3));
                                        await cache.RemoveProcessingUser(args.Id);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3));
                        await cache.RemoveProcessingUser(args.Id);
                        continue;
                    }
                }

                if (beforeStatus is not null && beforeStatus.State.Contains(i.Status))
                {
                    await cache.RemoveProcessingUser(args.Id);
                    continue;
                }

                if (toRemove.Any())
                {
                    try
                    {
                        await curUser.RemoveRolesAsync(toRemove);
                    }
                    catch
                    {
                        Log.Error($"Unable to remove statusroles in {guild} due to permission issues.");
                        await Task.Delay(TimeSpan.FromSeconds(3));
                        await cache.RemoveProcessingUser(args.Id);
                    }
                }

                if (toAdd.Any())
                {
                    try
                    {
                        await curUser.AddRolesAsync(toAdd);
                    }
                    catch
                    {
                        Log.Error($"Unable to add statusroles in {guild} due to permission issues.");
                        await Task.Delay(TimeSpan.FromSeconds(3));
                        await cache.RemoveProcessingUser(args.Id);
                    }
                }

                var channel = await guild.GetTextChannelAsync(i.StatusChannelId);

                if (channel is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    await cache.RemoveProcessingUser(args.Id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(i.StatusEmbed))
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    await cache.RemoveProcessingUser(args.Id);
                    continue;
                }

                var rep = new ReplacementBuilder().WithDefault(curUser, channel, guild as SocketGuild, client).Build();

                if (SmartEmbed.TryParse(rep.Replace(i.StatusEmbed), guild.Id, out var embeds, out var plainText, out var components))
                {
                    await channel.SendMessageAsync(plainText ?? null, embeds: embeds ?? Array.Empty<Embed>(), components: components?.Build());
                }
                else
                {
                    await channel.SendMessageAsync(rep.Replace(i.StatusEmbed));
                }
                await Task.Delay(TimeSpan.FromSeconds(3));
                await cache.RemoveProcessingUser(args.Id);
            }
        }
        catch (Exception e)
        {
            var status = args3.Activities?.FirstOrDefault() as CustomStatusGame;
            Log.Error("Error in StatusRolesService. After Status: {status} args: {args2} args2: {args3}\n{Exception}", status.State, args2, args3, e);
            await Task.Delay(TimeSpan.FromSeconds(6));
            await cache.RemoveProcessingUser(args.Id);
        }
    }

    public async Task<bool> AddStatusRoleConfig(string status, ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var toAdd = new StatusRolesTable
        {
            Status = status, GuildId = guildId
        };
        if (uow.StatusRoles.Where(x => x.GuildId == guildId).Any(x => x.Status == status))
            return false;
        uow.StatusRoles.Add(toAdd);
        await uow.SaveChangesAsync();
        await cache.SetStatusRoleCache(uow.StatusRoles.ToList());
        return true;
    }

    public async Task RemoveStatusRoleConfig(int index)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return;
        uow.StatusRoles.Remove(status);
        await uow.SaveChangesAsync();
        await cache.SetStatusRoleCache(uow.StatusRoles.ToList());
    }

    public async Task RemoveStatusRoleConfig(StatusRolesTable status)
    {
        try
        {
            await using var uow = db.GetDbContext();
            uow.StatusRoles.Remove(status);
            await uow.SaveChangesAsync();
            await cache.SetStatusRoleCache(uow.StatusRoles.ToList());
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<List<StatusRolesTable>?> GetStatusRoleConfig(ulong guildId)
    {
        var statusList = await cache.GetStatusRoleCache();
        if (!statusList.Any())
            return new List<StatusRolesTable>();
        statusList = statusList.Where(x => x.GuildId == guildId).ToList();
        return statusList.Any() ? statusList : new List<StatusRolesTable>();
    }

    public async Task<bool> SetAddRoles(int index, string toAdd)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.ToAdd = toAdd;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> SetRemoveRoles(int index, string toAdd)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.ToRemove = toAdd;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> SetStatusChannel(int index, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.StatusChannelId = channelId;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> SetStatusEmbed(int index, string embedText)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.StatusEmbed = embedText;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> ToggleRemoveAdded(int index)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.RemoveAdded = !status.RemoveAdded;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return status.RemoveAdded;
    }

    public async Task<bool> ToggleAddRemoved(int index)
    {
        await using var uow = db.GetDbContext();
        var status = uow.StatusRoles.FirstOrDefault(x => x.Id == index);
        if (status is null)
            return false;
        status.ReaddRemoved = !status.ReaddRemoved;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return status.ReaddRemoved;
    }

     public async Task<bool> SetAddRoles(StatusRolesTable status, string toAdd)
    {
        await using var uow = db.GetDbContext();
        status.ToAdd = toAdd;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> SetRemoveRoles(StatusRolesTable status, string toAdd)
    {
        await using var uow = db.GetDbContext();
        status.ToRemove = toAdd;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> SetStatusChannel(StatusRolesTable status, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        status.StatusChannelId = channelId;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> SetStatusEmbed(StatusRolesTable status, string embedText)
    {
        await using var uow = db.GetDbContext();
        status.StatusEmbed = embedText;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return true;
    }

    public async Task<bool> ToggleRemoveAdded(StatusRolesTable status)
    {
        await using var uow = db.GetDbContext();
        status.RemoveAdded = !status.RemoveAdded;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return status.RemoveAdded;
    }

    public async Task<bool> ToggleAddRemoved(StatusRolesTable status)
    {
        await using var uow = db.GetDbContext();
        status.ReaddRemoved = !status.ReaddRemoved;
        uow.StatusRoles.Update(status);
        await uow.SaveChangesAsync();
        var statusCache = await cache.GetStatusRoleCache();
        var listIndex = statusCache.IndexOf(statusCache.FirstOrDefault(x => x.Id == status.Id));
        statusCache[listIndex] = status;
        await cache.SetStatusRoleCache(statusCache);
        return status.ReaddRemoved;
    }
}