using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Utility.Common;
using DataModel;
using LinqToDB;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service responsible for managing and executing repeating messages across Discord guilds.
///     Handles initialization, scheduling, and cleanup of message repeaters.
/// </summary>
public class MessageRepeaterService : INService, IReadyExecutor, IDisposable
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly Mewdeko bot;
    private readonly GuildSettingsService gss;
    private readonly EventHandler handler;

    /// <summary>
    ///     Gets the collection of active repeaters organized by guild ID and repeater ID.
    ///     The outer dictionary maps guild IDs to their repeaters, while the inner dictionary maps
    ///     repeater IDs to their respective runner instances.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentDictionary<int, RepeatRunner>> Repeaters { get; }
        = new();

    /// <summary>
    ///     Gets a value indicating whether the repeater service has completed its initialization
    ///     and is ready to handle repeater operations.
    /// </summary>
    public bool RepeaterReady { get; private set; }

    /// <summary>
    ///     Initializes a new instance of the MessageRepeaterService class.
    ///     Sets up event handlers for guild-related events and initializes the service dependencies.
    /// </summary>
    /// <param name="client">The Discord client instance used for sending messages and handling events.</param>
    /// <param name="dbFactory">Provider for database context access.</param>
    /// <param name="bot">The main bot instance.</param>
    /// <param name="gss">Service for accessing guild settings.</param>
    /// <param name="handler">Service for handling Discord events asynchronously</param>
    public MessageRepeaterService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        Mewdeko bot,
        GuildSettingsService gss, EventHandler handler)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.bot = bot;
        this.gss = gss;
        this.handler = handler;

        handler.GuildAvailable += OnGuildAvailable;
        handler.GuildUnavailable += OnGuildUnavailable;
        handler.JoinedGuild += OnJoinedGuild;
        handler.LeftGuild += OnLeftGuild;
    }


    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        try
        {
            Log.Information($"Starting {GetType()} Cache");
            await bot.Ready.Task.ConfigureAwait(false);
            Log.Information("Loading message repeaters");

            foreach (var guild in client.Guilds)
            {
                await InitializeGuildRepeaters(guild);
            }

            RepeaterReady = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during repeater initialization");
            RepeaterReady = false;
        }
    }

    /// <summary>
    ///     Removes a repeater from both the active runners and the database.
    ///     Ensures proper cleanup of resources associated with the repeater.
    /// </summary>
    /// <param name="r">The repeater configuration to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveRepeater(GuildRepeater r)
    {
        if (Repeaters.TryGetValue(r.GuildId, out var guildRepeaters))
        {
            if (guildRepeaters.TryRemove(r.Id, out var runner))
            {
                runner.Stop();
            }
        }

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query by Id and GuildId
        var toDelete = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(x => x.Id == r.Id && x.GuildId == r.GuildId);

        if (toDelete != null)
        {
            await dbContext.DeleteAsync(toDelete);

        }
    }

    /// <summary>
    ///     Updates the last message ID for a specific repeater in the database.
    ///     This is used to track the most recent message sent by each repeater.
    /// </summary>
    /// <param name="repeaterId">The ID of the repeater to update.</param>
    /// <param name="lastMsgId">The ID of the last message sent by the repeater.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetRepeaterLastMessage(int repeaterId, ulong lastMsgId)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var rep = await dbContext.GuildRepeaters.FirstOrDefaultAsync(x => x.Id == repeaterId).ConfigureAwait(false);
            rep.LastMessageId = lastMsgId;

        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update last message for repeater {RepeaterId}", repeaterId);
        }
    }

    private async Task InitializeGuildRepeaters(IGuild guild)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();

            // Direct query with GuildId filter
            var guildRepeaters = await dbContext.GuildRepeaters
                .Where(gr => gr.GuildId == guild.Id && gr.DateAdded != null)
                .ToListAsync();

            var repeaterDictionary = new ConcurrentDictionary<int, RepeatRunner>();

            foreach (var repeater in guildRepeaters)
            {
                try
                {
                    var runner = new RepeatRunner(client, guild, repeater, this);
                    repeaterDictionary.TryAdd(repeater.Id, runner);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize repeater {RepeaterId} for guild {GuildId}",
                        repeater.Id, guild.Id);
                }
            }

            Repeaters.AddOrUpdate(guild.Id, repeaterDictionary,
                (_, existing) =>
                {
                    foreach (var runner in existing.Values)
                    {
                        runner.Stop();
                    }

                    return repeaterDictionary;
                });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load repeaters for Guild {GuildId}", guild.Id);
        }
    }


    private Task OnGuildAvailable(IGuild guild)
    {
        _ = Task.Run(async () =>
        {
            await InitializeGuildRepeaters(guild);
        });
        return Task.CompletedTask;
    }

    private Task OnGuildUnavailable(SocketGuild guild)
    {
        if (!Repeaters.TryRemove(guild.Id, out var repeaters)) return Task.CompletedTask;
        foreach (var runner in repeaters.Values)
        {
            runner.Stop();
        }

        return Task.CompletedTask;
    }

    private Task OnJoinedGuild(IGuild args)
    {
        return OnGuildAvailable(args);
    }

    private Task OnLeftGuild(SocketGuild guild)
    {
        return OnGuildUnavailable(guild);
    }

    /// <summary>
    ///     Creates a new repeater with the specified configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the repeater will run.</param>
    /// <param name="channelId">The ID of the channel where messages will be sent.</param>
    /// <param name="interval">The interval between messages.</param>
    /// <param name="message">The message to repeat.</param>
    /// <param name="startTimeOfDay">Optional specific time of day to start the repeater.</param>
    /// <param name="allowMentions">Whether to allow mentions in the message.</param>
    /// <returns>The created repeater runner.</returns>
    public async Task<RepeatRunner?> CreateRepeaterAsync(
        ulong guildId,
        ulong channelId,
        TimeSpan interval,
        string message,
        string? startTimeOfDay = null,
        bool allowMentions = false)
    {
        var toAdd = new GuildRepeater
        {
            ChannelId = channelId,
            GuildId = guildId,
            Interval = interval.ToString(),
            Message = allowMentions ? message : message.SanitizeMentions(true),
            NoRedundant = false,
            StartTimeOfDay = startTimeOfDay,
            DateAdded = DateTime.UtcNow
        };

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Add directly to Repeaters
        await dbContext.InsertAsync(toAdd);


        var guild = client.GetGuild(guildId);
        if (guild == null) return null;

        var runner = new RepeatRunner(client, guild, toAdd, this);

        Repeaters.AddOrUpdate(guildId,
            new ConcurrentDictionary<int, RepeatRunner>([
                new KeyValuePair<int, RepeatRunner>(toAdd.Id, runner)
            ]), (_, old) =>
            {
                old.TryAdd(runner.Repeater.Id, runner);
                return old;
            });

        return runner;
    }

    /// <summary>
    ///     Updates the message of an existing repeater.
    /// </summary>
    public async Task<bool> UpdateRepeaterMessageAsync(ulong guildId, int repeaterId, string newMessage,
        bool allowMentions)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query by Id and GuildId
        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.Message = allowMentions ? newMessage : newMessage.SanitizeMentions(true);


        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.Message = item.Message;
        }

        return true;
    }


    /// <summary>
    ///     Updates the channel of an existing repeater.
    /// </summary>
    public async Task<bool> UpdateRepeaterChannelAsync(ulong guildId, int repeaterId, ulong newChannelId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query by Id and GuildId
        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.ChannelId = newChannelId;


        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.ChannelId = newChannelId;
        }

        return true;
    }

    /// <summary>
    ///     Toggles the redundancy setting of a repeater.
    /// </summary>
    public async Task<bool> ToggleRepeaterRedundancyAsync(ulong guildId, int repeaterId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query by Id and GuildId
        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.NoRedundant = !item.NoRedundant;


        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.NoRedundant = item.NoRedundant;
        }

        return true;
    }

    /// <summary>
    ///     Gets a repeater by its index in the guild's list.
    /// </summary>
    public RepeatRunner? GetRepeaterByIndex(ulong guildId, int index)
    {
        if (!Repeaters.TryGetValue(guildId, out var guildRepeaters))
            return null;

        var repeaterList = guildRepeaters.ToList();
        if (index < 0 || index >= repeaterList.Count)
            return null;

        return repeaterList[index].Value;
    }

    /// <summary>
    ///     Gets all repeaters for a guild.
    /// </summary>
    public IReadOnlyList<RepeatRunner> GetGuildRepeaters(ulong guildId)
    {
        if (!Repeaters.TryGetValue(guildId, out var guildRepeaters))
            return Array.Empty<RepeatRunner>();

        return guildRepeaters.Values.ToList();
    }

    /// <summary>
    ///     Performs cleanup of resources used by the service.
    ///     Stops all active repeaters and unsubscribes from Discord client events.
    /// </summary>
    public void Dispose()
    {
        foreach (var guildRepeaters in Repeaters.Values)
        {
            foreach (var runner in guildRepeaters.Values)
            {
                runner.Stop();
            }
        }

        handler.GuildAvailable -= OnGuildAvailable;
        handler.GuildUnavailable -= OnGuildUnavailable;
        handler.JoinedGuild -= OnJoinedGuild;
        handler.LeftGuild -= OnLeftGuild;
    }
}