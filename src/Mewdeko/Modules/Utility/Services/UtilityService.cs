using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Extensions.Caching.Memory;
using Mewdeko.Modules.Utility.Common;
using VirusTotalNet;
using VirusTotalNet.Results;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Provides various utility functionalities including message sniping, link previews, reaction management, and URL
///     checking.
/// </summary>
public partial class UtilityService : INService
{
    private static readonly TimeSpan SnipeRetention = TimeSpan.FromDays(3);
    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> SnipeLocks = new();
    private const string SnipeSnapshotCachePrefix = "snipe:snapshot:";

    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;
    private readonly IMemoryCache memoryCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UtilityService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database factory.</param>
    /// <param name="cache">The data cache service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="eventHandler">The event handler service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="memoryCache">The memory cache used for recent message snapshots.</param>
    public UtilityService(
        IDataConnectionFactory dbFactory,
        IDataCache cache,
        GuildSettingsService guildSettings,
        EventHandler eventHandler,
        DiscordShardedClient client,
        IMemoryCache memoryCache)
    {
        eventHandler.Subscribe("MessageDeleted", "UtilityService", MsgStore);
        eventHandler.Subscribe("MessageUpdated", "UtilityService", MsgStore2);
        eventHandler.Subscribe("MessageReceived", "UtilityService", MsgReciev);
        eventHandler.Subscribe("MessagesBulkDeleted", "UtilityService", BulkMsgStore);
        this.dbFactory = dbFactory;
        this.cache = cache;
        this.guildSettings = guildSettings;
        this.client = client;
        this.memoryCache = memoryCache;
    }

    /// <summary>
    ///     Retrieves sniped messages for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve sniped messages for.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of sniped messages.</returns>
    public async Task<List<SnipeStore>> GetSnipes(ulong guildId)
    {
        return await cache.GetSnipesForGuild(guildId).ConfigureAwait(false) ?? [];
    }

    /// <summary>
    ///     Checks whether link previewing is enabled for a specific guild.
    /// </summary>
    /// <param name="id">The ID of the guild to check.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a boolean indicating if link previewing is
    ///     enabled.
    /// </returns>
    public async Task<int> GetPLinks(ulong id)
    {
        return (await guildSettings.GetGuildConfig(id)).PreviewLinks;
    }

    /// <summary>
    ///     Toggles link previewing for a specific guild.
    /// </summary>
    /// <param name="guild">The guild to toggle link previewing for.</param>
    /// <param name="yesnt">A string indicating whether to enable or disable link previewing.</param>
    public async Task PreviewLinks(IGuild guild, string yesnt)
    {
        var yesno = -1;

        yesno = yesnt switch
        {
            "y" => 1,
            "n" => 0,
            _ => yesno
        };

        await using var db = await dbFactory.CreateConnectionAsync();

        // Using LinqToDB to update the guild config
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guild.Id);

        if (gc != null)
        {
            gc.PreviewLinks = yesno;
            await db.UpdateAsync(gc);
        }

        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Retrieves the snipe set status for a specific guild.
    /// </summary>
    /// <param name="id">The ID of the guild to check.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating if snipe set is enabled.</returns>
    public async Task<bool> GetSnipeSet(ulong id)
    {
        return (await guildSettings.GetGuildConfig(id)).Snipeset;
    }

    /// <summary>
    ///     Sets the snipe set status for a specific guild.
    /// </summary>
    /// <param name="guild">The guild to set the snipe set status for.</param>
    /// <param name="enabled">A boolean indicating whether to enable or disable snipe set.</param>
    public async Task SnipeSet(IGuild guild, bool enabled)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild config using LinqToDB
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guild.Id);

        if (gc != null)
        {
            gc.Snipeset = enabled;

            // Update using LinqToDB
            await db.UpdateAsync(gc);
        }

        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    private static readonly JsonSerializerOptions SnipeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    /// <summary>
    ///     Serializes message data to JSON for snipe storage.
    /// </summary>
    private static string? SerializeMessageData(IMessage msg)
    {
        try
        {
            var data = new SnipeMessageData
            {
                Content = msg.Content,
                MessageId = msg.Id,
                ChannelId = msg.Channel.Id,
                AuthorId = msg.Author.Id,
                AuthorName = msg.Author.ToString(),
                Timestamp = msg.Timestamp,
                Embeds = msg.Embeds.Where(e => e.Type == EmbedType.Rich).Select(e => new SnipeEmbedData
                {
                    Title = e.Title,
                    Description = e.Description,
                    Url = e.Url,
                    Color = e.Color?.RawValue,
                    AuthorName = e.Author?.Name,
                    AuthorUrl = e.Author?.Url,
                    AuthorIconUrl = e.Author?.IconUrl,
                    FooterText = e.Footer?.Text,
                    FooterIconUrl = e.Footer?.IconUrl,
                    ImageUrl = e.Image?.Url,
                    ThumbnailUrl = e.Thumbnail?.Url,
                    Fields = e.Fields.Select(f => new SnipeEmbedFieldData
                    {
                        Name = f.Name,
                        Value = f.Value,
                        Inline = f.Inline
                    }).ToList()
                }).ToList(),
                Attachments = msg.Attachments.Select(a => new SnipeAttachmentData
                {
                    Filename = a.Filename,
                    Url = a.Url,
                    ProxyUrl = a.ProxyUrl,
                    Size = a.Size,
                    ContentType = a.ContentType
                }).ToList()
            };

            if (msg is IUserMessage userMsg && userMsg.ReferencedMessage != null)
            {
                data.ReferencedMessageId = userMsg.ReferencedMessage.Id;
                data.ReferencedMessageContent = userMsg.ReferencedMessage.Content;
                data.ReferencedMessageAuthor = userMsg.ReferencedMessage.Author?.ToString();
            }

            return JsonSerializer.Serialize(data, SnipeJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task BulkMsgStore(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
        Cacheable<IMessageChannel, ulong> channel)
    {
        if (!channel.HasValue)
            return;

        if (channel.Value is not SocketTextChannel chan)
            return;

        if (!await GetSnipeSet(chan.Guild.Id))
            return;

        if (!messages.Select(x => x.HasValue).Any())
            return;


        var msgs = messages
            .Where(x => x.HasValue)
            .Select(x => CreateSnipeStore(x.Value, chan, false))
            .ToList();

        if (msgs.Count == 0)
            return;

        await AddSnipesToCache(chan.Guild.Id, msgs).ConfigureAwait(false);
    }

    private async Task MsgStore(Cacheable<IMessage, ulong> optMsg, Cacheable<IMessageChannel, ulong> ch)
    {
        if (!ch.HasValue || ch.Value is not IGuildChannel channel)
            return;

        if (!await GetSnipeSet(channel.Guild.Id)) return;

        var snipemsg = (optMsg.HasValue ? optMsg.Value : null) is IUserMessage msg
            ? CreateSnipeStore(msg, channel, false)
            : TryGetSnipeSnapshot(optMsg.Id, channel, false);

        if (snipemsg is null)
            return;

        await AddSnipesToCache(channel.Guild.Id, snipemsg).ConfigureAwait(false);
        memoryCache.Remove(GetSnipeSnapshotCacheKey(snipemsg.MessageId));
    }

    private async Task MsgStore2(Cacheable<IMessage, ulong> optMsg, SocketMessage imsg2, ISocketMessageChannel ch)
    {
        if (ch is not IGuildChannel channel) return;

        if (!await GetSnipeSet(channel.Guild.Id)) return;

        var snipemsg = (optMsg.HasValue ? optMsg.Value : null) is IUserMessage msg
            ? CreateSnipeStore(msg, channel, true)
            : TryGetSnipeSnapshot(optMsg.Id, channel, true);

        if (snipemsg is not null)
            await AddSnipesToCache(channel.Guild.Id, snipemsg).ConfigureAwait(false);

        if (imsg2 is IUserMessage updatedMsg)
            CacheSnipeSnapshot(updatedMsg, channel);
    }

    private async Task AddSnipesToCache(ulong guildId, SnipeStore snipe)
    {
        await AddSnipesToCache(guildId, [snipe]).ConfigureAwait(false);
    }

    private async Task AddSnipesToCache(ulong guildId, IReadOnlyCollection<SnipeStore> newSnipes)
    {
        if (newSnipes.Count == 0)
            return;

        var guildLock = SnipeLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await guildLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var snipes = await cache.GetSnipesForGuild(guildId).ConfigureAwait(false) ?? [];
            snipes.RemoveAll(x => DateTime.UtcNow.Subtract(x.DateAdded) >= SnipeRetention);
            snipes.AddRange(newSnipes);
            await cache.AddSnipeToCache(guildId, snipes).ConfigureAwait(false);
        }
        finally
        {
            guildLock.Release();
        }
    }

    private static SnipeStore CreateSnipeStore(IMessage msg, IGuildChannel channel, bool edited)
    {
        var attachments = BuildAttachmentStore(msg);
        return new SnipeStore
        {
            GuildId = channel.Guild.Id,
            ChannelId = channel.Id,
            MessageId = msg.Id,
            MessageTimestamp = msg.Timestamp,
            Message = BuildSnipeMessageContent(msg, attachments),
            ReferenceMessage = msg is IUserMessage { ReferencedMessage: not null } userMsg
                ? $"{Format.Bold(userMsg.ReferencedMessage.Author.ToString())}: {userMsg.ReferencedMessage.Content.TrimTo(edited ? 1048 : 400)}"
                : null,
            Attachments = attachments,
            JsonData = SerializeMessageData(msg),
            UserId = msg.Author.Id,
            Edited = edited,
            DateAdded = DateTime.UtcNow
        };
    }

    private static string GetSnipeSnapshotCacheKey(ulong messageId)
    {
        return $"{SnipeSnapshotCachePrefix}{messageId}";
    }

    private void CacheSnipeSnapshot(IUserMessage msg, IGuildChannel channel)
    {
        if (msg.Author is null)
            return;

        memoryCache.Set(GetSnipeSnapshotCacheKey(msg.Id), CreateSnipeStore(msg, channel, false),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SnipeRetention
            });
    }

    private SnipeStore? TryGetSnipeSnapshot(ulong messageId, IGuildChannel channel, bool edited)
    {
        if (!memoryCache.TryGetValue(GetSnipeSnapshotCacheKey(messageId), out SnipeStore? snapshot) ||
            snapshot is null ||
            snapshot.GuildId != channel.Guild.Id ||
            snapshot.ChannelId != channel.Id)
            return null;

        return new SnipeStore
        {
            GuildId = snapshot.GuildId,
            ChannelId = snapshot.ChannelId,
            MessageId = snapshot.MessageId,
            MessageTimestamp = snapshot.MessageTimestamp,
            Message = snapshot.Message,
            ReferenceMessage = snapshot.ReferenceMessage,
            Attachments = snapshot.Attachments,
            JsonData = snapshot.JsonData,
            UserId = snapshot.UserId,
            Edited = edited,
            DateAdded = DateTime.UtcNow
        };
    }

    /// <summary>
    ///     Checks a URL for malware and other security threats.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>A task that represents the asynchronous operation, containing a report on the URL's security status.</returns>
    public static Task<UrlReport> UrlChecker(string url)
    {
        var vcheck = new VirusTotal("e49046afa41fdf4e8ca72ea58a5542d0b8fbf72189d54726eed300d2afe5d9a9");
        return vcheck.GetUrlReportAsync(url, true);
    }

    private async Task MsgReciev(IMessage msg)
    {
        if (msg is IUserMessage userMsg && msg.Channel is IGuildChannel guildChannel &&
            await GetSnipeSet(guildChannel.Guild.Id).ConfigureAwait(false))
            CacheSnipeSnapshot(userMsg, guildChannel);

        if (msg.Channel is SocketTextChannel t)
        {
            if (msg.Author.IsBot) return;
            var gid = t.Guild;
            if (await GetPLinks(gid.Id) == 1)
            {
                var linkParser = MyRegex();
                foreach (Match m in linkParser.Matches(msg.Content))
                {
                    var e = new Uri(m.Value);
                    var en = e.Host.Split(".");
                    if (!en.Contains("discord")) continue;
                    var eb = string.Join("", e.Segments).Split("/");
                    if (!eb.Contains("channels")) continue;
                    if (eb.Length < 5
                        || !ulong.TryParse(eb[2], out var linkGuildId)
                        || !ulong.TryParse(eb[3], out var linkChannelId)
                        || !ulong.TryParse(eb[4], out var linkMessageId))
                        continue;
                    SocketGuild guild;
                    if (gid.Id != linkGuildId)
                    {
                        guild = client.GetGuild(linkGuildId);
                        if (guild is null) return;
                    }
                    else
                    {
                        guild = gid;
                    }

                    if (guild != t.Guild)
                        return;
                    var em = await ((IGuild)guild).GetTextChannelAsync(linkChannelId).ConfigureAwait(false);
                    if (em == null) return;
                    var msg2 = await em.GetMessageAsync(linkMessageId).ConfigureAwait(false);
                    if (msg2 is null) return;
                    var en2 = new EmbedBuilder
                    {
                        Color = Mewdeko.OkColor,
                        Author = new EmbedAuthorBuilder
                        {
                            Name = msg2.Author.Username, IconUrl = msg2.Author.GetAvatarUrl(size: 2048)
                        },
                        Footer = new EmbedFooterBuilder
                        {
                            IconUrl = ((IGuild)guild).IconUrl, Text = $"{((IGuild)guild).Name}: {em.Name}"
                        }
                    };
                    if (msg2.Embeds.Count > 0)
                    {
                        en2.AddField("Embed Content:", msg2.Embeds.FirstOrDefault()?.Description);
                        if (msg2.Embeds.FirstOrDefault()!.Image != null)
                        {
                            var embedImage = msg2.Embeds.FirstOrDefault()?.Image;
                            if (embedImage != null)
                                en2.ImageUrl = embedImage?.Url;
                        }
                    }

                    if (msg2.Content.Length > 0) en2.Description = msg2.Content;

                    if (msg2.Attachments.Count > 0) en2.ImageUrl = msg2.Attachments.FirstOrDefault().Url;

                    await msg.Channel.SendMessageAsync(embed: en2.WithTimestamp(msg2.Timestamp).Build())
                        .ConfigureAwait(false);
                }
            }
        }
    }

    [GeneratedRegex(
        @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();

    private static List<SnipeAttachmentStore> BuildAttachmentStore(IMessage msg)
    {
        return msg.Attachments.Select(a => new SnipeAttachmentStore
        {
            Filename = a.Filename,
            Url = a.Url,
            ProxyUrl = a.ProxyUrl
        }).ToList();
    }

    private static string BuildSnipeMessageContent(IMessage msg, IReadOnlyCollection<SnipeAttachmentStore> attachments)
    {
        if (!string.IsNullOrWhiteSpace(msg.Content))
            return msg.Content;

        if (attachments.Count == 0)
            return string.Empty;

        var names = attachments
            .Take(5)
            .Select(a => string.IsNullOrWhiteSpace(a.Filename) ? "unnamed attachment" : a.Filename);

        var summary = $"[Attachment-only message] {string.Join(", ", names)}";
        if (attachments.Count > 5)
            summary += $" (+{attachments.Count - 5} more)";

        return summary;
    }
}

/// <summary>
///     Represents serialized message data for snipe storage.
/// </summary>
public class SnipeMessageData
{
    /// <summary>
    ///     The message content.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    ///     The message ID.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     The channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The author's user ID.
    /// </summary>
    public ulong AuthorId { get; set; }

    /// <summary>
    ///     The author's display name.
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    ///     When the message was originally sent.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    ///     Referenced message ID if this was a reply.
    /// </summary>
    public ulong? ReferencedMessageId { get; set; }

    /// <summary>
    ///     Referenced message content.
    /// </summary>
    public string? ReferencedMessageContent { get; set; }

    /// <summary>
    ///     Referenced message author.
    /// </summary>
    public string? ReferencedMessageAuthor { get; set; }

    /// <summary>
    ///     Embeds in the message.
    /// </summary>
    public List<SnipeEmbedData>? Embeds { get; set; }

    /// <summary>
    ///     Attachments in the message.
    /// </summary>
    public List<SnipeAttachmentData>? Attachments { get; set; }
}

/// <summary>
///     Represents embed data for snipe storage.
/// </summary>
public class SnipeEmbedData
{
    /// <summary>
    ///     Embed title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     Embed description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Embed URL.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    ///     Embed color as raw value.
    /// </summary>
    public uint? Color { get; set; }

    /// <summary>
    ///     Author name.
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    ///     Author URL.
    /// </summary>
    public string? AuthorUrl { get; set; }

    /// <summary>
    ///     Author icon URL.
    /// </summary>
    public string? AuthorIconUrl { get; set; }

    /// <summary>
    ///     Footer text.
    /// </summary>
    public string? FooterText { get; set; }

    /// <summary>
    ///     Footer icon URL.
    /// </summary>
    public string? FooterIconUrl { get; set; }

    /// <summary>
    ///     Image URL.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     Thumbnail URL.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    ///     Embed fields.
    /// </summary>
    public List<SnipeEmbedFieldData>? Fields { get; set; }
}

/// <summary>
///     Represents embed field data for snipe storage.
/// </summary>
public class SnipeEmbedFieldData
{
    /// <summary>
    ///     Field name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Field value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    ///     Whether the field is inline.
    /// </summary>
    public bool Inline { get; set; }
}

/// <summary>
///     Represents attachment data for snipe storage.
/// </summary>
public class SnipeAttachmentData
{
    /// <summary>
    ///     Attachment filename.
    /// </summary>
    public string? Filename { get; set; }

    /// <summary>
    ///     Attachment URL.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    ///     Attachment proxy URL.
    /// </summary>
    public string? ProxyUrl { get; set; }

    /// <summary>
    ///     Attachment size in bytes.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    ///     Content type of the attachment.
    /// </summary>
    public string? ContentType { get; set; }
}