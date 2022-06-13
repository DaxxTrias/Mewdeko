﻿using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Administration.Services;

public class VcRoleService : INService
{
    private readonly DiscordSocketClient _client;
    private readonly DbService _db;

    public VcRoleService(DiscordSocketClient client, Mewdeko bot, DbService db)
    {
        _db = db;
        _client = client;

        _client.UserVoiceStateUpdated += ClientOnUserVoiceStateUpdated;
        VcRoles = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, IRole>>();
        ToAssign = new ConcurrentDictionary<ulong, ConcurrentQueue<(bool, IGuildUser, IRole)>>();

        using (var uow = db.GetDbContext())
        {
            var guildIds = client.Guilds.Select(x => x.Id).ToList();
            var configs = uow.GuildConfigs
                .AsQueryable()
                .Include(x => x.VcRoleInfos)
                .Where(x => guildIds.Contains(x.GuildId))
                .ToList();

            Task.WhenAll(configs.Select(InitializeVcRole));
        }

        Task.Run(async () =>
        {
            while (true)
            {
                var tasks = ToAssign.Values.Select(queue => Task.Run(async () =>
                {
                    while (queue.TryDequeue(out var item))
                    {
                        var (add, user, role) = item;
                        if (add)
                        {
                            if (!user.RoleIds.Contains(role.Id))
                            {
                                try
                                {
                                    await user.AddRoleAsync(role).ConfigureAwait(false);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }
                        else
                        {
                            if (user.RoleIds.Contains(role.Id))
                            {
                                try
                                {
                                    await user.RemoveRoleAsync(role).ConfigureAwait(false);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }

                        await Task.Delay(250).ConfigureAwait(false);
                    }
                }));

                await Task.WhenAll(tasks.Append(Task.Delay(1000))).ConfigureAwait(false);
            }
        });

        _client.LeftGuild += _client_LeftGuild;
        bot.JoinedGuild += Bot_JoinedGuild;
    }

    public ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, IRole>> VcRoles { get; }
    public ConcurrentDictionary<ulong, ConcurrentQueue<(bool, IGuildUser, IRole)>> ToAssign { get; }

    private Task Bot_JoinedGuild(GuildConfig arg)
    {
        // includeall no longer loads vcrole
        // need to load new guildconfig with vc role included 
        using var uow = _db.GetDbContext();
        var configWithVcRole = uow.ForGuildId(
            arg.GuildId,
            set => set.Include(x => x.VcRoleInfos)
        );
        var _ = InitializeVcRole(configWithVcRole);

        return Task.CompletedTask;
    }

    private Task _client_LeftGuild(SocketGuild arg)
    {
        VcRoles.TryRemove(arg.Id, out _);
        ToAssign.TryRemove(arg.Id, out _);
        return Task.CompletedTask;
    }

    private async Task InitializeVcRole(GuildConfig gconf)
    {
        await Task.Yield();
        var g = _client.GetGuild(gconf.GuildId);
        if (g == null)
            return;

        var infos = new ConcurrentDictionary<ulong, IRole>();
        var missingRoles = new List<VcRoleInfo>();
        VcRoles.AddOrUpdate(gconf.GuildId, infos, delegate { return infos; });
        foreach (var ri in gconf.VcRoleInfos)
        {
            var role = g.GetRole(ri.RoleId);
            if (role == null)
            {
                missingRoles.Add(ri);
                continue;
            }

            infos.TryAdd(ri.VoiceChannelId, role);
        }

        if (missingRoles.Count > 0)
        {
            await using var uow = _db.GetDbContext();
            Log.Warning($"Removing {missingRoles.Count} missing roles from {nameof(VcRoleService)}");
            uow.RemoveRange(missingRoles);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public void AddVcRole(ulong guildId, IRole role, ulong vcId)
    {
        if (role == null)
            throw new ArgumentNullException(nameof(role));

        var guildVcRoles = VcRoles.GetOrAdd(guildId, new ConcurrentDictionary<ulong, IRole>());

        guildVcRoles.AddOrUpdate(vcId, role, (_, _) => role);
        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set.Include(x => x.VcRoleInfos));
        var toDelete = conf.VcRoleInfos.FirstOrDefault(x => x.VoiceChannelId == vcId); // remove old one
        if (toDelete != null) uow.Remove(toDelete);
        conf.VcRoleInfos.Add(new VcRoleInfo
        {
            VoiceChannelId = vcId,
            RoleId = role.Id
        }); // add new one
        uow.SaveChanges();
    }

    public bool RemoveVcRole(ulong guildId, ulong vcId)
    {
        if (!VcRoles.TryGetValue(guildId, out var guildVcRoles))
            return false;

        if (!guildVcRoles.TryRemove(vcId, out _))
            return false;

        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set.Include(x => x.VcRoleInfos));
        var toRemove = conf.VcRoleInfos.Where(x => x.VoiceChannelId == vcId).ToList();
        uow.RemoveRange(toRemove);
        uow.SaveChanges();

        return true;
    }

    private Task ClientOnUserVoiceStateUpdated(SocketUser usr, SocketVoiceState oldState,
        SocketVoiceState newState)
    {
        if (usr is not SocketGuildUser gusr)
            return Task.CompletedTask;

        var oldVc = oldState.VoiceChannel;
        var newVc = newState.VoiceChannel;
        var _ = Task.Factory.StartNew(() =>
        {
            try
            {
                if (oldVc == newVc) return;
                var guildId = newVc?.Guild.Id ?? oldVc.Guild.Id;

                if (!VcRoles.TryGetValue(guildId, out var guildVcRoles)) return;
                //remove old
                if (oldVc != null && guildVcRoles.TryGetValue(oldVc.Id, out var role))
                    Assign(false, gusr, role);
                //add new
                if (newVc != null && guildVcRoles.TryGetValue(newVc.Id, out role)) Assign(true, gusr, role);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in VcRoleService VoiceStateUpdate");
            }
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    private void Assign(bool v, SocketGuildUser gusr, IRole role)
    {
        var queue = ToAssign.GetOrAdd(gusr.Guild.Id, new ConcurrentQueue<(bool, IGuildUser, IRole)>());
        queue.Enqueue((v, gusr, role));
    }
}