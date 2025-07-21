using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     The record of whether or not a patron has been delivered the benefit they are owed because of their member tier
/// </summary>
public class Deliverable : PatreonResource
{
    /// <summary>
    ///     Deliverable attributes containing all the deliverable's properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public DeliverableAttributes Attributes { get; set; } = new();

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public DeliverableRelationships? Relationships { get; set; }
}

/// <summary>
///     All attributes for a Patreon deliverable
/// </summary>
public class DeliverableAttributes
{
    /// <summary>
    ///     When the creator marked the deliverable as completed or fulfilled to the patron. Can be null.
    /// </summary>
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     One of delivered, not_delivered, wont_deliver
    /// </summary>
    [JsonPropertyName("delivery_status")]
    public string? DeliveryStatus { get; set; }

    /// <summary>
    ///     When the deliverable is due to the patron
    /// </summary>
    [JsonPropertyName("due_at")]
    public DateTime? DueAt { get; set; }
}

/// <summary>
///     Deliverable relationships to other resources
/// </summary>
public class DeliverableRelationships
{
    /// <summary>
    ///     The Benefit the Deliverables were generated for
    /// </summary>
    [JsonPropertyName("benefit")]
    public SingleRelationship? Benefit { get; set; }

    /// <summary>
    ///     The Campaign the Deliverables were generated for
    /// </summary>
    [JsonPropertyName("campaign")]
    public SingleRelationship? Campaign { get; set; }

    /// <summary>
    ///     The member who has been granted the deliverable
    /// </summary>
    [JsonPropertyName("member")]
    public SingleRelationship? Member { get; set; }

    /// <summary>
    ///     The user who has been granted the deliverable. This user is the same as the member user.
    /// </summary>
    [JsonPropertyName("user")]
    public SingleRelationship? User { get; set; }
}

/// <summary>
///     A file uploaded to patreon.com, usually an image
/// </summary>
public class Media : PatreonResource
{
    /// <summary>
    ///     Media attributes containing all the media's properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public MediaAttributes Attributes { get; set; } = new();
}

/// <summary>
///     All attributes for a Patreon media file
/// </summary>
public class MediaAttributes
{
    /// <summary>
    ///     When the file was created
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    ///     The URL to download this media. Valid for 24 hours.
    /// </summary>
    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }

    /// <summary>
    ///     File name
    /// </summary>
    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    /// <summary>
    ///     The resized image URLs for this media. Valid for 2 weeks.
    /// </summary>
    [JsonPropertyName("image_urls")]
    public object? ImageUrls { get; set; }

    /// <summary>
    ///     Metadata related to the file. Can be null.
    /// </summary>
    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }

    /// <summary>
    ///     Mimetype of uploaded file, eg: "application/jpeg"
    /// </summary>
    [JsonPropertyName("mimetype")]
    public string? Mimetype { get; set; }

    /// <summary>
    ///     Ownership id (See also owner_type)
    /// </summary>
    [JsonPropertyName("owner_id")]
    public string? OwnerId { get; set; }

    /// <summary>
    ///     Ownership relationship type for multi-relationship medias
    /// </summary>
    [JsonPropertyName("owner_relationship")]
    public string? OwnerRelationship { get; set; }

    /// <summary>
    ///     Type of the resource that owns the file
    /// </summary>
    [JsonPropertyName("owner_type")]
    public string? OwnerType { get; set; }

    /// <summary>
    ///     Size of file in bytes
    /// </summary>
    [JsonPropertyName("size_bytes")]
    public int? SizeBytes { get; set; }

    /// <summary>
    ///     The state of the file
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    ///     When the upload URL expires
    /// </summary>
    [JsonPropertyName("upload_expires_at")]
    public DateTime? UploadExpiresAt { get; set; }

    /// <summary>
    ///     All the parameters that have to be added to the upload form request
    /// </summary>
    [JsonPropertyName("upload_parameters")]
    public object? UploadParameters { get; set; }

    /// <summary>
    ///     The URL to perform a POST request to in order to upload the media file
    /// </summary>
    [JsonPropertyName("upload_url")]
    public string? UploadUrl { get; set; }
}

/// <summary>
///     Webhooks are fired based on events happening on a particular campaign
/// </summary>
public class Webhook : PatreonResource
{
    /// <summary>
    ///     Webhook attributes containing all the webhook's properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public WebhookAttributes Attributes { get; set; } = new();

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public WebhookRelationships? Relationships { get; set; }
}

/// <summary>
///     All attributes for a Patreon webhook
/// </summary>
public class WebhookAttributes
{
    /// <summary>
    ///     Last date that the webhook was attempted or used
    /// </summary>
    [JsonPropertyName("last_attempted_at")]
    public DateTime? LastAttemptedAt { get; set; }

    /// <summary>
    ///     Number of times the webhook has failed consecutively, when in an error state
    /// </summary>
    [JsonPropertyName("num_consecutive_times_failed")]
    public int? NumConsecutiveTimesFailed { get; set; }

    /// <summary>
    ///     True if the webhook is paused as a result of repeated failed attempts to post to uri. Set to false to attempt to
    ///     re-enable a previously failing webhook.
    /// </summary>
    [JsonPropertyName("paused")]
    public bool? Paused { get; set; }

    /// <summary>
    ///     Secret used to sign your webhook message body, so you can validate authenticity upon receipt
    /// </summary>
    [JsonPropertyName("secret")]
    public string? Secret { get; set; }

    /// <summary>
    ///     List of events that will trigger this webhook
    /// </summary>
    [JsonPropertyName("triggers")]
    public List<string>? Triggers { get; set; }

    /// <summary>
    ///     Fully qualified uri where webhook will be sent (e.g. https://www.example.com/webhooks/incoming)
    /// </summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
}

/// <summary>
///     Webhook relationships to other resources
/// </summary>
public class WebhookRelationships
{
    /// <summary>
    ///     The campaign whose events trigger the webhook
    /// </summary>
    [JsonPropertyName("campaign")]
    public SingleRelationship? Campaign { get; set; }

    /// <summary>
    ///     The client which created the webhook
    /// </summary>
    [JsonPropertyName("client")]
    public SingleRelationship? Client { get; set; }
}

/// <summary>
///     Constants for webhook trigger types
/// </summary>
public static class WebhookTriggers
{
    /// <summary>
    ///     Triggered when a new member is created
    /// </summary>
    public const string MembersCreate = "members:create";

    /// <summary>
    ///     Triggered when the membership information is changed
    /// </summary>
    public const string MembersUpdate = "members:update";

    /// <summary>
    ///     Triggered when a membership is deleted
    /// </summary>
    public const string MembersDelete = "members:delete";

    /// <summary>
    ///     Triggered when a new pledge is created for a member
    /// </summary>
    public const string MembersPledgeCreate = "members:pledge:create";

    /// <summary>
    ///     Triggered when a member updates their pledge
    /// </summary>
    public const string MembersPledgeUpdate = "members:pledge:update";

    /// <summary>
    ///     Triggered when a member deletes their pledge
    /// </summary>
    public const string MembersPledgeDelete = "members:pledge:delete";

    /// <summary>
    ///     Triggered when a post is published on a campaign
    /// </summary>
    public const string PostsPublish = "posts:publish";

    /// <summary>
    ///     Triggered when a post is updated on a campaign
    /// </summary>
    public const string PostsUpdate = "posts:update";

    /// <summary>
    ///     Triggered when a post is deleted on a campaign
    /// </summary>
    public const string PostsDelete = "posts:delete";
}

/// <summary>
///     Constants for deliverable delivery status
/// </summary>
public static class DeliveryStatus
{
    /// <summary>
    ///     Deliverable has been delivered
    /// </summary>
    public const string Delivered = "delivered";

    /// <summary>
    ///     Deliverable has not been delivered
    /// </summary>
    public const string NotDelivered = "not_delivered";

    /// <summary>
    ///     Deliverable will not be delivered
    /// </summary>
    public const string WontDeliver = "wont_deliver";
}