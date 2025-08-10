namespace Mewdeko.Modules.Reputation.Common;

/// <summary>
///     Event type constants for reputation events.
/// </summary>
public static class RepEventType
{
    /// <summary>
    ///     Happy hour event (2x multiplier during specific times).
    /// </summary>
    public const string HappyHour = "happy_hour";

    /// <summary>
    ///     Weekend bonus event.
    /// </summary>
    public const string WeekendBonus = "weekend_bonus";

    /// <summary>
    ///     Seasonal/holiday event.
    /// </summary>
    public const string Seasonal = "seasonal";

    /// <summary>
    ///     First-of-month bonus.
    /// </summary>
    public const string MonthlyBonus = "monthly_bonus";

    /// <summary>
    ///     Custom time-based event.
    /// </summary>
    public const string Custom = "custom";
}