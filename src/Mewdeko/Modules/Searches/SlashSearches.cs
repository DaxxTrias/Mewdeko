using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using MartineApiNet;
using MartineApiNet.Enums;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Modules.Searches.Services;
using Refit;

namespace Mewdeko.Modules.Searches;

/// <summary>
///     Provides slash command interactions for searching and retrieving content from various sources.
/// </summary>
/// <param name="martineApi">The martineApi parameter.</param>
/// <param name="interactiveService">The interactive service used for paginated responses.</param>
/// <param name="logger">The logger instance for structured logging.</param>
public class SlashSearches(
    MartineApi martineApi,
    InteractiveService interactiveService,
    ILogger<SlashSearches> logger)
    : MewdekoSlashModuleBase<SearchesService>
{
    /// <summary>
    ///     Searches for a movie and displays the IMDb-style result.
    /// </summary>
    /// <param name="query">The movie title to search for.</param>
    [SlashCommand("imdb", "Searches for a movie and displays an IMDb-style result.")]
    public Task Imdb(string query)
    {
        return SendMovieResultAsync(query);
    }

    /// <summary>
    ///     Searches for a movie and displays the IMDb-style result.
    /// </summary>
    /// <param name="query">The movie title to search for.</param>
    [SlashCommand("movie", "Searches for a movie and displays an IMDb-style result.")]
    public Task Movie(string query)
    {
        return SendMovieResultAsync(query);
    }

    private async Task SendMovieResultAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        await DeferAsync().ConfigureAwait(false);
        var movie = await Service.GetMovieDataAsync(query).ConfigureAwait(false);
        if (movie is null)
        {
            await ErrorAsync(Strings.ImdbFail(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await SendMoviePagesAsync(movie, ctx.User, ctx.Interaction).ConfigureAwait(false);
    }

    private async Task SendMoviePagesAsync(WikiMovie movie, IUser user, IDiscordInteraction interaction)
    {
        if (movie.ImageUrls.Count <= 1)
        {
            var msg = await interaction.FollowupAsync(embed: Service.BuildMovieEmbed(movie).Build())
                .ConfigureAwait(false);
            await Service.TrackCleanupReaction(msg, user.Id).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(user)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(movie.ImageUrls.Count - 1)
            .AddOption(new Emoji("◀️"), PaginatorAction.Backward)
            .AddOption(new Emoji("▶️"), PaginatorAction.Forward)
            .AddOption(new Emoji("🗑️"), PaginatorAction.Exit)
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, interaction, TimeSpan.FromMinutes(60),
                InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);
        return;

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return Service.BuildMoviePage(movie, page);
        }
    }

    /// <summary>
    ///     Handles the "randomimage" component interaction, fetching and displaying a new random image from the specified
    ///     category.
    /// </summary>
    /// <param name="tag">The category of image to fetch.</param>
    /// <param name="userId">The Discord user ID who initiated the interaction.</param>
    /// <remarks>
    ///     This interaction command fetches a new random image from the specified category via the Martine API.
    ///     It supports ephemerality, showing the response only to the initiating user.
    /// </remarks>
    [ComponentInteraction("randomimage:*.*", true)]
    public async Task RandomImageButton(SearchesService.ImageTag tag, string userId)
    {
        await DeferAsync().ConfigureAwait(false);
        ulong.TryParse(userId, out var id);

        try
        {
            var image = await Service.GetRandomImageAsync(tag).ConfigureAwait(false);
            var button = new ComponentBuilder().WithButton("Another!", $"randomimage:{tag}.{ctx.User.Id}");

            var em = new EmbedBuilder()
                .WithOkColor()
                .WithAuthor(Strings.SearchAuthorReddit(ctx.Guild.Id, image.Data.Author.Name))
                .WithDescription($"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})")
                .WithFooter(Strings.RedditUpvotes(ctx.Guild.Id, image.Data.Upvotes, image.Data.Subreddit.Name))
                .WithImageUrl(image.Data.ImageUrl);

            if (ctx.User.Id != id)
            {
                await ctx.Interaction.FollowupAsync(
                    embed: em.Build(),
                    components: button.Build(),
                    ephemeral: true
                ).ConfigureAwait(false);
                return;
            }

            await ctx.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Embed = em.Build();
                x.Components = button.Build();
            }).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            logger.LogError(
                "Image fetch failed in button handler. Error:\nCode: {StatusCode}\nContent: {Content}",
                ex.StatusCode,
                ex.HasContent ? ex.Content : "No Content"
            );

            var errorEmbed = new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.FetchFailed(ctx.Guild.Id));

            if (ctx.User.Id != id)
            {
                await ctx.Interaction.FollowupAsync(
                    embed: errorEmbed.Build(),
                    ephemeral: true
                ).ConfigureAwait(false);
                return;
            }

            await ctx.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Embed = errorEmbed.Build();
                x.Components = new ComponentBuilder().Build(); // Remove the button on error
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Handles the "meme" component interaction, fetching and showing a random meme.
    /// </summary>
    /// <param name="userid">The Discord user ID who initiated the meme fetch interaction.</param>
    /// <remarks>
    ///     This interaction command fetches a random meme from the configured sources via the Martine API
    ///     and presents it to the user who triggered the interaction.
    ///     The command supports ephemerality, showing the response only to the initiating user.
    /// </remarks>
    [ComponentInteraction("meme:*", true)]
    public async Task Meme(string userid)
    {
        await DeferAsync().ConfigureAwait(false);
        ulong.TryParse(userid, out var id);
        var image = await martineApi.RedditApi.GetRandomMeme(Toptype.year).ConfigureAwait(false);
        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = $"u/{image.Data.Author.Name}"
            },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder
            {
                Text =
                    $"{image.Data.Upvotes} Upvotes {image.Data.Downvotes} Downvotes | r/{image.Data.Subreddit.Name} | Powered by MartineApi"
            },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        if (ctx.User.Id != id)
        {
            await ctx.Interaction.FollowupAsync(embed: em.Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = em.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the "randomreddit" component interaction, fetching and displaying a random post from a specified subreddit.
    /// </summary>
    /// <param name="subreddit">The subreddit from which to fetch a random post.</param>
    /// <param name="userId">The Discord user ID who initiated the subreddit fetch interaction.</param>
    /// <remarks>
    ///     This interaction command fetches a random post from the specified subreddit via the Martine API.
    ///     It supports ephemerality, allowing the response to be visible only to the user who initiated the interaction.
    /// </remarks>
    [ComponentInteraction("randomreddit:*.*", true)]
    public async Task RandomReddit(string subreddit, string userId)
    {
        await DeferAsync().ConfigureAwait(false);
        ulong.TryParse(userId, out var id);

        var image = await martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.year).ConfigureAwait(false);

        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = $"u/{image.Data.Author.Name}"
            },
            Description = $"Title: {image.Data.Title}\n[Source]({image.Data.PostUrl})",
            Footer = new EmbedFooterBuilder
            {
                Text = Strings.RedditUpvotes(ctx.Guild.Id, image.Data.Upvotes, image.Data.Subreddit.Name)
            },
            ImageUrl = image.Data.ImageUrl,
            Color = Mewdeko.OkColor
        };
        if (ctx.User.Id != id)
        {
            await ctx.Interaction.FollowupAsync(embed: em.Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = em.Build()).ConfigureAwait(false);
    }
}

/// <summary>
///     Provides slash commands for MyAnimeList searches.
/// </summary>
/// <param name="interactiveService">The interactive service used for paginated responses.</param>
[Group("mal", "MyAnimeList searches.")]
public class SlashMyAnimeListSearches(InteractiveService interactiveService)
    : MewdekoSlashModuleBase<SearchesService>
{
    /// <summary>
    ///     Searches for a MyAnimeList user profile.
    /// </summary>
    /// <param name="name">The MyAnimeList username.</param>
    [SlashCommand("profile", "Searches for a MyAnimeList user profile.")]
    public async Task Profile(string name)
    {
        await DeferAsync().ConfigureAwait(false);
        var embed = await Service.BuildMalProfileEmbedAsync(ctx.Guild.Id, name).ConfigureAwait(false);
        if (embed is null)
            return;

        await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Searches MyAnimeList for anime.
    /// </summary>
    /// <param name="query">The anime title to search for.</param>
    [SlashCommand("anime", "Searches MyAnimeList for anime.")]
    public async Task Anime(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        await DeferAsync().ConfigureAwait(false);
        var results = await Service.SearchMalAnimeAsync(query, ctx.Channel is ITextChannel
        {
            IsNsfw: true
        }).ConfigureAwait(false);
        if (results.Count == 0)
        {
            await ErrorAsync(Strings.AnimeNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(results.Count - 1)
            .WithDefaultCanceledPage()
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();
        await interactiveService.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(60),
                InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);
        return;

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return Service.BuildMalAnimePage(ctx.Guild.Id, results[page]);
        }
    }
}