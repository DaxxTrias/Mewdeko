namespace Mewdeko.Controllers.Common.UserSettings;

/// <summary>
///     Request model for setting AFK status
/// </summary>
public class AfkRequest
{
    /// <summary>
    ///     AFK message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     Whether this is a timed AFK
    /// </summary>
    public bool IsTimed { get; set; }

    /// <summary>
    ///     When the timed AFK expires
    /// </summary>
    public DateTime? Until { get; set; }
}