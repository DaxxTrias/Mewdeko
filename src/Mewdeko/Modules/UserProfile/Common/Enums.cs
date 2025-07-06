namespace Mewdeko.Modules.UserProfile.Common;

/// <summary>
///     Specifies the display mode for birthdays.
/// </summary>
public enum BirthdayDisplayModeEnum
{
    /// <summary>
    ///     Default display mode.
    /// </summary>
    Default,

    /// <summary>
    ///     Display only the month.
    /// </summary>
    MonthOnly,

    /// <summary>
    ///     Display only the year.
    /// </summary>
    YearOnly,

    /// <summary>
    ///     Display both the month and date.
    /// </summary>
    MonthAndDate,

    /// <summary>
    ///     Birthday display is disabled.
    /// </summary>
    Disabled
}

/// <summary>
///     Specifies the privacy level for user profiles.
/// </summary>
public enum ProfilePrivacyEnum
{
    /// <summary>
    ///     Marks the profile as viewable by everyone.
    /// </summary>
    Public = 0,

    /// <summary>
    ///     Makes it so only the user who owns the profile can view the profile
    /// </summary>
    Private = 1
}