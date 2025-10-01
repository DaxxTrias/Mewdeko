namespace Mewdeko.Controllers.Common.Repeaters;

/// <summary>
///     Response model for repeater statistics
/// </summary>
public class RepeaterStatsResponse
{
    /// <summary>
    ///     Total number of repeaters in the guild
    /// </summary>
    public int TotalRepeaters { get; set; }

    /// <summary>
    ///     Number of active (enabled) repeaters
    /// </summary>
    public int ActiveRepeaters { get; set; }

    /// <summary>
    ///     Number of disabled repeaters
    /// </summary>
    public int DisabledRepeaters { get; set; }

    /// <summary>
    ///     Total number of times all repeaters have been displayed
    /// </summary>
    public int TotalDisplays { get; set; }

    /// <summary>
    ///     Number of repeaters using each trigger mode
    /// </summary>
    public Dictionary<string, int> TriggerModeDistribution { get; set; } = new();

    /// <summary>
    ///     Most active repeater by display count
    /// </summary>
    public RepeaterResponse? MostActiveRepeater { get; set; }

    /// <summary>
    ///     Number of repeaters with time-based scheduling
    /// </summary>
    public int TimeScheduledRepeaters { get; set; }

    /// <summary>
    ///     Number of repeaters with conversation detection enabled
    /// </summary>
    public int ConversationAwareRepeaters { get; set; }
}