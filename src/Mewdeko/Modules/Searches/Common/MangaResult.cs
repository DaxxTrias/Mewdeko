using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents a manga search result, containing various details about a manga.
/// </summary>
public class MangaResult
{
    /// <summary>
    ///     Gets or sets the ID of the manga.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the publishing status of the manga.
    /// </summary>
    [JsonPropertyName("publishing_status")]
    public string PublishingStatus { get; set; }

    /// <summary>
    ///     Gets or sets the large image URL of the manga.
    /// </summary>
    [JsonPropertyName("image_url_lge")]
    public string ImageUrlLge { get; set; }

    /// <summary>
    ///     Gets or sets the English title of the manga.
    /// </summary>
    [JsonPropertyName("title_english")]
    public string TitleEnglish { get; set; }

    /// <summary>
    ///     Gets or sets the total number of chapters in the manga.
    /// </summary>
    [JsonPropertyName("total_chapters")]
    public int TotalChapters { get; set; }

    /// <summary>
    ///     Gets or sets the total number of volumes in the manga.
    /// </summary>
    [JsonPropertyName("total_volumes")]
    public int TotalVolumes { get; set; }

    /// <summary>
    ///     Gets or sets the description of the manga.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    ///     Gets or sets the genres associated with the manga.
    /// </summary>
    public string[] Genres { get; set; }

    /// <summary>
    ///     Gets or sets the average score of the manga.
    /// </summary>
    [JsonPropertyName("average_score")]
    public string AverageScore { get; set; }

    /// <summary>
    ///     Gets the link to the manga's page on AniList.
    /// </summary>
    public string Link
    {
        get
        {
            return $"https://anilist.co/manga/{Id}";
        }
    }

    /// <summary>
    ///     Gets a brief synopsis of the manga, limited to 500 characters if necessary.
    /// </summary>
    public string Synopsis
    {
        get
        {
            return $"{Description[..(Description.Length > 500 ? 500 : Description.Length)]}...";
        }
    }
}