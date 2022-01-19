﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Services.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

public sealed class RedisImagesCache : IImageCache
{
    public enum ImageKey
    {
        CoinsHeads,
        CoinsTails,
        Dice,
        SlotsBg,
        SlotsNumbers,
        SlotsEmojis,
        RategirlMatrix,
        RategirlDot,
        XpBg,
        RipBg,
        RipOverlay,
        Currency
    }

    private const string BASE_PATH = "data/";
    private const string OLD_BASE_PATH = "data/images/";
    private const string CARDS_PATH = "data/images/cards";
    private readonly ConnectionMultiplexer _con;
    private readonly IBotCredentials _creds;
    private readonly HttpClient _http;

    public RedisImagesCache(ConnectionMultiplexer con, IBotCredentials creds)
    {
        _con = con;
        _creds = creds;
        _http = new HttpClient();

        Migrate();
        ImageUrls = JsonConvert.DeserializeObject<ImageUrls>(
            File.ReadAllText(Path.Combine(BASE_PATH, "images.json")));
    }

    private IDatabase Db => _con.GetDatabase();

    public ImageUrls ImageUrls { get; private set; }

    public IReadOnlyList<byte[]> Heads => GetByteArrayData(ImageKey.CoinsHeads);

    public IReadOnlyList<byte[]> Tails => GetByteArrayData(ImageKey.CoinsTails);

    public IReadOnlyList<byte[]> Dice => GetByteArrayData(ImageKey.Dice);

    public IReadOnlyList<byte[]> SlotEmojis => GetByteArrayData(ImageKey.SlotsEmojis);

    public IReadOnlyList<byte[]> SlotNumbers => GetByteArrayData(ImageKey.SlotsNumbers);

    public IReadOnlyList<byte[]> Currency => GetByteArrayData(ImageKey.Currency);

    public byte[] SlotBackground => GetByteData(ImageKey.SlotsBg);

    public byte[] RategirlMatrix => GetByteData(ImageKey.RategirlMatrix);

    public byte[] RategirlDot => GetByteData(ImageKey.RategirlDot);

    public byte[] XpBackground => GetByteData(ImageKey.XpBg);

    public byte[] Rip => GetByteData(ImageKey.RipBg);

    public byte[] RipOverlay => GetByteData(ImageKey.RipOverlay);

    public byte[] GetCard(string key) => _con.GetDatabase().StringGet(GetKey("card_" + key));

    public async Task Reload()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var obj = JObject.Parse(
                await File.ReadAllTextAsync(Path.Combine(BASE_PATH, "images.json")));

            ImageUrls = obj.ToObject<ImageUrls>();
            var t = new ImageLoader(_http, _con, GetKey)
                .LoadAsync(obj);

            var loadCards = Task.Run(async () => await Db.StringSetAsync(Directory.GetFiles(CARDS_PATH)
                        .ToDictionary(
                            x => GetKey("card_" + Path.GetFileNameWithoutExtension(x)),
                            x => (RedisValue)File
                                .ReadAllBytes(x)) // loads them and creates <name, bytes> pairs to store in redis
                        .ToArray())
                    .ConfigureAwait(false));

            await Task.WhenAll(t, loadCards).ConfigureAwait(false);

            sw.Stop();
            Log.Information($"Images reloaded in {sw.Elapsed.TotalSeconds:F2}s");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reloading image service");
            throw;
        }
    }

    private void Migrate()
    {
        try
        {
            Migrate1();
            Migrate2();
            Migrate3();
        }
        catch (Exception ex)
        {
            Log.Warning(ex.Message);
            Log.Error("Something has been incorrectly formatted in your 'images.json' file.\n" +
                      "Use the 'images_example.json' file as reference to fix it and restart the bot.");
        }
    }

    private static void Migrate1()
    {
        if (!File.Exists(Path.Combine(OLD_BASE_PATH, "images.json")))
            return;
        Log.Information("Migrating images v0 to images v1.");
        // load old images
        var oldUrls = JsonConvert.DeserializeObject<ImageUrls>(
            File.ReadAllText(Path.Combine(OLD_BASE_PATH, "images.json")));
        // load new images
        var newUrls = JsonConvert.DeserializeObject<ImageUrls>(
            File.ReadAllText(Path.Combine(BASE_PATH, "images.json")));

        //swap new links with old ones if set. Also update old links.
        newUrls.Coins = oldUrls.Coins;

        newUrls.Currency = oldUrls.Currency;
        newUrls.Dice = oldUrls.Dice;
        newUrls.Rategirl = oldUrls.Rategirl;
        newUrls.Xp = oldUrls.Xp;
        newUrls.Version = 1;

        File.WriteAllText(Path.Combine(BASE_PATH, "images.json"),
            JsonConvert.SerializeObject(newUrls, Formatting.Indented));
        File.Delete(Path.Combine(OLD_BASE_PATH, "images.json"));
    }

    private void Migrate2()
    {
        // load new images
        var urls = JsonConvert.DeserializeObject<ImageUrls>(
            File.ReadAllText(Path.Combine(BASE_PATH, "images.json")));

        if (urls.Version >= 2)
            return;
        Log.Information("Migrating images v1 to images v2.");
        urls.Version = 2;

        var prefix = $"{_creds.RedisKey()}_localimg_";
        Db.KeyDelete(new[]
            {
                prefix + "heads",
                prefix + "tails",
                prefix + "dice",
                prefix + "slot_background",
                prefix + "slotnumbers",
                prefix + "slotemojis",
                prefix + "wife_matrix",
                prefix + "rategirl_dot",
                prefix + "xp_card",
                prefix + "rip",
                prefix + "rip_overlay"
            }
            .Select(x => (RedisKey) x).ToArray());

        File.WriteAllText(Path.Combine(BASE_PATH, "images.json"),
            JsonConvert.SerializeObject(urls, Formatting.Indented));
    }

    private static void Migrate3()
    {
        var urls = JsonConvert.DeserializeObject<ImageUrls>(
            File.ReadAllText(Path.Combine(BASE_PATH, "images.json")));

        if (urls.Version >= 3)
            return;
        urls.Version = 3;
        Log.Information("Migrating images v2 to images v3.");

        var baseStr = "https://Mewdeko-pictures.nyc3.digitaloceanspaces.com/other/currency/";

        var replacementTable = new Dictionary<Uri, Uri>
        {
            {new Uri(baseStr + "0.jpg"), new Uri(baseStr + "0.png")},
            {new Uri(baseStr + "1.jpg"), new Uri(baseStr + "1.png")},
            {new Uri(baseStr + "2.jpg"), new Uri(baseStr + "2.png")}
        };

        if (replacementTable.Keys.Any(x => urls.Currency.Contains(x)))
            urls.Currency = urls.Currency.Select(x => replacementTable.TryGetValue(x, out var newUri)
                    ? newUri
                    : x).Append(new Uri(baseStr + "3.png"))
                .ToArray();

        File.WriteAllText(Path.Combine(BASE_PATH, "images.json"),
            JsonConvert.SerializeObject(urls, Formatting.Indented));
    }

    public async Task<bool> AllKeysExist()
    {
        try
        {
            var results = await Task.WhenAll(Enum.GetNames(typeof(ImageKey))
                    .Select(x => x.ToLowerInvariant())
                    .Select(x => Db.KeyExistsAsync(GetKey(x))))
                .ConfigureAwait(false);

            var cardsExist = await Task.WhenAll(GetAllCardNames()
                    .Select(x => "card_" + x)
                    .Select(x => Db.KeyExistsAsync(GetKey(x))))
                .ConfigureAwait(false);

            return results.All(x => x) && cardsExist.All(x => x);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error checking for Image keys");
            return false;
        }
    }

    private static IEnumerable<string> GetAllCardNames(bool showExtension = false) =>
        Directory.GetFiles(CARDS_PATH) // gets all cards from the cards folder
                 .Select(x => showExtension
                     ? Path.GetFileName(x)
                     : Path.GetFileNameWithoutExtension(x)); // gets their names

    public RedisKey GetKey(string key) => $"{_creds.RedisKey()}_localimg_{key.ToLowerInvariant()}";

    public byte[] GetByteData(string key) => Db.StringGet(GetKey(key));

    public byte[] GetByteData(ImageKey key) => GetByteData(key.ToString());

    private RedisImageArray GetByteArrayData(string key) => new(GetKey(key), _con);

    public RedisImageArray GetByteArrayData(ImageKey key) => GetByteArrayData(key.ToString());
}