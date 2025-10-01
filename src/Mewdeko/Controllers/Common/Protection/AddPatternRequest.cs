namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for adding a pattern to anti-pattern protection
/// </summary>
public class AddPatternRequest
{
    /// <summary>
    ///     The regex pattern to match against usernames/display names
    /// </summary>
    public required string Pattern { get; set; }

    /// <summary>
    ///     Optional name for the pattern
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Whether to check usernames against this pattern
    /// </summary>
    public bool CheckUsername { get; set; } = true;

    /// <summary>
    ///     Whether to check display names against this pattern
    /// </summary>
    public bool CheckDisplayName { get; set; } = true;
}