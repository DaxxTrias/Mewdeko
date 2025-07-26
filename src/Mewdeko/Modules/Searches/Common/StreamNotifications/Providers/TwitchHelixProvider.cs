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

        Logger.Information("[TwitchHelixProvider] Initializing with ClientId: {ClientId}, HasSecret: {HasSecret}",
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
#if DEBUG
            Logger.Information("[TwitchHelixProvider] Requesting access token...");
#endif
            var token = await api.Value.Auth.GetAccessTokenAsync().ConfigureAwait(false);

            if (token is null)
            {
                Logger.Error("[TwitchHelixProvider] Failed to get access token - returned null");
            }
            else
            {
#if DEBUG
                Logger.Information("[TwitchHelixProvider] Successfully obtained access token");
#endif
            }

            return token;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[TwitchHelixProvider] Exception while getting access token: {ErrorMessage}", ex.Message);
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
#if DEBUG
        Logger.Information("[TwitchHelixProvider] GetStreamDataAsync called with {LoginCount} logins: {Logins}",
            logins.Count, string.Join(", ", logins));
#endif

        if (logins.Count == 0)
        {
#if DEBUG
            Logger.Information("[TwitchHelixProvider] No logins provided, returning empty result");
#endif
            return [];
        }

        var token = await EnsureTokenValidAsync().ConfigureAwait(false);

        if (token is null)
        {
#if DEBUG
            Logger.Error(
                "[TwitchHelixProvider] Failed to get valid token - Twitch client ID and Secret are incorrect! Please go to https://dev.twitch.tv and create an application!");
#endif
            return [];
        }

#if DEBUG
        Logger.Information("[TwitchHelixProvider] Setting up HTTP client with authentication headers");
#endif
        using var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("Client-Id", clientId);
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var loginsSet = logins.Select(x => x.ToLowerInvariant())
            .Distinct()
            .ToHashSet();
#if DEBUG
        Logger.Information("[TwitchHelixProvider] Processing {UniqueLoginCount} unique logins after deduplication",
            loginsSet.Count);
#endif

        var dataDict = new Dictionary<string, StreamData>();

#if DEBUG
        Logger.Information("[TwitchHelixProvider] Starting user data retrieval phase");
#endif
        foreach (var chunk in logins.Chunk(100))
        {
            try
            {
                var url = $"https://api.twitch.tv/helix/users?{chunk.Select(x => $"login={x}").Join('&')}&first=100";
#if DEBUG
                Logger.Information(
                    "[TwitchHelixProvider] Requesting user data for chunk of {ChunkSize} users: {UserChunk}",
                    chunk.Length, string.Join(", ", chunk));
#endif

                var str = await http.GetStringAsync(url).ConfigureAwait(false);
#if DEBUG
                Logger.Information("[TwitchHelixProvider] Received user data response, length: {ResponseLength}",
                    str.Length);
#endif

                var resObj = JsonSerializer.Deserialize<HelixUsersResponse>(str);

                if (resObj?.Data is null || resObj.Data.Count == 0)
                {
#if DEBUG
                    Logger.Warning("[TwitchHelixProvider] No user data returned for chunk: {UserChunk}",
                        string.Join(", ", chunk));
#endif
                    continue;
                }

#if DEBUG
                Logger.Information("[TwitchHelixProvider] Successfully parsed {UserCount} users from response",
                    resObj.Data.Count);
#endif
                foreach (var user in resObj.Data)
                {
                    var lowerLogin = user.Login.ToLowerInvariant();
                    if (loginsSet.Remove(lowerLogin))
                    {
                        dataDict[lowerLogin] = UserToStreamData(user);
#if DEBUG
                        Logger.Information(
                            "[TwitchHelixProvider] Added user data for {Username} (DisplayName: {DisplayName})",
                            user.Login, user.DisplayName);
#endif
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Logger.Error(ex,
                    "[TwitchHelixProvider] Error retrieving user data for chunk {UserChunk}: {ErrorMessage}",
                    string.Join(", ", chunk), ex.Message);
#endif
                return new List<StreamData>();
            }
        }

        // any item left over loginsSet is an invalid username
        if (loginsSet.Count > 0)
        {
#if DEBUG
            Logger.Warning("[TwitchHelixProvider] {InvalidUserCount} usernames were not found: {InvalidUsers}",
                loginsSet.Count, string.Join(", ", loginsSet));
#endif
            foreach (var login in loginsSet)
            {
                FailingStreams.TryAdd(login, DateTime.UtcNow);
            }
        }

        // only get streams for users which exist
#if DEBUG
        Logger.Information(
            "[TwitchHelixProvider] Starting stream data retrieval phase for {ValidUserCount} valid users",
            dataDict.Count);
#endif
        foreach (var chunk in dataDict.Keys.Chunk(100))
        {
            try
            {
                var url =
                    $"https://api.twitch.tv/helix/streams?{chunk.Select(x => $"user_login={x}").Join('&')}&first=100";
#if DEBUG
                Logger.Information(
                    "[TwitchHelixProvider] Requesting stream data for chunk of {ChunkSize} users: {UserChunk}",
                    chunk.Length, string.Join(", ", chunk));
#endif

                var str = await http.GetStringAsync(url).ConfigureAwait(false);
#if DEBUG
                Logger.Information("[TwitchHelixProvider] Received stream data response, length: {ResponseLength}",
                    str.Length);
#endif

                var res = JsonSerializer.Deserialize<HelixStreamsResponse>(str);

                if (res?.Data is null || res.Data.Count == 0)
                {
#if DEBUG
                    Logger.Information("[TwitchHelixProvider] No streams currently live for chunk: {UserChunk}",
                        string.Join(", ", chunk));
#endif
                    continue;
                }

#if DEBUG
                Logger.Information("[TwitchHelixProvider] Found {LiveStreamCount} live streams in response",
                    res.Data.Count);
#endif
                foreach (var helixStreamData in res.Data)
                {
                    var login = helixStreamData.UserLogin.ToLowerInvariant();
                    if (dataDict.TryGetValue(login, out var old))
                    {
                        dataDict[login] = FillStreamData(old, helixStreamData);
#if DEBUG
                        Logger.Information(
                            "[TwitchHelixProvider] Updated stream data for {Username}: Live={IsLive}, Game={Game}, Viewers={Viewers}",
                            helixStreamData.UserLogin, helixStreamData.Type == "live",
                            helixStreamData.GameName ?? "(none)", helixStreamData.ViewerCount);
#endif
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Logger.Error(ex,
                    "[TwitchHelixProvider] Error retrieving stream data for chunk {UserChunk}: {ErrorMessage}",
                    string.Join(", ", chunk), ex.Message);
#endif
                return new List<StreamData>();
            }
        }

        var result = dataDict.Values.ToList();
        var liveCount = result.Count(x => x.IsLive);
        var offlineCount = result.Count - liveCount;
#if DEBUG
        Logger.Information(
            "[TwitchHelixProvider] Returning {TotalStreamCount} streams: {LiveCount} live, {OfflineCount} offline",
            result.Count, liveCount, offlineCount);
#endif

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