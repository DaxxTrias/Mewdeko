namespace Mewdeko.Controllers.Common.Logging;

/// <summary>
///     Model for mapping a log type to a channel
/// </summary>
public class LogTypeMapping
{
    /// <summary>
    ///     The log type name
    /// </summary>
    public string LogType { get; set; } = string.Empty;

    /// <summary>
    ///     The channel ID to assign (null to disable)
    /// </summary>
    public ulong? ChannelId { get; set; }
}