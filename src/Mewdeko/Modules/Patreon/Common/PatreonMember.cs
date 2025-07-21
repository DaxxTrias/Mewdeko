using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     The record of a user's membership to a campaign. Remains consistent across months of pledging.
/// </summary>
public class Member : PatreonResource
{
    /// <summary>
    ///     Member attributes containing all the member's properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public MemberAttributes Attributes { get; set; } = new();

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public MemberRelationships? Relationships { get; set; }
}

/// <summary>
///     All attributes for a Patreon member
/// </summary>
public class MemberAttributes
{
    /// <summary>
    ///     The total amount that the member has ever paid to the campaign in the campaign's currency. 0 if never paid.
    /// </summary>
    [JsonPropertyName("campaign_lifetime_support_cents")]
    public int? CampaignLifetimeSupportCents { get; set; }

    /// <summary>
    ///     The amount in cents that the member is entitled to. This includes a current pledge, or payment that covers the
    ///     current payment period.
    /// </summary>
    [JsonPropertyName("currently_entitled_amount_cents")]
    public int? CurrentlyEntitledAmountCents { get; set; }

    /// <summary>
    ///     The member's email address. Requires the campaigns.members[email] scope. Members may restrict the sharing of their
    ///     email address.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    ///     Full name of the member user
    /// </summary>
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    /// <summary>
    ///     The user is in a free trial period
    /// </summary>
    [JsonPropertyName("is_free_trial")]
    public bool? IsFreeTrial { get; set; }

    /// <summary>
    ///     The user's membership is from a free gift
    /// </summary>
    [JsonPropertyName("is_gifted")]
    public bool? IsGifted { get; set; }

    /// <summary>
    ///     Datetime of last attempted charge. null if never charged. Can be null.
    /// </summary>
    [JsonPropertyName("last_charge_date")]
    public DateTime? LastChargeDate { get; set; }

    /// <summary>
    ///     The result of the last attempted charge. The only successful status is Paid. null if never charged.
    ///     One of Paid, Declined, Deleted, Pending, Refunded, Fraud, Refunded by Patreon, Other, Partially Refunded, Free
    ///     Trial. Can be null.
    /// </summary>
    [JsonPropertyName("last_charge_status")]
    public string? LastChargeStatus { get; set; }

    /// <summary>
    ///     Datetime of next charge. null if annual pledge downgrade. Can be null.
    /// </summary>
    [JsonPropertyName("next_charge_date")]
    public DateTime? NextChargeDate { get; set; }

    /// <summary>
    ///     The creator's notes on the member
    /// </summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>
    ///     One of active_patron, declined_patron, former_patron. A null value indicates the member has never pledged. Can be
    ///     null.
    /// </summary>
    [JsonPropertyName("patron_status")]
    public string? PatronStatus { get; set; }

    /// <summary>
    ///     Number of months between charges. Can be null.
    /// </summary>
    [JsonPropertyName("pledge_cadence")]
    public int? PledgeCadence { get; set; }

    /// <summary>
    ///     Datetime of beginning of most recent pledge chain from this member to the campaign. Pledge updates do not change
    ///     this value. Can be null.
    /// </summary>
    [JsonPropertyName("pledge_relationship_start")]
    public DateTime? PledgeRelationshipStart { get; set; }

    /// <summary>
    ///     The amount in cents the user will pay at the next pay cycle
    /// </summary>
    [JsonPropertyName("will_pay_amount_cents")]
    public int? WillPayAmountCents { get; set; }
}

/// <summary>
///     Member relationships to other resources
/// </summary>
public class MemberRelationships
{
    /// <summary>
    ///     The member's shipping address that they entered for the campaign. Requires the campaign.members.address scope.
    /// </summary>
    [JsonPropertyName("address")]
    public SingleRelationship? Address { get; set; }

    /// <summary>
    ///     The campaign that the membership is for
    /// </summary>
    [JsonPropertyName("campaign")]
    public SingleRelationship? Campaign { get; set; }

    /// <summary>
    ///     The tiers that the member is entitled to. This includes a current pledge, or payment that covers the current
    ///     payment period.
    /// </summary>
    [JsonPropertyName("currently_entitled_tiers")]
    public MultipleRelationship? CurrentlyEntitledTiers { get; set; }

    /// <summary>
    ///     The pledge history of the member
    /// </summary>
    [JsonPropertyName("pledge_history")]
    public MultipleRelationship? PledgeHistory { get; set; }

    /// <summary>
    ///     The user who is pledging to the campaign
    /// </summary>
    [JsonPropertyName("user")]
    public SingleRelationship? User { get; set; }
}

/// <summary>
///     Enum for patron status values
/// </summary>
public static class PatronStatus
{
    /// <summary>
    ///     Member is an active patron
    /// </summary>
    public const string ActivePatron = "active_patron";

    /// <summary>
    ///     Member's payment was declined
    /// </summary>
    public const string DeclinedPatron = "declined_patron";

    /// <summary>
    ///     Member is a former patron
    /// </summary>
    public const string FormerPatron = "former_patron";
}

/// <summary>
///     Enum for last charge status values
/// </summary>
public static class LastChargeStatus
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