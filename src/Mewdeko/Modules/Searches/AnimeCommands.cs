#nullable enable
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Anilist4Net;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using JikanDotNet;
using MartineApiNet;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Services;
using NekosBestApiNet;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    /// <summary>
    ///     Group of commands related to anime.
    /// </summary>
    [Group]
    public class AnimeCommands(
        InteractiveService service,
        MartineApi martineApi,
        NekosBestApi nekosBestApi,
        HttpClient httpClient)
        : MewdekoSubmodule<SearchesService>
    {
        /// <summary>
        ///     Sends a ship image based on compatibility between two users.
        /// </summary>
        /// <param name="user">The first user to be compared.</param>
        /// <param name="user2">The second user to be compared.</param>
        /// <remarks>
        ///     This command calculates the compatibility score between two users and sends a ship image
        ///     with a message based on the score.
        /// </remarks>
        /// <example>
        ///     <code>.ship @user1 @user2</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task Ship(IUser user, IUser user2)
        {
            var random = new Random().Next(0, 101);
            var getShip = await Service.GetShip(user.Id, user2.Id);
            if (getShip is not null)
                random = getShip.Score;
            else
                await Service.SetShip(user.Id, user2.Id, random);
            var shipRequest = await martineApi.ImageGenerationApi.GenerateShipImage(random,
                    user.RealAvatarUrl().AbsoluteUri, user2.RealAvatarUrl().AbsoluteUri)
                .ConfigureAwait(false);
            var bytes = await shipRequest.ReadAsByteArrayAsync().ConfigureAwait(false);
            var ms = new MemoryStream(bytes);
            await using var _ = ms.ConfigureAwait(false);
            var color = new Color();
            var response = string.Empty;
            switch (random)
            {
                case < 30:
                    response = Strings.ShipNoChance(ctx.Guild.Id);
                    break;
                case <= 50 and >= 31:
                    response = Strings.ShipMaybeChance(ctx.Guild.Id);
                    break;
                case 69:
                    response = Strings.ShipSixnine(ctx.Guild.Id);
                    break;
                case <= 70 and >= 60:
                    response = Strings.ShipGoodChance(ctx.Guild.Id);
                    break;
                case <= 100 and >= 71:
                    response = Strings.ShipExcellentChance(ctx.Guild.Id);
                    break;
            }

            await ctx.Channel.SendFileAsync(ms, "ship.png",
                    embed: new EmbedBuilder().WithColor(color)
                        .WithDescription(Strings.CompatibilityResult(ctx.Guild.Id, random, response))
                        .WithImageUrl("attachment://ship.png").Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sends a ship image based on compatibility between the current user and another user.
        /// </summary>
        /// <param name="user">The user to be compared with the current user.</param>
        /// <remarks>
        ///     This command calculates the compatibility score between the current user and another user
        ///     and sends a ship image with a message based on the score.
        /// </remarks>
        /// <example>
        ///     <code>.ship @user</code>
        /// </example>
        [Cmd]
        [Aliases]
        public Task Ship(IUser user)
        {
            return Ship(ctx.User, user);
        }

        /// <summary>
        ///     Sends a random neko image.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a random neko image from the API and sends it in the channel.
        /// </remarks>
        /// <example>
        ///     <code>.randomneko</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task RandomNeko()
        {
            var req = await nekosBestApi.CategoryApi.Neko().ConfigureAwait(false);
            var em = new EmbedBuilder
            {
                Description = Strings.NekoSource(ctx.Guild.Id, req.Results.FirstOrDefault()?.SourceUrl),
                ImageUrl = req.Results.FirstOrDefault()?.Url,
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sends a random kitsune image.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a random kitsune image from the API and sends it in the channel.
        /// </remarks>
        /// <example>
        ///     <code>.randomkitsune</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task RandomKitsune()
        {
            var req = await nekosBestApi.CategoryApi.Kitsune().ConfigureAwait(false);
            var em = new EmbedBuilder
            {
                Description = Strings.KitsuneSource(ctx.Guild.Id, req.Results.FirstOrDefault()?.SourceUrl),
                ImageUrl = req.Results.FirstOrDefault()?.Url,
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sends a random waifu image.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a random waifu image from the API and sends it in the channel.
        /// </remarks>
        /// <example>
        ///     <code>.randomwaifu</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task RandomWaifu()
        {
            var req = await nekosBestApi.CategoryApi.Waifu().ConfigureAwait(false);
            var em = new EmbedBuilder
            {
                Description = Strings.WaifuSource(ctx.Guild.Id, req.Results.FirstOrDefault()?.SourceUrl),
                ImageUrl = req.Results.FirstOrDefault()?.Url,
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves and displays information about a MyAnimeList profile.
        /// </summary>
        /// <param name="name">The username of the MyAnimeList profile.</param>
        /// <remarks>
        ///     This command fetches and displays various statistics and information about a MyAnimeList profile,
        ///     including watching, completed, on hold, dropped, and plan to watch anime lists, as well as other details.
        /// </remarks>
        /// <example>
        ///     <code>.mal username</code>
        /// </example>
        [Cmd]
        [Aliases]
        [Priority(0)]
        public async Task Mal([Remainder] string? name)
        {
            var embed = await Service.BuildMalProfileEmbedAsync(ctx.Guild.Id, name).ConfigureAwait(false);
            if (embed is not null)
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves and displays information about a MyAnimeList profile for a specified user in the current guild.
        /// </summary>
        /// <param name="usr">The user for whom to retrieve the MyAnimeList profile information.</param>
        /// <remarks>
        ///     This command fetches and displays various statistics and information about the MyAnimeList profile of a specified
        ///     user
        ///     within the current guild, including watching, completed, on hold, dropped, and plan to watch anime lists, as well
        ///     as other details.
        /// </remarks>
        /// <example>
        ///     <code>.mal @username</code>
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public Task Mal(IGuildUser usr)
        {
            return Mal(usr.Username);
        }

        /// <summary>
        ///     Finds anime information based on an image.
        /// </summary>
        /// <param name="e">The image URL or an attached image to use for searching.</param>
        /// <remarks>
        ///     This command finds anime information based on an image using the Trace.moe API and displays relevant details.
        /// </remarks>
        /// <example>
        ///     <code>.findanime image_url</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task FindAnime(string? e = null)
        {
            var t = string.Empty;
            if (e != null) t = e;
            if (e is null)
            {
                try
                {
                    t = ctx.Message.Attachments.FirstOrDefault()?.Url;
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync(
                            Strings.YouNeedToAttachFileOrUrl(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }
            }

            var c2 = new Client();
            var response = await httpClient.PostAsync(
                $"https://api.trace.moe/search?url={t}", null).ConfigureAwait(false);
            var responseContent = response.Content;
            using var reader = new StreamReader(await responseContent.ReadAsStreamAsync().ConfigureAwait(false));
            var er = await reader.ReadToEndAsync().ConfigureAwait(false);
            var stuff = JsonSerializer.Deserialize<MoeResponse>(er,
                new JsonSerializerOptions
                {
                    RespectNullableAnnotations = true
                });
            if (!string.IsNullOrWhiteSpace(stuff?.Error))
            {
                await ctx.Channel.SendErrorAsync(
                        Strings.FindAnimeError(ctx.Guild.Id, stuff.Error), Config)
                    .ConfigureAwait(false);
                return;
            }

            var ert = stuff?.Result?.FirstOrDefault();
            if (ert?.Filename is null)
            {
                await ctx.Channel.SendErrorAsync(
                        Strings.NoResultsTryDifferent(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var image = await c2.GetMediaById(ert.Anilist).ConfigureAwait(false);
            var eb = new EmbedBuilder
            {
                ImageUrl = image?.CoverImageLarge, Color = Mewdeko.OkColor
            };
            var te = image?.SeasonInt.ToString()?[2..] is ""
                ? image.SeasonInt.ToString()?[1..]
                : image?.SeasonInt.ToString()?[2..];
            var entitle = image?.EnglishTitle;
            if (image?.EnglishTitle == null) entitle = "None";
            eb.AddField(Strings.AnimeEnglishTitle(ctx.Guild.Id), entitle);
            eb.AddField(Strings.AnimeJapaneseTitle(ctx.Guild.Id), image?.NativeTitle);
            eb.AddField(Strings.AnimeRomanjiTitle(ctx.Guild.Id), image?.RomajiTitle);
            eb.AddField(Strings.AnimeAirStartDate(ctx.Guild.Id), image?.AiringStartDate);
            eb.AddField(Strings.AnimeAirEndDate(ctx.Guild.Id), image?.AiringEndDate);
            eb.AddField(Strings.AnimeSeasonNumber(ctx.Guild.Id), te);
            if (ert.Episode is not 0) eb.AddField(Strings.AnimeEpisode(ctx.Guild.Id), ert.Episode);
            eb.AddField(Strings.AnimeAnilistLink(ctx.Guild.Id), image?.SiteUrl);
            eb.AddField(Strings.AnimeMalLink(ctx.Guild.Id), $"https://myanimelist.net/anime/{image?.IdMal}");
            eb.AddField(Strings.AnimeScore(ctx.Guild.Id), image?.MeanScore);
            eb.AddField(Strings.AnimeDescription(ctx.Guild.Id), image?.DescriptionMd.TrimTo(1024).StripHtml());
            _ = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves and displays information about a character.
        /// </summary>
        /// <param name="chara">The name of the character to search for.</param>
        /// <remarks>
        ///     This command retrieves and displays information about a character, including their full name,
        ///     alternative names, native name, description/backstory, and an image.
        /// </remarks>
        /// <example>
        ///     <code>.charinfo character_name</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task CharInfo([Remainder] string chara)
        {
            var anilist = new Client();
            var te = await anilist.GetCharacterBySearch(chara).ConfigureAwait(false);
            var desc = string.Empty;
            if (te.DescriptionMd is null) desc = "None";
            if (te.DescriptionMd != null) desc = te.DescriptionMd;
            if (te.DescriptionMd is { Length: > 1024 }) desc = te.DescriptionMd.TrimTo(1024);
            var altnames = string.IsNullOrEmpty(te.AlternativeNames.FirstOrDefault())
                ? "None"
                : string.Join(",", te.AlternativeNames);
            var eb = new EmbedBuilder();
            eb.AddField(Strings.AnimeFullName(ctx.Guild.Id), te.FullName);
            eb.AddField(Strings.AnimeAlternativeNames(ctx.Guild.Id), altnames);
            eb.AddField(Strings.AnimeNativeName(ctx.Guild.Id), te.NativeName);
            eb.AddField(Strings.AnimeDescriptionBackstory(ctx.Guild.Id), desc);
            eb.ImageUrl = te.ImageLarge;
            eb.Color = Mewdeko.OkColor;
            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Searches MyAnimeList for anime and displays information about the search results.
        /// </summary>
        /// <param name="query">The query to search for.</param>
        /// <remarks>
        ///     This command extends the MyAnimeList search set to anime titles, using the same result
        ///     pages as the anime command.
        /// </remarks>
        /// <example>
        ///     <code>.malanime search_query</code>
        /// </example>
        [Cmd]
        [Aliases]
        public Task MalAnime([Remainder] string query)
        {
            return SendMalAnimeSearchAsync(query);
        }

        /// <summary>
        ///     Searches for anime and displays information about the search results.
        /// </summary>
        /// <param name="query">The query to search for.</param>
        /// <remarks>
        ///     This command searches for anime based on the provided query and displays relevant information
        ///     about the search results, including titles, genres, episodes, scores, and more.
        /// </remarks>
        /// <example>
        ///     <code>.anime search_query</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task Anime([Remainder] string query)
        {
            await SendMalAnimeSearchAsync(query).ConfigureAwait(false);
        }

        private async Task SendMalAnimeSearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;

            var results = await Service.SearchMalAnimeAsync(query, ctx.Channel is ITextChannel
            {
                IsNsfw: true
            }).ConfigureAwait(false);
            if (results.Count == 0)
            {
                await ctx.Channel.SendErrorAsync(
                        Strings.AnimeNotFound(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
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
            await service.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return Service.BuildMalAnimePage(ctx.Guild.Id, results[page]);
            }
        }

        /// <summary>
        ///     Searches for manga and displays information about the search results.
        /// </summary>
        /// <param name="query">The query to search for.</param>
        /// <remarks>
        ///     This command searches for manga based on the provided query and displays relevant information
        ///     about the search results, including titles, publish dates, volumes, scores, and more.
        /// </remarks>
        /// <example>
        ///     <code>.manga search_query</code>
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Manga([Remainder] string query)
        {
            var msg = await ctx.Channel.SendConfirmAsync(
                Strings.SearchingFor(ctx.Guild.Id, query)).ConfigureAwait(false);
            var jikan = new Jikan();
            var result = await jikan.SearchMangaAsync(query).ConfigureAwait(false);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(result.Data.Count - 1)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await msg.DeleteAsync().ConfigureAwait(false);
            await service.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                var data = result.Data.Skip(page).FirstOrDefault();
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder()
                    .WithTitle(Format.Bold($"{data?.Titles?.First()?.Title ?? "Unknown"}"))
                    .AddField(Strings.MangaFirstPublishDate(ctx.Guild.Id), data?.Published?.ToString() ?? "Unknown")
                    .AddField(Strings.MangaVolumes(ctx.Guild.Id), data?.Volumes?.ToString() ?? "Unknown")
                    .AddField(Strings.MangaIsStillActive(ctx.Guild.Id), data?.Publishing ?? false)
                    .AddField(Strings.AnimeScore(ctx.Guild.Id), data?.Score?.ToString() ?? "Unknown")
                    .AddField(Strings.MangaUrl(ctx.Guild.Id), data?.Url ?? "")
                    .WithDescription(data?.Background ?? Strings.NoDescriptionAvailable(ctx.Guild.Id))
                    .WithImageUrl(data?.Images?.WebP?.MaximumImageUrl ?? "").WithColor(Mewdeko.OkColor);
            }
        }
    }
}