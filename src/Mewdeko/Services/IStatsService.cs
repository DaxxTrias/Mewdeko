namespace Mewdeko.Services;

/// <summary>
///     Interface for a service responsible for providing various statistics.
/// </summary>
public interface IStatsService : INService
{
    /// <summary>
    ///     Gets a string representing the current heap usage statistics.
    /// </summary>
    public string Heap { get; }

    /// <summary>
    ///     Gets a string representing the library information.
    /// </summary>
    public string Library { get; }

    /// <summary>
    ///     Gets a formatted uptime string.
    /// </summary>
    /// <param name="separator">Optional separator to be used between different components of the uptime string.</param>
    /// <returns>A string representing the uptime.</returns>
    public string GetUptimeString(string separator = ", ");
}