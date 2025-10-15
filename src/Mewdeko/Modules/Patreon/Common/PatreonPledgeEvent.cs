using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     The record of a pledging action taken by the user, or that action's failure
/// </summary>
public class PledgeEvent : PatreonResource
{
    /// <summary>
    ///     PledgeEvent attributes containing all the pledge event's properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public PledgeEventAttributes Attributes { get; set; } = new();

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public PledgeEventRelationships? Relationships { get; set; }
}

/// <summary>
///     All attributes for a Patreon pledge event
/// </summary>
public class PledgeEventAttributes
{
    /// <summary>
    ///     Amount (in the currency in which the patron paid) of the underlying event
    /// </summary>
    [JsonPropertyName("amount_cents")]
    public int? AmountCents { get; set; }

    /// <summary>
    ///     ISO code of the currency of the event
    /// </summary>
    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; set; }

    /// <summary>
    ///     The date which this event occurred
    /// </summary>
    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    /// <summary>
    ///     The payment status of the pledge. One of queued, pending, valid, declined, fraud, disabled.
    /// </summary>
    [JsonPropertyName("pledge_payment_status")]
    public string? PledgePaymentStatus { get; set; }

    /// <summary>
    ///     Status of underlying payment. One of Paid, Declined, Deleted, Pending, Refunded, Fraud, Refunded by Patreon, Other,
    ///     Partially Refunded, Free Trial
    /// </summary>
    [JsonPropertyName("payment_status")]
    public string? PaymentStatus { get; set; }

    /// <summary>
    ///     Id of the tier associated with the pledge. Can be null.
    /// </summary>
    [JsonPropertyName("tier_id")]
    public string? TierId { get; set; }

    /// <summary>
    ///     Title of the reward tier associated with the pledge. Can be null.
    /// </summary>
    [JsonPropertyName("tier_title")]
    public string? TierTitle { get; set; }

    /// <summary>
    ///     Event type. One of pledge_start, pledge_upgrade, pledge_downgrade, pledge_delete, subscription
    /// </summary>
    [JsonPropertyName("type")]
    public string? EventType { get; set; }
}

/// <summary>
///     PledgeEvent relationships to other resources
/// </summary>
public class PledgeEventRelationships
{
    /// <summary>
    ///     The campaign being pledged to
    /// </summary>
    [JsonPropertyName("campaign")]
    public SingleRelationship? Campaign { get; set; }

    /// <summary>
    ///     The pledging user
    /// </summary>
    [JsonPropertyName("patron")]
    public SingleRelationship? Patron { get; set; }

    /// <summary>
    ///     The tier associated with this pledge event
    /// </summary>
    [JsonPropertyName("tier")]
    public SingleRelationship? Tier { get; set; }
}

/// <summary>
///     Constants for pledge payment status values
/// </summary>
public static class PledgePaymentStatus
{
    /// <summary>
    ///     Payment is queued
    /// </summary>
    public const string Queued = "queued";

    /// <summary>
    ///     Payment is pending
    /// </summary>
    public const string Pending = "pending";

    /// <summary>
    ///     Payment is valid
    /// </summary>
    public const string Valid = "valid";

    /// <summary>
    ///     Payment was declined
    /// </summary>
    public const string Declined = "declined";

    /// <summary>
    ///     Payment was marked as fraud
    /// </summary>
    public const string Fraud = "fraud";

    /// <summary>
    ///     Payment is disabled
    /// </summary>
    public const string Disabled = "disabled";
}

/// <summary>
///     Constants for payment status values
/// </summary>
public static class PaymentStatus
{
    /// <summary>
    ///     Payment was successful
    /// </summary>
    public const string Paid = "Paid";

    /// <summary>
    ///     Payment was declined
    /// </summary>
    public const string Declined = "Declined";

    /// <summary>
    ///     Payment was deleted
    /// </summary>
    public const string Deleted = "Deleted";

    /// <summary>
    ///     Payment is pending
    /// </summary>
    public const string Pending = "Pending";

    /// <summary>
    ///     Payment was refunded
    /// </summary>
    public const string Refunded = "Refunded";

    /// <summary>
    ///     Payment was marked as fraud
    /// </summary>
    public const string Fraud = "Fraud";

    /// <summary>
    ///     Payment was refunded by Patreon
    /// </summary>
    public const string RefundedByPatreon = "Refunded by Patreon";

    /// <summary>
    ///     Other payment status
    /// </summary>
    public const string Other = "Other";

    /// <summary>
    ///     Payment was partially refunded
    /// </summary>
    public const string PartiallyRefunded = "Partially Refunded";

    /// <summary>
    ///     Member is in free trial
    /// </summary>
    public const string FreeTrial = "Free Trial";
}

/// <summary>
///     Constants for pledge event types
/// </summary>
public static class PledgeEventType
{
    /// <summary>
    ///     Pledge was started
    /// </summary>
    public const string PledgeStart = "pledge_start";

    /// <summary>
    ///     Pledge was upgraded
    /// </summary>
    public const string PledgeUpgrade = "pledge_upgrade";

    /// <summary>
    ///     Pledge was downgraded
    /// </summary>
    public const string PledgeDowngrade = "pledge_downgrade";

    /// <summary>
    ///     Pledge was deleted
    /// </summary>
    public const string PledgeDelete = "pledge_delete";

    /// <summary>
    ///     Subscription event
    /// </summary>
    public const string Subscription = "subscription";
}