namespace Mewdeko.Controllers.Common.Birthday;

/// <summary>
///     Response model for user birthday information.
/// </summary>
public class BirthdayUserResponse
{
    /// <summary>
    ///     The user's Discord ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The user's Discord username.
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    ///     The user's guild nickname.
    /// </summary>
    public string? Nickname { get; set; }

    /// <summary>
    ///     The user's avatar URL.
    /// </summary>
    public string AvatarUrl { get; set; } = null!;

    /// <summary>
    ///     The user's birthday date.
    /// </summary>
    public DateTime? Birthday { get; set; }

    /// <summary>
    ///     The user's birthday display mode.
    /// </summary>
    public int BirthdayDisplayMode { get; set; }

    /// <summary>
    ///     Whether the user has birthday announcements enabled.
    /// </summary>
    public bool BirthdayAnnouncementsEnabled { get; set; }

    /// <summary>
    ///     The user's timezone for birthday calculations.
    /// </summary>
    public string? BirthdayTimezone { get; set; }

    /// <summary>
    ///     Days until birthday (null if not applicable).
    /// </summary>
    public int? DaysUntil { get; set; }
}