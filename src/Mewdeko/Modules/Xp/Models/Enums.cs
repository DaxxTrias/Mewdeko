namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Specifies the direction of the XP template bar.
/// </summary>
public enum XpTemplateDirection
{
    /// <summary>
    ///     Up
    /// </summary>
    Up,

    /// <summary>
    ///     Down
    /// </summary>
    Down,

    /// <summary>
    ///     Left
    /// </summary>
    Left,

    /// <summary>
    ///     Right
    /// </summary>
    Right
}

/// <summary>
///     Specifies the type of XP curve to use for calculating levels.
/// </summary>
public enum XpCurveType
{
    /// <summary>
    ///     Standard curve (default).
    /// </summary>
    Standard = 0,

    /// <summary>
    ///     Linear curve (consistent level-up requirements).
    /// </summary>
    Linear = 1,

    /// <summary>
    ///     Accelerated curve (faster early levels, steeper later levels).
    /// </summary>
    Accelerated = 2,

    /// <summary>
    ///     Decelerated curve (slower early levels, more gradual later levels).
    /// </summary>
    Decelerated = 3,

    /// <summary>
    ///     Custom curve defined by formula.
    /// </summary>
    Custom = 4
}

/// <summary>
///     Specifies the type of item excluded from XP gain.
/// </summary>
public enum ExcludedItemType
{
    /// <summary>
    ///     A channel is excluded.
    /// </summary>
    Channel = 0,

    /// <summary>
    ///     A role is excluded.
    /// </summary>
    Role = 1,

    /// <summary>
    ///     A user is excluded.
    /// </summary>
    User = 2
}
