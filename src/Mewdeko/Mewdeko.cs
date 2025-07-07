﻿using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using DataModel;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Figgle.Fonts;
using Lavalink4NET;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Interactions;
using Mewdeko.Services.Impl;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using TypeReader = Discord.Commands.TypeReader;

namespace Mewdeko;

/// <summary>
///     The main class for the Mewdeko bot, responsible for initializing services, handling events, and managing the bot's
///     lifecycle.
/// </summary>
public class Mewdeko
{
    // Cached JsonSerializerOptions for performance
    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    ///     Initializes a new instance of the Mewdeko class.
    /// </summary>
    /// <param name="services">The service provider for dependency injection.</param>
    public Mewdeko(IServiceProvider services)
    {
        Services = services;
        Credentials = Services.GetRequiredService<BotCredentials>();
        Cache = Services.GetRequiredService<IDataCache>();
        Client = Services.GetRequiredService<DiscordShardedClient>();
        CommandService = Services.GetRequiredService<CommandService>();
        GuildSettingsService = Services.GetRequiredService<GuildSettingsService>();
    }

    /// <summary>
    ///     Gets the credentials used by the bot.
    /// </summary>
    public BotCredentials Credentials { get; }

    private int ReadyCount { get; set; }

    /// <summary>
    ///     Gets the Discord client used by the bot.
    /// </summary>
    public DiscordShardedClient Client { get; }

    private GuildSettingsService GuildSettingsService { get; }

    private CommandService CommandService { get; }

    /// <summary>
    ///     Gets or sets the color used for successful operations.
    /// </summary>
    public static Color OkColor { get; set; }

    /// <summary>
    ///     Gets or sets the color used for error operations.
    /// </summary>
    public static Color ErrorColor { get; set; }

    /// <summary>
    ///     Gets a TaskCompletionSource that completes when the bot is ready.
    /// </summary>
    public TaskCompletionSource<bool> Ready { get; } = new();

    private IServiceProvider Services { get; }
    private IDataCache Cache { get; }

    /// <summary>
    ///     Event that occurs when the bot joins a guild.
    /// </summary>
    public event Func<GuildConfig, Task> JoinedGuild = delegate { return Task.CompletedTask; };

    /// <summary>
    ///     Loads type readers from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to load type readers from.</param>
    private void LoadTypeReaders(Assembly assembly)
    {
        var sw = new Stopwatch();
        sw.Start();
        var interactionService = Services.GetService<InteractionService>();
        Type[] allTypes;
        try
        {
            allTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Log.Warning(ex.LoaderExceptions[0], "Error getting types");
            return;
        }

        var filteredTypes = allTypes
            .Where(x => x.IsSubclassOf(typeof(TypeReader))
                        && x.BaseType.GetGenericArguments().Length > 0
                        && !x.IsAbstract);

        foreach (var ft in filteredTypes)
        {
            var x = (TypeReader)ActivatorUtilities.CreateInstance(Services, ft);
            var baseType = ft.BaseType;
            var typeArgs = baseType?.GetGenericArguments();
            if (typeArgs != null)
                CommandService.AddTypeReader(typeArgs[0], x);
        }

        CommandService.AddTypeReaders<IEmote>(
            new TryParseTypeReader<Emote>(Emote.TryParse),
            new TryParseTypeReader<Emoji>(Emoji.TryParse));

        interactionService.AddTypeConverter<TimeSpan>(new TimeSpanConverter());
        interactionService.AddTypeConverter(typeof(IRole[]), new RoleArrayConverter());
        interactionService.AddTypeConverter(typeof(IUser[]), new UserArrayConverter());
        interactionService.AddTypeConverter<StatusRole>(new StatusRolesTypeConverter());


        sw.Stop();
        Log.Information("TypeReaders loaded in {ElapsedTotalSeconds}s", sw.Elapsed.TotalSeconds);
    }

    private async Task LoginAsync(string token)
    {
        Client.Log += Client_Log;
        var clientReady = new TaskCompletionSource<bool>();

        Task SetClientReady(DiscordSocketClient unused)
        {
            ReadyCount++;
            Log.Information($"Shard {unused.ShardId} is ready");
            Log.Information($"{ReadyCount}/{Client.Shards.Count} shards connected");
            if (ReadyCount != Client.Shards.Count)
                return Task.CompletedTask;
            _ = Task.Run(() => clientReady.TrySetResult(true));
            return Task.CompletedTask;
        }

        Log.Information("Logging in...");
        try
        {
            // Login but don't start shards yet
            await Client.LoginAsync(TokenType.Bot, token.Trim()).ConfigureAwait(false);
            var gw = await Client.GetBotGatewayAsync();

            var maxConcurrency = gw.SessionStartLimit.MaxConcurrency;

            // Start shards in rate-limited batches according to max concurrency
            var totalShards = Client.Shards.Count;
            Log.Information($"Starting {totalShards} shards with max concurrency of {maxConcurrency}");

            // Group shards by their rate limit bucket using the formula from Discord docs
            var shardGroups = Client.Shards
                .GroupBy(shard => shard.ShardId % maxConcurrency)
                .OrderBy(group => group.Key)
                .ToList();

            // Start each batch of shards
            foreach (var group in shardGroups)
            {
                var tasks = group.Select(shard => shard.StartAsync()).ToList();
                Log.Information($"Starting shard bucket {group.Key} with {tasks.Count} shards");
                await Task.WhenAll(tasks);

                // If not the last group, add a small delay between buckets
                if (group != shardGroups.Last())
                    await Task.Delay(5000);
            }
        }
        catch (HttpException ex)
        {
            LoginErrorHandler.Handle(ex);
            Helpers.ReadErrorAndExit(3);
        }
        catch (Exception ex)
        {
            LoginErrorHandler.Handle(ex);
            Helpers.ReadErrorAndExit(4);
        }

        Client.ShardReady += SetClientReady;
        await clientReady.Task.ConfigureAwait(false);
        Client.ShardReady -= SetClientReady;
        Client.JoinedGuild += Client_JoinedGuild;
        Client.LeftGuild += Client_LeftGuild;
        Log.Information("Logged in.");
        Log.Information("Logged in as:");
        Console.WriteLine(FiggleFonts.Digital.Render(Client.CurrentUser.Username));
    }

    private Task Client_LeftGuild(SocketGuild arg)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var chan = await Client.Rest.GetChannelAsync(Credentials.GuildJoinsChannelId).ConfigureAwait(false);
                await ((RestTextChannel)chan).SendErrorAsync($"Left server: {arg.Name} [{arg.Id}]", new BotConfig(),
                [
                    new EmbedFieldBuilder().WithName("Total Guilds")
                        .WithValue(Client.Guilds.Count)
                ]).ConfigureAwait(false);
            }
            catch
            {
                //ignored
            }

            Log.Information("Left server: {0} [{1}]", arg.Name, arg.Id);
        });
        return Task.CompletedTask;
    }

    private Task Client_JoinedGuild(SocketGuild arg)
    {
        _ = Task.Run(async () =>
        {
            Log.Information("Joined server: {0} [{1}]", arg.Name, arg.Id);

            var gc = await GuildSettingsService.GetGuildConfig(arg.Id).ConfigureAwait(false);

            await JoinedGuild.Invoke(gc).ConfigureAwait(false);
            var chan =
                await Client.Rest.GetChannelAsync(Credentials.GuildJoinsChannelId).ConfigureAwait(false) as
                    RestTextChannel;
            var eb = new EmbedBuilder();
            eb.WithTitle($"Joined {Format.Bold(arg.Name)} {arg.Id}");
            eb.AddField("Members", arg.MemberCount);
            eb.AddField("Boosts", arg.PremiumSubscriptionCount);
            eb.AddField("Owner", $"Name: {arg.Owner}\nID: {arg.OwnerId}");
            eb.AddField("Text Channels", arg.TextChannels.Count);
            eb.AddField("Voice Channels", arg.VoiceChannels.Count);
            eb.AddField("Total Guilds", Client.Guilds.Count);
            eb.WithThumbnailUrl(arg.IconUrl);
            eb.WithColor(OkColor);
            await chan.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Runs the bot, initializing all necessary components and services.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAsync()
    {
        var sw = Stopwatch.StartNew();

        var circularDependencies = FindCircularDependencies();

        if (circularDependencies.Count > 0)
        {
            Log.Error("Circular dependencies found:");
            foreach (var dependency in circularDependencies)
            {
                Log.Error(dependency);
            }
        }
        else
        {
            Log.Information("No circular dependencies found.");
        }

        await LoginAsync(Credentials.Token).ConfigureAwait(false);

        Log.Information("Loading Services...");
        try
        {
            LoadTypeReaders(typeof(Mewdeko).Assembly);
            var audioService = Services.GetService<IAudioService>();
            try
            {
                await audioService.StartAsync();
            }
            catch (Exception e)
            {
                Log.Error("Unable to start audio service: {Message}", e.Message);
            }

            var dbProvider = Services.GetRequiredService<IDataConnectionFactory>();
            await using var dbContext = await dbProvider.CreateConnectionAsync();
            await dbContext.EnsureUserCreated(Client.CurrentUser.Id, Client.CurrentUser.Username,
                Client.CurrentUser.AvatarId);
        }
        catch (Exception ex)
        {
            Log.Error($"Error adding services: {ex}");
            Helpers.ReadErrorAndExit(9);
        }


        sw.Stop();
        Log.Information("Connected in {Elapsed:F2}s", sw.Elapsed.TotalSeconds);
        var commandService = Services.GetService<CommandService>();
        commandService.Log += LogCommandsService;
        var interactionService = Services.GetRequiredService<InteractionService>();
        try
        {
            await commandService.AddModulesAsync(Assembly.GetExecutingAssembly(), Services);
            await interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), Services);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
#if !DEBUG
        await interactionService.RegisterCommandsGloballyAsync().ConfigureAwait(false);
#endif
#if DEBUG
        if (Client.Guilds.Select(x => x.Id).Contains(Credentials.DebugGuildId))
            await interactionService.RegisterCommandsToGuildAsync(Credentials.DebugGuildId);
#endif

        _ = Task.Run(HandleStatusChanges);
        _ = Task.Run(async () => await ExecuteReadySubscriptions());
        var performanceMonitor = Services.GetRequiredService<PerformanceMonitorService>();
        performanceMonitor.Initialize(typeof(Mewdeko).Assembly, "Mewdeko");
        Ready.TrySetResult(true);
        Log.Information("Ready.");
    }

    private static Task LogCommandsService(LogMessage arg)
    {
        Log.Information(arg.ToString());
        return Task.CompletedTask;
    }

    private async Task ExecuteReadySubscriptions()
    {
        var readyExecutors = Services.GetServices<IReadyExecutor>();
        var tasks = readyExecutors.Select(async toExec =>
        {
            try
            {
                await toExec.OnReadyAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed running OnReadyAsync method on {Type} type: {Message}", toExec.GetType().Name,
                    ex.Message);
            }
        });
        await tasks.WhenAll();
    }

    private async static Task Client_Log(LogMessage arg)
    {
        var severity = arg.Severity switch
        {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error => LogEventLevel.Error,
            LogSeverity.Warning => LogEventLevel.Warning,
            LogSeverity.Info => LogEventLevel.Information,
            LogSeverity.Verbose => LogEventLevel.Verbose,
            LogSeverity.Debug => LogEventLevel.Debug,
            _ => LogEventLevel.Information
        };
        Log.Write(severity, arg.Exception, "[{Source}] {Message}", arg.Source, arg.Message);
        await Task.CompletedTask;
    }

    private void HandleStatusChanges()
    {
        var sub = Services.GetService<IDataCache>().Redis.GetSubscriber();

        sub.Subscribe(RedisChannel.Literal($"{Client.CurrentUser.Id}_status.game_set"), async (_, game) =>
        {
            try
            {
                var status = JsonSerializer.Deserialize<GameStatus>((string)game, CachedJsonOptions);
                await Client.SetGameAsync(status?.Name, type: status?.Activity ?? ActivityType.Playing)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error setting game");
            }
        }, CommandFlags.FireAndForget);

        sub.Subscribe(RedisChannel.Literal($"{Client.CurrentUser.Id}_status.stream_set"), async (_, streamData) =>
        {
            try
            {
                var stream = JsonSerializer.Deserialize<StreamStatus>((string)streamData, CachedJsonOptions);
                await Client.SetGameAsync(stream?.Name, stream?.Url, ActivityType.Streaming)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error setting stream");
            }
        }, CommandFlags.FireAndForget);
    }

    /// <summary>
    ///     Sets the bot's game status.
    /// </summary>
    /// <param name="game">The name of the game to set.</param>
    /// <param name="type">The type of activity.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetGameAsync(string? game, ActivityType type)
    {
        var obj = new
        {
            Name = game, Activity = type
        };
        var sub = Services.GetService<IDataCache>().Redis.GetSubscriber();
        await sub.PublishAsync(RedisChannel.Literal($"{Client.CurrentUser.Id}_status.game_set"),
                JsonSerializer.Serialize(obj))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Self explanatory
    /// </summary>
    /// <returns></returns>
    private static List<string> FindCircularDependencies()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes();

        return (from type in types
            let path = new HashSet<Type>
            {
                type
            }
            where HasCircularDependency(type, path)
            select string.Join(" -> ", path.Select(t => t.Name))).ToList();
    }

    private static bool HasCircularDependency(Type type, HashSet<Type> path)
    {
        var constructors = type.GetConstructors();

        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();

            foreach (var parameter in parameters)
            {
                var parameterType = parameter.ParameterType;

                if (path.Contains(parameterType))
                {
                    path.Add(parameterType);
                    return true;
                }

                if (parameterType.Assembly != type.Assembly) continue;

                path.Add(parameterType);
                if (HasCircularDependency(parameterType, path))
                {
                    return true;
                }

                path.Remove(parameterType);
            }
        }

        return false;
    }
}