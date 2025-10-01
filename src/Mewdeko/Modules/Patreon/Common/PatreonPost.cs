using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     Content posted by a creator on a campaign page
/// </summary>
public class Post : PatreonResource
{
    /// <summary>
    ///     Post attributes containing all the post's properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public PostAttributes Attributes { get; set; } = new();

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public PostRelationships? Relationships { get; set; }
}

/// <summary>
///     All attributes for a Patreon post
/// </summary>
public class PostAttributes
{
    /// <summary>
    ///     Platform app id. Can be null.
    /// </summary>
    [JsonPropertyName("app_id")]
    public int? AppId { get; set; }

    /// <summary>
    ///     Processing status of the post. Can be null.
    /// </summary>
    [JsonPropertyName("app_status")]
    public string? AppStatus { get; set; }

    /// <summary>
    ///     Post content. Can be null.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    ///     An object containing embed data if media is embedded in the post, None if there is no embed
    /// </summary>
    [JsonPropertyName("embed_data")]
    public object? EmbedData { get; set; }

    /// <summary>
    ///     Embed media url. Can be null.
    /// </summary>
    [JsonPropertyName("embed_url")]
    public string? EmbedUrl { get; set; }

    /// <summary>
    ///     True if the post incurs a bill as part of a pay-per-post campaign. Can be null.
    /// </summary>
    [JsonPropertyName("is_paid")]
    public bool? IsPaid { get; set; }

    /// <summary>
    ///     True if the post is viewable by anyone, False if only patrons (or a subset of patrons) can view. Can be null.
    /// </summary>
    [JsonPropertyName("is_public")]
    public bool? IsPublic { get; set; }

    /// <summary>
    ///     The tier ids that the post is locked for if only patrons (or a subset of patrons) can view. Can be null.
    /// </summary>
    [JsonPropertyName("tiers")]
    public List<string>? Tiers { get; set; }

    /// <summary>
    ///     Datetime that the creator most recently published (made publicly visible) the post. Can be null.
    /// </summary>
    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    ///     Post title. Can be null.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    ///     A URL to access this post on patreon.com
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>
///     Post relationships to other resources
/// </summary>
public class PostRelationships
{
    /// <summary>
    ///     The author of the post
    /// </summary>
    [JsonPropertyName("user")]
    public SingleRelationship? User { get; set; }

    /// <summary>
    ///     The campaign that the post belongs to
    /// </summary>
    [JsonPropertyName("campaign")]
    public SingleRelationship? Campaign { get; set; }
}