using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     Base class for all Patreon API resources following JSON:API specification
/// </summary>
public abstract class PatreonResource
{
    /// <summary>
    ///     The unique identifier for this resource
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     The type of resource (e.g., "user", "campaign", "member")
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
///     Generic JSON:API response wrapper for Patreon API responses
/// </summary>
/// <typeparam name="T">The data type being returned</typeparam>
public class PatreonResponse<T>
{
    /// <summary>
    ///     The primary data for this request
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>
    ///     Related resources that are included in the response
    /// </summary>
    [JsonPropertyName("included")]
    public List<PatreonResource>? Included { get; set; }

    /// <summary>
    ///     Links for pagination and navigation
    /// </summary>
    [JsonPropertyName("links")]
    public PatreonLinks? Links { get; set; }

    /// <summary>
    ///     Metadata about the response including pagination information
    /// </summary>
    [JsonPropertyName("meta")]
    public PatreonMeta? Meta { get; set; }
}

/// <summary>
///     Links object containing pagination and navigation URLs
/// </summary>
public class PatreonLinks
{
    /// <summary>
    ///     URL to the current resource
    /// </summary>
    [JsonPropertyName("self")]
    public string? Self { get; set; }

    /// <summary>
    ///     URL to the first page of results
    /// </summary>
    [JsonPropertyName("first")]
    public string? First { get; set; }

    /// <summary>
    ///     URL to the previous page of results
    /// </summary>
    [JsonPropertyName("prev")]
    public string? Previous { get; set; }

    /// <summary>
    ///     URL to the next page of results
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }

    /// <summary>
    ///     URL to the last page of results
    /// </summary>
    [JsonPropertyName("last")]
    public string? Last { get; set; }

    /// <summary>
    ///     URL to related resource
    /// </summary>
    [JsonPropertyName("related")]
    public string? Related { get; set; }
}

/// <summary>
///     Metadata object containing pagination and other response information
/// </summary>
public class PatreonMeta
{
    /// <summary>
    ///     Pagination information
    /// </summary>
    [JsonPropertyName("pagination")]
    public PatreonPagination? Pagination { get; set; }

    /// <summary>
    ///     Total count of results
    /// </summary>
    [JsonPropertyName("count")]
    public int? Count { get; set; }
}

/// <summary>
///     Pagination information for navigating through result sets
/// </summary>
public class PatreonPagination
{
    /// <summary>
    ///     Cursor-based pagination information
    /// </summary>
    [JsonPropertyName("cursors")]
    public PatreonCursors? Cursors { get; set; }

    /// <summary>
    ///     Total number of items across all pages
    /// </summary>
    [JsonPropertyName("total")]
    public int? Total { get; set; }
}

/// <summary>
///     Cursor pagination information
/// </summary>
public class PatreonCursors
{
    /// <summary>
    ///     Cursor for the next page
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }

    /// <summary>
    ///     Cursor for the previous page
    /// </summary>
    [JsonPropertyName("prev")]
    public string? Previous { get; set; }
}

/// <summary>
///     Resource identifier object for relationships
/// </summary>
public class ResourceIdentifier
{
    /// <summary>
    ///     The unique identifier of the related resource
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     The type of the related resource
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
///     Generic relationship object for JSON:API relationships
/// </summary>
/// <typeparam name="T">The type of data in the relationship</typeparam>
public class PatreonRelationship<T>
{
    /// <summary>
    ///     The data reference for this relationship
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>
    ///     Links related to this relationship
    /// </summary>
    [JsonPropertyName("links")]
    public PatreonLinks? Links { get; set; }
}

/// <summary>
///     Single resource relationship
/// </summary>
public class SingleRelationship : PatreonRelationship<ResourceIdentifier>
{
}

/// <summary>
///     Multiple resource relationship
/// </summary>
public class MultipleRelationship : PatreonRelationship<List<ResourceIdentifier>>
{
}