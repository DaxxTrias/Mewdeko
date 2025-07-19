using System.Text.Json.Serialization;

namespace Mewdeko.Controllers.Common.ClientOperations;

/// <summary>
///     To avoid stupid errors
/// </summary>
public class NeededRoleInfo
{
    /// <summary>
    ///     Name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    ///     And badge number
    /// </summary>
    [JsonPropertyName("id")]
    public ulong Id { get; set; }
}