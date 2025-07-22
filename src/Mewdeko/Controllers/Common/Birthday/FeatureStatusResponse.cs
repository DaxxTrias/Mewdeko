namespace Mewdeko.Controllers.Common.Birthday;

/// <summary>
///     Response model for feature status.
/// </summary>
public class FeatureStatusResponse
{
    /// <summary>
    ///     Dictionary of feature names and their enabled status.
    /// </summary>
    public Dictionary<string, bool> Features { get; set; } = new();
}