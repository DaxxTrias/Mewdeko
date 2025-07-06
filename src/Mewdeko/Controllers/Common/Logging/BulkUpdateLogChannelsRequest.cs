namespace Mewdeko.Controllers.Common.Logging;

/// <summary>
///     Request model for bulk updating log channels
/// </summary>
public class BulkUpdateLogChannelsRequest
{
    /// <summary>
    ///     List of log type to channel mappings
    /// </summary>
    public List<LogTypeMapping> LogTypeMappings { get; set; } = new();
}