using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     Patreon API response wrapper
/// </summary>
public class PatreonApiResponse<T>
{
    /// <summary>
    ///     The main data returned by the API
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>
    ///     Additional resources included in the response
    /// </summary>
    [JsonPropertyName("included")]
    public List<PatreonResource>? Included { get; set; }

    /// <summary>
    ///     Links for pagination and navigation
    /// </summary>
    [JsonPropertyName("links")]
    public PatreonLinks? Links { get; set; }

    /// <summary>
    ///     Metadata about the response including pagination info
    /// </summary>
    [JsonPropertyName("meta")]
    public PatreonMeta? Meta { get; set; }
}

/// <summary>
///     Base Patreon resource
/// </summary>
public class PatreonResource
{
    /// <summary>
    ///     Unique identifier for this resource
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    /// <summary>
    ///     Type of resource (campaign, tier, member, etc.)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    /// <summary>
    ///     Attributes containing the resource data
    /// </summary>
    [JsonPropertyName("attributes")]
    public Dictionary<string, object>? Attributes { get; set; }

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public Dictionary<string, PatreonRelationship>? Relationships { get; set; }
}

/// <summary>
///     Patreon campaign data
/// </summary>
public class PatreonCampaign : PatreonResource
{
    /// <summary>
    ///     ISO datetime when the campaign was created
    /// </summary>
    public string? CreatedAt => GetStringAttribute("created_at");

    /// <summary>
    ///     Name of the campaign/creation
    /// </summary>
    public string? CreationName => GetStringAttribute("creation_name");

    /// <summary>
    ///     Setting for displaying patron goals
    /// </summary>
    public string? DisplayPatronGoals => GetStringAttribute("display_patron_goals");

    /// <summary>
    ///     Visibility setting for campaign earnings
    /// </summary>
    public int? EarningsVisibility => GetIntAttribute("earnings_visibility");

    /// <summary>
    ///     Small sized campaign image URL
    /// </summary>
    public string? ImageSmallUrl => GetStringAttribute("image_small_url");

    /// <summary>
    ///     Full sized campaign image URL
    /// </summary>
    public string? ImageUrl => GetStringAttribute("image_url");

    /// <summary>
    ///     Whether patrons are charged immediately
    /// </summary>
    public bool? IsChargedImmediately => GetBoolAttribute("is_charged_immediately");

    /// <summary>
    ///     Whether this is a monthly campaign
    /// </summary>
    public bool? IsMonthly => GetBoolAttribute("is_monthly");

    /// <summary>
    ///     Whether this campaign contains NSFW content
    /// </summary>
    public bool? IsNsfw => GetBoolAttribute("is_nsfw");

    /// <summary>
    ///     Main video embed HTML for the campaign
    /// </summary>
    public string? MainVideoEmbed => GetStringAttribute("main_video_embed");

    /// <summary>
    ///     Main video URL for the campaign
    /// </summary>
    public string? MainVideoUrl => GetStringAttribute("main_video_url");

    /// <summary>
    ///     One-liner description of the campaign
    /// </summary>
    public string? OneLiner => GetStringAttribute("one_liner");

    /// <summary>
    ///     Number of patrons supporting this campaign
    /// </summary>
    public int? PatronCount => GetIntAttribute("patron_count");

    /// <summary>
    ///     Name of what patrons pay per (e.g., "per month", "per video")
    /// </summary>
    public string? PayPerName => GetStringAttribute("pay_per_name");

    /// <summary>
    ///     Total amount pledged in cents
    /// </summary>
    public int? PledgeSum => GetIntAttribute("pledge_sum");

    /// <summary>
    ///     URL to the campaign's pledge page
    /// </summary>
    public string? PledgeUrl => GetStringAttribute("pledge_url");

    /// <summary>
    ///     ISO datetime when the campaign was published
    /// </summary>
    public string? PublishedAt => GetStringAttribute("published_at");

    /// <summary>
    ///     Summary/description of the campaign
    /// </summary>
    public string? Summary => GetStringAttribute("summary");

    /// <summary>
    ///     HTML embed for the thank you message
    /// </summary>
    public string? ThanksEmbed => GetStringAttribute("thanks_embed");

    /// <summary>
    ///     Thank you message text
    /// </summary>
    public string? ThanksMsg => GetStringAttribute("thanks_msg");

    /// <summary>
    ///     Thank you video URL
    /// </summary>
    public string? ThanksVideoUrl => GetStringAttribute("thanks_video_url");

    /// <summary>
    ///     Public URL of the campaign
    /// </summary>
    public string? Url => GetStringAttribute("url");

    private string? GetStringAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true ? value?.ToString() : null;

    private int? GetIntAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true && int.TryParse(value?.ToString(), out var intValue)
            ? intValue
            : null;

    private bool? GetBoolAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true && bool.TryParse(value?.ToString(), out var boolValue)
            ? boolValue
            : null;
}

/// <summary>
///     Patreon tier data
/// </summary>
public class PatreonTierData : PatreonResource
{
    /// <summary>
    ///     Amount in cents required for this tier
    /// </summary>
    public int? AmountCents => GetIntAttribute("amount_cents");

    /// <summary>
    ///     ISO datetime when the tier was created
    /// </summary>
    public string? CreatedAt => GetStringAttribute("created_at");

    /// <summary>
    ///     Description of the tier benefits
    /// </summary>
    public string? Description => GetStringAttribute("description");

    /// <summary>
    ///     Discord role IDs associated with this tier
    /// </summary>
    public string? DiscordRoleIds => GetStringAttribute("discord_role_ids");

    /// <summary>
    ///     Image URL for the tier
    /// </summary>
    public string? ImageUrl => GetStringAttribute("image_url");

    /// <summary>
    ///     Number of patrons in this tier
    /// </summary>
    public int? PatronCount => GetIntAttribute("patron_count");

    /// <summary>
    ///     Whether this tier grants access to post counts
    /// </summary>
    public bool? PostCount => GetBoolAttribute("post_count");

    /// <summary>
    ///     Whether this tier is published and visible
    /// </summary>
    public bool? Published => GetBoolAttribute("published");

    /// <summary>
    ///     Title/name of the tier
    /// </summary>
    public string? Title => GetStringAttribute("title");

    /// <summary>
    ///     URL to the tier page
    /// </summary>
    public string? Url => GetStringAttribute("url");

    private string? GetStringAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true ? value?.ToString() : null;

    private int? GetIntAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true && int.TryParse(value?.ToString(), out var intValue)
            ? intValue
            : null;

    private bool? GetBoolAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true && bool.TryParse(value?.ToString(), out var boolValue)
            ? boolValue
            : null;
}

/// <summary>
///     Patreon member/supporter data
/// </summary>
public class PatreonMember : PatreonResource
{
    /// <summary>
    ///     Amount in cents the member is currently entitled to based on their pledge
    /// </summary>
    public int? CurrentlyEntitledAmountCents => GetIntAttribute("currently_entitled_amount_cents");

    /// <summary>
    ///     Email address of the member
    /// </summary>
    public string? Email => GetStringAttribute("email");

    /// <summary>
    ///     Full name of the member
    /// </summary>
    public string? FullName => GetStringAttribute("full_name");

    /// <summary>
    ///     Whether the member is following the creator
    /// </summary>
    public bool? IsFollower => GetBoolAttribute("is_follower");

    /// <summary>
    ///     ISO datetime of the last successful charge
    /// </summary>
    public string? LastChargeDate => GetStringAttribute("last_charge_date");

    /// <summary>
    ///     Status of the last charge attempt
    /// </summary>
    public string? LastChargeStatus => GetStringAttribute("last_charge_status");

    /// <summary>
    ///     Total amount in cents the member has supported over their lifetime
    /// </summary>
    public int? LifetimeSupportCents => GetIntAttribute("lifetime_support_cents");

    /// <summary>
    ///     ISO datetime of the next scheduled charge
    /// </summary>
    public string? NextChargeDate => GetStringAttribute("next_charge_date");

    /// <summary>
    ///     Note from the creator about this member
    /// </summary>
    public string? Note => GetStringAttribute("note");

    /// <summary>
    ///     Current patron status (active_patron, declined_patron, former_patron)
    /// </summary>
    public string? PatronStatus => GetStringAttribute("patron_status");

    /// <summary>
    ///     ISO datetime when the pledge relationship started
    /// </summary>
    public string? PledgeRelationshipStart => GetStringAttribute("pledge_relationship_start");

    /// <summary>
    ///     Amount in cents the member will pay on the next charge
    /// </summary>
    public int? WillPayAmountCents => GetIntAttribute("will_pay_amount_cents");

    private string? GetStringAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true ? value?.ToString() : null;

    private int? GetIntAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true && int.TryParse(value?.ToString(), out var intValue)
            ? intValue
            : null;

    private bool? GetBoolAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true && bool.TryParse(value?.ToString(), out var boolValue)
            ? boolValue
            : null;
}

/// <summary>
///     Patreon goal data
/// </summary>
public class PatreonGoalData : PatreonResource
{
    /// <summary>
    ///     Target amount in cents for this goal
    /// </summary>
    public int? AmountCents => GetIntAttribute("amount_cents");

    /// <summary>
    ///     Percentage of completion (0-100)
    /// </summary>
    public int? CompletedPercentage => GetIntAttribute("completed_percentage");

    /// <summary>
    ///     ISO datetime when the goal was created
    /// </summary>
    public string? CreatedAt => GetStringAttribute("created_at");

    /// <summary>
    ///     Description of what the goal will achieve
    /// </summary>
    public string? Description => GetStringAttribute("description");

    /// <summary>
    ///     ISO datetime when the goal was reached (null if not reached)
    /// </summary>
    public string? ReachedAt => GetStringAttribute("reached_at");

    /// <summary>
    ///     Title/name of the goal
    /// </summary>
    public string? Title => GetStringAttribute("title");

    private string? GetStringAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true ? value?.ToString() : null;

    private int? GetIntAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true && int.TryParse(value?.ToString(), out var intValue)
            ? intValue
            : null;
}

/// <summary>
///     Patreon user data
/// </summary>
public class PatreonUser : PatreonResource
{
    /// <summary>
    ///     About section from the user's profile
    /// </summary>
    public string? About => GetStringAttribute("about");

    /// <summary>
    ///     ISO datetime when the user account was created
    /// </summary>
    public string? Created => GetStringAttribute("created");

    /// <summary>
    ///     Email address of the user
    /// </summary>
    public string? Email => GetStringAttribute("email");

    /// <summary>
    ///     First name of the user
    /// </summary>
    public string? FirstName => GetStringAttribute("first_name");

    /// <summary>
    ///     Full name of the user
    /// </summary>
    public string? FullName => GetStringAttribute("full_name");

    /// <summary>
    ///     Profile image URL of the user
    /// </summary>
    public string? ImageUrl => GetStringAttribute("image_url");

    /// <summary>
    ///     Last name of the user
    /// </summary>
    public string? LastName => GetStringAttribute("last_name");

    /// <summary>
    ///     Social media connections data
    /// </summary>
    public string? SocialConnections => GetStringAttribute("social_connections");

    /// <summary>
    ///     Thumbnail image URL of the user
    /// </summary>
    public string? ThumbUrl => GetStringAttribute("thumb_url");

    /// <summary>
    ///     Public profile URL of the user
    /// </summary>
    public string? Url => GetStringAttribute("url");

    /// <summary>
    ///     Vanity URL/username of the user
    /// </summary>
    public string? Vanity => GetStringAttribute("vanity");

    private string? GetStringAttribute(string key) =>
        Attributes?.TryGetValue(key, out var value) == true ? value?.ToString() : null;
}

/// <summary>
///     Patreon relationship data
/// </summary>
public class PatreonRelationship
{
    /// <summary>
    ///     Related resource data or reference
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }

    /// <summary>
    ///     Links related to this relationship
    /// </summary>
    [JsonPropertyName("links")]
    public PatreonLinks? Links { get; set; }
}

/// <summary>
///     Patreon links
/// </summary>
public class PatreonLinks
{
    /// <summary>
    ///     Link to the next page of results
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }

    /// <summary>
    ///     Link to the first page of results
    /// </summary>
    [JsonPropertyName("first")]
    public string? First { get; set; }

    /// <summary>
    ///     Link to related resources
    /// </summary>
    [JsonPropertyName("related")]
    public string? Related { get; set; }
}

/// <summary>
///     Patreon metadata
/// </summary>
public class PatreonMeta
{
    /// <summary>
    ///     Pagination information for the response
    /// </summary>
    [JsonPropertyName("pagination")]
    public PatreonPagination? Pagination { get; set; }
}

/// <summary>
///     Patreon pagination info
/// </summary>
public class PatreonPagination
{
    /// <summary>
    ///     Cursor information for pagination
    /// </summary>
    [JsonPropertyName("cursors")]
    public PatreonCursors? Cursors { get; set; }

    /// <summary>
    ///     Total number of items available
    /// </summary>
    [JsonPropertyName("total")]
    public int? Total { get; set; }
}

/// <summary>
///     Patreon cursors for pagination
/// </summary>
public class PatreonCursors
{
    /// <summary>
    ///     Cursor for the next page
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }
}

/// <summary>
///     OAuth token response
/// </summary>
public class PatreonTokenResponse
{
    /// <summary>
    ///     Access token for API requests
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    /// <summary>
    ///     Refresh token for getting new access tokens
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = null!;

    /// <summary>
    ///     Number of seconds until the access token expires
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    ///     Scope of permissions granted
    /// </summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = null!;

    /// <summary>
    ///     Type of token (usually "Bearer")
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;
}

/// <summary>
///     Patreon API error response
/// </summary>
public class PatreonErrorResponse
{
    /// <summary>
    ///     List of errors returned by the API
    /// </summary>
    [JsonPropertyName("errors")]
    public List<PatreonError>? Errors { get; set; }
}

/// <summary>
///     Individual Patreon error
/// </summary>
public class PatreonError
{
    /// <summary>
    ///     Error code number
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    /// <summary>
    ///     Human readable error code name
    /// </summary>
    [JsonPropertyName("code_name")]
    public string? CodeName { get; set; }

    /// <summary>
    ///     Detailed error description
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    ///     Unique identifier for this error instance
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    ///     HTTP status code
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    ///     Short error title
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }
}