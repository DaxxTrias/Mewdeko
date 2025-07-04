namespace Mewdeko.Controllers.Common.JoinLeave;

/// <summary>
///     Summary statistics for the graph period
/// </summary>
public class GraphSummaryDto
{
    /// <summary>
    ///     Total number of events in the period
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     Average events per day
    /// </summary>
    public double Average { get; set; }

    /// <summary>
    ///     Date with highest number of events
    /// </summary>
    public DateTime PeakDate { get; set; }

    /// <summary>
    ///     Number of events on the peak date
    /// </summary>
    public int PeakCount { get; set; }
}