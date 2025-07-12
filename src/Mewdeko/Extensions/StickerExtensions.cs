namespace Mewdeko.Extensions;

/// <summary>
/// get the sticker URL from the IStickerItem
/// </summary>
public static class StickerExtensions
{
    public static string GetStickerUrl(this IStickerItem sticker)
    {
        // Assuming the URL can be constructed using the sticker ID and format
        return $"https://cdn.discordapp.com/stickers/{sticker.Id}.{sticker.Format.ToString().ToLower()}";
    }
}