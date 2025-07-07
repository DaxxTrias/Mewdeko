using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     A membership level on a campaign, which can have benefits attached to it
/// </summary>
public class Tier : PatreonResource
{
    /// <summary>
    ///     Tier attributes containing all the tier's properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public TierAttributes Attributes { get; set; } = new();

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public TierRelationships? Relationships { get; set; }
}

/// <summary>
///     All attributes for a Patreon tier
/// </summary>
public class TierAttributes
{
    /// <summary>
    ///     Monetary amount associated with this tier (in U.S. cents)
    /// </summary>
    [JsonPropertyName("amount_cents")]
    public int? AmountCents { get; set; }

    /// <summary>
    ///     Datetime this tier was created
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    ///     Tier display description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    ///     The discord role IDs granted by this tier. Can be null.
    /// </summary>
    [JsonPropertyName("discord_role_ids")]
    public List<string>? DiscordRoleIds { get; set; }

    /// <summary>
    ///     Datetime tier was last modified
    /// </summary>
    [JsonPropertyName("edited_at")]
    public DateTime? EditedAt { get; set; }

    /// <summary>
    ///     Full qualified image URL associated with this tier. Can be null.
    /// </summary>
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     Number of patrons currently registered for this tier
    /// </summary>
    [JsonPropertyName("patron_count")]
    public int? PatronCount { get; set; }

    /// <summary>
    ///     Number of posts published to this tier. Can be null.
    /// </summary>
    [JsonPropertyName("post_count")]
    public int? PostCount { get; set; }

    /// <summary>
    ///     True if the tier is currently published
    /// </summary>
    [JsonPropertyName("published")]
    public bool? Published { get; set; }

    /// <summary>
    ///     Datetime this tier was last published. Can be null.
    /// </summary>
    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    ///     Remaining number of patrons who may subscribe, if there is a user_limit. Can be null.
    /// </summary>
    [JsonPropertyName("remaining")]
    public int? Remaining { get; set; }

    /// <summary>
    ///     True if this tier requires a shipping address from patrons
    /// </summary>
    [JsonPropertyName("requires_shipping")]
    public bool? RequiresShipping { get; set; }

    /// <summary>
    ///     Tier display title
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    ///     Datetime tier was unpublished, while applicable. Can be null.
    /// </summary>
    [JsonPropertyName("unpublished_at")]
    public DateTime? UnpublishedAt { get; set; }

    /// <summary>
    ///     Fully qualified URL associated with this tier
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    ///     Maximum number of patrons this tier is limited to, if applicable. Can be null.
    /// </summary>
    [JsonPropertyName("user_limit")]
    public int? UserLimit { get; set; }
}

/// <summary>
///     Tier relationships to other resources
/// </summary>
public class TierRelationships
{
    /// <summary>
    ///     The benefits attached to the tier, which are used for generating deliverables
    /// </summary>
    [JsonPropertyName("benefits")]
    public MultipleRelationship? Benefits { get; set; }

    /// <summary>
    ///     The campaign the tier belongs to
    /// </summary>
    [JsonPropertyName("campaign")]
    public SingleRelationship? Campaign { get; set; }

    /// <summary>
    ///     The image file associated with the tier
    /// </summary>
    [JsonPropertyName("tier_image")]
    public SingleRelationship? TierImage { get; set; }
}