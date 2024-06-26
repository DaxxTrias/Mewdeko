using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using CodeHollow.FeedReader.Parser;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Searches.Services;

public class FeedsService : INService
{
    private readonly DiscordSocketClient client;
    private readonly DbService db;
    private bool isRunning;

    private readonly ConcurrentDictionary<string, DateTime> lastPosts = new();

    private readonly ConcurrentDictionary<string, HashSet<FeedSub>> subs;

    public FeedsService(Mewdeko bot, DbService db, DiscordSocketClient client)
    {
        this.db = db;
        this.isRunning = true;

        using (var uow = db.GetDbContext())
        {
            var guildConfigIds = uow.GuildConfigs.Where(x => bot.Client.Guilds.Select(socketGuild => socketGuild.Id).Contains(x.GuildId)).Select(x => x.Id);
            subs = uow.GuildConfigs
                .AsQueryable()
                .Where(x => guildConfigIds.Contains(x.Id))
                .Include(x => x.FeedSubs)
                .ToList()
                .SelectMany(x => x.FeedSubs)
                .GroupBy(x => x.Url.ToLower())
                .ToDictionary(x => x.Key, x => x.ToHashSet())
                .ToConcurrent();
        }

        this.client = client;

        _ = Task.Run(TrackFeeds);
    }

    public async Task<EmbedBuilder> TrackFeeds()
    {
        while (isRunning)
        {
            var allSendTasks = new List<Task>(subs.Count);
            foreach (var (rssUrl, value) in subs)
            {
                if (value.Count == 0)
                    continue;

                try
                {
                    var feed = await FeedReader.ReadAsync(rssUrl).ConfigureAwait(false);

                    string feedTitle = null;

                    // Check for RSS 2.0 type
                    if (feed.Type == FeedType.Rss_2_0)
                    {
                        var rss20feed = (Rss20Feed)feed.SpecificFeed;
                        feedTitle = rss20feed.Title;

                        /*
                        feedImage = rss20feed.Image;
                        Log.Information("pubDate: " + rss20feed.PublishingDate);
                        Log.Information("Description: " + rss20feed.Description);
                        Log.Information("Title: " + rss20feed.Title);
                        Log.Information("Link: " + rss20feed.Link);
                        */
                    }

                    var items = feed
                        .Items
                        .Select(item => (Item: item, LastUpdate: item.PublishingDate?.ToUniversalTime()
                                                                 ?? (item.SpecificItem as AtomFeedItem)?.UpdatedDate
                                                                 ?.ToUniversalTime()))
                        .Where(data => data.LastUpdate is not null)
                        .Select(data => (data.Item, LastUpdate: (DateTime)data.LastUpdate))
                        .OrderByDescending(data => data.LastUpdate)
                        .Reverse() // start from the oldest
                        .ToList();

                    if (!lastPosts.TryGetValue(rssUrl, out var lastFeedUpdate))
                        lastFeedUpdate = lastPosts[rssUrl] =
                            items.Any() ? items[^1].LastUpdate : DateTime.UtcNow;

                    foreach (var (feedItem, itemUpdateDate) in items)
                    {
                        var repbuilder = new ReplacementBuilder()
                            .WithOverride("%title%", () => feedItem.Title ?? "Unkown")
                            .WithOverride("%author%", () => feedItem.Author ?? "Unknown")
                            .WithOverride("%content%", () => feedItem.Description?.StripHtml())
                            .WithOverride("%image_url%", () =>
                            {
                                if (feedItem.SpecificItem is AtomFeedItem atomFeedItem)
                                {
                                    var previewElement = atomFeedItem.Element.Elements()
                                        .FirstOrDefault(x => x.Name.LocalName == "preview") ?? atomFeedItem.Element.Elements()
                                        .FirstOrDefault(x => x.Name.LocalName == "thumbnail");

                                    var urlAttribute = previewElement?.Attribute("url");
                                    if (urlAttribute != null && !string.IsNullOrWhiteSpace(urlAttribute.Value)
                                                             && Uri.IsWellFormedUriString(urlAttribute.Value,
                                                                 UriKind.Absolute))
                                    {
                                        return urlAttribute.Value;
                                    }
                                }

                                if (feedItem.SpecificItem is not MediaRssFeedItem mediaRssFeedItem
                                    || !(mediaRssFeedItem.Enclosure?.MediaType?.StartsWith("image/") ?? false))
                                        return feed.ImageUrl;
                                var imgUrl = mediaRssFeedItem.Enclosure.Url;
                                if (!string.IsNullOrWhiteSpace(imgUrl) &&
                                    Uri.IsWellFormedUriString(imgUrl, UriKind.Absolute))
                                {
                                    return imgUrl;
                                }

                                return feed.ImageUrl;
                            })
                            .WithOverride("%categories%", () => string.Join(", ", feedItem.Categories))
                            .WithOverride("%timestamp%", () => TimestampTag.FromDateTime(feedItem.PublishingDate.Value, TimestampTagStyles.LongDateTime).ToString())
                            .WithOverride("%url%", () => feedItem.Link ?? feedItem.SpecificItem.Link)
                            .WithOverride("%feedurl%", () => rssUrl)
                            .Build();

                        if (itemUpdateDate <= lastFeedUpdate) continue;
                        var embed = new EmbedBuilder()
                            .WithFooter(rssUrl);

                        lastPosts[rssUrl] = itemUpdateDate;

                        var link = feedItem.SpecificItem.Link;
                        if (!string.IsNullOrWhiteSpace(link) && Uri.IsWellFormedUriString(link, UriKind.Absolute))
                            embed.WithUrl(link);

                        var title = string.IsNullOrWhiteSpace(feedTitle)
                            ? (string.IsNullOrWhiteSpace(feedItem.Title) ? "-" : feedItem.Title)
                            : feedTitle;

                        var gotImage = false;

                        // Check for RSS 1.0 compliant media
                        if (feedItem.SpecificItem is MediaRssFeedItem mrfi &&
                            (mrfi.Enclosure?.MediaType?.StartsWith("image/") ?? false))
                        {
                            var imgUrl = mrfi.Enclosure.Url;
                            if (!string.IsNullOrWhiteSpace(imgUrl) &&
                                Uri.IsWellFormedUriString(imgUrl, UriKind.Absolute))
                            {
                                embed.WithImageUrl(imgUrl);
                                gotImage = true;
                            }
                        }

                        //todo: Disabling this until I have desire to implement a proper img resizer or scraper
                        // Some issues with regex filtering inside the descriptor field from nitter
                        // Can either host a custom nitter, setup a proper regex scraper,
                        // or get a lib dedicated to parsing this specific stuff
                        /*
                        // Check for RSS 2.0 compliant media
                        if (!gotImage && feed.Type == FeedType.Rss_2_0)
                        {
                            var rss20feed = (Rss20Feed)feed.SpecificFeed;
                            var imageUrl = rss20feed?.Image?.Url;
                            if (!string.IsNullOrWhiteSpace(imageUrl) &&
                                Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
                            {
                                embed.WithImageUrl(imageUrl);
                                gotImage = true;
                            }
                        }
                        */

                        // Check for ATOM format images
                        if (!gotImage && feedItem.SpecificItem is AtomFeedItem afi)
                        {
                            var previewElement = afi.Element.Elements()
                                .FirstOrDefault(x => x.Name.LocalName == "preview") ?? afi.Element.Elements()
                                .FirstOrDefault(x => x.Name.LocalName == "thumbnail");

                            var urlAttribute = previewElement?.Attribute("url");
                            if (urlAttribute != null && !string.IsNullOrWhiteSpace(urlAttribute.Value)
                                                     && Uri.IsWellFormedUriString(urlAttribute.Value,
                                                         UriKind.Absolute))
                            {
                                embed.WithImageUrl(urlAttribute.Value);
                            }
                        }


                        embed.WithTitle(title.TrimTo(256));

                        var desc = feedItem.Description?.StripHtml();
                        if (!string.IsNullOrWhiteSpace(feedItem.Description))
                            embed.WithDescription(desc.TrimTo(2048));

                        //send the created embed to all subscribed channels
                        var feedSendTasks = value.Where(x => x.GuildConfig != null);
                        foreach (var feed1 in feedSendTasks)
                        {
                            var channel = client.GetGuild(feed1.GuildConfig.GuildId).GetTextChannel(feed1.ChannelId);
                            if (channel is null)
                                continue;
                            var (builder, content, componentBuilder) = await GetFeedEmbed(repbuilder.Replace(feed1.Message), channel.Guild.Id);

                            /*
                            if (feed1.Message is "-" or null)
                            {
                                allSendTasks.Add(channel.EmbedAsync(embed));
                            }
                            else
                            {
                                allSendTasks.Add(channel.EmbedAsync(embed));
                                allSendTasks.Add(channel.SendMessageAsync(content ?? "", embeds: builder ?? null, components: componentBuilder?.Build()));
                            }
                            */

                            if (feed1.Message is "-" or null)
                                allSendTasks.Add(channel.EmbedAsync(embed));
                            else
                                allSendTasks.Add(channel.SendMessageAsync(content ?? "", embeds: builder ?? null, components: componentBuilder?.Build()));
                            
                        }
                    }
                }
                catch (UrlNotFoundException ex)
                {
                    Log.Warning($"URL not found for RSS URL: {rssUrl}. Exception: {ex.Message}");
                }
                catch (InvalidFeedLinkException ex)
                {
                    Log.Warning($"Invalid feed link for RSS URL: {rssUrl}. Exception: {ex.Message}");
                }
                catch (FeedTypeNotSupportedException ex)
                {
                    Log.Warning($"Feed type not supported for RSS URL: {rssUrl}. The feed might be of a different type not supported by the library. Exception: {ex.Message}");
                }
                catch (Exception ex) // General Exception for other errors
                {
                    Log.Warning($"Error occurred in feed reader for RSS URL: {rssUrl}. Exception: {ex.Message}. StackTrace: {ex.StackTrace}");

                    if (ex.InnerException != null)
                    {
                        Log.Warning($"Inner Exception: {ex.InnerException.Message}. StackTrace: {ex.InnerException.StackTrace}");
                    }
                }
            }
            await Task.WhenAll(Task.WhenAll(allSendTasks), Task.Delay(180000)).ConfigureAwait(false);
        }
        await Task.Delay(5000); // Sleep for a bit when not running (5 seconds)
        return null;
    }

    public async Task TestRss(FeedSub sub, ITextChannel channel, bool sendBoth = false)
    {
        var feed = await FeedReader.ReadAsync(sub.Url);

        string feedTitle = null;

        // Check for RSS 2.0 type
        if (feed.Type == FeedType.Rss_2_0)
        {
            var rss20feed = (Rss20Feed)feed.SpecificFeed;
            feedTitle = rss20feed.Title;

            /*
            feedImage = rss20feed.Image;
            Log.Information("pubDate: " + rss20feed.PublishingDate);
            Log.Information("Description: " + rss20feed.Description);
            Log.Information("Title: " + rss20feed.Title);
            Log.Information("Link: " + rss20feed.Link);
            */
        }

        var (feedItem, _) = feed.Items
            .Select(item => (Item: item,
                LastUpdate: item.PublishingDate?.ToUniversalTime() ?? (item.SpecificItem as AtomFeedItem)?.UpdatedDate?.ToUniversalTime()))
            .Where(data => data.LastUpdate is not null).Select(data => (data.Item, LastUpdate: (DateTime)data.LastUpdate)).LastOrDefault();

        var repbuilder = new ReplacementBuilder()
            .WithOverride("%title%", () => feedItem.Title ?? "Unkown")
            .WithOverride("%author%", () => feedItem.Author ?? "Unknown")
            .WithOverride("%content%", () => feedItem.Description?.StripHtml())
            .WithOverride("%image_url%", () =>
            {
                if (feedItem.SpecificItem is AtomFeedItem atomFeedItem)
                {
                    var previewElement = atomFeedItem.Element.Elements().FirstOrDefault(x => x.Name.LocalName == "preview") ??
                                         atomFeedItem.Element.Elements().FirstOrDefault(x => x.Name.LocalName == "thumbnail");
                    var urlAttribute = previewElement?.Attribute("url");
                    if (urlAttribute != null
                        && !string.IsNullOrWhiteSpace(urlAttribute.Value)
                        && Uri.IsWellFormedUriString(urlAttribute.Value, UriKind.Absolute))
                        return urlAttribute.Value;
                }

                if (feedItem.SpecificItem is not MediaRssFeedItem mediaRssFeedItem || !(mediaRssFeedItem.Enclosure?.MediaType?.StartsWith("image/") ?? false))
                    return feed.ImageUrl;

                var imgUrl = mediaRssFeedItem.Enclosure.Url;
                if (!string.IsNullOrWhiteSpace(imgUrl) && Uri.IsWellFormedUriString(imgUrl, UriKind.Absolute))
                    return imgUrl;

                return feed.ImageUrl;
            })
            .WithOverride("%categories%", () => string.Join(", ", feedItem.Categories))
            .WithOverride("%timestamp%",
                () => TimestampTag.FromDateTime(feedItem.PublishingDate.Value, TimestampTagStyles.LongDateTime).ToString())
            .WithOverride("%url%", () => feedItem.Link ?? feedItem.SpecificItem.Link)
            .WithOverride("%feedurl%", () => sub.Url).Build();
        var embed = new EmbedBuilder().WithFooter(sub.Url);
        var link = feedItem.SpecificItem.Link;
        if (!string.IsNullOrWhiteSpace(link) && Uri.IsWellFormedUriString(link, UriKind.Absolute))
            embed.WithUrl(link);

        /*
        var title = string.IsNullOrWhiteSpace(feedItem.Title) ? "-" : feedItem.Title;
        */

        var title = string.IsNullOrWhiteSpace(feedTitle)
                            ? (string.IsNullOrWhiteSpace(feedItem.Title) ? "-" : feedItem.Title)
                            : feedTitle;

        var gotImage = false;

        // Check for RSS 1.0 compliant media
        if (feedItem.SpecificItem is MediaRssFeedItem mrfi &&
            (mrfi.Enclosure?.MediaType?.StartsWith("image/") ?? false))
        {
            var imgUrl = mrfi.Enclosure.Url;
            if (!string.IsNullOrWhiteSpace(imgUrl) &&
                Uri.IsWellFormedUriString(imgUrl, UriKind.Absolute))
            {
                embed.WithImageUrl(imgUrl);
                gotImage = true;
            }
        }

        /*
        // Check for RSS 2.0 compliant media
        if (!gotImage && feed.Type == FeedType.Rss_2_0)
        {
            var rss20feed = (Rss20Feed)feed.SpecificFeed;
            var imageUrl = rss20feed?.Image?.Url;
            if (!string.IsNullOrWhiteSpace(imageUrl) &&
                Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
            {
                embed.WithImageUrl(imageUrl);
                gotImage = true;
            }
        }
        */

        // Check for ATOM format images
        if (!gotImage && feedItem.SpecificItem is AtomFeedItem afi)
        {
            var previewElement = afi.Element.Elements()
                .FirstOrDefault(x => x.Name.LocalName == "preview") ?? afi.Element.Elements()
                .FirstOrDefault(x => x.Name.LocalName == "thumbnail");

            var urlAttribute = previewElement?.Attribute("url");
            if (urlAttribute != null && !string.IsNullOrWhiteSpace(urlAttribute.Value)
                                     && Uri.IsWellFormedUriString(urlAttribute.Value,
                                         UriKind.Absolute))
            {
                embed.WithImageUrl(urlAttribute.Value);
            }
        }

        embed.WithTitle(title.TrimTo(256));
        var desc = feedItem.Description?.StripHtml();

        if (!string.IsNullOrWhiteSpace(feedItem.Description))
            embed.WithDescription(desc.TrimTo(2048));

        var (builder, content, componentBuilder) = await GetFeedEmbed(repbuilder.Replace(sub.Message), channel.GuildId);

        if (sendBoth || sub.Message is "-" or null)
        {
            await channel.EmbedAsync(embed);
        }

        if (sendBoth || !(sub.Message is "-" or null))
        {
            await channel.SendMessageAsync(content ?? "", embeds: builder ?? null, components: componentBuilder?.Build());
        }

        /*
        if (sub.Message is "-" or null) await channel.EmbedAsync(embed);
        else await channel.SendMessageAsync(content ?? "", embeds: builder ?? null, components: componentBuilder?.Build());
        */
    }

    private Task<(Embed[] builder, string content, ComponentBuilder componentBuilder)> GetFeedEmbed(string message, ulong guildId)
        => SmartEmbed.TryParse(message, guildId, out var embed, out var content, out var components)
            ? Task.FromResult((embed, content, components))
            : Task.FromResult<(Embed[], string, ComponentBuilder)>((Array.Empty<Embed>(), message, null));

    public List<FeedSub?> GetFeeds(ulong guildId)
    {
        using var uow = db.GetDbContext();
        return uow.ForGuildId(guildId,
                set => set.Include(x => x.FeedSubs)).GetAwaiter().GetResult()
            .FeedSubs
            .OrderBy(x => x.Id)
            .ToList();
    }

    public async Task<bool> AddFeed(ulong guildId, ulong channelId, string rssFeed)
    {
        rssFeed.ThrowIfNull(nameof(rssFeed));

        var fs = new FeedSub
        {
            ChannelId = channelId, Url = rssFeed.Trim()
        };

        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId,
            set => set.Include(x => x.FeedSubs));

        if (gc.FeedSubs.Any(x => x.Url.ToLower() == fs.Url.ToLower()))
            return false;
        if (gc.FeedSubs.Count >= 20) return false;

        gc.FeedSubs.Add(fs);
        await uow.SaveChangesAsync();
        //adding all, in case bot wasn't on this guild when it started
        foreach (var feed in gc.FeedSubs)
            subs.AddOrUpdate(feed.Url.ToLower(), new HashSet<FeedSub>
            {
                feed
            }, (_, old) =>
            {
                old.Add(feed);
                return old;
            });

        return true;
    }

    public async Task<bool> AddFeedMessage(ulong guildId, int index, string message)
    {
        if (index < 0)
            return false;
        await using var uow = db.GetDbContext();
        var items = uow.ForGuildId(guildId, set => set.Include(x => x.FeedSubs)).GetAwaiter().GetResult()
            .FeedSubs
            .OrderBy(x => x.Id)
            .ToList();
        var toupdate = items[index];
        subs.AddOrUpdate(toupdate.Url.ToLower(), new HashSet<FeedSub>(), (_, old) =>
        {
            old.Remove(toupdate);
            return old;
        });
        toupdate.Message = message;
        uow.Update(toupdate);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        subs.AddOrUpdate(toupdate.Url.ToLower(), new HashSet<FeedSub>(), (_, old) =>
        {
            old.Add(toupdate);
            return old;
        });
        return true;
    }

    public bool RemoveFeed(ulong guildId, int index)
    {
        if (index < 0)
            return false;

        using var uow = db.GetDbContext();
        var items = uow.ForGuildId(guildId, set => set.Include(x => x.FeedSubs)).GetAwaiter().GetResult()
            .FeedSubs
            .OrderBy(x => x.Id)
            .ToList();

        if (items.Count <= index)
            return false;
        var toRemove = items[index];
        subs.AddOrUpdate(toRemove.Url.ToLower(), new HashSet<FeedSub>(), (_, old) =>
        {
            old.Remove(toRemove);
            return old;
        });
        uow.Remove(toRemove);
        uow.SaveChanges();

        return true;
    }

    public bool StartTracking(ulong guildId)
    {
        try
        {
            isRunning = true;
            _ = Task.Run(TrackFeeds); // Should we keep a reference to this task?
            return true;
        }
        catch
        {
            throw;
        }
        return false;
    }
    public bool StopTracking(ulong guildId)
    {
        try
        {
            isRunning = false;
            return true;
        }
        catch
        {
            throw;
        }
        return false;
    }
}