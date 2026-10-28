using DataModel;

namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     Result of adding a watched name to anti-pattern protection.
/// </summary>
public class AddPatternNameResult
{
    /// <summary>
    ///     Gets the result type of the operation.
    /// </summary>
    public AddPatternNameResultType Result { get; init; }

    /// <summary>
    ///     Gets the name that was added, when successful.
    /// </summary>
    public AntiPatternName? AddedName { get; init; }

    /// <summary>
    ///     Gets the matching existing name, when the normalized name is already listed.
    /// </summary>
    public AntiPatternName? ExistingName { get; init; }

    /// <summary>
    ///     Gets the normalized key generated from the requested name.
    /// </summary>
    public string NormalizedName { get; init; } = "";
}

/// <summary>
///     Type of result from adding an anti-pattern watched name.
/// </summary>
public enum AddPatternNameResultType
{
    /// <summary>
    ///     The watched name was added.
    /// </summary>
    Success,

    /// <summary>
    ///     Neither username nor display-name checks were enabled.
    /// </summary>
    NoMatchingTargets,

    /// <summary>
    ///     The normalized name is too short to be matched safely.
    /// </summary>
    NormalizedNameTooShort,

    /// <summary>
    ///     Anti-pattern protection has not been enabled for the guild.
    /// </summary>
    AntiPatternNotEnabled,

    /// <summary>
    ///     The normalized name is already configured.
    /// </summary>
    AlreadyExists
}
