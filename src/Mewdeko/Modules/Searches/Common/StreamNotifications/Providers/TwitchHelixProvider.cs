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
            Logger.Information("[TwitchHelixProvider] Requesting access token...");
            var token = await api.Value.Auth.GetAccessTokenAsync().ConfigureAwait(false);

            if (token is null)
            {
                Logger.Error("[TwitchHelixProvider] Failed to get access token - returned null");
            }
            else
            {
                Logger.Information("[TwitchHelixProvider] Successfully obtained access token");
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
            Logger.Information("[TwitchHelixProvider] No logins provided, returning empty result");
            return [];
        }

        var token = await EnsureTokenValidAsync().ConfigureAwait(false);

        if (token is null)
        {
            Logger.Error(
                "[TwitchHelixProvider] Failed to get valid token - Twitch client ID and Secret are incorrect! Please go to https://dev.twitch.tv and create an application!");
            return [];
        }

        Logger.Information("[TwitchHelixProvider] Setting up HTTP client with authentication headers");
        using var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("Client-Id", clientId);
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var loginsSet = logins.Select(x => x.ToLowerInvariant())
            .Distinct()
            .ToHashSet();
        Logger.Information("[TwitchHelixProvider] Processing {UniqueLoginCount} unique logins after deduplication",
            loginsSet.Count);

        var dataDict = new Dictionary<string, StreamData>();

        Logger.Information("[TwitchHelixProvider] Starting user data retrieval phase");
        foreach (var chunk in logins.Chunk(100))
        {
            try
            {
                var url = $"https://api.twitch.tv/helix/users?{chunk.Select(x => $"login={x}").Join('&')}&first=100";
                Logger.Information(
                    "[TwitchHelixProvider] Requesting user data for chunk of {ChunkSize} users: {UserChunk}",
                    chunk.Length, string.Join(", ", chunk));

                var str = await http.GetStringAsync(url).ConfigureAwait(false);
                Logger.Information("[TwitchHelixProvider] Received user data response, length: {ResponseLength}",
                    str.Length);

                var resObj = JsonSerializer.Deserialize<HelixUsersResponse>(str);

                if (resObj?.Data is null || resObj.Data.Count == 0)
                {
                    Logger.Warning("[TwitchHelixProvider] No user data returned for chunk: {UserChunk}",
                        string.Join(", ", chunk));
                    continue;
                }

                Logger.Information("[TwitchHelixProvider] Successfully parsed {UserCount} users from response",
                    resObj.Data.Count);
                foreach (var user in resObj.Data)
                {
                    var lowerLogin = user.Login.ToLowerInvariant();
                    if (loginsSet.Remove(lowerLogin))
                    {
                        dataDict[lowerLogin] = UserToStreamData(user);
                        Logger.Information(
                            "[TwitchHelixProvider] Added user data for {Username} (DisplayName: {DisplayName})",
                            user.Login, user.DisplayName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex,
                    "[TwitchHelixProvider] Error retrieving user data for chunk {UserChunk}: {ErrorMessage}",
                    string.Join(", ", chunk), ex.Message);
                return new List<StreamData>();
            }
        }

        // any item left over loginsSet is an invalid username
        if (loginsSet.Count > 0)
        {
            Logger.Warning("[TwitchHelixProvider] {InvalidUserCount} usernames were not found: {InvalidUsers}",
                loginsSet.Count, string.Join(", ", loginsSet));
            foreach (var login in loginsSet)
            {
                FailingStreams.TryAdd(login, DateTime.UtcNow);
            }
        }

        // only get streams for users which exist
        Logger.Information(
            "[TwitchHelixProvider] Starting stream data retrieval phase for {ValidUserCount} valid users",
            dataDict.Count);
        foreach (var chunk in dataDict.Keys.Chunk(100))
        {
            try
            {
                var url =
                    $"https://api.twitch.tv/helix/streams?{chunk.Select(x => $"user_login={x}").Join('&')}&first=100";
                Logger.Information(
                    "[TwitchHelixProvider] Requesting stream data for chunk of {ChunkSize} users: {UserChunk}",
                    chunk.Length, string.Join(", ", chunk));

                var str = await http.GetStringAsync(url).ConfigureAwait(false);
                Logger.Information("[TwitchHelixProvider] Received stream data response, length: {ResponseLength}",
                    str.Length);

                var res = JsonSerializer.Deserialize<HelixStreamsResponse>(str);

                if (res?.Data is null || res.Data.Count == 0)
                {
                    Logger.Information("[TwitchHelixProvider] No streams currently live for chunk: {UserChunk}",
                        string.Join(", ", chunk));
                    continue;
                }

                Logger.Information("[TwitchHelixProvider] Found {LiveStreamCount} live streams in response",
                    res.Data.Count);
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
                Logger.Error(ex,
                    "[TwitchHelixProvider] Error retrieving stream data for chunk {UserChunk}: {ErrorMessage}",
                    string.Join(", ", chunk), ex.Message);
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