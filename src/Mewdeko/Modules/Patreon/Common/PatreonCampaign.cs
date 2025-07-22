using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     The creator's page, and the top-level object for accessing lists of members, tiers, etc.
/// </summary>
public class Campaign : PatreonResource
{
    /// <summary>
    ///     Campaign attributes containing all the campaign's properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public CampaignAttributes Attributes { get; set; } = new();

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public CampaignRelationships? Relationships { get; set; }
}

/// <summary>
///     All attributes for a Patreon campaign
/// </summary>
public class CampaignAttributes
{
    /// <summary>
    ///     Datetime that the creator first began the campaign creation process. See published_at.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    ///     The type of content the creator is creating, as in "vanity is creating creation_name". Can be null.
    /// </summary>
    [JsonPropertyName("creation_name")]
    public string? CreationName { get; set; }

    /// <summary>
    ///     The ID of the external discord server that is linked to this campaign. Can be null.
    /// </summary>
    [JsonPropertyName("discord_server_id")]
    public string? DiscordServerId { get; set; }

    /// <summary>
    ///     The ID of the Google Analytics tracker that the creator wants metrics to be sent to. Can be null.
    /// </summary>
    [JsonPropertyName("google_analytics_id")]
    public string? GoogleAnalyticsId { get; set; }

    /// <summary>
    ///     Whether this user has opted-in to rss feeds
    /// </summary>
    [JsonPropertyName("has_rss")]
    public bool? HasRss { get; set; }

    /// <summary>
    ///     Whether or not the creator has sent a one-time rss notification email
    /// </summary>
    [JsonPropertyName("has_sent_rss_notify")]
    public bool? HasSentRssNotify { get; set; }

    /// <summary>
    ///     URL for the campaign's profile image
    /// </summary>
    [JsonPropertyName("image_small_url")]
    public string? ImageSmallUrl { get; set; }

    /// <summary>
    ///     Banner image URL for the campaign
    /// </summary>
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     True if the campaign charges upfront, false otherwise. Can be null.
    /// </summary>
    [JsonPropertyName("is_charged_immediately")]
    public bool? IsChargedImmediately { get; set; }

    /// <summary>
    ///     True if the campaign charges per month, false if the campaign charges per-post
    /// </summary>
    [JsonPropertyName("is_monthly")]
    public bool? IsMonthly { get; set; }

    /// <summary>
    ///     True if the creator has marked the campaign as containing nsfw content
    /// </summary>
    [JsonPropertyName("is_nsfw")]
    public bool? IsNsfw { get; set; }

    /// <summary>
    ///     Main video embed code. Can be null.
    /// </summary>
    [JsonPropertyName("main_video_embed")]
    public string? MainVideoEmbed { get; set; }

    /// <summary>
    ///     Main video URL. Can be null.
    /// </summary>
    [JsonPropertyName("main_video_url")]
    public string? MainVideoUrl { get; set; }

    /// <summary>
    ///     Pithy one-liner for this campaign, displayed on the creator page. Can be null.
    /// </summary>
    [JsonPropertyName("one_liner")]
    public string? OneLiner { get; set; }

    /// <summary>
    ///     Number of patrons having access to this creator
    /// </summary>
    [JsonPropertyName("patron_count")]
    public int? PatronCount { get; set; }

    /// <summary>
    ///     The thing which patrons are paying per, as in "vanity is making $1000 per pay_per_name". Can be null.
    /// </summary>
    [JsonPropertyName("pay_per_name")]
    public string? PayPerName { get; set; }

    /// <summary>
    ///     Relative (to patreon.com) URL for the pledge checkout flow for this campaign
    /// </summary>
    [JsonPropertyName("pledge_url")]
    public string? PledgeUrl { get; set; }

    /// <summary>
    ///     Datetime that the creator most recently published (made publicly visible) the campaign. Can be null.
    /// </summary>
    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    ///     The url for the rss album artwork. Can be null.
    /// </summary>
    [JsonPropertyName("rss_artwork_url")]
    public string? RssArtworkUrl { get; set; }

    /// <summary>
    ///     The title of the campaigns rss feed
    /// </summary>
    [JsonPropertyName("rss_feed_title")]
    public string? RssFeedTitle { get; set; }

    /// <summary>
    ///     Whether the campaign's total earnings are shown publicly
    /// </summary>
    [JsonPropertyName("show_earnings")]
    public bool? ShowEarnings { get; set; }

    /// <summary>
    ///     The creator's summary of their campaign. Can be null.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>
    ///     Thank you embed code. Can be null.
    /// </summary>
    [JsonPropertyName("thanks_embed")]
    public string? ThanksEmbed { get; set; }

    /// <summary>
    ///     Thank you message shown to patrons after they pledge to this campaign. Can be null.
    /// </summary>
    [JsonPropertyName("thanks_msg")]
    public string? ThanksMsg { get; set; }

    /// <summary>
    ///     URL for the video shown to patrons after they pledge to this campaign. Can be null.
    /// </summary>
    [JsonPropertyName("thanks_video_url")]
    public string? ThanksVideoUrl { get; set; }

    /// <summary>
    ///     A URL to access this campaign on patreon.com
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    ///     The campaign's vanity. Can be null.
    /// </summary>
    [JsonPropertyName("vanity")]
    public string? Vanity { get; set; }
}

/// <summary>
///     Campaign relationships to other resources
/// </summary>
public class CampaignRelationships
{
    /// <summary>
    ///     The campaign's benefits
    /// </summary>
    [JsonPropertyName("benefits")]
    public MultipleRelationship? Benefits { get; set; }

    /// <summary>
    ///     The campaign's categories
    /// </summary>
    [JsonPropertyName("categories")]
    public MultipleRelationship? Categories { get; set; }

    /// <summary>
    ///     The campaign owner
    /// </summary>
    [JsonPropertyName("creator")]
    public SingleRelationship? Creator { get; set; }

    /// <summary>
    ///     The campaign's tiers
    /// </summary>
    [JsonPropertyName("tiers")]
    public MultipleRelationship? Tiers { get; set; }
}