using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     A patron's shipping address
/// </summary>
public class Address : PatreonResource
{
    /// <summary>
    ///     Address attributes containing all the address properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public AddressAttributes Attributes { get; set; } = new();

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public AddressRelationships? Relationships { get; set; }
}

/// <summary>
///     All attributes for a Patreon address
/// </summary>
public class AddressAttributes
{
    /// <summary>
    ///     Full recipient name. Can be null.
    /// </summary>
    [JsonPropertyName("addressee")]
    public string? Addressee { get; set; }

    /// <summary>
    ///     City
    /// </summary>
    [JsonPropertyName("city")]
    public string? City { get; set; }

    /// <summary>
    ///     Country
    /// </summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>
    ///     Datetime address was first created
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    ///     First line of street address. Can be null.
    /// </summary>
    [JsonPropertyName("line_1")]
    public string? Line1 { get; set; }

    /// <summary>
    ///     Second line of street address. Can be null.
    /// </summary>
    [JsonPropertyName("line_2")]
    public string? Line2 { get; set; }

    /// <summary>
    ///     Telephone number. Specified for non-US addresses. Can be null.
    /// </summary>
    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }

    /// <summary>
    ///     Postal or zip code. Can be null.
    /// </summary>
    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; set; }

    /// <summary>
    ///     State or province name. Can be null.
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }
}

/// <summary>
///     Address relationships to other resources
/// </summary>
public class AddressRelationships
{
    /// <summary>
    ///     The campaigns that have access to the address
    /// </summary>
    [JsonPropertyName("campaigns")]
    public MultipleRelationship? Campaigns { get; set; }

    /// <summary>
    ///     The user this address belongs to
    /// </summary>
    [JsonPropertyName("user")]
    public SingleRelationship? User { get; set; }
}