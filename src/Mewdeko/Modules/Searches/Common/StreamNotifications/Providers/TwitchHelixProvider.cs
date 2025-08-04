using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Serilog;
using TwitchLib.Api;
using ILogger = Serilog.ILogger;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

/// <inheritdoc />
public class TwitchHelixProvider : Provider
{
    private static readonly ILogger Logger = Log.ForContext<TwitchHelixProvider>();
    private readonly Lazy<TwitchAPI> api;
    private readonly string clientId;
    private readonly IHttpClientFactory httpClientFactory;

    /// <inheritdoc />
    public TwitchHelixProvider(IHttpClientFactory httpClientFactory, IBotCredentials credsProvider)
    {
        this.httpClientFactory = httpClientFactory;

        var creds = credsProvider;
        clientId = creds.TwitchClientId;
        var clientSecret = creds.TwitchClientSecret;

        Logger.Information("Initializing with ClientId: {ClientId}, HasSecret: {HasSecret}",
            string.IsNullOrEmpty(clientId) ? "(empty)" : clientId[..Math.Min(8, clientId.Length)] + "...",
            !string.IsNullOrEmpty(clientSecret));

        api = new Lazy<TwitchAPI>(() => new TwitchAPI
        {
            Helix =
            {
                Settings =
                {
                    ClientId = clientId, Secret = clientSecret
                }
            }
        });
    }

    private static Regex Regex { get; } = new(@"twitch.tv/(?<name>[\w\d\-_]+)/?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <inheritdoc />
    public override FType Platform
    {
        get
        {
            return FType.Twitch;
        }
    }

    private async Task<string?> EnsureTokenValidAsync()
    {
        try
        {
            var token = await api.Value.Auth.GetAccessTokenAsync().ConfigureAwait(false);

            if (token is null)
            {
                Logger.Error("Failed to get access token - returned null");
            }

            return token;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception while getting access token: {ErrorMessage}", ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public override Task<bool> IsValidUrl(string url)
    {
        var match = Regex.Match(url);
        if (!match.Success)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public override Task<StreamData?> GetStreamDataByUrlAsync(string url)
    {
        var match = Regex.Match(url);
        if (!match.Success) return Task.FromResult<StreamData>(null);
        var name = match.Groups["name"].Value;
        return GetStreamDataAsync(name);
    }

    /// <inheritdoc />
    public override async Task<StreamData?> GetStreamDataAsync(string login)
    {
        var data = await GetStreamDataAsync([login]).ConfigureAwait(false);

        return data.FirstOrDefault();
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyCollection<StreamData>> GetStreamDataAsync(List<string> logins)
    {
        if (logins.Count == 0)
        {
            return [];
        }

        var token = await EnsureTokenValidAsync().ConfigureAwait(false);

        if (token is null)
        {
            return [];
        }

        using var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("Client-Id", clientId);
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var loginsSet = logins.Select(x => x.ToLowerInvariant())
            .Distinct()
            .ToHashSet();

        var dataDict = new Dictionary<string, StreamData>();

        foreach (var chunk in logins.Chunk(100))
        {
            try
            {
                var url = $"https://api.twitch.tv/helix/users?{chunk.Select(x => $"login={x}").Join('&')}&first=100";

                var str = await http.GetStringAsync(url).ConfigureAwait(false);

                var resObj = JsonSerializer.Deserialize<HelixUsersResponse>(str);

                if (resObj?.Data is null || resObj.Data.Count == 0)
                {
                    continue;
                }

                foreach (var user in resObj.Data)
                {
                    var lowerLogin = user.Login.ToLowerInvariant();
                    if (loginsSet.Remove(lowerLogin))
                    {
                        dataDict[lowerLogin] = UserToStreamData(user);
                    }
                }
            }
            catch
            {
                return new List<StreamData>();
            }
        }

        // any item left over loginsSet is an invalid username
        if (loginsSet.Count > 0)
        {
            foreach (var login in loginsSet)
            {
                FailingStreams.TryAdd(login, DateTime.UtcNow);
            }
        }

        // only get streams for users which exist

        foreach (var chunk in dataDict.Keys.Chunk(100))
        {
            try
            {
                var url =
                    $"https://api.twitch.tv/helix/streams?{chunk.Select(x => $"user_login={x}").Join('&')}&first=100";

                var str = await http.GetStringAsync(url).ConfigureAwait(false);

                var res = JsonSerializer.Deserialize<HelixStreamsResponse>(str);

                if (res?.Data is null || res.Data.Count == 0)
                {
                    continue;
                }

                foreach (var helixStreamData in res.Data)
                {
                    var login = helixStreamData.UserLogin.ToLowerInvariant();
                    if (dataDict.TryGetValue(login, out var old))
                    {
                        dataDict[login] = FillStreamData(old, helixStreamData);
                    }
                }
            }
            catch
            {
                return new List<StreamData>();
            }
        }

        var result = dataDict.Values.ToList();
        var liveCount = result.Count(x => x.IsLive);
        var offlineCount = result.Count - liveCount;

        return result;
    }

    private static StreamData UserToStreamData(HelixUsersResponse.User user)
    {
        return new StreamData
        {
            UniqueName = user.Login,
            Name = user.DisplayName,
            AvatarUrl = user.ProfileImageUrl,
            IsLive = false,
            StreamUrl = $"https://twitch.tv/{user.Login}",
            StreamType = FType.Twitch,
            Preview = user.OfflineImageUrl,
            ChannelId = user.Id
        };
    }

    private static StreamData FillStreamData(StreamData partial, HelixStreamsResponse.StreamData apiData)
    {
        return partial with
        {
            StreamType = FType.Twitch,
            Viewers = apiData.ViewerCount,
            Title = apiData.Title,
            IsLive = apiData.Type == "live",
            Preview = apiData.ThumbnailUrl
                .Replace("{width}", "640")
                .Replace("{height}", "480"),
            Game = apiData.GameName
        };
    }
}