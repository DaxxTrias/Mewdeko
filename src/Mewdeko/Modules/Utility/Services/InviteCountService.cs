﻿using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Common.ModuleBehaviors;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Invite Count Service
/// </summary>
public class InviteCountService : INService, IReadyExecutor
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, IInviteMetadata>> guildInvites = new();
    private readonly ConcurrentDictionary<ulong, InviteCountSetting> inviteCountSettings = new();
    private readonly ILogger<InviteCountService> logger;

    /// <summary>
    ///     Service for counting invites
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="dbFactory"></param>
    /// <param name="client"></param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public InviteCountService(EventHandler handler, IDataConnectionFactory dbFactory, DiscordShardedClient client,
        ILogger<InviteCountService> logger)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.logger = logger;

        handler.Subscribe("JoinedGuild", "InviteCountService", UpdateGuildInvites);
        handler.Subscribe("UserJoined", "InviteCountService", OnUserJoined);
        handler.Subscribe("UserLeft", "InviteCountService", OnUserLeft);
        handler.Subscribe("InviteCreated", "InviteCountService", OnInviteCreated);
        handler.Subscribe("InviteDeleted", "InviteCountService", OnInviteDeleted);
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        var guilds = client.Guilds.ToHashSet();
        var guildIds = client.Guilds.Select(g => g.Id).ToHashSet();

        await using var uow = await dbFactory.CreateConnectionAsync();
        var allSettings = await uow.InviteCountSettings
            .Where(s => guildIds.Contains(s.GuildId))
            .ToDictionaryAsync(s => s.GuildId);

        var mergedSettings = guildIds.ToDictionary(
            guildId => guildId,
            guildId => allSettings.TryGetValue(guildId, out var settings)
                ? settings
                : new InviteCountSetting
                {
                    GuildId = guildId, RemoveInviteOnLeave = true, IsEnabled = true
                }
        );

        foreach (var kvp in mergedSettings)
        {
            inviteCountSettings[kvp.Key] = kvp.Value;
        }

        var newSettingsList = mergedSettings.Values
            .Where(s => !allSettings.ContainsKey(s.GuildId))
            .ToList();

        if (newSettingsList.Count != 0)
        {
            var table = uow.GetTable<InviteCountSetting>();
            await table.BulkCopyAsync(newSettingsList);
            logger.LogInformation("Bulk inserted {Count} new invite settings", newSettingsList.Count);
        }


        var guildsToUpdate = guilds
            .Where(i => i.Users.FirstOrDefault(x => x.Id == client.CurrentUser.Id)?.GuildPermissions
                .Has(GuildPermission.ManageGuild) == true)
            .ToList();

        var semaphore = new SemaphoreSlim(10);
        var tasks = guildsToUpdate.Select(guild => ProcessGuildWithRateLimiting(guild, semaphore)).ToList();

        await Task.WhenAll(tasks);
    }

    private async Task OnUserLeft(IGuild guild, IUser user)
    {
        var settings = await GetInviteCountSettingsAsync(guild.Id);
        if (!settings.IsEnabled || !settings.RemoveInviteOnLeave) return;

        await using var uow = await dbFactory.CreateConnectionAsync();

        var invitedBy = await uow.InvitedBies
            .FirstOrDefaultAsync(x => x.UserId == user.Id && x.GuildId == guild.Id);

        if (invitedBy != null)
        {
            var inviterCount = await uow.InviteCounts
                .FirstOrDefaultAsync(x => x.UserId == invitedBy.InviterId && x.GuildId == guild.Id);

            if (inviterCount is { Count: > 0 })
            {
                inviterCount.Count--;
            }

            // Remove the InvitedBy record
            await uow.DeleteAsync(invitedBy);
        }
    }

    /// <summary>
    ///     Gets invite count settings for the guild
    /// </summary>
    /// <param name="guildId"></param>
    /// <returns></returns>
    public async Task<InviteCountSetting> GetInviteCountSettingsAsync(ulong guildId)
    {
        if (inviteCountSettings.TryGetValue(guildId, out var cachedSettings))
        {
            return cachedSettings;
        }

        await using var uow = await dbFactory.CreateConnectionAsync();
        var settings = await uow.InviteCountSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            settings = new InviteCountSetting
            {
                GuildId = guildId, RemoveInviteOnLeave = false, MinAccountAge = TimeSpan.Zero, IsEnabled = true
            };
            await uow.InsertAsync(settings);
        }

        inviteCountSettings[guildId] = settings;
        return settings;
    }

    private async Task UpdateInviteCountSettingsAsync(ulong guildId, Action<InviteCountSetting> updateAction)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var settings = await uow.InviteCountSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            settings = new InviteCountSetting
            {
                GuildId = guildId
            };
            await uow.InsertAsync(settings);
        }

        updateAction(settings);
        await uow.UpdateAsync(settings);


        inviteCountSettings[guildId] = settings;
    }

    /// <summary>
    ///     Sets whether invite tracking is enabled or disabled
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="isEnabled"></param>
    /// <returns></returns>
    public async Task<bool> SetInviteTrackingEnabledAsync(ulong guildId, bool isEnabled)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.IsEnabled = isEnabled);
        return isEnabled;
    }

    /// <summary>
    ///     Sets whether invite count gets removed when a user leaves
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="removeOnLeave"></param>
    /// <returns></returns>
    public async Task<bool> SetRemoveInviteOnLeaveAsync(ulong guildId, bool removeOnLeave)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.RemoveInviteOnLeave = removeOnLeave);
        return removeOnLeave;
    }

    /// <summary>
    ///     Sets the minimum account age for an invite to get counted
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="minAge"></param>
    /// <returns></returns>
    public async Task<TimeSpan> SetMinAccountAgeAsync(ulong guildId, TimeSpan minAge)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.MinAccountAge = minAge);
        return minAge;
    }


    private async Task OnUserJoined(IGuildUser user)
    {
        if (inviteCountSettings.TryGetValue(user.Guild.Id, out var settings))
            if (!settings.IsEnabled)
                return;
        var guild = user.Guild;
        var curUser = await guild.GetCurrentUserAsync();
        if (!curUser.GuildPermissions.Has(GuildPermission.ManageGuild))
            return;
        var newInvites = await guild.GetInvitesAsync();
        var usedInvite = FindUsedInvite(guild.Id, newInvites);

        if (usedInvite != null)
        {
            await UpdateInviteCount(usedInvite.Inviter.Id, guild.Id);
            await UpdateInvitedBy(user.Id, usedInvite.Inviter.Id, guild.Id);
        }

        await UpdateGuildInvites(user.Guild);
    }

    private Task OnInviteCreated(IInvite invite)
    {
        if (!this.guildInvites.TryGetValue(invite.Guild.Id, out var concurrentDictionary))
        {
            concurrentDictionary = new ConcurrentDictionary<string, IInviteMetadata>();
            this.guildInvites[invite.Guild.Id] = concurrentDictionary;
        }

        concurrentDictionary[invite.Code] = invite as IInviteMetadata;
        return Task.CompletedTask;
    }

    private Task OnInviteDeleted(IGuildChannel channel, string code)
    {
        if (this.guildInvites.TryGetValue(channel.Guild.Id, out var invites))
        {
            invites.TryRemove(code, out _);
        }

        return Task.CompletedTask;
    }


    private async Task UpdateGuildInvites(IGuild guild)
    {
        try
        {
            var invites = await guild.GetInvitesAsync();
            guildInvites[guild.Id] = new ConcurrentDictionary<string, IInviteMetadata>(
                invites.ToDictionary(x => x.Code, x => x));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update invites for guild {guild.Id}: {ex.Message}");
        }
    }

    private IInvite? FindUsedInvite(ulong guildId, IEnumerable<IInviteMetadata> newInvites)
    {
        if (!guildInvites.TryGetValue(guildId, out var oldInvites))
            return null;

        foreach (var newInvite in newInvites)
        {
            if (!oldInvites.TryGetValue(newInvite.Code, out var oldInvite)) continue;
            if (newInvite.Uses > oldInvite.Uses)
                return newInvite;
        }

        return null;
    }

    private async Task UpdateInviteCount(ulong inviterId, ulong guildId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var inviter = await uow.InviteCounts.FirstOrDefaultAsync(x => x.UserId == inviterId && x.GuildId == guildId);

        if (inviter == null)
        {
            inviter = new InviteCount
            {
                UserId = inviterId, GuildId = guildId, Count = 1
            };
            await uow.InsertAsync(inviter);
        }
        else
        {
            inviter.Count++;
        }
    }

    private async Task UpdateInvitedBy(ulong userId, ulong inviterId, ulong guildId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var invitedUser = new InvitedBy
        {
            UserId = userId, InviterId = inviterId, GuildId = guildId
        };

        await uow.InsertAsync(invitedUser);
    }

    /// <summary>
    ///     Gets the invite count for a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="guildId"></param>
    /// <returns></returns>
    public async Task<int> GetInviteCount(ulong userId, ulong guildId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var inviteCount = await uow.InviteCounts
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .Select(x => x.Count)
            .FirstOrDefaultAsync();

        return inviteCount;
    }

    /// <summary>
    ///     Gets who invited a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="guild"></param>
    /// <returns></returns>
    public async Task<IUser?> GetInviter(ulong userId, IGuild guild)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var inviterId = await uow.InvitedBies
            .Where(x => x.UserId == userId && x.GuildId == guild.Id)
            .Select(x => x.InviterId)
            .FirstOrDefaultAsync();

        return inviterId != 0 ? await guild.GetUserAsync(inviterId) : null;
    }

    /// <summary>
    ///     Gets all users invited by a user
    /// </summary>
    /// <param name="inviterId"></param>
    /// <param name="guild"></param>
    /// <returns></returns>
    public async Task<List<IUser>> GetInvitedUsers(ulong inviterId, IGuild guild)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var invitedUserIds = await uow.InvitedBies
            .Where(x => x.InviterId == inviterId && x.GuildId == guild.Id)
            .Select(x => x.UserId)
            .ToListAsync();

        var invitedUsers = new List<IUser>();
        foreach (var userId in invitedUserIds)
        {
            var user = await guild.GetUserAsync(userId);
            if (user != null)
                invitedUsers.Add(user);
        }

        return invitedUsers;
    }

    /// <summary>
    ///     Gets the invite leaderboard
    /// </summary>
    /// <param name="guild"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    public async Task<List<(ulong UserId, string Username, int InviteCount)>> GetInviteLeaderboardAsync(IGuild guild,
        int page = 1, int pageSize = 10)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var leaderboard = await uow.InviteCounts
            .Where(x => x.GuildId == guild.Id)
            .OrderByDescending(x => x.Count)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.UserId, x.Count
            })
            .ToListAsync();


        var result = new List<(ulong UserId, string Username, int InviteCount)>();
        foreach (var entry in leaderboard)
        {
            var user = await guild.GetUserAsync(entry.UserId);
            var username = user?.Username ?? "Unknown User";
            result.Add((entry.UserId, username, entry.Count));
        }

        return result;
    }

    private async Task ProcessGuildWithRateLimiting(IGuild guild, SemaphoreSlim semaphore)
    {
        try
        {
            await semaphore.WaitAsync();
            await UpdateGuildInvites(guild);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating invites for guild {guild.Id}: {ex.Message}");
        }
        finally
        {
            semaphore.Release();
            await Task.Delay(100);
        }
    }
}