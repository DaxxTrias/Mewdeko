﻿using System.IO;
using Mewdeko.Common.ModuleBehaviors;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Service for caching images in FusionCache.
/// </summary>
public sealed class FusionImagesCache : IImageCache, IReadyExecutor, INService
{
    /// <summary>
    ///     Enum representing the keys for the images. Used to retrieve images from the cache.
    /// </summary>
    public enum ImageKeys
    {
        /// <summary>
        ///     Coin heads image key.
        /// </summary>
        CoinHeads,

        /// <summary>
        ///     Coin tails image key.
        /// </summary>
        CoinTails,

        /// <summary>
        ///     Dice image key.
        /// </summary>
        Dice,

        /// <summary>
        ///     Slot machine background image key.
        /// </summary>
        SlotBg,

        /// <summary>
        ///     Slot machine emojis image key.
        /// </summary>
        SlotEmojis,

        /// <summary>
        ///     Currency image key.
        /// </summary>
        Currency,

        /// <summary>
        ///     Rip overlay image key.
        /// </summary>
        RipOverlay,

        /// <summary>
        ///     Rip background image key.
        /// </summary>
        RipBg,

        /// <summary>
        ///     XP background image key.
        /// </summary>
        XpBg
    }

    private const string BasePath = "data/images/";
    private const string CardsPath = BasePath + "cards/";
    private const string CoinPath = BasePath + "coin/";
    private const string EmojiPath = BasePath + "emoji/";

    private readonly IFusionCache cache;

    private readonly ILogger<FusionImagesCache> logger;
    // private readonly string imagesPath; // Currently unused

    /// <summary>
    ///     Initializes a new instance of the <see cref="FusionImagesCache" /> class.
    /// </summary>
    /// <param name="cache">The FusionCache instance.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public FusionImagesCache(IFusionCache cache, IBotCredentials creds, ILogger<FusionImagesCache> logger)
    {
        this.cache = cache;
        this.logger = logger;
    }

    /// <summary>
    ///     Image URLs.
    /// </summary>
    public ImageUrls ImageUrls { get; }

    /// <summary>
    ///     Retrieves a byte array representing the background image for the XP system.
    /// </summary>
    public byte[] XpBackground
    {
        get
        {
            return GetByteData(ImageKeys.XpBg, BasePath + "xp-background/", "xp.png");
        }
    }

    /// <summary>
    ///     Retrieves a byte array representing the RIP background image.
    /// </summary>
    public byte[] Rip
    {
        get
        {
            return GetByteData(ImageKeys.RipBg, EmojiPath, "rip-bg.png");
        }
    }

    /// <summary>
    ///     Retrieves a byte array representing the RIP overlay image.
    /// </summary>
    public byte[] RipOverlay
    {
        get
        {
            return GetByteData(ImageKeys.RipOverlay, EmojiPath, "rip-overlay.png");
        }
    }


    /// <summary>
    ///     Retrieves a card image byte array based on the provided key.
    /// </summary>
    /// <param name="key">The key of the card image.</param>
    /// <returns>The byte array representing the card image.</returns>
    public byte[] GetCard(string key)
    {
        return File.ReadAllBytes(Path.Join(CardsPath, $"{key}.jpg"));
    }

    /// <summary>
    ///     Reloads all image data from the specified sources.
    /// </summary>
    public async Task Reload()
    {
        await Load(ImageKeys.CoinHeads, Directory.GetFiles(CoinPath, "head-coin.png")).ConfigureAwait(false);
        await Load(ImageKeys.CoinTails, Directory.GetFiles(CoinPath, "tail-coin.png")).ConfigureAwait(false);
        await Load(ImageKeys.Dice, Directory.GetFiles(BasePath, "dice*.png")).ConfigureAwait(false);
        await Load(ImageKeys.SlotEmojis, Directory.GetFiles(EmojiPath)).ConfigureAwait(false);
        await Load(ImageKeys.XpBg, Directory.GetFiles(BasePath + "xp-background/", "xp.png")).ConfigureAwait(false);
    }

    /// <summary>
    ///     Called when the bot is ready. Checks if all required keys exist in the cache and reloads them if necessary.
    /// </summary>
    public async Task OnReadyAsync()
    {
        logger.LogInformation($"Starting {GetType()} Cache");
        if (await AllKeysExist().ConfigureAwait(false))
            return;

        await Reload().ConfigureAwait(false);
    }

    private async Task Load(ImageKeys key, string path)
    {
        var data = await GetImageDataFromFile(path).ConfigureAwait(false);
        if (data is null)
            return;

        await cache.SetAsync(GetCacheKey(key), data).ConfigureAwait(false);
    }

    private async Task Load(ImageKeys key, string[] paths)
    {
        var tasks = paths.Select(GetImageDataFromFile);
        var imageData = await Task.WhenAll(tasks).ConfigureAwait(false);
        var validData = imageData.Where(x => x is not null).ToArray();

        await cache.SetAsync(GetCacheKey(key), validData).ConfigureAwait(false);

        if (paths.Length != validData.Length)
        {
            logger.LogInformation("{Loaded}/{Max} paths for the key '{ImageKey}' have been loaded.\n" +
                                  "Some of the supplied paths are either unavailable or invalid",
                validData.Length, paths.Length, key);
        }
    }

    private async Task<byte[]?> GetImageDataFromFile(string path)
    {
        try
        {
            return await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed reading image bytes from path: {Path}", path);
            return null;
        }
    }

    private async Task<bool> AllKeysExist()
    {
        var tasks = GetAllKeys()
            .Select(key => cache.TryGetAsync<byte[]>(GetCacheKey(key)).AsTask());

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return results.All(result => result.HasValue);
    }


    private static IEnumerable<ImageKeys> GetAllKeys()
    {
        return Enum.GetValues<ImageKeys>();
    }

    private IReadOnlyList<byte[]> GetByteArrayData(ImageKeys key, string path, string pattern)
    {
        return cache.GetOrSet(GetCacheKey(key), async _ =>
        {
            var files = Directory.GetFiles(path, pattern);
            var data = await Task.WhenAll(files.Select(file => File.ReadAllBytesAsync(file)));
            return data;
        }, TimeSpan.FromDays(1)).Result;
    }

    private byte[] GetByteData(ImageKeys key, string path, string fileName)
    {
        return cache.GetOrSet(GetCacheKey(key), async _ =>
        {
            var filePath = Path.Combine(path, fileName);
            if (File.Exists(filePath))
            {
                return await File.ReadAllBytesAsync(filePath);
            }

            return [];
        }, TimeSpan.FromDays(1)).Result;
    }

    private string GetCacheKey(ImageKeys key)
    {
        return $"{key}";
    }
}