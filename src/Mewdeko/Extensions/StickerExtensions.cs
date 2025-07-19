namespace Mewdeko.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IStickerItem"/>.
/// </summary>
public static class StickerExtensions
{
    /// <summary>
    /// Gets the URL of the sticker from the specified <see cref="IStickerItem"/>.
    /// </summary>
    /// <param name="sticker">The sticker item.</param>
    /// <returns>The URL of the sticker.</returns>
    public static string GetStickerUrl(this IStickerItem sticker)
    {
        return $"https://cdn.discordapp.com/stickers/{sticker.Id}.{sticker.Format.ToString().ToLower()}";
    }
}