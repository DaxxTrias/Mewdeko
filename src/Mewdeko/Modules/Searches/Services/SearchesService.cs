using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using GTranslate.Translators;
using LinqToDB;
using MartineApiNet;
using MartineApiNet.Enums;
using MartineApiNet.Models.Images;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Services.Strings;
using Newtonsoft.Json.Linq;
using Refit;
using SkiaSharp;

namespace Mewdeko.Modules.Searches.Services;

/// <summary>
///     Service for handling search-related commands.
/// </summary>
public class SearchesService : INService, IUnloadableService
{
    /// <summary>
    ///     Represents the type of Image search.
    /// </summary>
    public enum ImageTag
    {
        /// <summary>
        ///     Represents a search for food images.
        /// </summary>
        Food,

        /// <summary>
        ///     Represents a search for dog images.
        /// </summary>
        Dogs,

        /// <summary>
        ///     Represents a search for cat images.
        /// </summary>
        Cats,

        /// <summary>
        ///     Represents a search for bird images.
        /// </summary>
        Birds
    }

    // Cached JsonSerializerOptions for performance
    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HtmlParser GoogleParser = new(new HtmlParserOptions
    {
        IsScripting = false,
        IsEmbedded = false,
        IsSupportingProcessingInstructions = false,
        IsKeepingSourceReferences = false,
        IsNotSupportingFrames = true
    });

    private readonly IDataCache cache;
    private readonly IBotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IGoogleApiService google;
    private readonly IHttpClientFactory httpFactory;

    private readonly ConcurrentDictionary<ulong, SearchImageCacher> imageCacher = new();
    private readonly IImageCache imgs;
    private readonly ILogger<SearchesService> logger;
    private readonly MartineApi martineApi;
    private readonly List<string> nsfwreddits;
    private readonly MewdekoRandom rng;
    private readonly GeneratedBotStrings strings;
    private readonly List<string?> yomamaJokes;

    private readonly object yomamaLock = new();
    private int yomamaJokeIndex;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SearchesService" /> class.
    /// </summary>
    /// <param name="google">The Google API service.</param>
    /// <param name="cache">The data cache.</param>
    /// <param name="factory">The HTTP client factory.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="handler">Async discord event handler because stoopid</param>
    /// <param name="martineApi">Reddit!</param>
    /// <param name="dbFactory">The db context provider</param>
    public SearchesService(IGoogleApiService google, IDataCache cache,
        IHttpClientFactory factory,
        IBotCredentials creds, EventHandler handler, MartineApi martineApi, IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings, ILogger<SearchesService> logger)
    {
        httpFactory = factory;
        this.google = google;
        imgs = cache.LocalImages;
        this.cache = cache;
        this.creds = creds;
        this.martineApi = martineApi;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.logger = logger;
        rng = new MewdekoRandom();

        //translate commands
        handler.Subscribe("MessageReceived", "SearchesService", async (SocketMessage msg) =>
        {
            try
            {
                if (msg is not SocketUserMessage umsg)
                    return;

                if (!TranslatedChannels.TryGetValue(umsg.Channel.Id, out var autoDelete))
                    return;

                var key = (umsg.Author.Id, umsg.Channel.Id);

                if (!UserLanguages.TryGetValue(key, out var langs))
                    return;
                string text;
                if (langs.Contains('<'))
                {
                    var split = langs.Split('<');
                    text = await AutoTranslate(umsg.Resolve(TagHandling.Ignore), split[1], split[0])
                        .ConfigureAwait(false);
                }
                else
                {
                    var split = langs.Split('>');
                    text = await AutoTranslate(umsg.Resolve(TagHandling.Ignore), split[0], split[1])
                        .ConfigureAwait(false);
                }

                if (autoDelete)
                {
                    try
                    {
                        await umsg.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                await umsg.Channel.SendConfirmAsync(
                        strings.SearchResultUser(((IGuildChannel)umsg.Channel).GuildId, umsg.Author.Mention,
                            text.Replace("<@ ", "<@", StringComparison.InvariantCulture)
                                .Replace("<@! ", "<@!", StringComparison.InvariantCulture)))
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        });

        //joke commands
        if (File.Exists("data/wowjokes.json"))
            WowJokes = JsonSerializer.Deserialize<List<WoWJoke>>(File.ReadAllText("data/wowjokes.json"));
        else
            logger.LogWarning("data/wowjokes.json is missing. WOW Jokes are not loaded");

        if (File.Exists("data/magicitems.json"))
            MagicItems = JsonSerializer.Deserialize<List<MagicItem>>(File.ReadAllText("data/magicitems.json"));
        else
            logger.LogWarning("data/magicitems.json is missing. Magic items are not loaded");

        if (File.Exists("data/yomama.txt"))
        {
            yomamaJokes = File.ReadAllLines("data/yomama.txt")
                .Shuffle()
                .ToList();
        }

        if (File.Exists("data/ultimatelist.txt"))
        {
            nsfwreddits = File.ReadAllLines("data/ultimatelist.txt")
                .ToList();
        }
    }

    /// <summary>
    ///     Gets the collection of channels where auto translation is enabled.
    /// </summary>
    public ConcurrentDictionary<ulong, bool> TranslatedChannels { get; } = new();

    // (userId, channelId)
    /// <summary>
    ///     Gets the collection of user languages.
    /// </summary>
    public ConcurrentDictionary<(ulong UserId, ulong ChannelId), string> UserLanguages { get; } = new();

    /// <summary>
    ///     Gets the collection of WOW jokes.
    /// </summary>
    public List<WoWJoke> WowJokes { get; } = [];

    /// <summary>
    ///     Gets the collection of magic items.
    /// </summary>
    public List<MagicItem> MagicItems { get; } = [];

    /// <summary>
    ///     Gets the collection of auto hentai timers.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoHentaiTimers { get; } = new();

    /// <summary>
    ///     Gets the collection of auto boob timers.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoBoobTimers { get; } = new();

    /// <summary>
    ///     Gets the collection of auto butt timers.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoButtTimers { get; } = new();

    /// <summary>
    ///     Unloads the service, clearing timers and caches.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    ///     This method should be called when the service is being unloaded to clean up resources.
    /// </remarks>
    public Task Unload()
    {
        AutoBoobTimers.ForEach(x => x.Value.Change(Timeout.Infinite, Timeout.Infinite));
        AutoBoobTimers.Clear();
        AutoButtTimers.ForEach(x => x.Value.Change(Timeout.Infinite, Timeout.Infinite));
        AutoButtTimers.Clear();
        AutoHentaiTimers.ForEach(x => x.Value.Change(Timeout.Infinite, Timeout.Infinite));
        AutoHentaiTimers.Clear();

        imageCacher.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Sets the relationship score between two users.
    /// </summary>
    /// <param name="user1">The ID of the first user.</param>
    /// <param name="user2">The ID of the second user.</param>
    /// <param name="score">The score indicating the relationship strength.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    ///     This method sets the relationship score between two users, typically used in a dating context.
    /// </remarks>
    public Task SetShip(ulong user1, ulong user2, int score)
    {
        return cache.SetShip(user1, user2, score);
    }

    /// <summary>
    ///     Retrieves the relationship score between two users.
    /// </summary>
    /// <param name="user1">The ID of the first user.</param>
    /// <param name="user2">The ID of the second user.</param>
    /// <returns>A task representing the asynchronous operation, returning the relationship score.</returns>
    /// <remarks>
    ///     This method retrieves the relationship score between two users, typically used in a dating context.
    /// </remarks>
    public Task<ShipCache?> GetShip(ulong user1, ulong user2)
    {
        return cache.GetShip(user1, user2);
    }

    /// <summary>
    ///     Generates a "rest in peace" image with the provided text and avatar URL.
    /// </summary>
    /// <param name="text">The text to display on the image.</param>
    /// <param name="imgUrl">The URL of the avatar image.</param>
    /// <returns>A stream containing the generated image.</returns>
    /// <remarks>
    ///     This method generates an image with the provided text and an avatar image, typically used in memorial contexts.
    /// </remarks>
    public async Task<Stream?> GetRipPictureAsync(string text, Uri imgUrl)
    {
        var data = await cache.GetOrAddCachedDataAsync($"Mewdeko_rip_{text}_{imgUrl}",
            GetRipPictureFactory,
            (text, imgUrl),
            TimeSpan.FromDays(1)).ConfigureAwait(false);

        return data.ToStream();
    }

    private async Task<byte[]> GetRipPictureFactory((string text, Uri avatarUrl) arg)
    {
        var (text, avatarUrl) = arg;

        var bg = SKBitmap.Decode(imgs.Rip.ToArray());
        var (succ, data) = await cache.TryGetImageDataAsync(avatarUrl);
        if (!succ)
        {
            using var http = httpFactory.CreateClient();
            data = await http.GetByteArrayAsync(avatarUrl).ConfigureAwait(false);

            using (var avatarImg = SKBitmap.Decode(data))
            {
                var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear);
                var resizedAvatarImg = avatarImg.Resize(new SKImageInfo(85, 85), samplingOptions);
                var roundedAvatarImg = ApplyRoundedCorners(resizedAvatarImg, 42);

                data = SKImage.FromBitmap(roundedAvatarImg).Encode().ToArray();
                DrawAvatar(bg, roundedAvatarImg);
            }

            await cache.SetImageDataAsync(avatarUrl, data).ConfigureAwait(false);
        }
        else
        {
            using var avatarImg = SKBitmap.Decode(data);
            DrawAvatar(bg, avatarImg);
        }

        // Create a font first
        var textFont = new SKFont
        {
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright),
            Size = 14
        };

        var textPaint = new SKPaint
        {
            IsAntialias = true, Color = SKColors.Black
        };

        using var canvas = new SKCanvas(bg);

        canvas.DrawText(text, 25, 225, textFont, textPaint);

        //flowa
        using var flowers = SKBitmap.Decode(imgs.RipOverlay.ToArray());
        DrawImage(bg, flowers, new SKPoint(0, 0));

        return SKImage.FromBitmap(bg).Encode().ToArray();
    }

// Helper method to draw rounded corners
    private static SKBitmap ApplyRoundedCorners(SKBitmap input, float radius)
    {
        var output = new SKBitmap(input.Width, input.Height, input.AlphaType == SKAlphaType.Opaque);

        using var paint = new SKPaint();
        paint.IsAntialias = true;
        using var clipPath = new SKPath();

        var rect = new SKRect(0, 0, input.Width, input.Height);
        clipPath.AddRoundRect(rect, radius, radius);

        using var canvas = new SKCanvas(output);
        canvas.ClipPath(clipPath);
        canvas.DrawBitmap(input, 0, 0, paint);

        return output;
    }


// Helper method to draw an image on a canvas
    private static void DrawAvatar(SKBitmap bg, SKBitmap avatar)
    {
        using var canvas = new SKCanvas(bg);
        canvas.DrawBitmap(avatar, new SKPoint(0, 0));
    }

// Helper method to draw an image on a canvas
    private static void DrawImage(SKBitmap bg, SKBitmap image, SKPoint location)
    {
        using var canvas = new SKCanvas(bg);
        canvas.DrawBitmap(image, location);
    }


    /// <summary>
    ///     Fetches weather data for the specified location.
    /// </summary>
    /// <param name="query">The location for which to fetch weather data.</param>
    /// <returns>
    ///     A task representing the asynchronous operation, returning the weather data for the specified location.
    /// </returns>
    /// <remarks>
    ///     This method fetches weather data for the specified location using the OpenWeatherMap API.
    /// </remarks>
    public Task<WeatherData?> GetWeatherDataAsync(string query)
    {
        query = query.Trim().ToLowerInvariant();

        return cache.GetOrAddCachedDataAsync($"Mewdeko_weather_{query}",
            GetWeatherDataFactory,
            query,
            TimeSpan.FromHours(3));
    }

    private async Task<WeatherData?> GetWeatherDataFactory(string query)
    {
        // todo theyre EOLing 2.5 api
        // will need to update to 3.0 sooner or later
        // docs here: https://openweathermap.org/one-call-transfer
        using var http = httpFactory.CreateClient();
        try
        {
            logger.LogInformation("Fetching weather data for query: {Query}", query);
            var data = await http.GetStringAsync(
                    $"https://api.openweathermap.org/data/2.5/weather?q={query}&appid=42cd627dd60debf25a5739e50a217d74&units=metric")
                .ConfigureAwait(false);

            return string.IsNullOrEmpty(data) ? null : JsonSerializer.Deserialize<WeatherData>(data);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex.Message);
            return null;
        }
    }

    /// <summary>
    ///     Retrieves time data for the specified location.
    /// </summary>
    /// <param name="arg">The query string specifying the location.</param>
    /// <returns>
    ///     A tuple containing the address, time, and timezone name for the specified location,
    ///     along with any errors encountered during the operation.
    /// </returns>
    /// <remarks>
    ///     This method retrieves time data for the specified location by geocoding the query and
    ///     querying the timezone database API.
    /// </remarks>
    public Task<((string Address, DateTime Time, string TimeZoneName), TimeErrors?)> GetTimeDataAsync(string arg)
    {
        return GetTimeDataFactory(arg);
    }

    private async Task<((string Address, DateTime Time, string TimeZoneName), TimeErrors?)> GetTimeDataFactory(
        string query)
    {
        query = query.Trim();

        if (string.IsNullOrEmpty(query)) return (default, TimeErrors.InvalidInput);

        if (string.IsNullOrWhiteSpace(creds.LocationIqApiKey))
            return (default, TimeErrors.ApiKeyMissing);
        if (string.IsNullOrWhiteSpace(creds.TimezoneDbApiKey))
            return (default, TimeErrors.ApiKey2Missing);

        try
        {
            using var http = httpFactory.CreateClient();
            var res = await cache.GetOrAddCachedDataAsync($"geo_{query}", _ =>
            {
                var url =
                    $"https://eu1.locationiq.com/v1/search.php?{(string.IsNullOrWhiteSpace(creds.LocationIqApiKey) ? "key=" : $"key={creds.LocationIqApiKey}&")}q={Uri.EscapeDataString(query)}&format=json";

                return http.GetStringAsync(url);
            }, "", TimeSpan.FromHours(1)).ConfigureAwait(false);

            if (res != null)
            {
                var responses = JsonSerializer.Deserialize<LocationIqResponse[]>(res);
                if (responses is null || responses.Length == 0)
                {
                    logger.LogWarning("Geocode lookup failed for: {Query}", query);
                    return (default, TimeErrors.NotFound);
                }

                var geoData = responses[0];

                using var req = new HttpRequestMessage(HttpMethod.Get,
                    $"http://api.timezonedb.com/v2.1/get-time-zone?key={creds.TimezoneDbApiKey}&format=json&by=position&lat={geoData.Lat}&lng={geoData.Lon}");
                using var geoRes = await http.SendAsync(req).ConfigureAwait(false);
                var resString = await geoRes.Content.ReadAsStringAsync().ConfigureAwait(false);
                var timeObj = JsonSerializer.Deserialize<TimeZoneResult>(resString);

                var time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(timeObj.Timestamp);

                return ((
                    Address: responses[0].DisplayName,
                    Time: time,
                    TimeZoneName: timeObj.TimezoneName
                ), default);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Weather error: {Message}", ex.Message);
            return (default, TimeErrors.NotFound);
        }

        return (default, TimeErrors.NotFound);
    }

    /// <summary>
    ///     Gets a random image from a specified category using the Martine API.
    /// </summary>
    /// <param name="tag">The category of image to fetch.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the image data.</returns>
    /// <exception cref="ApiException">Thrown when the API request fails.</exception>
    /// <remarks>
    ///     This method fetches random images using the Martine API, selecting from multiple themed subreddits per category.
    /// </remarks>
    public async Task<RedditPost> GetRandomImageAsync(ImageTag tag)
    {
        var subreddit = tag switch
        {
            ImageTag.Food => rng.Next() switch
            {
                var n when n % 5 == 0 => "FoodPorn",
                var n when n % 5 == 1 => "food",
                var n when n % 5 == 2 => "cooking",
                var n when n % 5 == 3 => "recipes",
                _ => "culinary"
            },
            ImageTag.Dogs => rng.Next() switch
            {
                var n when n % 6 == 0 => "dogpictures",
                var n when n % 6 == 1 => "rarepuppers",
                var n when n % 6 == 2 => "puppies",
                var n when n % 6 == 3 => "dogs",
                var n when n % 6 == 4 => "dogswithjobs",
                _ => "WhatsWrongWithYourDog"
            },
            ImageTag.Cats => rng.Next() switch
            {
                var n when n % 7 == 0 => "cats",
                var n when n % 7 == 1 => "CatPictures",
                var n when n % 7 == 2 => "catpics",
                var n when n % 7 == 3 => "SupermodelCats",
                var n when n % 7 == 4 => "CatsStandingUp",
                var n when n % 7 == 5 => "CatsInSinks",
                _ => "TheCatTrapIsWorking"
            },
            ImageTag.Birds => rng.Next() switch
            {
                var n when n % 5 == 0 => "birdpics",
                var n when n % 5 == 1 => "parrots",
                var n when n % 5 == 2 => "birding",
                var n when n % 5 == 3 => "whatsthisbird",
                _ => "Birbs"
            },
            _ => throw new ArgumentException($"Unsupported image tag: {tag}", nameof(tag))
        };

        try
        {
            return await martineApi.RedditApi.GetRandomFromSubreddit(subreddit, Toptype.month).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            logger.LogError("Failed to fetch image from Martine API for tag {Tag} (subreddit: r/{Subreddit}): {Error}",
                tag,
                subreddit,
                ex.HasContent ? ex.Content : "No Content");
            throw;
        }
    }

    /// <summary>
    ///     Automatically translates the input string from one language to another.
    /// </summary>
    /// <param name="str">The string to translate.</param>
    /// <param name="from">The source language code.</param>
    /// <param name="to">The target language code.</param>
    /// <returns>A task representing the asynchronous operation, returning the translated string.</returns>
    private static async Task<string> AutoTranslate(string str, string from, string to)
    {
        using var translator = new AggregateTranslator();
        var translation = await translator.TranslateAsync(str, to, from).ConfigureAwait(false);
        return translation.Translation == str
            ? (await translator.TransliterateAsync(str, to, from).ConfigureAwait(false)).Transliteration
            : translation.Translation;
    }

    /// <summary>
    ///     Translates the input text to the specified languages.
    /// </summary>
    /// <param name="langs">A string representing the target languages separated by comma (e.g., "en,fr,de").</param>
    /// <param name="text">The text to translate. If not provided, the method translates the language of the provided text.</param>
    /// <returns>A task representing the asynchronous operation, returning the translated string.</returns>
    public static async Task<string> Translate(string langs, string? text = null)
    {
        using var translator = new AggregateTranslator();
        var translation = await translator.TranslateAsync(text, langs).ConfigureAwait(false);
        return translation.Translation == text
            ? (await translator.TransliterateAsync(text, langs).ConfigureAwait(false)).Transliteration
            : translation.Translation;
    }

    /// <summary>
    ///     Performs a search using the DAPI (Danbooru) API.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <param name="type">The type of search (e.g., Safe, Explicit).</param>
    /// <param name="guild">The ID of the guild where the search is performed.</param>
    /// <param name="isExplicit">A boolean indicating whether the search is explicit or not.</param>
    /// <returns>A task representing the asynchronous operation, returning the search result.</returns>
    public async Task<ImageCacherObject?> DapiSearch(string? tag, DapiSearchType type, ulong? guild,
        bool isExplicit = false)
    {
        tag ??= "";
        if (string.IsNullOrWhiteSpace(tag)
            && (tag.Contains("loli") || tag.Contains("shota")))
        {
            return null;
        }

        var tags = tag
            .Split('+')
            .Select(x => x.ToLowerInvariant().Replace(' ', '_'))
            .ToArray();

        if (guild.HasValue)
        {
            var hashSet = await GetBlacklistedTags(guild.Value);

            var cacher = imageCacher.GetOrAdd(guild.Value, _ => new SearchImageCacher(httpFactory));

            return await cacher.GetImage(tags, isExplicit, type, hashSet);
        }
        else
        {
            var cacher = imageCacher.GetOrAdd(guild ?? 0, _ => new SearchImageCacher(httpFactory));

            return await cacher.GetImage(tags, isExplicit, type);
        }
    }

    /// <summary>
    ///     Retrieves the blacklisted tags for the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A HashSet containing the blacklisted tags for the guild.</returns>
    private async Task<HashSet<string>> GetBlacklistedTags(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query with GuildId filter
        var tags = await dbContext.NsfwBlacklistedTags
            .Where(t => t.GuildId == guildId)
            .Select(x => x.Tag)
            .ToListAsync();

        return tags.Count != 0 ? tags.ToHashSet() : [];
    }

    /// <summary>
    ///     Checks if a given Reddit is marked as NSFW.
    /// </summary>
    /// <param name="reddit">The Reddit to check.</param>
    /// <returns>True if the Reddit is marked as NSFW, otherwise false.</returns>
    public bool NsfwCheck(string reddit)
    {
        return nsfwreddits.Contains(reddit, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Retrieves a "Yo Mama" joke.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning the "Yo Mama" joke.</returns>
    public Task<string?> GetYomamaJoke()
    {
        string? joke;
        lock (yomamaLock)
        {
            if (yomamaJokeIndex >= yomamaJokes.Count)
            {
                yomamaJokeIndex = 0;
                var newList = yomamaJokes.ToList();
                yomamaJokes.Clear();
                yomamaJokes.AddRange(newList.Shuffle());
            }

            joke = yomamaJokes[yomamaJokeIndex++];
        }

        return Task.FromResult(joke);

        // using (var http = _httpFactory.CreateClient())
        // {
        //     var response = await http.GetStringAsync(new Uri("http://api.yomomma.info/")).ConfigureAwait(false);
        //     return JObject.Parse(response)["joke"].ToString() + " 😆";
        // }
    }

    /// <summary>
    ///     Retrieves a random joke.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation, returning a tuple containing the setup and punchline of the
    ///     joke.
    /// </returns>
    public async Task<(string? Setup, string Punchline)> GetRandomJoke()
    {
        using var http = httpFactory.CreateClient();
        var res = await http.GetStringAsync("https://official-joke-api.appspot.com/random_joke").ConfigureAwait(false);

        var resObj = JsonSerializer.Deserialize<JokeResponse>(res, CachedJsonOptions);
        return (resObj?.Setup, resObj?.Punchline ?? string.Empty);
    }

    /// <summary>
    ///     Retrieves a Chuck Norris joke.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, returning the Chuck Norris joke.</returns>
    public async Task<string?> GetChuckNorrisJoke()
    {
        using var http = httpFactory.CreateClient();
        var response = await http.GetStringAsync(new Uri("https://api.icndb.com/jokes/random/"))
            .ConfigureAwait(false);
        return $"{JObject.Parse(response)["value"]["joke"]} 😆";
    }

    /// <summary>
    ///     Retrieves movie data asynchronously from Wikipedia.
    /// </summary>
    /// <param name="name">The name of the movie.</param>
    /// <returns>A task representing the asynchronous operation, returning the movie data.</returns>
    public Task<WikiMovie?> GetMovieDataAsync(string name)
    {
        name = name.Trim().ToLowerInvariant();
        return cache.GetOrAddCachedDataAsync($"Mewdeko_movie_{name}",
            GetMovieDataFactory,
            name,
            TimeSpan.FromDays(1));
    }

    private async Task<WikiMovie?> GetMovieDataFactory(string name)
    {
        using var http = httpFactory.CreateClient();

        // First search for the movie
        var searchUrl =
            $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(name)}%20film&format=json&prop=info&inprop=url";
        var searchResponse = await http.GetStringAsync(searchUrl).ConfigureAwait(false);
        var searchResult = JsonSerializer.Deserialize<WikiSearchResponse>(searchResponse);

        if (searchResult?.Query?.Search == null || searchResult.Query.Search.Count == 0)
            return null;

        // Get the full page data
        var pageId = searchResult.Query.Search[0].PageId;
        var contentUrl =
            $"https://en.wikipedia.org/w/api.php?action=query&prop=extracts|pageimages|info&pithumbsize=500&inprop=url&explaintext=1&pageids={pageId}&format=json";
        var contentResponse = await http.GetStringAsync(contentUrl).ConfigureAwait(false);
        var contentResult = JsonSerializer.Deserialize<WikiContentResponse>(contentResponse);

        if (!contentResult?.Query?.Pages?.ContainsKey(pageId.ToString()) ?? true)
            return null;

        var page = contentResult.Query.Pages[pageId.ToString()];

        // Parse the year from the text
        var yearMatch = Regex.Match(page.Extract, @"(?:released|premiered)[^\d]*(\d{4})");
        var year = yearMatch.Success ? yearMatch.Groups[1].Value : "N/A";

        return new WikiMovie
        {
            Title = page.Title.Replace("(film)", "").Trim(),
            Year = year,
            Plot = GetFirstParagraph(page.Extract),
            Url = page.FullUrl,
            ImageUrl = page.Thumbnail?.Source
        };
    }

    private string GetFirstParagraph(string extract)
    {
        var firstParagraph = extract.Split("\n\n").FirstOrDefault() ?? "";
        return firstParagraph.Length > 1000 ? firstParagraph[..1000] + "..." : firstParagraph;
    }

    /// <summary>
    ///     Retrieves detailed information about a Steam game by its name.
    /// </summary>
    /// <param name="query">The name of the game to search for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the SteamGameInfo if found;
    ///     otherwise, null.
    /// </returns>
    public async Task<SteamGameInfo?> GetSteamGameInfoByName(string query)
    {
        query = query.Trim().ToLowerInvariant();
        var searchCacheKey = $"steam_game_search_{query}";

        // First try to get the AppId from search
        var searchResult = await cache.GetOrAddCachedDataAsync(
            searchCacheKey,
            async _ =>
            {
                using var http = httpFactory.CreateClient();
                var response =
                    await http.GetAsync(
                        $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(query)}&l=en&cc=US");

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<StoreSearchResponse>(content, CachedJsonOptions);

                if (result?.Items == null || !result.Items.Any())
                    return null;

                // Return the most relevant match
                return result.Items[0];
            },
            default(string),
            TimeSpan.FromHours(1)
        );

        if (searchResult == null)
            return null;

        // Then get detailed info
        return await cache.GetOrAddCachedDataAsync(
            $"steam_game_details_{searchResult.Id}",
            async _ =>
            {
                using var http = httpFactory.CreateClient();
                var detailsStr =
                    await http.GetStringAsync(
                        $"https://store.steampowered.com/api/appdetails?appids={searchResult.Id}");

                var response =
                    JsonSerializer.Deserialize<Dictionary<string, AppDetailsResponse>>(detailsStr, CachedJsonOptions);

                if (response?.TryGetValue(searchResult.Id.ToString(), out var gameDetails) == true &&
                    gameDetails.Success)
                {
                    var data = gameDetails.Data;
                    data.Price = searchResult.Price; // Include the price from search result as it's more reliable
                    data.Metascore = searchResult.Metascore; // Include metascore from search as it's already parsed
                    return data;
                }

                return null;
            },
            default(string),
            TimeSpan.FromHours(6)
        );
    }


    /// <summary>
    ///     Performs a Google search asynchronously.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>A task representing the asynchronous operation, returning the Google search results.</returns>
    public async Task<GoogleSearchResultData?> GoogleSearchAsync(string query)
    {
        query = WebUtility.UrlEncode(query)?.Replace(' ', '+');

        var fullQueryLink = $"https://www.google.ca/search?q={query}&safe=on&lr=lang_eng&hl=en&ie=utf-8&oe=utf-8";

        using var msg = new HttpRequestMessage(HttpMethod.Get, fullQueryLink);
        msg.Headers.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.77 Safari/537.36");
        msg.Headers.Add("Cookie", "CONSENT=YES+shp.gws-20210601-0-RC2.en+FX+423;");

        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        var sw = Stopwatch.StartNew();
        using var response = await http.SendAsync(msg).ConfigureAwait(false);
        var content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        sw.Stop();
        logger.LogInformation("Took {Miliseconds}ms to parse results", sw.ElapsedMilliseconds);

        using var document = await GoogleParser.ParseDocumentAsync(content).ConfigureAwait(false);
        var elems = document.QuerySelectorAll("div.g > div > div");

        var resultsElem = document.QuerySelectorAll("#resultStats").FirstOrDefault();
        var totalResults = resultsElem?.TextContent;
        //var time = resultsElem.Children.FirstOrDefault()?.TextContent
        //^ this doesn't work for some reason, <nobr> is completely missing in parsed collection
        if (!elems.Any())
            return default;

        var results = elems.Select(elem =>
            {
                var children = elem.Children.ToList();
                if (children.Count < 2)
                    return null;

                var href = (children[0].QuerySelector("a") as IHtmlAnchorElement)?.Href;
                var name = children[0].QuerySelector("h3")?.TextContent;

                if (href == null || name == null)
                    return null;

                var txt = children[1].TextContent;

                if (string.IsNullOrWhiteSpace(txt))
                    return null;

                return new GoogleSearchResult(name, href, txt);
            })
            .Where(x => x != null)
            .ToList();

        return new GoogleSearchResultData(
            results.AsReadOnly(),
            fullQueryLink,
            totalResults);
    }

    /// <summary>
    ///     Performs a DuckDuckGo search asynchronously.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <returns>A task representing the asynchronous operation, returning the DuckDuckGo search results.</returns>
    public async Task<GoogleSearchResultData?> DuckDuckGoSearchAsync(string query)
    {
        query = WebUtility.UrlEncode(query)?.Replace(' ', '+');

        const string fullQueryLink = "https://html.duckduckgo.com/html";

        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.77 Safari/537.36");

        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(query), "q");
        using var response = await http.PostAsync(fullQueryLink, formData).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        using var document = await GoogleParser.ParseDocumentAsync(content).ConfigureAwait(false);
        var searchResults = document.QuerySelector(".results");
        var elems = searchResults.QuerySelectorAll(".result");

        if (!elems.Any())
            return default;

        var results = elems.Select(elem =>
            {
                if (elem.QuerySelector(".result__a") is IHtmlAnchorElement anchor)
                {
                    var href = anchor.Href;
                    var name = anchor.TextContent;

                    if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(name))
                        return null;

                    var txt = elem.QuerySelector(".result__snippet")?.TextContent;

                    if (string.IsNullOrWhiteSpace(txt))
                        return null;

                    return new GoogleSearchResult(name, href, txt);
                }

                return null;
            })
            .Where(x => x != null)
            .ToList();

        return new GoogleSearchResultData(
            results.AsReadOnly(),
            fullQueryLink,
            "0");
    }
}

/// <summary>
///     Represents already posted Reddit posts.
/// </summary>
public record RedditCache
{
    /// <summary>
    ///     The guild where the post was posted.
    /// </summary>
    public IGuild Guild { get; set; }

    /// <summary>
    ///     The url of the post.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
///     Represents the result data of a Google search operation.
/// </summary>
public class GoogleSearchResultData
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GoogleSearchResultData" /> class.
    /// </summary>
    /// <param name="results">The list of search results.</param>
    /// <param name="fullQueryLink">The full query link used for the search.</param>
    /// <param name="totalResults">The total number of search results.</param>
    public GoogleSearchResultData(IReadOnlyList<GoogleSearchResult> results, string fullQueryLink, string totalResults)
    {
        Results = results;
        FullQueryLink = fullQueryLink;
        TotalResults = totalResults;
    }

    /// <summary>
    ///     Gets the list of search results.
    /// </summary>
    public IReadOnlyList<GoogleSearchResult> Results { get; }

    /// <summary>
    ///     Gets the full query link used for the search.
    /// </summary>
    public string FullQueryLink { get; }

    /// <summary>
    ///     Gets the total number of search results.
    /// </summary>
    public string TotalResults { get; }
}

/// <summary>
///     Enumerates the possible time-related errors.
/// </summary>
public enum TimeErrors
{
    /// <summary>
    ///     Invalid input error.
    /// </summary>
    InvalidInput,

    /// <summary>
    ///     API key missing error.
    /// </summary>
    ApiKeyMissing,

    /// <summary>
    ///     API 2 key missing error.
    /// </summary>
    ApiKey2Missing,

    /// <summary>
    ///     Not found error.
    /// </summary>
    NotFound,

    /// <summary>
    ///     Unknown error.
    /// </summary>
    Unknown
}

/// <summary>
///     Represents data related to a ship.
/// </summary>
public class ShipCache
{
    /// <summary>
    ///     Gets or sets the first user ID.
    /// </summary>
    public ulong User1 { get; set; }

    /// <summary>
    ///     Gets or sets the second user ID.
    /// </summary>
    public ulong User2 { get; set; }

    /// <summary>
    ///     Gets or sets the score of the ship.
    /// </summary>
    public int Score { get; set; }
}