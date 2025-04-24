using System.Collections.Immutable;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using LinqToDB;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;
using Octokit;
using Serilog;
using StackExchange.Redis;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Image = Discord.Image;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     Service for owner-only commands.
/// </summary>
public class OwnerOnlyService : ILateExecutor, IReadyExecutor, INService
{
    private readonly Mewdeko bot;
    private readonly BotConfigService bss;

    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly CommandHandler cmdHandler;
    private readonly IBotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;
    private readonly IHttpClientFactory httpFactory;
    private readonly Replacer rep;
    private readonly GeneratedBotStrings strings;
    private string sourceDirectory;
    private readonly string dataDirectory;
    private int totalCommands;

#pragma warning disable CS8714
    private ConcurrentDictionary<ulong?, ConcurrentDictionary<int, Timer>> autoCommands =
#pragma warning restore CS8714
        new();

    private int currentStatusNum;

    private ImmutableDictionary<ulong, IDMChannel> ownerChannels =
        new Dictionary<ulong, IDMChannel>().ToImmutableDictionary();

    /// <summary>
    ///     Initializes a new instance of the <see cref="OwnerOnlyService" /> class.
    ///     This service handles owner-only commands and functionalities for the bot.
    /// </summary>
    /// <param name="client">The Discord client used for interacting with the Discord API.</param>
    /// <param name="cmdHandler">Handles command processing and execution.</param>
    /// <param name="dbFactory">Provides access to the database for data persistence.</param>
    /// <param name="strings">Provides access to localized bot strings.</param>
    /// <param name="creds">Contains the bot's credentials and configuration.</param>
    /// <param name="cache">Provides caching functionalities.</param>
    /// <param name="factory">Factory for creating instances of <see cref="HttpClient" />.</param>
    /// <param name="bss">Service for accessing bot configuration settings.</param>
    /// <param name="phProviders">A collection of providers for placeholder values.</param>
    /// <param name="bot">Reference to the main bot instance.</param>
    /// <param name="guildSettings">Service for accessing guild-specific settings.</param>
    /// <param name="handler">Event handler for subscribing to bot events.</param>
    /// <remarks>
    ///     The constructor subscribes to message received events and sets up periodic tasks for rotating statuses
    ///     and checking for updates. It also listens for commands to leave guilds or reload images via Redis subscriptions.
    /// </remarks>
    public OwnerOnlyService(DiscordShardedClient client, CommandHandler cmdHandler, IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings, IBotCredentials creds, IDataCache cache, IHttpClientFactory factory,
        BotConfigService bss, IEnumerable<IPlaceholderProvider> phProviders, Mewdeko bot,
        GuildSettingsService guildSettings, EventHandler handler)
    {
        var redis = cache.Redis;
        this.cmdHandler = cmdHandler;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.client = client;
        this.creds = creds;
        this.cache = cache;
        this.bot = bot;
        this.guildSettings = guildSettings;
        var imgs = cache.LocalImages;
        httpFactory = factory;
        this.bss = bss;
        rep = new ReplacementBuilder()
            .WithClient(client)
            .WithProviders(phProviders)
            .Build();

        dataDirectory = "data";

        _ = Task.Run(RotatingStatuses);

        var sub = redis.GetSubscriber();
        sub.Subscribe(RedisChannel.Literal($"{this.creds.RedisKey()}_reload_images"),
            delegate { imgs.Reload(); }, CommandFlags.FireAndForget);

        sub.Subscribe(RedisChannel.Literal($"{this.creds.RedisKey()}_leave_guild"), async void (_, v) =>
        {
            try
            {
                var guildStr = v.ToString()?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(guildStr))
                    return;
                var server = this.client.Guilds.FirstOrDefault(g => g.Id.ToString() == guildStr) ??
                             this.client.Guilds.FirstOrDefault(g => g.Name.Trim().ToUpperInvariant() == guildStr);

                if (server == null) return;

                if (server.OwnerId != this.client.CurrentUser.Id)
                {
                    await server.LeaveAsync().ConfigureAwait(false);
                    Log.Information("Left server {ServerName} [{ServerId}]", server.Name, server.Id);
                }
                else
                {
                    await server.DeleteAsync().ConfigureAwait(false);
                    Log.Information("Deleted server {ServerName} [{ServerId}]", server.Name, server.Id);
                }
            }
            catch
            {
                // ignored
            }
        }, CommandFlags.FireAndForget);

        _ = CheckUpdateTimer();
        handler.GuildMemberUpdated += QuarantineCheck;
    }

    /// <summary>
    ///     Forwards direct messages (DMs) received by the bot to the owners' DMs. This allows bot owners to monitor and
    ///     respond to user messages directly.
    /// </summary>
    /// <param name="discordShardedClient">The Discord client through which the message was received.</param>
    /// <param name="guild">The guild associated with the message, if any.</param>
    /// <param name="msg">The message that was received and is to be forwarded.</param>
    /// <remarks>
    ///     The method checks if the message was sent in a DM channel and forwards it to all owners if the setting is enabled.
    ///     Attachments are also forwarded. Errors in sending messages to any owner are logged but not thrown.
    /// </remarks>
    public async Task LateExecute(DiscordShardedClient discordShardedClient, IGuild guild, IUserMessage msg)
    {
        var bs = bss.Data;
        if (msg.Channel is IDMChannel && bss.Data.ForwardMessages && ownerChannels.Count > 0)
        {
            var title = $"{strings.DmFrom(guild.Id)} [{msg.Author}]({msg.Author.Id})";

            var attachamentsTxt = strings.Attachments(guild.Id);

            var toSend = msg.Content;

            if (msg.Attachments.Count > 0)
            {
                toSend +=
                    $"\n\n{Format.Code(attachamentsTxt)}:\n{string.Join("\n", msg.Attachments.Select(a => a.ProxyUrl))}";
            }

            if (bs.ForwardToAllOwners)
            {
                var allOwnerChannels = ownerChannels.Values;

                foreach (var ownerCh in allOwnerChannels.Where(ch => ch.Recipient.Id != msg.Author.Id))
                {
                    try
                    {
                        await ownerCh.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                    }
                    catch
                    {
                        Log.Warning("Can't contact owner with id {0}", ownerCh.Recipient.Id);
                    }
                }
            }
            else
            {
                var firstOwnerChannel = ownerChannels.Values.First();
                if (firstOwnerChannel.Recipient.Id != msg.Author.Id)
                {
                    try
                    {
                        await firstOwnerChannel.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Initializes required services and loads configurations when the bot is ready. This includes setting up automatic
    ///     commands based on their configured intervals and creating direct message channels for the bot owners.
    /// </summary>
    /// <remarks>
    ///     This method is typically called once when the bot starts and is ready to receive and process messages. It prepares
    ///     the bot for operation by loading necessary configurations and establishing connections.
    /// </remarks>
    public async Task OnReadyAsync()
    {
        Log.Information($"Starting {GetType()} Cache");

        await using var dbContext = await dbFactory.CreateConnectionAsync();


        autoCommands =
            (await dbContext.AutoCommands
                .ToListAsync())
            .Where(x => x.Interval >= 5)
            .AsEnumerable()
            .GroupBy(x => x.GuildId)
            .ToDictionary(x => x.Key,
                y => y.ToDictionary(x => x.Id, TimerFromAutoCommand)
                    .ToConcurrent())
            .ToConcurrent();

        foreach (var cmd in dbContext.AutoCommands.Where(x => x.Interval == 0))
        {
            try
            {
                await ExecuteCommand(cmd).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        var channels = await Task.WhenAll(creds.OwnerIds.Select(id =>
        {
            var user = client.GetUser(id);
            return user == null ? Task.FromResult<IDMChannel?>(null) : user.CreateDMChannelAsync();
        })).ConfigureAwait(false);

        ownerChannels = channels.Where(x => x is not null)
            .ToDictionary(x => x.Recipient.Id, x => x)
            .ToImmutableDictionary();

        if (ownerChannels.Count == 0)
        {
            Log.Warning(
                "No owner channels created! Make sure you've specified the correct OwnerId in the credentials.json file and invited the bot to a Discord server");
        }
        else
        {
            Log.Information(
                $"Created {ownerChannels.Count} out of {creds.OwnerIds.Length} owner message channels.");
        }
    }

    private async Task QuarantineCheck(Cacheable<SocketGuildUser, ulong> args, SocketGuildUser arsg2)
    {
        if (!args.HasValue)
            return;

        if (args.Id != client.CurrentUser.Id)
            return;

        var value = args.Value;

        if (value.Roles is null)
            return;


        if (!bss.Data.QuarantineNotification)
            return;

        if (!Equals(value.Roles, arsg2.Roles))
        {
            var quarantineRole = value.Guild.Roles.FirstOrDefault(x => x.Name == "Quarantine");
            if (quarantineRole is null)
                return;

            if (value.Roles.All(x => x.Id != quarantineRole.Id) && arsg2.Roles.Any(x => x.Id == quarantineRole.Id))
            {
                if (bss.Data.ForwardToAllOwners)
                {
                    foreach (var i in creds.OwnerIds)
                    {
                        var user = await client.Rest.GetUserAsync(i);
                        if (user is null) continue;
                        var channel = await user.CreateDMChannelAsync();
                        await channel.SendMessageAsync(strings.QuarantineNotification(args.Value.Guild.Id,
                            arsg2.Guild.Name, value.Guild.Id));
                    }
                }
                else
                {
                    var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                    if (user is not null)
                    {
                        var channel = await user.CreateDMChannelAsync();
                        await channel.SendMessageAsync(strings.QuarantineNotification(args.Value.Guild.Id,
                            arsg2.Guild.Name, value.Guild.Id));
                    }
                }
            }
        }
    }

    private async Task CheckUpdateTimer()
    {
        var interval = bss.Data.CheckUpdateInterval;
        if (interval < 1)
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromHours(interval));
        do
        {
            var github = new GitHubClient(new ProductHeaderValue("Mewdeko"));
            var redis = cache.Redis.GetDatabase();
            switch (bss.Data.CheckForUpdates)
            {
                case UpdateCheckType.Release:
                    var latestRelease = await github.Repository.Release.GetLatest("SylveonDeko", "Mewdeko");
                    var eb = new EmbedBuilder()
                        .WithAuthor(strings.NewReleaseTitle(null, latestRelease.TagName),
                            "https://seeklogo.com/images/G/github-logo-5F384D0265-seeklogo.com.png",
                            latestRelease.HtmlUrl)
                        .WithDescription(strings.NewReleaseDesc(null,
                            latestRelease.Assets[0].BrowserDownloadUrl,
                            bss.Data.Prefix))
                        .WithOkColor();
                    var list = await redis.StringGetAsync($"{creds.RedisKey()}_ReleaseList");
                    if (!list.HasValue)
                    {
                        await redis.StringSetAsync($"{creds.RedisKey()}_ReleaseList",
                            JsonSerializer.Serialize(latestRelease));
                        Log.Information("Setting latest release to {ReleaseTag}", latestRelease.TagName);
                    }
                    else
                    {
                        var release = JsonSerializer.Deserialize<Release>(list);
                        if (release.TagName != latestRelease.TagName)
                        {
                            Log.Information("New release found: {ReleaseTag}", latestRelease.TagName);
                            await redis.StringSetAsync($"{creds.RedisKey()}_ReleaseList",
                                JsonSerializer.Serialize(latestRelease));
                            if (bss.Data.ForwardToAllOwners)
                            {
                                foreach (var i in creds.OwnerIds)
                                {
                                    var user = await client.Rest.GetUserAsync(i);
                                    if (user is null) continue;
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(embed: eb.Build());
                                }
                            }
                            else
                            {
                                var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                                if (user is not null)
                                {
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(embed: eb.Build());
                                }
                            }
                        }
                    }

                    break;
                case UpdateCheckType.Commit:
                    var latestCommit =
                        await github.Repository.Commit.Get("SylveonDeko", "Mewdeko", bss.Data.UpdateBranch);
                    if (latestCommit is null)
                    {
                        Log.Warning(
                            "Failed to get latest commit, make sure you have the correct branch set in bot.yml");
                        break;
                    }

                    var redisCommit = await redis.StringGetAsync($"{creds.RedisKey()}_CommitList");
                    if (!redisCommit.HasValue)
                    {
                        await redis.StringSetAsync($"{creds.RedisKey()}_CommitList",
                            latestCommit.Sha);
                        Log.Information("Setting latest commit to {CommitSha}", latestCommit.Sha);
                    }
                    else
                    {
                        if (redisCommit.ToString() != latestCommit.Sha)
                        {
                            Log.Information("New commit found: {CommitSha}", latestCommit.Sha);
                            await redis.StringSetAsync($"{creds.RedisKey()}_CommitList",
                                latestCommit.Sha);
                            if (bss.Data.ForwardToAllOwners)
                            {
                                foreach (var i in creds.OwnerIds)
                                {
                                    var user = await client.Rest.GetUserAsync(i);
                                    if (user is null) continue;
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(
                                        $"New commit found: {latestCommit.Sha}\n{latestCommit.HtmlUrl}");
                                }
                            }
                            else
                            {
                                var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                                if (user is not null)
                                {
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(
                                        $"New commit found: {latestCommit.Sha}\n{latestCommit.HtmlUrl}");
                                }
                            }
                        }
                    }

                    break;
                case UpdateCheckType.None:
                    break;
                default:
                    Log.Error("Invalid UpdateCheckType {UpdateCheckType}", bss.Data.CheckForUpdates);
                    break;
            }
        } while (await timer.WaitForNextTickAsync());
    }

    private async Task RotatingStatuses()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                await using var dbContext = await dbFactory.CreateConnectionAsync();

                if (!bss.Data.RotateStatuses) continue;

                IReadOnlyList<RotatingStatus> rotatingStatuses =
                    await dbContext.RotatingStatuses.OrderBy(x => x.Id).ToListAsync();

                if (rotatingStatuses.Count == 0)
                    continue;

                var playingStatus = currentStatusNum >= rotatingStatuses.Count
                    ? rotatingStatuses[currentStatusNum = 0]
                    : rotatingStatuses[currentStatusNum++];

                var statusText = rep.Replace(playingStatus.Status);
                await bot.SetGameAsync(statusText, (ActivityType)playingStatus.Type).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Rotating playing status errored: {ErrorMessage}", ex.Message);
            }
        }
    }

    /// <summary>
    ///     Removes a playing status from the rotating statuses list based on its index.
    /// </summary>
    /// <param name="index">The zero-based index of the status to remove.</param>
    /// <returns>The status that was removed, or null if the index was out of bounds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="index" /> is less than 0.</exception>
    public async Task<string?> RemovePlayingAsync(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        await using var dbContext = await dbFactory.CreateConnectionAsync();


        var toRemove = await dbContext.RotatingStatuses
            .Skip(index)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (toRemove is null)
            return null;

        await dbContext.RotatingStatuses.Select(x => toRemove).DeleteAsync();
        return toRemove.Status;
    }

    /// <summary>
    ///     Adds a new playing status to the list of rotating statuses.
    /// </summary>
    /// <param name="t">The type of activity for the status (e.g., playing, streaming).</param>
    /// <param name="status">The text of the status to display.</param>
    /// <returns>A task that represents the asynchronous add operation.</returns>
    public async Task AddPlaying(ActivityType t, string status)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var toAdd = new RotatingStatus
        {
            Status = status, Type = (int)t
        };
        await dbContext.InsertAsync(toAdd);
    }

    /// <summary>
    ///     Toggles the rotation of playing statuses on or off.
    /// </summary>
    /// <returns>True if rotation is enabled after the toggle, false otherwise.</returns>
    public bool ToggleRotatePlaying()
    {
        var enabled = false;
        bss.ModifyConfig(bs => enabled = bs.RotateStatuses = !bs.RotateStatuses);
        return enabled;
    }

    /// <summary>
    ///     Retrieves the current list of rotating playing statuses.
    /// </summary>
    /// <returns>A read-only list of <see cref="RotatingStatuses" /> representing the current rotating statuses.</returns>
    public async Task<IReadOnlyList<RotatingStatus>> GetRotatingStatuses()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        return await dbContext.RotatingStatuses.ToListAsync();
    }

    private Timer TimerFromAutoCommand(AutoCommand x)
    {
        return new Timer(async void (obj) => await ExecuteCommand((AutoCommand)obj).ConfigureAwait(false),
            x,
            x.Interval * 1000,
            x.Interval * 1000);
    }

    private async Task ExecuteCommand(AutoCommand cmd)
    {
        try
        {
            if (cmd.GuildId is null)
                return;
            var guild = client.GetGuild(cmd.GuildId.Value);
            var prefix = await guildSettings.GetPrefix(guild);
            //if someone already has .die as their startup command, ignore it
            if (cmd.CommandText.StartsWith($"{prefix}die", StringComparison.InvariantCulture))
                return;
            await cmdHandler.ExecuteExternal(cmd.GuildId, cmd.ChannelId, cmd.CommandText).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in SelfService ExecuteCommand");
        }
    }

    /// <summary>
    ///     Adds a new auto command to the database and schedules it if necessary.
    /// </summary>
    /// <param name="cmd">The auto command to be added.</param>
    /// <remarks>
    ///     If the command's interval is 5 seconds or more, it's also scheduled to be executed periodically according to its
    ///     interval.
    /// </remarks>
    public async Task AddNewAutoCommand(AutoCommand cmd)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        await dbContext.InsertAsync(cmd);


        if (cmd.Interval >= 5)
        {
            var autos = autoCommands.GetOrAdd(cmd.GuildId, new ConcurrentDictionary<int, Timer>());
            autos.AddOrUpdate(cmd.Id, _ => TimerFromAutoCommand(cmd), (_, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return TimerFromAutoCommand(cmd);
            });
        }
    }

    /// <summary>
    ///     Sets the default prefix for bot commands.
    /// </summary>
    /// <param name="prefix">The new prefix to be set.</param>
    /// <returns>The newly set prefix.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="prefix" /> is null or whitespace.</exception>
    public string SetDefaultPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));

        bss.ModifyConfig(bs => bs.Prefix = prefix);

        return prefix;
    }

    /// <summary>
    ///     Retrieves a list of auto commands set to execute at bot startup (interval of 0).
    /// </summary>
    /// <returns>A list of startup auto commands.</returns>
    public async Task<IEnumerable<AutoCommand>> GetStartupCommands()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        return
            await dbContext.AutoCommands
                .Where(x => x.Interval == 0)
                .OrderBy(x => x.Id)
                .ToListAsync();
    }

    /// <summary>
    ///     Retrieves a list of auto commands with an interval of 5 seconds or more.
    /// </summary>
    /// <returns>A list of auto commands set to execute periodically.</returns>
    public async Task<IEnumerable<AutoCommand>> GetAutoCommands()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        return
            await dbContext.AutoCommands
                .Where(x => x.Interval >= 5)
                .OrderBy(x => x.Id)
                .ToListAsync();
    }

    /// <summary>
    ///     Instructs the bot to leave a guild based on the guild's identifier or name.
    /// </summary>
    /// <param name="guildStr">The guild identifier or name.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task LeaveGuild(string guildStr)
    {
        var sub = cache.Redis.GetSubscriber();
        return sub.PublishAsync(RedisChannel.Literal($"{creds.RedisKey()}_leave_guild"), guildStr);
    }


    /// <summary>
    ///     Removes a startup command (a command with an interval of 0) at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the startup command to remove.</param>
    /// <returns>True if a command was found and removed; otherwise, false.</returns>
    public async Task<bool> RemoveStartupCommand(int index)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var cmd = await dbContext.AutoCommands
            .Where(x => x.Interval == 0)
            .Skip(index)
            .FirstOrDefaultAsync();

        if (cmd == null) return false;
        await dbContext.DeleteAsync(cmd);

        return true;
    }

    /// <summary>
    ///     Removes an auto command based on its index in the collection of commands with an interval of 5 seconds or more.
    /// </summary>
    /// <param name="index">The zero-based index of the command to remove.</param>
    /// <returns>True if a command was successfully found and removed; otherwise, false.</returns>
    public async Task<bool> RemoveAutoCommand(int index)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var cmd = await dbContext.AutoCommands
            .Where(x => x.Interval >= 5)
            .Skip(index)
            .FirstOrDefaultAsync();

        if (cmd == null) return false;
        await dbContext.DeleteAsync(cmd);
        if (!autoCommands.TryGetValue(cmd.GuildId, out var autos)) return true;
        if (autos.TryRemove(cmd.Id, out var timer))
            timer.Change(Timeout.Infinite, Timeout.Infinite);

        return true;
    }

    /// <summary>
    ///     Sets a new avatar for the bot by downloading an image from a specified URL.
    /// </summary>
    /// <param name="img">The URL of the image to set as the new avatar.</param>
    /// <returns>True if the avatar was successfully updated; otherwise, false.</returns>
    public async Task<bool> SetAvatar(string img)
    {
        if (string.IsNullOrWhiteSpace(img))
            return false;

        if (!Uri.IsWellFormedUriString(img, UriKind.Absolute))
            return false;

        var uri = new Uri(img);

        using var http = httpFactory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (!sr.IsImage())
            return false;

        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await client.CurrentUser.ModifyAsync(u => u.Avatar = new Image(imgStream)).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    ///     Clears all startup commands from the database.
    /// </summary>
    public async Task ClearStartupCommands()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var toRemove = dbContext.AutoCommands
            .Where(x => x.Interval == 0);

        await dbContext.DeleteAsync(toRemove);
    }

    /// <summary>
    ///     Reloads images from a source, typically used for refreshing local or cached resources.
    /// </summary>
    public void ReloadImages()
    {
        var sub = cache.Redis.GetSubscriber();
        sub.Publish(RedisChannel.Literal($"{creds.RedisKey()}_reload_images"), "");
    }


    /// <summary>
    ///     Toggles the bot's message forwarding feature.
    /// </summary>
    /// <returns>True if message forwarding is enabled after the toggle; otherwise, false.</returns>
    public bool ForwardMessages()
    {
        var isForwarding = false;
        bss.ModifyConfig(config => isForwarding = config.ForwardMessages = !config.ForwardMessages);

        return isForwarding;
    }

    /// <summary>
    ///     Toggles whether the bot forwards messages to all owners or just the primary owner.
    /// </summary>
    /// <returns>True if forwarding to all owners is enabled after the toggle; otherwise, false.</returns>
    public bool ForwardToAll()
    {
        var isToAll = false;
        bss.ModifyConfig(config => isToAll = config.ForwardToAllOwners = !config.ForwardToAllOwners);
        return isToAll;
    }

    /// <summary>
    ///     Set a custom source directory for documentation generation
    /// </summary>
    /// <param name="sourceDirectory">The directory containing source code files</param>
    public void SetSourceDirectory(string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            throw new ArgumentException("Source directory cannot be null or empty", nameof(sourceDirectory));
        }

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
        }

        sourceDirectory = sourceDirectory;
        Log.Information("Source directory set to: {SourceDirectory}", sourceDirectory);
    }

    /// <summary>
    ///     Generate command documentation YAML files with enum support and proper formatting
    /// </summary>
    /// <param name="sourcePath">Optional override for the source code path</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task GenerateDocumentationAsync(string sourcePath = null)
    {
        try
        {
            // Use the provided source path or the previously set one
            var sourceDir = sourcePath ?? sourceDirectory;

            if (string.IsNullOrEmpty(sourceDir))
            {
                throw new InvalidOperationException(
                    "Source directory not set. Please set a source directory using SetSourceDirectory or provide it as a parameter.");
            }

            Log.Information("Generating command documentation from source directory: {SourceDir}", sourceDir);

            if (!Directory.Exists(sourceDir))
            {
                Log.Error("Source directory not found: {SourceDir}", sourceDir);
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }

            // Define output paths
            var commandsPath = Path.Combine(dataDirectory, "strings", "commands", "commands.en-US.yml");
            var enumsPath = Path.Combine(dataDirectory, "strings", "enums", "enums.en-US.yml");

            // Ensure the directories exist
            Directory.CreateDirectory(Path.GetDirectoryName(commandsPath));
            Directory.CreateDirectory(Path.GetDirectoryName(enumsPath));

            // Process source files
            var commands = new Dictionary<string, CommandInfo>();
            var aliases = new Dictionary<string, List<string>>();
            var enums = new Dictionary<string, EnumInfo>();

            totalCommands = 0;
            await Task.Run(() => ProcessSourceDirectory(sourceDir, commands, aliases, enums));

            // Write output files
            await WriteYamlFileAsync(commands, commandsPath);
            await WriteEnumsYamlFileAsync(enums, enumsPath);

            Log.Information(
                "Documentation generated successfully. Found {TotalCommands} commands and {TotalEnums} enums.",
                totalCommands, enums.Count);
            Log.Information("Commands: {CommandsPath}", Path.GetFullPath(commandsPath));
            Log.Information("Enums: {EnumsPath}", Path.GetFullPath(enumsPath));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating documentation");
            throw;
        }
    }

    /// <summary>
    ///     Processes a directory of source files to extract command and enum information.
    /// </summary>
    /// <param name="directory">The directory containing source files</param>
    /// <param name="commands">Dictionary to store command information</param>
    /// <param name="aliases">Dictionary to store command aliases</param>
    /// <param name="enums">Dictionary to store enum information</param>
    private void ProcessSourceDirectory(string directory, Dictionary<string, CommandInfo> commands,
        Dictionary<string, List<string>> aliases, Dictionary<string, EnumInfo> enums)
    {
        try
        {
            // Process all .cs files in the directory and subdirectories
            foreach (var file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                try
                {
                    if (file.Contains("Service") || file.Contains("Common") || directory.Contains("Common") ||
                        directory.Contains("Impl") || directory.Contains("Service"))
                        continue;

                    ProcessSourceFile(file, commands, aliases, enums);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error processing file: {FilePath}", file);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error processing directory: {Directory}", directory);
        }
    }

    private bool IsPotentialModuleFile(string content)
    {
        return content.Contains("Mewdeko.Modules") ||
               content.Contains("[Cmd") ||
               content.Contains("MewdekoModuleBase") ||
               content.Contains("MewdekoSubmodule") ||
               Regex.IsMatch(content, @"class\s+\w+\s*\([^)]*\)\s*:\s*MewdekoModuleBase") ||
               Regex.IsMatch(content, @"class\s+\w+.*?:\s*MewdekoModuleBase<");
    }

    /// <summary>
    ///     Processes a single source file to extract commands, aliases, and enums.
    /// </summary>
    /// <param name="filePath">Path to the source file</param>
    /// <param name="commands">Dictionary to store command information</param>
    /// <param name="aliases">Dictionary to store command aliases</param>
    /// <param name="enums">Dictionary to store enum information</param>
    private void ProcessSourceFile(string filePath, Dictionary<string, CommandInfo> commands,
        Dictionary<string, List<string>> aliases, Dictionary<string, EnumInfo> enums)
    {
        var content = File.ReadAllText(filePath);
        var filename = Path.GetFileName(filePath);

        Log.Debug("Checking file: {FilePath}", filePath);

        // Extract enums first
        ExtractEnums(content, enums);

        // Check if this is a potential module file
        if (!IsPotentialModuleFile(content))
        {
            return;
        }

        // Handle Xp.cs specifically with the combined [Cmd, Aliases] format
        if (filename == "Xp.cs")
        {
            // Pattern for [Cmd, Aliases] format on the same line
            var xpCommandMatches = Regex.Matches(content,
                @"\[Cmd,\s*Aliases\][\s\S]*?public\s+(?:async\s+)?Task(?:<.*?>)?\s+(\w+)\s*\(([\s\S]*?)\)(?=\s*\{)");

            if (xpCommandMatches.Count > 0)
            {
                Log.Debug("Found {Count} commands via XP-specific pattern in {FileName}",
                    xpCommandMatches.Count, filename);

                foreach (Match match in xpCommandMatches)
                {
                    var methodName = match.Groups[1].Value;
                    var parameters = match.Groups[2].Value;

                    // Find nearby summary and extract parameter descriptions
                    var summary = FindNearbyXmlSummary(content, match.Index);
                    var paramDescriptions = ExtractParamDescriptions(content, match.Index);

                    // Call the updated AddCommand with parameter descriptions
                    AddCommand(methodName, summary, "", parameters, commands, aliases, paramDescriptions, enums);
                }

                Log.Information("Successfully processed {Count} commands from {FileName}",
                    xpCommandMatches.Count, filename);
                return; // Skip further processing
            }
        }

        var moduleClassesCount = ExtractCommandsFromModuleClasses(content, commands, aliases, enums);
        var cmdMethodsCount = ExtractCommandMethods(content, commands, aliases, enums);

        // Log results
        if (moduleClassesCount > 0 || cmdMethodsCount > 0)
        {
            Log.Debug("Found {ClassCount} module classes and {MethodCount} command methods in {FilePath}",
                moduleClassesCount, cmdMethodsCount, filename);
        }
    }

    private string FindNearbyXmlSummary(string content, int methodIndex)
    {
        // Look for nearby XML summary before the method
        var searchStart = Math.Max(0, methodIndex - 500);
        var searchLength = Math.Min(500, methodIndex - searchStart);

        if (searchLength <= 0)
            return "";

        var searchArea = content.Substring(searchStart, searchLength);

        var summaryMatch = Regex.Match(searchArea, @"\/\/\/\s*<summary>([\s\S]*?)<\/summary>");
        if (summaryMatch.Success)
        {
            return CleanSummary(summaryMatch.Groups[1].Value);
        }

        return "";
    }

    /// <summary>
    ///     Extracts enum declarations and their values from source code.
    /// </summary>
    /// <param name="content">The source code content</param>
    /// <param name="enums">Dictionary to store the extracted enum information</param>
    private void ExtractEnums(string content, Dictionary<string, EnumInfo> enums)
    {
        // Match enum declarations with potential XML comments
        var enumMatches = Regex.Matches(content,
            @"(?:\/\/\/\s*<summary>([\s\S]*?)<\/summary>[\s\S]*?)?public\s+enum\s+(\w+)(?:\s*:\s*\w+)?\s*\{([\s\S]*?)\}");

        foreach (Match match in enumMatches)
        {
            if (!match.Success) continue;

            var enumName = match.Groups[2].Value;
            var summary = match.Groups[1].Success ? CleanSummary(match.Groups[1].Value) : "";
            var valuesBlock = match.Groups[3].Value;

            var enumInfo = new EnumInfo
            {
                Name = enumName,
                Description = summary,
                Values = ExtractEnumValues(valuesBlock, content, match.Index, enumName)
            };

            enums[enumName] = enumInfo;
            Log.Debug("Extracted enum: {EnumName} with {ValueCount} values", enumName, enumInfo.Values.Count);
        }
    }

    /// <summary>
    ///     Extracts individual enum values and their documentation.
    /// </summary>
    /// <param name="valuesBlock">The content block containing enum values</param>
    /// <param name="fullContent">The full source file content</param>
    /// <param name="enumStartIndex">The starting position of the enum in the source</param>
    /// <param name="enumName">The name of the enum</param>
    /// <returns>List of enum value information objects</returns>
    private List<EnumValueInfo> ExtractEnumValues(string valuesBlock, string fullContent, int enumStartIndex,
        string enumName)
    {
        var result = new List<EnumValueInfo>();

        // Match enum value entries with potential XML comments or inline comments
        var valueMatches = Regex.Matches(valuesBlock,
            @"(?:\/\/\/\s*<summary>([\s\S]*?)<\/summary>[\s\S]*?)?(?:\/\/\s*(.*?)\r?\n)?(\w+)(?:\s*=\s*([^,\s]+))?\s*,?");

        foreach (Match match in valueMatches)
        {
            if (!match.Success) continue;

            var valueName = match.Groups[3].Value;
            var valueDescription = "";

            // Try to get description from XML comment
            if (match.Groups[1].Success)
            {
                valueDescription = CleanSummary(match.Groups[1].Value);
            }
            // Or from inline comment
            else if (match.Groups[2].Success)
            {
                valueDescription = match.Groups[2].Value.Trim();
            }
            // Or try to find XML comment elsewhere
            else
            {
                // Look for potential XML doc for this value
                var xmlValueMatch = Regex.Match(fullContent,
                    $@"<member name=""{enumName}\.{valueName}"">\s*<summary>([\s\S]*?)<\/summary>");

                if (xmlValueMatch.Success)
                {
                    valueDescription = CleanSummary(xmlValueMatch.Groups[1].Value);
                }
            }

            result.Add(new EnumValueInfo
            {
                Name = valueName,
                Description = valueDescription,
                Value = match.Groups[4].Success ? match.Groups[4].Value : null
            });
        }

        return result;
    }

    /// <summary>
    ///     Extracts command methods from source code content.
    /// </summary>
    /// <param name="content">The source code content</param>
    /// <param name="commands">Dictionary to store command information</param>
    /// <param name="aliases">Dictionary to store command aliases</param>
    /// <param name="enums">Dictionary of enum information</param>
    /// <returns>The number of command methods found and extracted</returns>
    private int ExtractCommandMethods(string content, Dictionary<string, CommandInfo> commands,
        Dictionary<string, List<string>> aliases, Dictionary<string, EnumInfo> enums)
    {
        var count = 0;

        // Improved patterns for better matching
        var methodPatterns = new[]
        {
            // Standard pattern with XML docs - capture summary, aliases, method name, and parameters
            @"(?:\/\/\/\s*<summary>([\s\S]*?)<\/summary>[\s\S]*?)(?:\[Cmd\][\s\S]*?)(?:\[Aliases(?:\((.*?)\))?\][\s\S]*?)?public\s+(?:async\s+)?Task(?:<.*?>)?\s+(\w+)\s*\(([\s\S]*?)\)(?=\s*\{)",

            // Pattern for combined attributes [Cmd, Aliases] - capture summary, method name, and parameters
            @"(?:\/\/\/\s*<summary>([\s\S]*?)<\/summary>[\s\S]*?)?\[Cmd,\s*Aliases(?:\((.*?)\))?\][\s\S]*?public\s+(?:async\s+)?Task(?:<.*?>)?\s+(\w+)\s*\(([\s\S]*?)\)(?=\s*\{)",

            // Simplified pattern - capture method name and parameters
            @"\[Cmd\][\s\S]*?public\s+(?:async\s+)?Task(?:<.*?>)?\s+(\w+)\s*\(([\s\S]*?)\)(?=\s*\{)"
        };

        foreach (var pattern in methodPatterns)
        {
            var matches = Regex.Matches(content, pattern);

            foreach (Match match in matches)
            {
                try
                {
                    // Extract method information based on pattern
                    var summary = "";
                    var aliasesAttr = "";
                    var methodName = "";
                    var parameters = "";

                    if (pattern.Contains("summary") && match.Groups.Count >= 4)
                    {
                        // First or second pattern with XML docs
                        summary = match.Groups[1].Success ? CleanSummary(match.Groups[1].Value) : "";

                        if (pattern.Contains("Aliases") && match.Groups.Count >= 5)
                        {
                            aliasesAttr = match.Groups[2].Success ? match.Groups[2].Value : "";
                            methodName = match.Groups[3].Value;
                            parameters = CleanParametersString(match.Groups[4].Value);
                        }
                        else
                        {
                            methodName = match.Groups[2].Value;
                            parameters = CleanParametersString(match.Groups[3].Value);
                            aliasesAttr = FindNearbyAliases(content, match.Index);
                        }
                    }
                    else if (match.Groups.Count >= 2)
                    {
                        // Simplified pattern without XML docs
                        methodName = match.Groups[1].Value;
                        parameters = CleanParametersString(match.Groups[2].Value);

                        // Try to find the summary from nearby XML docs
                        summary = FindNearbyXmlSummary(content, match.Index);
                        aliasesAttr = FindNearbyAliases(content, match.Index);
                    }

                    // Extract parameter descriptions with larger search window
                    var paramDescriptions = ExtractParamDescriptions(content, match.Index);

                    // Add command with parameter handling
                    AddCommand(methodName, summary, aliasesAttr, parameters, commands, aliases, paramDescriptions,
                        enums);
                    count++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error extracting command from match: {ErrorMessage}", ex.Message);
                }
            }
        }

        return count;
    }

    private string FindNearbyAliases(string content, int methodIndex)
    {
        // Look for nearby Aliases attribute before the method
        var searchStart = Math.Max(0, methodIndex - 200);
        var searchLength = Math.Min(200, methodIndex - searchStart);

        if (searchLength <= 0)
            return "";

        var searchArea = content.Substring(searchStart, searchLength);

        var aliasesMatch = Regex.Match(searchArea, @"\[Aliases\((.*?)\)\]");
        if (aliasesMatch.Success)
        {
            return aliasesMatch.Groups[1].Value;
        }

        return "";
    }

    /// <summary>
    ///     Extracts commands from module classes in the source code.
    /// </summary>
    /// <param name="content">The source code content</param>
    /// <param name="commands">Dictionary to store command information</param>
    /// <param name="aliases">Dictionary to store command aliases</param>
    /// <param name="enums">Dictionary of enum information</param>
    /// <returns>The number of module classes processed</returns>
    private int ExtractCommandsFromModuleClasses(string content, Dictionary<string, CommandInfo> commands,
        Dictionary<string, List<string>> aliases, Dictionary<string, EnumInfo> enums)
    {
        var count = 0;

        // Find all classes that inherit from MewdekoModuleBase or MewdekoSubmodule
        var classPattern =
            @"class\s+(\w+)(?:\s*\([^)]*\))?\s*:\s*(MewdekoModuleBase(?:<[^>]+>)?|MewdekoSubmodule(?:<[^>]+>)?)";
        var classMatches = Regex.Matches(content, classPattern);

        foreach (Match classMatch in classMatches)
        {
            var className = classMatch.Groups[1].Value;

            // Find the start and end of the class
            var classStart = content.IndexOf(classMatch.Value);
            var classEnd = FindClassEnd(content, classStart);

            if (classEnd > classStart)
            {
                // Extract the class content
                var classContent = content.Substring(classStart, classEnd - classStart);

                // Extract commands within this class
                count += ExtractCommandMethods(classContent, commands, aliases, enums);
            }
        }

        return count;
    }

    private int FindClassEnd(string content, int classStart)
    {
        // Simple braces counter to find the end of the class
        var braceLevel = 0;
        var foundFirstBrace = false;

        for (var i = classStart; i < content.Length; i++)
        {
            if (content[i] == '{')
            {
                foundFirstBrace = true;
                braceLevel++;
            }
            else if (content[i] == '}')
            {
                braceLevel--;
                if (foundFirstBrace && braceLevel == 0)
                {
                    return i + 1;
                }
            }
        }

        return content.Length;
    }

    /// <summary>
    ///     Cleans a parameter string to avoid parsing errors.
    /// </summary>
    /// <param name="parameters">The raw parameter string</param>
    /// <returns>A cleaned parameter string</returns>
    private string CleanParametersString(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return string.Empty;

        // Remove comments
        parameters = Regex.Replace(parameters, @"\/\/.*?$", "", RegexOptions.Multiline);

        // Normalize whitespace
        parameters = Regex.Replace(parameters, @"\s+", " ").Trim();

        // Handle C# attributes like [Remainder]
        parameters = Regex.Replace(parameters, @"\[Remainder\]\s*", "");
        parameters = Regex.Replace(parameters, @"\[[^\]]+\]\s*", "");

        return parameters;
    }

// Improved method for extracting parameter descriptions with a larger window
    /// <summary>
    ///     Extracts parameter descriptions from XML documentation in the source code.
    /// </summary>
    /// <param name="content">The source code content</param>
    /// <param name="methodIndex">The position of the method in the source code</param>
    /// <returns>A dictionary mapping parameter names to their descriptions</returns>
    private Dictionary<string, string> ExtractParamDescriptions(string content, int methodIndex)
    {
        var result = new Dictionary<string, string>();

        // Look for XML param tags with a large window to catch them
        var searchStart = Math.Max(0, methodIndex - 2000);
        var searchLength = Math.Min(3000, content.Length - searchStart);

        if (searchLength <= 0)
            return result;

        var searchArea = content.Substring(searchStart, searchLength);

        // Find all param tags
        var paramMatches = Regex.Matches(searchArea, @"<param\s+name=""([^""]+)"">([\s\S]*?)<\/param>");

        foreach (Match match in paramMatches)
        {
            if (match.Success && match.Groups.Count >= 3)
            {
                var paramName = match.Groups[1].Value;
                var paramDesc = CleanXmlDescription(match.Groups[2].Value);
                result[paramName] = paramDesc;
            }
        }

        return result;
    }

    /// <summary>
    ///     Adds a command to the commands and aliases dictionaries.
    /// </summary>
    /// <param name="methodName">The name of the command method</param>
    /// <param name="summary">The summary description from XML docs</param>
    /// <param name="aliasesAttr">The aliases attribute text</param>
    /// <param name="parameters">The parameters string</param>
    /// <param name="commands">Dictionary to store command information</param>
    /// <param name="aliases">Dictionary to store command aliases</param>
    /// <param name="paramDescriptions">Dictionary of parameter descriptions</param>
    /// <param name="enums">Dictionary of enum information</param>
    private void AddCommand(string methodName, string summary, string aliasesAttr, string parameters,
        Dictionary<string, CommandInfo> commands, Dictionary<string, List<string>> aliases,
        Dictionary<string, string> paramDescriptions, Dictionary<string, EnumInfo> enums)
    {
        // If no summary provided, use a default one
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = $"Execute the {methodName} command";
        }

        // Extract argument patterns
        var argPatterns = ExtractArgPatterns(parameters, enums);

        // Parse parameters with descriptions
        var paramInfoList = ParseParameters(parameters, paramDescriptions, enums);

        // Create command key with better handling for overloads
        var commandKey = methodName.ToLower();
        if (commands.ContainsKey(commandKey))
        {
            // Create a unique key for overloads by adding parameter types
            var paramSignature = string.Join("_", paramInfoList.Select(p => p.Type.Replace(".", "_")));
            if (string.IsNullOrEmpty(paramSignature))
                paramSignature = "noparams";
            commandKey = $"{commandKey}_{paramSignature}";

            // If it's still a duplicate, add an index
            var overloadCount = 1;
            var baseKey = commandKey;
            while (commands.ContainsKey(commandKey))
            {
                commandKey = $"{baseKey}_{overloadCount++}";
            }
        }

        // Create command info
        var commandInfo = new CommandInfo
        {
            Desc = summary,
            Args = argPatterns,
            Parameters = paramInfoList,
            MethodSignature = $"{methodName}({string.Join(", ", paramInfoList.Select(p => p.Type))})",
            IsOverload = commands.Keys.Any(k => k.StartsWith(methodName.ToLower() + "_"))
        };

        // Add or update command
        commands[commandKey] = commandInfo;

        // Process aliases
        var aliasList = ParseAliases(methodName, aliasesAttr);
        aliases[commandKey] = aliasList;

        totalCommands++;
    }

    /// <summary>
    ///     Parses parameters from method signature string with enum support.
    /// </summary>
    /// <param name="parameters">The method parameters string</param>
    /// <param name="paramDescriptions">Dictionary of parameter descriptions</param>
    /// <param name="enums">Dictionary of enum information</param>
    /// <returns>List of parameter information objects</returns>
    private List<ParameterInfo> ParseParameters(string parameters, Dictionary<string, string> paramDescriptions,
        Dictionary<string, EnumInfo> enums)
    {
        var result = new List<ParameterInfo>();

        if (string.IsNullOrWhiteSpace(parameters))
            return result;

        // Split parameters with proper handling of generics and nested types
        var paramList = SplitParametersWithGenericsHandling(parameters);

        foreach (var param in paramList)
        {
            if (string.IsNullOrWhiteSpace(param)) continue;

            // More robust parameter parsing using regex
            var match = Regex.Match(param.Trim(), @"^(?:.*?\s+)?(\S+)\s+(\S+?)(?:\s*=\s*(.+))?$");

            if (!match.Success) continue;

            var type = match.Groups[1].Value.Trim();
            var name = match.Groups[2].Value.Trim().TrimEnd('?', ',', ';', ')');
            var defaultValueCapture = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;

            // Determine if parameter is optional
            var isOptional = defaultValueCapture != null || type.EndsWith("?") || param.Contains("= null") ||
                             param.Contains("=null");

            // Handle nullable types
            if (type.EndsWith("?"))
            {
                type = type.Substring(0, type.Length - 1);
                isOptional = true;
            }

            // Get description from param descriptions dictionary
            var description = paramDescriptions.TryGetValue(name, out var desc) ? desc : "";

            // Check if this is an enum type and add values to description
            if (enums.TryGetValue(type, out var enumInfo))
            {
                var enumValuesList = string.Join(", ", enumInfo.Values.Select(v => v.Name));
                if (!string.IsNullOrEmpty(description))
                    description += " ";
                description += $"Possible values: {enumValuesList}";
            }

            result.Add(new ParameterInfo
            {
                Name = name,
                Type = type,
                Description = description,
                IsOptional = isOptional,
                DefaultValue = defaultValueCapture
            });
        }

        return result;
    }

    /// <summary>
    ///     Helper method to split parameters while respecting generics and nested types.
    /// </summary>
    /// <param name="parameters">The parameters string to split</param>
    /// <returns>List of individual parameter strings</returns>
    private List<string> SplitParametersWithGenericsHandling(string parameters)
    {
        var result = new List<string>();
        var currentParam = new StringBuilder();
        var bracketLevel = 0;

        for (var i = 0; i < parameters.Length; i++)
        {
            var c = parameters[i];

            if (c == '<' || c == '[' || c == '(')
                bracketLevel++;
            else if (c == '>' || c == ']' || c == ')')
                bracketLevel--;

            if (c == ',' && bracketLevel == 0)
            {
                result.Add(currentParam.ToString().Trim());
                currentParam.Clear();
            }
            else
            {
                currentParam.Append(c);
            }
        }

        if (currentParam.Length > 0)
            result.Add(currentParam.ToString().Trim());

        return result;
    }

    /// <summary>
    ///     Extracts argument patterns from parameter list for command documentation.
    /// </summary>
    /// <param name="parameters">The method parameters string</param>
    /// <param name="enums">Dictionary of enum information</param>
    /// <returns>List of argument pattern strings</returns>
    private List<string> ExtractArgPatterns(string parameters, Dictionary<string, EnumInfo> enums)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(parameters))
        {
            result.Add("");
            return result;
        }

        var paramList = SplitParametersWithGenericsHandling(parameters);

        if (paramList.Count == 0)
        {
            result.Add("");
            return result;
        }

        // Build the complete pattern first
        var fullPattern = BuildArgPattern(paramList, enums);
        if (!string.IsNullOrWhiteSpace(fullPattern))
            result.Add(fullPattern);

        // Find optional parameters
        var requiredParams = new List<string>();
        var optionalParams = new List<string>();

        foreach (var param in paramList)
        {
            if (param.Contains("=") || param.Contains("null") || param.Contains("?"))
                optionalParams.Add(param);
            else
                requiredParams.Add(param);
        }

        // Add pattern with only required parameters
        if (requiredParams.Count > 0 && optionalParams.Count > 0)
        {
            var requiredPattern = BuildArgPattern(requiredParams, enums);
            if (!string.IsNullOrWhiteSpace(requiredPattern) && !result.Contains(requiredPattern))
                result.Add(requiredPattern);
        }

        // Add patterns for common usage scenarios
        AddSpecialCasePatterns(paramList, result, enums);

        // If still empty, add empty string
        if (result.Count == 0)
            result.Add("");

        return result.Distinct().ToList();
    }

    /// <summary>
    ///     Builds an argument pattern string from a parameter list.
    /// </summary>
    /// <param name="parameters">List of parameter strings</param>
    /// <param name="enums">Dictionary of enum information</param>
    /// <returns>A formatted argument pattern string</returns>
    private string BuildArgPattern(List<string> parameters, Dictionary<string, EnumInfo> enums)
    {
        var sb = new StringBuilder();

        foreach (var param in parameters)
        {
            var match = Regex.Match(param.Trim(), @"^(?:.*?\s+)?(\S+)\s+(\S+?)(?:\s*=\s*(.+))?$");
            if (!match.Success) continue;

            var type = match.Groups[1].Value.Trim();
            var name = match.Groups[2].Value.Trim().TrimEnd('?', ',', ';', ')');

            // Check if this is an enum and use its values
            if (enums.TryGetValue(type, out var enumInfo) && enumInfo.Values.Count > 0)
            {
                // Include top 3 enum values as examples
                var enumValues = enumInfo.Values.Take(3).Select(v => v.Name.ToLower());
                sb.Append($"{string.Join("|", enumValues)} ");
            }
            else
            {
                // Default parameter formatting based on type
                if (type.Contains("IUser") || type.Contains("IGuildUser"))
                    sb.Append("@user ");
                else if (type.Contains("IChannel"))
                    sb.Append("#channel ");
                else if (type.Contains("IRole"))
                    sb.Append("@role ");
                else if (Regex.IsMatch(type, @"\bint\b|\bdouble\b|\bfloat\b|\bdecimal\b|\blong\b"))
                    sb.Append("number ");
                else if (Regex.IsMatch(type, @"\bbool\b"))
                    sb.Append("true|false ");
                else if (Regex.IsMatch(type, @"\bstring\b"))
                    sb.Append("text ");
                else if (name.ToLower().Contains("time") || name.ToLower().Contains("date"))
                    sb.Append("time ");
                else if (name.ToLower().Contains("role"))
                    sb.Append("role ");
                else if (name.ToLower().Contains("amount") || name.ToLower().Contains("count"))
                    sb.Append("number ");
                else
                    sb.Append($"{name.ToLower()} ");
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    ///     Adds common usage patterns for parameter combinations.
    /// </summary>
    /// <param name="paramList">List of parameter strings</param>
    /// <param name="result">List to add the patterns to</param>
    /// <param name="enums">Dictionary of enum information</param>
    private void AddSpecialCasePatterns(List<string> paramList, List<string> result, Dictionary<string, EnumInfo> enums)
    {
        // Check for common parameter combinations
        var hasUserParam = paramList.Any(p => p.Contains("IUser"));
        var hasChannelParam = paramList.Any(p => p.Contains("IChannel"));
        var hasRoleParam = paramList.Any(p => p.Contains("IRole"));
        var hasNumberParam = paramList.Any(p =>
            Regex.IsMatch(p, @"\bint\b|\bdouble\b|\bfloat\b|\bdecimal\b|\blong\b") ||
            p.ToLower().Contains("amount") ||
            p.ToLower().Contains("count"));

        // Add User + Channel pattern
        if (hasUserParam && hasChannelParam)
        {
            var pattern = "@user #channel";
            if (!result.Contains(pattern))
                result.Add(pattern);
        }

        // Add User + Role pattern
        if (hasUserParam && hasRoleParam)
        {
            var pattern = "@user @role";
            if (!result.Contains(pattern))
                result.Add(pattern);
        }

        // Add User + Number pattern
        if (hasUserParam && hasNumberParam)
        {
            var pattern = "@user number";
            if (!result.Contains(pattern))
                result.Add(pattern);
        }

        // If there are enum parameters, add patterns with enum values
        foreach (var param in paramList)
        {
            var match = Regex.Match(param.Trim(), @"^(?:.*?\s+)?(\S+)\s+\S+");
            if (!match.Success) continue;

            var type = match.Groups[1].Value.Trim();

            if (enums.TryGetValue(type, out var enumInfo) && enumInfo.Values.Count > 0)
            {
                // Add patterns with top enum values
                foreach (var value in enumInfo.Values.Take(Math.Min(3, enumInfo.Values.Count)))
                {
                    var enumPattern = value.Name.ToLower();
                    if (!result.Contains(enumPattern))
                        result.Add(enumPattern);
                }
            }
        }
    }

    /// <summary>
    ///     Writes command information to a YAML file.
    /// </summary>
    /// <param name="commands">Dictionary of command information</param>
    /// <param name="filePath">Path to the output file</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task WriteYamlFileAsync(Dictionary<string, CommandInfo> commands, string filePath)
    {
        // Normalize and deduplicate commands
        var normalizedCommands = NormalizeCommands(commands);

        // Use better serialization settings to avoid formatting issues
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithIndentedSequences()
            .Build();

        var yamlCommands = normalizedCommands.ToDictionary(
            kvp => kvp.Key,
            kvp => new
            {
                kvp.Value.Desc,
                kvp.Value.Args,
                Params = kvp.Value.Parameters.Select(p => new
                {
                    p.Name,
                    Desc = p.Description,
                    p.Type,
                    Optional = p.IsOptional,
                    Default = p.DefaultValue
                }).ToList()
            }
        );

        await using var writer = new StreamWriter(filePath);
        await writer.WriteAsync(serializer.Serialize(yamlCommands));
    }

    /// <summary>
    ///     Writes enum information to a YAML file.
    /// </summary>
    /// <param name="enums">Dictionary of enum information</param>
    /// <param name="filePath">Path to the output file</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task WriteEnumsYamlFileAsync(Dictionary<string, EnumInfo> enums, string filePath)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithIndentedSequences()
            .Build();

        var yamlEnums = enums.ToDictionary(
            kvp => kvp.Key,
            kvp => new
            {
                kvp.Value.Description,
                Values = kvp.Value.Values.ToDictionary(
                    v => v.Name,
                    v => new
                    {
                        v.Description, v.Value
                    }
                )
            }
        );

        await using var writer = new StreamWriter(filePath);
        await writer.WriteAsync(serializer.Serialize(yamlEnums));
    }

// Helper methods that need to be kept unchanged
    /// <summary>
    ///     Cleans an XML description by removing tags and normalizing whitespace.
    /// </summary>
    /// <param name="description">The XML description to clean</param>
    /// <returns>A cleaned description string</returns>
    private string CleanXmlDescription(string description)
    {
        // Remove extra whitespace
        description = Regex.Replace(description, @"\s+", " ").Trim();
        // Remove common XML formatting
        description = Regex.Replace(description, @"<[^>]+>", "");
        return description;
    }

    /// <summary>
    ///     Cleans an XML summary by removing comment markers and tags.
    /// </summary>
    /// <param name="summary">The XML summary to clean</param>
    /// <returns>A cleaned summary string</returns>
    private string CleanSummary(string summary)
    {
        if (string.IsNullOrEmpty(summary))
            return string.Empty;

        // Remove XML documentation comment delimiters
        summary = summary.Replace("///", "").Replace("/*", "").Replace("*/", "");

        // Remove XML tags
        summary = Regex.Replace(summary, @"<see\s+cref=""([^""]+)""\s*/>", m =>
            m.Groups[1].Value.Split('.').Last());

        summary = Regex.Replace(summary, @"<[^>]+>", "");

        // Clean up whitespace
        summary = Regex.Replace(summary, @"\s+", " ").Trim();

        return summary;
    }

    /// <summary>
    ///     Parses command aliases from method name and aliases attribute.
    /// </summary>
    /// <param name="methodName">The name of the command method</param>
    /// <param name="aliasesAttr">The aliases attribute content</param>
    /// <returns>List of alias strings</returns>
    private List<string> ParseAliases(string methodName, string aliasesAttr)
    {
        var aliasList = new List<string>
        {
            methodName.ToLower()
        };

        // Extract explicit aliases if provided
        if (!string.IsNullOrEmpty(aliasesAttr))
        {
            var explicitAliases = aliasesAttr
                .Split(',')
                .Select(a => a.Trim(' ', '"', '\''))
                .Where(a => !string.IsNullOrEmpty(a))
                .Select(a => a.ToLower());

            aliasList.AddRange(explicitAliases);
        }

        return aliasList.Distinct().ToList();
    }

    /// <summary>
    /// Normalizes and deduplicates commands before writing to YAML
    /// </summary>
    private Dictionary<string, CommandInfo> NormalizeCommands(Dictionary<string, CommandInfo> commands)
    {
        // Create a dictionary to track unique commands by their true name and signature
        var uniqueCommands = new Dictionary<string, KeyValuePair<string, CommandInfo>>();

        foreach (var kvp in commands)
        {
            // Create a unique signature based on command name and parameters
            var cmdName = kvp.Key;
            var signature = $"{cmdName}:{string.Join(",", kvp.Value.Parameters.Select(p => p.Type))}";

            // Only add if we haven't seen this exact command before
            uniqueCommands.TryAdd(signature, kvp);
        }

        // Return the deduplicated commands
        return uniqueCommands.Values.ToDictionary(v => v.Key, v => v.Value);
    }

    /// <summary>
    ///     Class that stores information about an enum type, including its name, description, and values.
    /// </summary>
    public class EnumInfo
    {
        /// <summary>
        ///     The name of the enum type.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        ///     The description of the enum type from XML documentation.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        ///     List of values defined in the enum.
        /// </summary>
        public List<EnumValueInfo> Values { get; set; } = new();
    }

    /// <summary>
    ///     Class that stores information about a specific enum value, including its name, description, and numeric value.
    /// </summary>
    public class EnumValueInfo
    {
        /// <summary>
        ///     The name of the enum value.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        ///     The description of the enum value from XML documentation.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        ///     The explicit numeric value of the enum field, if specified.
        /// </summary>
        public string? Value { get; set; }
    }

    /// <summary>
    /// </summary>
    public class ParameterInfo
    {
        /// <summary>
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// </summary>
        public string? DefaultValue { get; set; }
    }

    /// <summary>
    /// </summary>
    public class CommandInfo
    {
        /// <summary>
        /// </summary>
        public List<string> Args { get; set; } = new();

        /// <summary>
        /// </summary>
        public string Desc { get; set; } = string.Empty;

        /// <summary>
        /// </summary>
        public List<ParameterInfo> Parameters { get; set; } = new();

        /// <summary>
        /// </summary>
        public string MethodSignature { get; set; } = string.Empty; // For overload identification

        /// <summary>
        /// </summary>
        public bool IsOverload { get; set; }
    }
}