using CodeHollow.FeedReader;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Services;
using Serilog;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    [Group]
    public class FeedCommands(InteractiveService serv) : MewdekoSubmodule<FeedsService>
    {
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedAdd(string url, [Remainder] ITextChannel? channel = null)
        {
            // Replace 'twitter.com' with 'nitter.cz'
            if (url.Contains("twitter.com"))
            {
                if (url.StartsWith("https://"))
                {
                    var baseString = url;
                    baseString = url + "/rss";
                    url = baseString;
                    url = url.Replace("twitter.com", "nitter.cz");
                }
                else
                {
                    var baseString = url;
                    baseString = "https://" + url + "/rss";
                    url = baseString;
                    url = url.Replace("twitter.com", "nitter.cz");
                }
            }

            else if (url.Contains("t.me") || url.Contains("telegram.me"))
            {
                // Extract the channel username from the URL
                var channelName = url.Replace("https://t.me/", "")
                                     .Replace("http://t.me/", "")
                                     .Replace("https://telegram.me/", "")
                                     .Replace("http://telegram.me/", "")
                                     .TrimEnd('/')
                                     .Split('?').First(); // Remove any query parameters

                // Construct the RSS feed URL using your RSSHub instance
                url = $"http://localhost:1200/telegram/channel/{channelName}";
            }

            var success = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                          (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            if (success)
            {
                channel ??= (ITextChannel)ctx.Channel;
                try
                {
                    await FeedReader.ReadAsync(url).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Information(ex, "Unable to get feeds from that url: " + url);
                    success = false;
                }
            }

            if (success)
            {
                success = await Service.AddFeed(ctx.Guild.Id, channel.Id, url);
                if (success)
                {
                    await ReplyConfirmLocalizedAsync("feed_added").ConfigureAwait(false);
                    return;
                }
            }

            Log.Information("error in url: " + url);
            await ReplyErrorLocalizedAsync("feed_not_valid").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedRemove(int index)
        {
            if (Service.RemoveFeed(ctx.Guild.Id, --index))
                await ReplyConfirmLocalizedAsync("feed_removed").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("feed_out_of_range").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedMessage(int index, [Remainder] string message)
        {
            if (await Service.AddFeedMessage(ctx.Guild.Id, --index, message).ConfigureAwait(false))
                await ReplyConfirmLocalizedAsync("feed_msg_updated").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("feed_out_of_range").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task RssTest(int index, bool sendBoth = false)
        {
            var feeds = Service.GetFeeds(ctx.Guild.Id);
            if (feeds.ElementAt(index - 1) is null)
            {
                await ReplyErrorLocalizedAsync("feed_out_of_range").ConfigureAwait(false);
                return;
            }

            await Service.TestRss(feeds.ElementAt(index - 1), ctx.Channel as ITextChannel, sendBoth).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedList()
        {
            var feeds = Service.GetFeeds(ctx.Guild.Id);

            if (feeds.Count == 0)
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(GetText("feed_no_feed")))
                    .ConfigureAwait(false);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(feeds.Count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var embed = new PageBuilder()
                    .WithOkColor();
                var i = 0;
                var fs = string.Join("\n", feeds.Skip(page * 10)
                    .Take(10)
                    .Select(x => $"`{(page * 10) + ++i}.` <#{x.ChannelId}> {x.Url}"));

                return embed.WithDescription(fs);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedStart()
        {
            if (Service.StartTracking(ctx.Guild.Id))
                await ReplyConfirmLocalizedAsync("feed_started").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("feed_already_started").ConfigureAwait(false);
        }
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task FeedStop()
        {
            if (Service.StopTracking(ctx.Guild.Id))
                await ReplyConfirmLocalizedAsync("feed_stopped").ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("feed_already_stopped").ConfigureAwait(false);
        }
    }
}