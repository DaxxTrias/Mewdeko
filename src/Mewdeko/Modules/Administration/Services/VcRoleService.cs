using System.Collections.Concurrent;
using System.Data;
using LinqToDB;
using Mewdeko.Common.ModuleBehaviors;
using Serilog;
using DiscordShardedClient = Discord.WebSocket.DiscordShardedClient;
using VcRole = DataModel.VcRole;


namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     The service for managing voice channel roles.
/// </summary>
public class VcRoleService : INService, IReadyExecutor
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VcRoleService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="eventHandler">The event handler.</param>
    public VcRoleService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        EventHandler eventHandler)
    {
        this.dbFactory = dbFactory;
        this.client = client;

        eventHandler.UserVoiceStateUpdated += ClientOnUserVoiceStateUpdated;

        ToAssign = new NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<(bool, IGuildUser, IRole)>>();

        Task.Run(async () =>
        {
            while (true)
            {
                var tasks = ToAssign.Values.Select(queue => Task.Run(async () =>
                {
                    while (queue.TryDequeue(out var item))
                    {
                        var (add, user, role) = item;
                        if (user?.Guild == null || role == null) continue; // Basic validation
                        try
                        {
                            if (add)
                            {
                                if (!user.RoleIds.Contains(role.Id))
                                    await user.AddRoleAsync(role).ConfigureAwait(false);
                            }
                            else
                            {
                                if (user.RoleIds.Contains(role.Id))
                                    await user.RemoveRoleAsync(role).ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex)
                        {
                             // Log specifics? e.g., user ID, role ID, guild ID
                             Log.Warning(ex, "Failed to modify role {RoleId} for user {UserId} in guild {GuildId}", role.Id, user.Id, user.GuildId);
                        }
                        await Task.Delay(250).ConfigureAwait(false);
                    }
                }));
                // Wait for all current queue processing tasks + 1 second delay
                await Task.WhenAll(tasks.Append(Task.Delay(1000))).ConfigureAwait(false);
            }
        });

        eventHandler.LeftGuild += _client_LeftGuild;
        eventHandler.JoinedGuild += Bot_JoinedGuild;
    }

    /// <summary>
    ///     A dictionary that maps guild IDs to another dictionary, which maps voice channel IDs to roles.
    /// </summary>
    public NonBlocking.ConcurrentDictionary<ulong, NonBlocking.ConcurrentDictionary<ulong, IRole>> VcRoles { get; } = new();

    /// <summary>
    ///     A dictionary that maps guild IDs to a queue of tuples, each containing a boolean indicating whether to add or
    ///     remove a role, a guild user, and a role.
    /// </summary>
    private NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<(bool, IGuildUser, IRole)>> ToAssign { get; }

    /// <summary>
    ///     Event handler for when the bot joins a guild. Initializes voice channel roles for the guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    private async Task Bot_JoinedGuild(IGuild guild)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var conf = await db.GetTable<VcRole>().Where(x => x.GuildId == guild.Id).ToListAsync().ConfigureAwait(false); // Use ToListAsync
        if (!conf.Any())
            return;
        await InitializeVcRole(conf); // Pass List<VcRole>
    }

    /// <summary>
    ///     Event handler for when the bot leaves a guild. Removes voice channel roles for the guild.
    /// </summary>
    /// <param name="arg">The guild.</param>
    private Task _client_LeftGuild(SocketGuild arg)
    {
        VcRoles.TryRemove(arg.Id, out _);
        ToAssign.TryRemove(arg.Id, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Initializes voice channel roles for a guild.
    /// </summary>
    /// <param name="config">The VcRole configuration list.</param>
    private async Task InitializeVcRole(List<VcRole> config) // Accept List
    {
        await Task.Yield(); // Allow context switching if needed
        var firstConf = config.FirstOrDefault();
        if (firstConf == null) return;

        var guildId = firstConf.GuildId;
        var g = client.GetGuild(guildId);
        if (g == null)
            return;

        var infos = new NonBlocking.ConcurrentDictionary<ulong, IRole>();
        VcRoles.AddOrUpdate(guildId, infos, (_, _) => infos); // Use AddOrUpdate correctly

        var missingRoles = new List<VcRole>();
        foreach (var ri in config)
        {
            var role = g.GetRole(ri.RoleId); // Use GetRole for efficiency
            if (role == null)
            {
                missingRoles.Add(ri);
                continue;
            }
            infos.TryAdd(ri.VoiceChannelId, role);
        }

        if (missingRoles.Any())
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            try
            {
                 var idsToRemove = missingRoles.Select(r => r.Id).ToList(); // Assuming Id is PK
                 var deletedCount = await db.GetTable<VcRole>()
                     .Where(x => idsToRemove.Contains(x.Id))
                     .DeleteAsync().ConfigureAwait(false);
                 Log.Warning("Removed {MissingRolesCount} missing VcRoles from DB for Guild {GuildId}", deletedCount, guildId);
            }
            catch (Exception ex)
            {
                 Log.Error(ex, "Error removing missing VcRoles from DB for Guild {GuildId}", guildId);
            }
        }
    }

    /// <summary>
    ///     Adds a voice channel role to a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the role to.</param>
    /// <param name="role">The role to add.</param>
    /// <param name="vcId">The ID of the voice channel to associate the role with.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddVcRole(ulong guildId, IRole role, ulong vcId)
    {
        ArgumentNullException.ThrowIfNull(role);

        var guildVcRoles = VcRoles.GetOrAdd(guildId, new NonBlocking.ConcurrentDictionary<ulong, IRole>());
        guildVcRoles.AddOrUpdate(vcId, role, (_, _) => role);

        const int maxRetries = 3;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false); // Use transaction

                // Delete existing setting(s) for this VC ID and Guild ID first
                await db.GetTable<VcRole>()
                    .Where(x => x.VoiceChannelId == vcId && x.GuildId == guildId)
                    .DeleteAsync().ConfigureAwait(false);

                // Insert the new one
                var newVcRole = new VcRole
                {
                    GuildId = guildId, VoiceChannelId = vcId, RoleId = role.Id
                };
                await db.InsertAsync(newVcRole).ConfigureAwait(false);

                await tx.CommitAsync().ConfigureAwait(false);
                return; // Success
            }
            // Catch specific exceptions if possible, otherwise generic Exception
            catch (Exception ex) when (ex is LinqToDBException || ex.InnerException is Npgsql.PostgresException) // Example: Handle potential DB specific or concurrency exceptions
            {
                 attempt++;
                 if (attempt >= maxRetries)
                 {
                     Log.Error(ex, "Failed to save VcRole after {Attempts} attempts due to concurrency or DB error for Guild {GuildId}, VC {VoiceChannelId}", attempt, guildId, vcId);
                     // Rethrow or handle failure case
                     throw;
                 }
                 await Task.Delay(100 * attempt).ConfigureAwait(false); // Backoff delay
            }
            catch(Exception ex) // Catch other unexpected errors
            {
                 Log.Error(ex, "Unexpected error saving VcRole for Guild {GuildId}, VC {VoiceChannelId}", guildId, vcId);
                 throw; // Rethrow unexpected errors
            }
        }
    }


    /// <summary>
    ///     Removes a voice channel role from a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to remove the role from.</param>
    /// <param name="vcId">The ID of the voice channel to disassociate the role from.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> RemoveVcRole(ulong guildId, ulong vcId)
    {
        if (!VcRoles.TryGetValue(guildId, out var guildVcRoles))
            return false;

        var removedFromCache = guildVcRoles.TryRemove(vcId, out _);

        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.GetTable<VcRole>()
            .Where(x => x.VoiceChannelId == vcId && x.GuildId == guildId) // Ensure GuildId match
            .DeleteAsync().ConfigureAwait(false);

        return removedFromCache || deletedCount > 0; // Return true if removed from cache OR db
    }

    /// <summary>
    ///     Event handler for when a user's voice state is updated. Assigns or removes roles based on the user's new voice
    ///     state.
    /// </summary>
    /// <param name="usr">The user whose voice state was updated.</param>
    /// <param name="oldState">The user's old voice state.</param>
    /// <param name="newState">The user's new voice state.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task ClientOnUserVoiceStateUpdated(SocketUser usr, SocketVoiceState oldState, SocketVoiceState newState)
    {
        if (usr is not SocketGuildUser gusr)
            return Task.CompletedTask;

        var oldVc = oldState.VoiceChannel;
        var newVc = newState.VoiceChannel;

        _ = Task.Run(() => // Run role assignment logic in background
        {
            try
            {
                if (oldVc?.Id == newVc?.Id) return; // No channel change
                var guildId = newVc?.Guild.Id ?? oldVc?.Guild.Id; // Get GuildId safely
                if(guildId == null) return; // Cannot proceed without GuildId

                if (!VcRoles.TryGetValue(guildId.Value, out var guildVcRoles)) return;

                // Remove old role if applicable
                if (oldVc != null && guildVcRoles.TryGetValue(oldVc.Id, out var roleToRemove))
                    Assign(false, gusr, roleToRemove);

                // Add new role if applicable
                if (newVc != null && guildVcRoles.TryGetValue(newVc.Id, out var roleToAdd))
                    Assign(true, gusr, roleToAdd);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in VcRoleService VoiceStateUpdate for user {UserId}", usr.Id);
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Assigns a role to a user in a guild.
    /// </summary>
    /// <param name="addRole">A boolean indicating whether to add or remove the role.</param>
    /// <param name="gusr">The user in the guild.</param>
    /// <param name="role">The role to assign or remove.</param>
    private void Assign(bool addRole, SocketGuildUser gusr, IRole role)
    {
        var queue = ToAssign.GetOrAdd(gusr.Guild.Id, _ => new ConcurrentQueue<(bool, IGuildUser, IRole)>());
        queue.Enqueue((addRole, gusr, role));
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        var guilds = client.Guilds;
        foreach (var guild in guilds)
        {
            try
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                var conf = await db.GetTable<VcRole>()
                    .Where(x => x.GuildId == guild.Id)
                    .ToListAsync().ConfigureAwait(false);

                if (!conf.Any())
                    continue;

                await InitializeVcRole(conf);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading VC roles on ready for guild {GuildId}", guild.Id);
            }
        }
    }
}