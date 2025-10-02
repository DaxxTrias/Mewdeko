using System.Text.Json.Serialization;

namespace Mewdeko.Controllers.Common.ClientOperations;

/// <summary>
///     Used for getting a specific channel type in the api
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChannelType
{
    /// <summary>
    ///     For text channels
    /// </summary>
    Text,

    /// <summary>
    ///     For voice channels
    /// </summary>
    Voice,

    /// <summary>
    ///     For category channels
    /// </summary>
    Category,

    /// <summary>
    ///     FOr announcement channels
    /// </summary>
    Announcement,

    /// <summary>
    ///     For forum channels
    /// </summary>
    Forum,

    /// <summary>
    ///     None
    /// </summary>
    None
}