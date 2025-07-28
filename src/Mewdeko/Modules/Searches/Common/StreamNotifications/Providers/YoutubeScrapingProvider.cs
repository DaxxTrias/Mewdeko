using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

/// <summary>
///     YouTube provider that uses web scraping for live stream detection. Stores streams using @username format.
/// </summary>
public partial class YoutubeScrapingProvider : Provider, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<YoutubeScrapingProvider>();
    private readonly HtmlDocument htmlDocument = new();

    private readonly HttpClient httpClient;

    /// <summary>
    ///     Initializes a new instance of the <see cref="YoutubeScrapingProvider" /> class.
    /// </summary>
    public YoutubeScrapingProvider()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip |
                                     DecompressionMethods.Deflate |
                                     DecompressionMethods.Brotli
        };
        httpClient = new HttpClient(handler);

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        httpClient.DefaultRequestHeaders.Add("DNT", "1");
        httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    private static Regex Regex { get; } = MyRegex();

    /// <inheritdoc />
    public override FType Platform
    {
        get
        {
            return FType.Youtube;
        }
    }

    /// <summary>
    ///     Disposes the HTTP client resources.
    /// </summary>
    public void Dispose()
    {
        httpClient.Dispose();
    }

    /// <inheritdoc />
    public override Task<bool> IsValidUrl(string url)
    {
        var match = Regex.Match(url);
        return Task.FromResult(match.Success);
    }

    /// <inheritdoc />
    public override async Task<StreamData?> GetStreamDataByUrlAsync(string url)
    {
        var match = Regex.Match(url);
        if (!match.Success) return null;

        string username;

        // If we have a username (@handle), use it directly
        if (match.Groups["name"].Success)
        {
            username = match.Groups["name"].Value.ToLowerInvariant();
        }
        // If we have a channel ID, convert it to @username
        else if (match.Groups["id"].Success)
        {
            var channelId = match.Groups["id"].Value;
            username = await ResolveUsernameFromChannelId(channelId);
            if (string.IsNullOrEmpty(username))
                return null;
        }
        else
        {
            return null;
        }

        return await GetStreamDataAsync(username);
    }

    /// <inheritdoc />
    public override async Task<StreamData?> GetStreamDataAsync(string username)
    {
        username = username.StartsWith("@") ? username[1..].ToLowerInvariant() : username.ToLowerInvariant();

        try
        {
            return await ScrapeUsernamePage(username);
        }
        catch
        {
#if DEBUG
            Logger.Information("Failed to get data for username: {Username}", username);
#endif
            FailingStreams[username] = DateTime.UtcNow;
            return null;
        }
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyCollection<StreamData>> GetStreamDataAsync(List<string> identifiers)
    {
        var streamDataList = new List<StreamData>();

        foreach (var identifier in identifiers)
        {
            try
            {
                var streamData = await GetStreamDataAsync(identifier);
                if (streamData != null)
                    streamDataList.Add(streamData);
            }
            catch
            {
                // Log error and continue with next identifier
                FailingStreams[identifier] = DateTime.UtcNow;
            }
        }

        return streamDataList;
    }

    /// <summary>
    ///     Scrapes a YouTube @username page for live stream data.
    /// </summary>
    /// <param name="username">The username without @ prefix.</param>
    /// <returns>Stream data with @username as UniqueName, or null if not found.</returns>
    private async Task<StreamData?> ScrapeUsernamePage(string username)
    {
        var url = $"https://www.youtube.com/@{username}";
        var html = await httpClient.GetStringAsync(url);
        htmlDocument.LoadHtml(html);

        var detectionReasons = new List<string>();

        if (html.Contains("\"isLiveContent\":true")) detectionReasons.Add("isLiveContent:true");
        if (html.Contains("\"style\":\"LIVE\"")) detectionReasons.Add("style:LIVE");
        if (html.Contains("\"badges\":[{\"liveBadgeRenderer\"")) detectionReasons.Add("liveBadgeRenderer");
        if (html.Contains("\"text\":\"LIVE\"")) detectionReasons.Add("text:LIVE");
        if (html.Contains("\"simpleText\":\"LIVE\"")) detectionReasons.Add("simpleText:LIVE");
        if (html.Contains("live-badge")) detectionReasons.Add("live-badge");
        if (html.Contains("overlay-style=\"LIVE\"")) detectionReasons.Add("overlay-style:LIVE");
        if (html.Contains("badge-shape-wiz__text\">LIVE")) detectionReasons.Add("badge-shape-wiz__text:LIVE");
        if (html.Contains("is-live-video=\"\"")) detectionReasons.Add("is-live-video empty");
        if (html.Contains("is-live-video=\"")) detectionReasons.Add("is-live-video");
        if (html.Contains("badge-shape-wiz--thumbnail-live")) detectionReasons.Add("thumbnail-live");
        if (html.Contains("ytd-thumbnail-overlay-time-status-renderer") && html.Contains("LIVE"))
            detectionReasons.Add("time-status-renderer+LIVE");
        if (html.Contains("\"videoDetails\":{\"videoId\"") && html.Contains("\"isLiveContent\":true"))
            detectionReasons.Add("videoDetails+isLiveContent");

        var liveMatch = Regex.Match(html,
            @"""videoId"":""([^""]+)""[^}]*""isLiveContent"":true",
            RegexOptions.IgnoreCase);
        if (liveMatch.Success)
        {
            detectionReasons.Add($"regex videoId+isLiveContent: {liveMatch.Groups[1].Value}");
        }

        var isLive = detectionReasons.Count > 0;

#if DEBUG
        if (isLive)
        {
            Logger.Information("@{Username} detected as LIVE due to: {Reasons}", username,
                string.Join(", ", detectionReasons));
        }
        else
        {
            Logger.Information("@{Username} detected as OFFLINE - no live indicators found", username);
        }
#endif

        var channelName = ExtractChannelName(html);
        var avatarUrl = ExtractChannelAvatar(html);

        if (isLive)
        {
            var liveVideoId = ExtractLiveVideoId(html);
            var streamUrl = !string.IsNullOrEmpty(liveVideoId)
                ? $"https://www.youtube.com/watch?v={liveVideoId}"
                : $"https://www.youtube.com/@{username}/live";

            if (!string.IsNullOrEmpty(liveVideoId))
            {
                return await GetLiveStreamData(username, liveVideoId, streamUrl);
            }

            return new StreamData
            {
                UniqueName = $"@{username}",
                Name = channelName ?? "Unknown Channel",
                AvatarUrl = avatarUrl ?? "",
                IsLive = true,
                StreamUrl = streamUrl,
                StreamType = FType.Youtube,
                Preview = avatarUrl ?? "",
                Title = "Live Stream"
            };
        }

        return new StreamData
        {
            UniqueName = $"@{username}",
            Name = channelName ?? "Unknown Channel",
            AvatarUrl = avatarUrl ?? "",
            IsLive = false,
            StreamUrl = $"https://www.youtube.com/@{username}",
            StreamType = FType.Youtube,
            Preview = avatarUrl ?? ""
        };
    }

    /// <summary>
    ///     Gets detailed live stream data by scraping the video page.
    /// </summary>
    /// <param name="username">The username without @ prefix.</param>
    /// <param name="videoId">The YouTube video ID.</param>
    /// <param name="streamUrl">The stream URL.</param>
    /// <returns>Detailed stream data with viewer count, title, etc.</returns>
    private async Task<StreamData?> GetLiveStreamData(string username, string videoId, string streamUrl)
    {
        try
        {
            var html = await httpClient.GetStringAsync($"https://www.youtube.com/watch?v={videoId}");
            htmlDocument.LoadHtml(html);

            // Extract data from page
            var title = ExtractPageTitle(html);
            var channelName = ExtractChannelName(html);
            var thumbnailUrl = $"https://i.ytimg.com/vi/{videoId}/maxresdefault.jpg";
            var viewerCount = ExtractViewerCount(html);

            return new StreamData
            {
                UniqueName = $"@{username}",
                Name = channelName ?? "Unknown Channel",
                AvatarUrl = ExtractChannelAvatar(html) ?? "",
                IsLive = true,
                StreamUrl = streamUrl,
                StreamType = FType.Youtube,
                Preview = thumbnailUrl,
                Title = title ?? "Live Stream",
                Viewers = viewerCount,
                Game = ExtractCategory(html) ?? ""
            };
        }
        catch
        {
            // Basic live data if scraping fails
            return new StreamData
            {
                UniqueName = $"@{username}",
                Name = "Unknown Channel",
                IsLive = true,
                StreamUrl = streamUrl,
                StreamType = FType.Youtube,
                Preview = $"https://i.ytimg.com/vi/{videoId}/maxresdefault.jpg"
            };
        }
    }

    /// <summary>
    ///     Resolves channel ID to @username
    /// </summary>
    private async Task<string?> ResolveUsernameFromChannelId(string channelId)
    {
        try
        {
            var html = await httpClient.GetStringAsync($"https://www.youtube.com/channel/{channelId}");
            htmlDocument.LoadHtml(html);

            // Look for @username in various places
            var patterns = new[]
            {
                @"""@([^""]+)""", @"youtube\.com/@([^""/?&]+)", @"/@([^""/?&]+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(html, pattern);
                if (match.Success)
                {
                    return match.Groups[1].Value.ToLowerInvariant();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }


    [GeneratedRegex(@"(?:https?://)?(?:www\.)?youtube\.com/(?:@(?<name>[^/?]+)|channel/(?<id>[^/?]+))/?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();

    #region HTML Parsing Helpers

    private string? ExtractPageTitle(string html)
    {
        htmlDocument.LoadHtml(html);

        // Try multiple selectors for title
        var titleNode = htmlDocument.DocumentNode.SelectSingleNode("//title") ??
                        htmlDocument.DocumentNode.SelectSingleNode("//meta[@property='og:title']");

        if (titleNode != null)
        {
            var title = titleNode.Name == "title"
                ? titleNode.InnerText
                : titleNode.GetAttributeValue("content", "");

            // Clean up YouTube title format
            return title.Replace(" - YouTube", "").Trim();
        }

        // Fallback to regex for JSON data
        var titleMatch = Regex.Match(html, @"""title"":""([^""]+)""");
        return titleMatch.Success ? DecodeJsonString(titleMatch.Groups[1].Value) : null;
    }

    private string? ExtractChannelName(string html)
    {
        htmlDocument.LoadHtml(html);

        // Try multiple selectors for channel name
        var channelNode = htmlDocument.DocumentNode.SelectSingleNode("//ytd-channel-name//a") ??
                          htmlDocument.DocumentNode.SelectSingleNode(
                              "//yt-formatted-string[contains(@class, 'ytd-channel-name')]//a") ??
                          htmlDocument.DocumentNode.SelectSingleNode("//meta[@property='og:title']");

        if (channelNode != null)
        {
            var name = channelNode.Name == "meta"
                ? channelNode.GetAttributeValue("content", "")
                : channelNode.InnerText;
            return name.Replace(" - YouTube", "").Trim();
        }

        // Fallback to regex
        var nameMatch = Regex.Match(html, @"""ownerChannelName"":""([^""]+)""");
        if (nameMatch.Success) return DecodeJsonString(nameMatch.Groups[1].Value);

        nameMatch = Regex.Match(html, @"""channelName"":""([^""]+)""");
        return nameMatch.Success ? DecodeJsonString(nameMatch.Groups[1].Value) : null;
    }

    private string? ExtractChannelAvatar(string html)
    {
        htmlDocument.LoadHtml(html);

        // Try to find avatar/profile image
        var avatarNode = htmlDocument.DocumentNode.SelectSingleNode("//img[contains(@class, 'yt-core-image')]") ??
                         htmlDocument.DocumentNode.SelectSingleNode("//yt-img-shadow//img") ??
                         htmlDocument.DocumentNode.SelectSingleNode("//meta[@property='og:image']");

        if (avatarNode != null)
        {
            var avatarUrl = avatarNode.Name == "meta"
                ? avatarNode.GetAttributeValue("content", "")
                : avatarNode.GetAttributeValue("src", "");

            if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.StartsWith("http"))
                return avatarUrl;
        }

        // Fallback to regex
        var avatarMatch = Regex.Match(html, @"""avatar"":\{""thumbnails"":\[\{""url"":""([^""]+)""");
        return avatarMatch.Success ? avatarMatch.Groups[1].Value : null;
    }

    private string? ExtractLiveVideoId(string html)
    {
        htmlDocument.LoadHtml(html);

        // Method 1: Look for live video links with HtmlAgilityPack
        var liveNodes = htmlDocument.DocumentNode.SelectNodes("//a[contains(@href, '/watch?v=')]");

        if (liveNodes != null)
        {
            foreach (var node in liveNodes)
            {
                var parent = node.ParentNode;

                // Check if this video has live indicators
                while (parent.Name != "html")
                {
                    var parentHtml = parent.OuterHtml;
                    if (parentHtml.Contains("overlay-style=\"LIVE\"") ||
                        parentHtml.Contains("is-live-video=\"\"") ||
                        parentHtml.Contains("is-live-video=\"") ||
                        parentHtml.Contains("ytd-thumbnail-overlay-now-playing-renderer") ||
                        parentHtml.Contains("badge-shape-wiz__text\">LIVE") ||
                        parentHtml.Contains("badge-shape-wiz--thumbnail-live") ||
                        parentHtml.Contains("Now playing"))
                    {
                        var href = node.GetAttributeValue("href", "");
                        var videoId = ExtractVideoIdFromUrl("https://youtube.com" + href);
                        if (!string.IsNullOrEmpty(videoId))
                            return videoId;
                    }

                    parent = parent.ParentNode;
                }
            }
        }

        // Method 2: Direct XPath search for live video elements
        var liveVideoNodes =
            htmlDocument.DocumentNode.SelectNodes(
                "//ytd-thumbnail[@is-live-video]//a[contains(@href, '/watch?v=')]");
        if (liveVideoNodes != null && liveVideoNodes.Any())
        {
            var href = liveVideoNodes.First().GetAttributeValue("href", "");
            var videoId = ExtractVideoIdFromUrl("https://youtube.com" + href);
            if (!string.IsNullOrEmpty(videoId))
                return videoId;
        }

        // Method 3: Look for overlay with LIVE badge
        var overlayLiveNodes = htmlDocument.DocumentNode.SelectNodes(
            "//ytd-thumbnail-overlay-time-status-renderer[@overlay-style='LIVE']//ancestor::ytd-thumbnail//a[contains(@href, '/watch?v=')]");
        if (overlayLiveNodes != null && overlayLiveNodes.Any())
        {
            var href = overlayLiveNodes.First().GetAttributeValue("href", "");
            var videoId = ExtractVideoIdFromUrl("https://youtube.com" + href);
            if (!string.IsNullOrEmpty(videoId))
                return videoId;
        }

        // Fallback to regex patterns
        var patterns = new[]
        {
            @"""videoId"":""([a-zA-Z0-9_-]{11})"".*?""isLiveContent"":true",
            @"href=""/watch\?v=([a-zA-Z0-9_-]{11})"".*?overlay-style=""LIVE""",
            @"href=""/watch\?v=([a-zA-Z0-9_-]{11})"".*?is-live-video=""""",
            @"/watch\?v=([a-zA-Z0-9_-]{11}).*?ytd-thumbnail-overlay-now-playing-renderer",
            @"/watch\?v=([a-zA-Z0-9_-]{11}).*?badge-shape-wiz--thumbnail-live",
            @"/watch\?v=([a-zA-Z0-9_-]{11}).*?Now playing"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.Singleline);
            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }

    private static int ExtractViewerCount(string html)
    {
        var viewerMatch = Regex.Match(html, @"""viewCount"":""(\d+)""");
        if (viewerMatch.Success && int.TryParse(viewerMatch.Groups[1].Value, out var count))
            return count;
        return 0;
    }

    private static string? ExtractCategory(string html)
    {
        var categoryMatch = Regex.Match(html, @"""category"":""([^""]+)""");
        if (categoryMatch.Success)
        {
            var category = categoryMatch.Groups[1].Value;
            return DecodeJsonString(category);
        }

        return null;
    }

    /// <summary>
    ///     Decodes JSON-encoded strings.
    /// </summary>
    private static string DecodeJsonString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        try
        {
            // Use JsonSerializer to properly decode the JSON string
            return JsonSerializer.Deserialize<string>($"\"{input}\"") ?? input;
        }
        catch
        {
            // Fallback to original string if deserialization fails
            return input;
        }
    }

    private static string? ExtractVideoIdFromUrl(string url)
    {
        var match = Regex.Match(url, @"[?&]v=([a-zA-Z0-9_-]{11})");
        return match.Success ? match.Groups[1].Value : null;
    }

    #endregion
}