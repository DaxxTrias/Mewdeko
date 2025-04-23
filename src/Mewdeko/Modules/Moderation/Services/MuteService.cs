using System.Diagnostics;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Data;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Serilog;

namespace Mewdeko.Modules.Moderation.Services;

/// <summary>
///     Represents the type of mute.
/// </summary>
public enum MuteType
{
    /// <summary>
    ///     Voice mute.
    /// </summary>
    Voice,

    /// <summary>
    ///     Chat mute.
    /// </summary>
    Chat,

    /// <summary>
    ///     Mute in both voice and chat.
    /// </summary>
    All
}

/// <summary>
///     Service for managing mutes.
/// </summary>
public class MuteService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     The type of timer for punishment.
    /// </summary>
    public enum TimerType
    {
        /// <summary>
        ///     Mute
        /// </summary>
        Mute,

        /// <summary>
        ///     Yeet
        /// </summary>
        Ban,

        /// <summary>
        ///     Add role
        /// </summary>
        AddRole
    }

    private static readonly OverwritePermissions DenyOverwrite =
        new(addReactions: PermValue.Deny, sendMessages: PermValue.Deny,
            attachFiles: PermValue.Deny, sendMessagesInThreads: PermValue.Deny, createPublicThreads: PermValue.Deny);

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;

    private readonly GuildSettingsService guildSettings;

    private readonly ConcurrentDictionary<TimerKey, TimerQueueItem> _scheduledItems = new();
    private Timer _processingTimer;
    private readonly object _timerLock = new();
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    private bool _isProcessing;

    private record struct TimerKey(ulong GuildId, ulong UserId, TimerType Type, ulong? RoleId = null);


    /// <summary>
    ///     Roles to remove on mute.
    /// </summary>
    public string[] Uroles = [];

    /// <summary>
    ///     Initializes a new instance of <see cref="MuteService" />.
    /// </summary>
    /// <param name="client">The discord client</param>
    /// <param name="dbFactory">The database provider</param>
    /// <param name="guildSettings">Service for retrieving guildconfigs</param>
    /// <param name="eventHandler">Handler for async events (Hear that dnet? ASYNC, not GATEWAY THREAD)</param>
    /// <param name="bot">The bot</param>
    public MuteService(DiscordShardedClient client, IDataConnectionFactory dbFactory, GuildSettingsService guildSettings,
        EventHandler eventHandler, Mewdeko bot)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.guildSettings = guildSettings;
        eventHandler.UserJoined += Client_UserJoined;
        UserMuted += OnUserMuted;
        UserUnmuted += OnUserUnmuted;
    }

    /// <summary>
    ///     Guild mute roles cache.
    /// </summary>
    public ConcurrentDictionary<ulong, string> GuildMuteRoles { get; } = new();

    /// <summary>
    ///     Muted users cache.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentHashSet<ulong>> MutedUsers { get; set; }

    /// <summary>
    ///     Unmute timers cache.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentDictionary<(ulong, TimerType), Timer>> UnTimers { get; }
        = new();

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var now = DateTime.UtcNow;

            // Load muted users
            var mutedUsersList = await dbContext.MutedUserIds
                .Where(x => x.GuildId != null && x.GuildId != 0)
                .Select(x => new
                {
                    x.GuildId, x.UserId
                })
                .Distinct()
                .ToListAsync();

            MutedUsers = new ConcurrentDictionary<ulong, ConcurrentHashSet<ulong>>(
                mutedUsersList
                    .GroupBy(x =>
                    {
                        Debug.Assert(x.GuildId != null, "x.GuildId != null");
                        return x.GuildId.Value;
                    })
                    .ToDictionary(
                        g => g.Key,
                        g => new ConcurrentHashSet<ulong>(g.Select(x => x.UserId).Distinct())
                    )
            );

            // Load unmute timers
            var unmuteTimers = await dbContext.UnmuteTimers
                .Where(x => x.GuildId != null && x.GuildId != 0)
                .Select(x => new
                {
                    x.GuildId, x.UserId, x.UnmuteAt
                })
                .ToListAsync();

            foreach (var timer in unmuteTimers)
            {
                var key = new TimerKey(timer.GuildId.Value, timer.UserId, TimerType.Mute);
                var item = new TimerQueueItem(key, timer.UnmuteAt);
                _scheduledItems[key] = item;
            }

            // Load unban timers
            var unbanTimers = await dbContext.UnbanTimers
                .Where(x => x.GuildId != null && x.GuildId != 0)
                .Select(x => new
                {
                    x.GuildId, x.UserId, x.UnbanAt
                })
                .ToListAsync();

            foreach (var timer in unbanTimers)
            {
                var key = new TimerKey(timer.GuildId.Value, timer.UserId, TimerType.Ban);
                var item = new TimerQueueItem(key, timer.UnbanAt);
                _scheduledItems[key] = item;
            }

            // Load unrole timers
            var unroleTimers = await dbContext.UnroleTimers
                .Select(x => new
                {
                    x.GuildId, x.UserId, x.RoleId, x.UnbanAt
                })
                .ToListAsync();

            foreach (var timer in unroleTimers)
            {
                var key = new TimerKey(timer.GuildId.Value, timer.UserId, TimerType.AddRole, timer.RoleId);
                var item = new TimerQueueItem(key, timer.UnbanAt);
                _scheduledItems[key] = item;
            }

            Log.Information(
                "Loaded {UnmuteCount} unmute timers, {UnbanCount} unban timers, and {UnroleCount} unrole timers",
                unmuteTimers.Count, unbanTimers.Count, unroleTimers.Count);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error in MuteService.OnReadyAsync");
        }
    }

    /// <summary>
    ///     Event for when a user is muted.
    /// </summary>
    public event EventHandler.AsyncEventHandler<IGuildUser, IUser, MuteType, string> UserMuted;

    /// <summary>
    ///     Event for when a user is unmuted.
    /// </summary>
    public event EventHandler.AsyncEventHandler<IGuildUser, IUser, MuteType, string> UserUnmuted;


    private async void ProcessExpiredItemsCallback(object state)
    {
        if (_isProcessing)
            return;

        lock (_timerLock)
        {
            if (_isProcessing)
                return;
            _isProcessing = true;
        }

        try
        {
            await ProcessExpiredItems();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing timer items");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task ProcessExpiredItems()
    {
        try
        {
            var now = DateTime.UtcNow;

            // Find all expired items
            var expiredItems = _scheduledItems.Values
                .Where(item => !item.IsProcessing && item.ExecuteAt <= now)
                .ToList();

            if (expiredItems.Count == 0)
                return;

            Log.Information("Processing {Count} expired timer items", expiredItems.Count);

            // Process items by type in batches
            var muteItems = expiredItems.Where(x => x.Key.Type == TimerType.Mute).ToList();
            var banItems = expiredItems.Where(x => x.Key.Type == TimerType.Ban).ToList();
            var roleItems = expiredItems.Where(x => x.Key.Type == TimerType.AddRole).ToList();

            // Mark all as processing to prevent duplicate processing
            foreach (var item in expiredItems)
            {
                item.IsProcessing = true;
            }

            // Process each batch
            if (muteItems.Any())
                await ProcessMuteItems(muteItems);

            if (banItems.Any())
                await ProcessBanItems(banItems);

            if (roleItems.Any())
                await ProcessRoleItems(roleItems);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing expired items");

            // Clean up database connections
            await CleanupDatabaseConnections();
        }
    }

    private async Task ProcessMuteItems(List<TimerQueueItem> items)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var successfulItems = new List<TimerQueueItem>();

        try
        {
            // Process each item
            foreach (var item in items)
            {
                try
                {
                    var guildId = item.Key.GuildId;
                    var userId = item.Key.UserId;

                    // Find the user
                    var guild = client.GetGuild(guildId);
                    if (guild != null)
                    {
                        // Unmute the user
                        await UnmuteUser(guildId, userId, client.CurrentUser, reason: "Timed mute expired");
                    }

                    // Add to successful items
                    successfulItems.Add(item);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error processing mute timer for guild {GuildId}, user {UserId}",
                        item.Key.GuildId, item.Key.UserId);
                }
            }

            // Remove successful items from the database and local queue
            if (successfulItems.Any())
            {
                // Get IDs to remove
                var idsToRemove = successfulItems.Select(i => new
                {
                    i.Key.GuildId, i.Key.UserId
                }).ToList();

                // Find the matching database entries by ID
                var dbEntries = await dbContext.UnmuteTimers
                    .Where(x => idsToRemove.Any(id => id.GuildId == x.GuildId && id.UserId == x.UserId))
                    .Select(x => x.Id)
                    .ToListAsync();

                // Remove them
                foreach (var id in dbEntries)
                {
                    await dbContext.UnmuteTimers.Where(x => x.Id == id).DeleteAsync();
                }

                // Remove from local queue
                foreach (var item in successfulItems)
                {
                    _scheduledItems.TryRemove(item.Key, out _);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in batch processing of mute timers");

            // Reset processing flag for failed items
            foreach (var item in items.Except(successfulItems))
            {
                item.IsProcessing = false;
            }
        }
    }

    private async Task ProcessBanItems(List<TimerQueueItem> items)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var successfulItems = new List<TimerQueueItem>();

        try
        {
            // Process each item
            foreach (var item in items)
            {
                try
                {
                    var guildId = item.Key.GuildId;
                    var userId = item.Key.UserId;

                    // Find the guild
                    var guild = client.GetGuild(guildId);
                    if (guild != null)
                    {
                        // Unban the user
                        await guild.RemoveBanAsync(userId);
                    }

                    // Add to successful items
                    successfulItems.Add(item);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error processing ban timer for guild {GuildId}, user {UserId}",
                        item.Key.GuildId, item.Key.UserId);
                }
            }

            // Remove successful items from the database and local queue
            if (successfulItems.Any())
            {
                // Get IDs to remove
                var idsToRemove = successfulItems.Select(i => new
                {
                    i.Key.GuildId, i.Key.UserId
                }).ToList();

                // Find the matching database entries by ID
                var dbEntries = await dbContext.UnbanTimers
                    .Where(x => idsToRemove.Any(id => id.GuildId == x.GuildId && id.UserId == x.UserId))
                    .Select(x => x.Id)
                    .ToListAsync();

                // Remove them
                foreach (var id in dbEntries)
                {
                    await dbContext.UnbanTimers.Where(x => x.Id == id).DeleteAsync();
                }

                // Remove from local queue
                foreach (var item in successfulItems)
                {
                    _scheduledItems.TryRemove(item.Key, out _);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in batch processing of ban timers");

            // Reset processing flag for failed items
            foreach (var item in items.Except(successfulItems))
            {
                item.IsProcessing = false;
            }
        }
    }

    private async Task ProcessRoleItems(List<TimerQueueItem> items)
    {
         await using var dbContext = await dbFactory.CreateConnectionAsync();
        var successfulItems = new List<TimerQueueItem>();

        try
        {
            // Process each item
            foreach (var item in items)
            {
                try
                {
                    var guildId = item.Key.GuildId;
                    var userId = item.Key.UserId;
                    var roleId = item.Key.RoleId.Value; // Safe to use .Value since we filtered for role items

                    // Find the user and role
                    var guild = client.GetGuild(guildId);
                    if (guild != null)
                    {
                        var user = guild.GetUser(userId);
                        var role = guild.GetRole(roleId);

                        if (user != null && role != null && user.Roles.Contains(role))
                        {
                            // Remove the role
                            await user.RemoveRoleAsync(role);
                        }
                    }

                    // Add to successful items
                    successfulItems.Add(item);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error processing role timer for guild {GuildId}, user {UserId}, role {RoleId}",
                        item.Key.GuildId, item.Key.UserId, item.Key.RoleId);
                }
            }

            // Remove successful items from the database and local queue
            if (successfulItems.Any())
            {
                // Get IDs to remove
                var idsToRemove = successfulItems.Select(i => new
                {
                    i.Key.GuildId, i.Key.UserId, i.Key.RoleId
                }).ToList();

                // Find the matching database entries by ID
                var dbEntries = await dbContext.UnroleTimers
                    .Where(x => idsToRemove.Any(id =>
                        id.GuildId == x.GuildId && id.UserId == x.UserId && id.RoleId == id.RoleId))
                    .Select(x => x.Id)
                    .ToListAsync();

                // Remove them
                foreach (var id in dbEntries)
                {
                    await dbContext.UnroleTimers.Select(x => x.Id == id).DeleteAsync();
                }

                // Remove from local queue
                foreach (var item in successfulItems)
                {
                    _scheduledItems.TryRemove(item.Key, out _);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in batch processing of role timers");

            // Reset processing flag for failed items
            foreach (var item in items.Except(successfulItems))
            {
                item.IsProcessing = false;
            }
        }
    }

// Add cleanup method for database connections
    private async Task CleanupDatabaseConnections()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            // Create and immediately dispose a context to help reset the connection pool
            await using var context = await dbFactory.CreateConnectionAsync();

            // Execute a simple command to verify the connection is working
            await context.ExecuteAsync("SELECT 1");

            // Explicitly close the connection
            await context.CloseAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during database connection cleanup");
        }
    }

    private static async Task OnUserMuted(IGuildUser user, IUser mod, MuteType type, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return;

        await user.SendMessageAsync(embed: new EmbedBuilder()
            .WithDescription($"You've been muted in {user.Guild} server")
            .AddField("Mute Type", type.ToString())
            .AddField("Moderator", mod.ToString())
            .AddField("Reason", reason)
            .Build());
    }

    private static async Task OnUserUnmuted(IGuildUser user, IUser mod, MuteType type, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return;

        await user.SendMessageAsync(embed: new EmbedBuilder()
            .WithDescription($"You've been unmuted in {user.Guild} server")
            .AddField("Unmute Type", type.ToString())
            .AddField("Moderator", mod.ToString())
            .AddField("Reason", reason)
            .Build());
    }

    private async Task Client_UserJoined(IGuildUser? usr)
    {
        try
        {
            MutedUsers.TryGetValue(usr.Guild.Id, out var muted);

            if (muted == null || !muted.Contains(usr.Id))
                return;
            await MuteUser(usr, client.CurrentUser, reason: "Sticky mute").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in MuteService UserJoined event");
        }
    }

    /// <summary>
    ///     Sets the mute role for a guild.
    /// </summary>
    /// <param name="guildId">The id of the guild to set the role in</param>
    /// <param name="name">The name of the role (What in your right fucking mind possessed you to make it this way kwoth???)</param>
    public async Task SetMuteRoleAsync(ulong guildId, string name)
    {
         await using var dbContext = await dbFactory.CreateConnectionAsync();

         var config = await guildSettings.GetGuildConfig(guildId);
        config.MuteRoleName = name;
        GuildMuteRoles.AddOrUpdate(guildId, name, (_, _) => name);
        await guildSettings.UpdateGuildConfig(guildId, config);
    }

    /// <summary>
    ///     Flow for muting a user
    /// </summary>
    /// <param name="usr">The user to mute </param>
    /// <param name="mod">The mod who muted the user</param>
    /// <param name="type">The type of mute</param>
    /// <param name="reason">The mute reason</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task MuteUser(IGuildUser? usr, IUser mod, MuteType type = MuteType.All, string reason = "")
    {
        switch (type)
        {
            case MuteType.All:
            {
                try
                {
                    await usr.ModifyAsync(x => x.Mute = true).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }

                 await using var dbContext = await dbFactory.CreateConnectionAsync();

                // Get user roles if needed for persistence
                var roles = usr.GetRoles().Where(p => p.Tags == null).Except([
                    usr.Guild.EveryoneRole
                ]);
                var enumerable = roles as IRole[] ?? roles.ToArray();
                var uroles = string.Join(" ", enumerable.Select(x => x.Id));

                // Remove any existing entry
                var existingMute = await dbContext.MutedUserIds
                    .FirstOrDefaultAsync(x => x.GuildId == usr.Guild.Id && x.UserId == usr.Id);

                if (existingMute != null)
                    await dbContext.MutedUserIds.Select(x => existingMute).DeleteAsync();

                // Create new entry based on settings
                var removeOnMute = await GetRemoveOnMute(usr.Guild.Id);
                var mutedUser = new MutedUserId
                {
                    GuildId = usr.Guild.Id, UserId = usr.Id, Roles = removeOnMute == 1 ? uroles : null
                };

                await dbContext.InsertAsync(mutedUser);

                if (MutedUsers.TryGetValue(usr.Guild.Id, out var muted))
                    muted.Add(usr.Id);

                // Remove any existing unmute timers
                var timersToRemove = dbContext.UnmuteTimers
                    .Where(x => x.GuildId == usr.Guild.Id && x.UserId == usr.Id);

                await timersToRemove.DeleteAsync();

                // Apply mute role
                var muteRole = await GetMuteRole(usr.Guild).ConfigureAwait(false);
                if (!usr.RoleIds.Contains(muteRole.Id))
                {
                    if (removeOnMute == 1)
                        await usr.RemoveRolesAsync(enumerable).ConfigureAwait(false);
                }

                await usr.AddRoleAsync(muteRole).ConfigureAwait(false);
                StopTimer(usr.GuildId, usr.Id, TimerType.Mute);

                await UserMuted(usr, mod, MuteType.All, reason);
                break;
            }
            case MuteType.Voice:
                try
                {
                    await usr.ModifyAsync(x => x.Mute = true).ConfigureAwait(false);
                    await UserMuted(usr, mod, MuteType.Voice, reason);
                }
                catch
                {
                    // ignored
                }

                break;
            case MuteType.Chat:
                await usr.AddRoleAsync(await GetMuteRole(usr.Guild).ConfigureAwait(false)).ConfigureAwait(false);
                await UserMuted(usr, mod, MuteType.Chat, reason);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    /// <summary>
    ///     gets whether roles should be removed on mute
    /// </summary>
    /// <param name="id">The server id</param>
    /// <returns></returns>
    public async Task<int> GetRemoveOnMute(ulong id)
    {
        return (await guildSettings.GetGuildConfig(id)).Removeroles;
    }

    /// <summary>
    ///     Sets whether roles should be removed on mute
    /// </summary>
    /// <param name="guild">The server to set this setting</param>
    /// <param name="yesnt">nosnt</param>
    public async Task Removeonmute(IGuild guild, string yesnt)
    {
        var yesno = -1;

        yesno = yesnt switch
        {
            "y" => 1,
            "n" => 0,
            _ => yesno
        };

        var gc = await guildSettings.GetGuildConfig(guild.Id);
        gc.Removeroles = yesno;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Flow for unmuting a user
    /// </summary>
    /// <param name="guildId">The guildId where the user is unmooted</param>
    /// <param name="usrId">The user to unmoot </param>
    /// <param name="mod">The mod who unmooted the user</param>
    /// <param name="type">The type of moot</param>
    /// <param name="reason">The unmoot reason reason</param>
    public async Task UnmuteUser(ulong guildId, ulong usrId, IUser mod, MuteType type = MuteType.All,
        string reason = "")
    {
        var usr = client.GetGuild(guildId)?.GetUser(usrId);
        switch (type)
        {
           case MuteType.All:
        {
            StopTimer(guildId, usrId, TimerType.Mute);

             await using var dbContext = await dbFactory.CreateConnectionAsync();

            // Find the muted user directly
            var mutedUser = await dbContext.MutedUserIds
                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == usrId);

            if (usr != null && await GetRemoveOnMute(usr.Guild.Id) == 1 && mutedUser?.Roles != null)
            {
                try
                {
                    Uroles = mutedUser.Roles.Split(' ');

                    // Restore roles
                    if (Uroles != null)
                    {
                        foreach (var i in Uroles)
                            if (ulong.TryParse(i, out var roleId))
                                try
                                {
                                    await usr.AddRoleAsync(usr.Guild.GetRole(roleId)).ConfigureAwait(false);
                                }
                                catch
                                {
                                    // ignored
                                }
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            // Remove muted user entry
            if (mutedUser != null)
                await dbContext.MutedUserIds.Select(x => mutedUser).DeleteAsync();

            if (MutedUsers.TryGetValue(guildId, out var muted))
                muted.TryRemove(usrId);

            // Remove unmute timers by ID
            var timersToRemove = await dbContext.UnmuteTimers
                .Where(x => x.GuildId == guildId && x.UserId == usrId)
                .Select(x => x.Id)
                .ToListAsync();

            foreach (var id in timersToRemove)
            {
                await dbContext.UnmuteTimers.Select(x => x.Id == id).DeleteAsync();
            }

            if (usr != null)
            {
                try
                {
                    await usr.ModifyAsync(x => x.Mute = false).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }

                try
                {
                    await usr.RemoveRoleAsync(await GetMuteRole(usr.Guild).ConfigureAwait(false))
                        .ConfigureAwait(false);
                }
                catch
                {
                    /*ignore*/
                }

                await UserUnmuted(usr, mod, MuteType.All, reason);
            }

            break;
        }
            case MuteType.Voice when usr == null:
                return;
            case MuteType.Voice:
                try
                {
                    await usr.ModifyAsync(x => x.Mute = false).ConfigureAwait(false);
                    await UserUnmuted(usr, mod, MuteType.Voice, reason);
                }
                catch
                {
                    // ignored
                }

                break;
            case MuteType.Chat when usr == null:
                return;
            case MuteType.Chat:
                await usr.RemoveRoleAsync(await GetMuteRole(usr.Guild).ConfigureAwait(false)).ConfigureAwait(false);
                await UserUnmuted(usr, mod, MuteType.Chat, reason);
                break;
        }
    }

    /// <summary>
    ///     Gets the mute role for a guild.
    /// </summary>
    /// <param name="guild">The guildid to get the mute role from</param>
    /// <returns>The mute <see cref="IRole" /></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<IRole> GetMuteRole(IGuild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);

        const string defaultMuteRoleName = "Mewdeko-mute";

        var muteRoleName = GuildMuteRoles.GetOrAdd(guild.Id, defaultMuteRoleName);

        var muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName);
        if (muteRole == null)
        {
            //if it doesn't exist, create it
            try
            {
                muteRole = await guild.CreateRoleAsync(muteRoleName, isMentionable: false).ConfigureAwait(false);
            }
            catch
            {
                //if creations fails,  maybe the name != correct, find default one, if doesn't work, create default one
                muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName) ??
                           await guild.CreateRoleAsync(defaultMuteRoleName, isMentionable: false)
                               .ConfigureAwait(false);
            }
        }

        foreach (var toOverwrite in await guild.GetTextChannelsAsync().ConfigureAwait(false))
        {
            try
            {
                if (toOverwrite is IThreadChannel)
                    continue;
                if (toOverwrite.PermissionOverwrites.Any(x => x.TargetId == muteRole.Id
                                                              && x.TargetType == PermissionTarget.Role))
                {
                    continue;
                }

                await toOverwrite.AddPermissionOverwriteAsync(muteRole, DenyOverwrite)
                    .ConfigureAwait(false);

                await Task.Delay(200).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        return muteRole;
    }

    /// <summary>
    ///     Mutes a user for a specified amount of time.
    /// </summary>
    /// <param name="user">The user to mute</param>
    /// <param name="mod">The mod who muted the user</param>
    /// <param name="after">The time to mute the user for</param>
    /// <param name="muteType">The type of mute</param>
    /// <param name="reason">The reason for the mute</param>
    public async Task TimedMute(IGuildUser? user, IUser mod, TimeSpan after, MuteType muteType = MuteType.All,
        string reason = "")
    {
        await MuteUser(user, mod, muteType, reason).ConfigureAwait(false);

         await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Add unmute timer directly
        var unmuteTimer = new UnmuteTimer
        {
            GuildId = user.GuildId,
            UserId = user.Id,
            UnmuteAt = DateTime.UtcNow + after
        };

        await dbContext.InsertAsync(unmuteTimer);

        StartUn_Timer(user.GuildId, user.Id, after, TimerType.Mute);
    }

    /// <summary>
    ///     Bans a user for a specified amount of time.
    /// </summary>
    /// <param name="guild">The guild to ban the user from</param>
    /// <param name="user">The user to ban</param>
    /// <param name="after">The time to ban the user for</param>
    /// <param name="reason">The reason for the ban</param>
    /// <param name="todelete">The time to delete the ban message</param>
    public async Task TimedBan(IGuild guild, IUser? user, TimeSpan after, string reason, TimeSpan todelete = default)
    {
        await guild.AddBanAsync(user.Id, todelete.Days, options: new RequestOptions
        {
            AuditLogReason = reason
        }).ConfigureAwait(false);

         await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Add unban timer directly
        var unbanTimer = new UnbanTimer
        {
            GuildId = guild.Id,
            UserId = user.Id,
            UnbanAt = DateTime.UtcNow + after
        };

        await dbContext.InsertAsync(unbanTimer);
        StartUn_Timer(guild.Id, user.Id, after, TimerType.Ban);
    }

    /// <summary>
    ///     Adds a role to a user for a specified amount of time.
    /// </summary>
    /// <param name="user">The user to add the role to</param>
    /// <param name="after">The time to add the role for</param>
    /// <param name="reason">The reason for adding the role</param>
    /// <param name="role">The role to add</param>
    public async Task TimedRole(IGuildUser? user, TimeSpan after, string reason, IRole role)
    {
        await user.AddRoleAsync(role).ConfigureAwait(false);

         await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Add unrole timer directly
        var unroleTimer = new UnroleTimer
        {
            GuildId = user.GuildId,
            UserId = user.Id,
            UnbanAt = DateTime.UtcNow + after,
            RoleId = role.Id
        };

        await dbContext.InsertAsync(unroleTimer);

        StartUn_Timer(user.GuildId, user.Id, after, TimerType.AddRole, role.Id);
    }

    /// <summary>
    ///     Starts a timer to unmute a user.
    /// </summary>
    /// <param name="guildId">The guildId where the user is unmuted</param>
    /// <param name="userId">The user to unmute</param>
    /// <param name="after">The time to unmute the user after</param>
    /// <param name="type">The type of timer</param>
    /// <param name="roleId">The role to remove</param>
    public void StartUn_Timer(ulong guildId, ulong userId, TimeSpan after, TimerType type, ulong? roleId = null)
    {
        var executeAt = DateTime.UtcNow + after;
        var key = new TimerKey(guildId, userId, type, roleId);
        var item = new TimerQueueItem(key, executeAt);

        // Add to queue
        _scheduledItems[key] = item;
    }


    /// <summary>
    ///     Stops a timer for a user.
    /// </summary>
    /// <param name="guildId">The guildId where the timer is stopped</param>
    /// <param name="userId">The user to stop the timer for</param>
    /// <param name="type">The type of timer</param>
    public void StopTimer(ulong guildId, ulong userId, TimerType type, ulong? roleId = null)
    {
        var key = new TimerKey(guildId, userId, type, roleId);
        _scheduledItems.TryRemove(key, out _);
    }

    /// <summary>
    /// Dispose
    /// </summary>
    /// <param name="disposing">Depose</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _processingTimer?.Dispose();
        }
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    private class TimerQueueItem
    {
        public TimerKey Key { get; }
        public DateTime ExecuteAt { get; }
        public bool IsProcessing { get; set; }

        public TimerQueueItem(TimerKey key, DateTime executeAt)
        {
            Key = key;
            ExecuteAt = executeAt;
            IsProcessing = false;
        }
    }
}