﻿using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Serilog;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

/// <inheritdoc />
public class TrovoProvider : Provider
{
    private readonly IBotCredentials creds;
    private readonly IHttpClientFactory httpClientFactory;

    private readonly Regex urlRegex = new(@"trovo.live\/(Mewdeko<channel>[\w\d\-_]+)/Mewdeko", RegexOptions.Compiled);

    /// <inheritdoc />
    public TrovoProvider(IHttpClientFactory httpClientFactory, IBotCredentials creds)
    {
        (this.httpClientFactory, this.creds) = (httpClientFactory, creds);

        if (string.IsNullOrWhiteSpace(creds.TrovoClientId))
        {
            Log.Warning("""
                        Trovo streams are using a default clientId.
                        If you are experiencing ratelimits, you should create your own application at: https://developer.trovo.live/
                        """);
        }
    }

    /// <inheritdoc />
    public override FType Platform
    {
        get
        {
            return FType.Trovo;
        }
    }

    /// <inheritdoc />
    public override Task<bool> IsValidUrl(string url)
    {
        return Task.FromResult(urlRegex.IsMatch(url));
    }

    /// <inheritdoc />
    public override Task<StreamData> GetStreamDataByUrlAsync(string url)
    {
        var match = urlRegex.Match(url);
        if (match.Length == 0)
            return Task.FromResult(default(StreamData));

        return GetStreamDataAsync(match.Groups["channel"].Value);
    }

    /// <inheritdoc />
    public override async Task<StreamData?> GetStreamDataAsync(string login)
    {
        using var http = httpClientFactory.CreateClient();

        var trovoClientId = creds.TrovoClientId;

        if (string.IsNullOrWhiteSpace(trovoClientId))
        {
            trovoClientId = "8b3cc4719b7051803099661a3265e50b";
        }

        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("Accept", "application/json");
        http.DefaultRequestHeaders.Add("Client-ID", trovoClientId);

        // trovo ratelimit is very generous (1200 per minute)
        // so there is no need for ratelimit checks atm
        try
        {
            var res = await http.PostAsJsonAsync(
                "https://open-api.trovo.live/openplatform/channels/id",
                new TrovoRequestData
                {
                    Username = login
                }).ConfigureAwait(false);

            res.EnsureSuccessStatusCode();

            var data = await res.Content.ReadFromJsonAsync<TrovoGetUsersResponse>().ConfigureAwait(false);

            if (data is null)
            {
                Log.Warning("An empty response received while retrieving stream data for trovo.live/{TrovoId}", login);
                FailingStreams.TryAdd(login, DateTime.UtcNow);
                return null;
            }

            FailingStreams.TryRemove(data.Username, out _);
            return new StreamData
            {
                IsLive = data.IsLive,
                Game = data.CategoryName,
                Name = data.Username,
                Title = data.LiveTitle,
                Viewers = data.CurrentViewers,
                AvatarUrl = data.ProfilePic,
                StreamType = Platform,
                StreamUrl = data.ChannelUrl,
                UniqueName = data.Username,
                Preview = data.Thumbnail
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error retrieving stream data for trovo.live/{TrovoId}", login);
            FailingStreams.TryAdd(login, DateTime.UtcNow);
            return null;
        }
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyCollection<StreamData?>> GetStreamDataAsync(List<string> usernames)
    {
        var trovoClientId = creds.TrovoClientId;

        if (string.IsNullOrWhiteSpace(trovoClientId))
        {
            Log.Warning("Trovo streams will be ignored until TrovoClientId is added to creds.yml");
            return [];
        }

        var results = new List<StreamData?>(usernames.Count);
        foreach (var chunk in usernames.Chunk(10)
                     .Select(x => x.Select(GetStreamDataAsync)))
        {
            var chunkResults = await Task.WhenAll(chunk).ConfigureAwait(false);
            results.AddRange(chunkResults.Where(x => x is not null));
            await Task.Delay(1000).ConfigureAwait(false);
        }

        return results;
    }
}