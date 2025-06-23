using System.Threading;

namespace Mewdeko.Modules.Nsfw;

/// <summary>
///     Service for searching images on various NSFW image boards.
/// </summary>
public interface ISearchImagesService
{
    /// <summary>
    ///     Dictionary containing timers for automatic Hentai requests.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoHentaiTimers { get; }

    /// <summary>
    ///     Dictionary containing timers for automatic Boob requests.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoBoobTimers { get; }

    /// <summary>
    ///     Dictionary containing timers for automatic Butt requests.
    /// </summary>
    public ConcurrentDictionary<ulong, Timer> AutoButtTimers { get; }

    /// <summary>
    ///     Searches Gelbooru for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> Gelbooru(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Searches Danbooru for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> Danbooru(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Searches Konachan for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> Konachan(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Searches Yandere for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> Yandere(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Searches Rule34 for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> Rule34(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Searches E621 for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> E621(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Searches DerpiBooru for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> DerpiBooru(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Searches Sankaku for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> Sankaku(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Searches SafeBooru for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> SafeBooru(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Searches RealBooru for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> RealBooru(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Searches Hentai for images based on the provided tags.
    /// </summary>
    public Task<UrlReply?> Hentai(ulong? guildId, bool forceExplicit, string[] tags);

    /// <summary>
    ///     Toggles the blacklisting of a tag for the specified guild.
    /// </summary>
    public ValueTask<bool> ToggleBlacklistTag(ulong guildId, string tag);

    /// <summary>
    ///     Gets the list of blacklisted tags for the specified guild.
    /// </summary>
    public ValueTask<string[]> GetBlacklistedTags(ulong guildId);
}