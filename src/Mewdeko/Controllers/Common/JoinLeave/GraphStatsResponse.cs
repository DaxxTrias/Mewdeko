namespace Mewdeko.Controllers.Common.JoinLeave;

/// <summary>
///     Response model containing join/leave graph and statistics data
/// </summary>
public class GraphStatsResponse
{
    /// <summary>
    ///     Data points for each day in the graph
    /// </summary>
    public List<DailyStatDto> DailyStats { get; set; }

    /// <summary>
    ///     Summary statistics for the timespan
    /// </summary>
    public GraphSummaryDto Summary { get; set; }
}