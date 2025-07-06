namespace Mewdeko.Controllers.Common.JoinLeave;

/// <summary>
///     Single data point for a specific day
/// </summary>
public class DailyStatDto
{
    /// <summary>
    ///     The date of this data point
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    ///     Number of events on this date
    /// </summary>
    public int Count { get; set; }
}