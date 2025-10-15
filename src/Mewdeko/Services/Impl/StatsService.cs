using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using Humanizer;
using Mewdeko.Modules.Utility.Services;
using Swan.Formatters;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Service for collecting and posting statistics about the bot.
/// </summary>
public class StatsService : IStatsService, IDisposable
{
    /// <summary>
    ///     The version of the bot. I should make this set from commits somehow idk
    /// </summary>
    public const string BotVersion = "7.8.10";

    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly IBotCredentials creds;
    private readonly HttpClient http;
    private readonly ILogger<StatsService> logger;

    private readonly DateTime started;
    private PeriodicTimer topGgTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StatsService" /> class.
    /// </summary>
    /// <param name="client">The discord client</param>
    /// <param name="creds">The bots credentials</param>
    /// <param name="http">The http client</param>
    /// <param name="cache">The caching service</param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public StatsService(
        DiscordShardedClient client, IBotCredentials creds,
        HttpClient http, IDataCache cache, ILogger<StatsService> logger)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.creds = creds ?? throw new ArgumentNullException(nameof(creds));
        this.http = http ?? throw new ArgumentNullException(nameof(http));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.logger = logger;

        started = DateTime.UtcNow;

            _ = PostToTopGg();
            _ = OnReadyAsync();
        }

    /// <summary>
    /// Gets the version of the Discord.Net library.
    /// </summary>
    public string Library => $"Discord.Net {DllVersionChecker.GetDllVersion()} ";

    /// <summary>
    /// Delegate for getting versions of specified DLLs.
    /// </summary>
    /// <param name="dllNames">List of DLL names to get versions for.</param>
    /// <returns>A dictionary with DLL names as keys and their versions as values.</returns>
    public delegate Dictionary<string, string?> GetVersionsDelegate(List<string> dllNames);

    /// <summary>
    /// Provides information about the libraries used by the bot.
    /// </summary>
    public class LibraryInfo
    {
        private GetVersionsDelegate _versionChecker;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryInfo"/> class.
        /// </summary>
        /// <param name="versionChecker">The delegate to get versions of specified DLLs.</param>
        public LibraryInfo(GetVersionsDelegate versionChecker)
        {
            _versionChecker = versionChecker;
        }

        /// <summary>
        /// Gets the version of the Discord.Net library.
        /// </summary>
        public string Library
        {
            get
            {
                var versions = _versionChecker.Invoke(new List<string> { "Discord.Net.WebSocket.dll" });
                return $"Discord.Net {versions["Discord.Net.WebSocket.dll"] ?? "Version not found"}";
            }
        }

        /// <summary>
        /// Gets the version of the OpenAI_API library.
        /// </summary>
        public string OpenAILib
        {
            get
            {
                var versions = _versionChecker.Invoke(new List<string> { "OpenAI_API.dll" });
                return $"OpenAI_API {versions["OpenAI_API.dll"] ?? "Version not found"}";
            }
        }

        /// <summary>
        /// Gets the target framework of the executing assembly.
        /// </summary>
        /// <returns>The target framework name.</returns>
        public static string GetTargetFramework()
        {
            var attribute = Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
                .FirstOrDefault() as System.Runtime.Versioning.TargetFrameworkAttribute;

            return attribute?.FrameworkName ?? "Unknown framework";
        }
    }

    /// <summary>
    ///     Disposes of the timers.
    /// </summary>
    public void Dispose()
    {
        topGgTimer?.Dispose();
    }

    /// <summary>
    ///     Gets the memory usage of the bot.
    /// </summary>
    public string Heap
    {
        get
        {
            return ByteSize.FromBytes(Process.GetCurrentProcess().WorkingSet64).Megabytes
                .ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    ///     Gets the uptime of the bot as a human-readable string.
    /// </summary>
    /// <param name="separator">The separator</param>
    /// <returns>A string used in .stats to display uptime</returns>
    public string GetUptimeString(string separator = ", ")
    {
        return GetUptime().Humanize(2, minUnit: TimeUnit.Minute, collectionSeparator: separator);
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        _ = Task.Run(async () =>
        {
            var periodicTimer = new PeriodicTimer(TimeSpan.FromHours(12));

            do
            {
                try
                {
                    logger.LogInformation("Updating top guilds");
                    var guilds = await client.Rest.GetGuildsAsync(true);
                    var servers = guilds.OrderByDescending(x => x.ApproximateMemberCount.Value)
                        .Where(x => !x.Name.Contains("botlist", StringComparison.CurrentCultureIgnoreCase)).Take(11)
                        .Select(x =>
                            new MewdekoPartialGuild
                            {
                                IconUrl = x.IconId.StartsWith("a_") ? x.IconUrl.Replace(".jpg", ".gif") : x.IconUrl,
                                MemberCount = x.ApproximateMemberCount.Value,
                                Name = x.Name
                            });

                    var serialied = Json.Serialize(servers);
                    await cache.Redis.GetDatabase().StringSetAsync($"{client.CurrentUser.Id}_topguilds", serialied)
                        .ConfigureAwait(false);
                    logger.LogInformation("Updated top guilds");
                }
                catch (Exception e)
                {
                    logger.LogError("Failed to update top guilds: {0}", e);
                    return;
                }
            } while (await periodicTimer.WaitForNextTickAsync());
        });
        return Task.CompletedTask;
    }

    private TimeSpan GetUptime()
    {
        return DateTime.UtcNow - started;
    }

    private async Task PostToTopGg()
    {
        if (string.IsNullOrEmpty(creds.VotesToken)) return;

        topGgTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (await topGgTimer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {
                    "shard_count", creds.TotalShards.ToString()
                },
                {
                    "server_count", client.Guilds.Count.ToString()
                }
            });

            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Authorization", creds.VotesToken);
            var response = await http
                .PostAsync(new Uri($"https://top.gg/api/bots/{client.CurrentUser.Id}/stats"), content)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode) continue;
            logger.LogError("Failed to post stats to Top.gg");
            return;
        }
    }

    /// <summary>
    ///     Represents a partial guild information.
    /// </summary>
    private class MewdekoPartialGuild
    {
        /// <summary>
        ///     Gets or sets the name of the guild.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        ///     Gets or sets the URL of the guild's icon.
        /// </summary>
        public string? IconUrl { get; set; }

        /// <summary>
        ///     Gets or sets the number of members in the guild.
        /// </summary>
        public int MemberCount { get; set; }
    }
}