namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Represents the progress of a role synchronization operation.
/// </summary>
public class RoleSyncProgress
{
    /// <summary>
    ///     Gets or sets the current user being processed.
    /// </summary>
    public int CurrentUser { get; set; }

    /// <summary>
    ///     Gets or sets the total number of users to process.
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    ///     Gets or sets the percentage of completion.
    /// </summary>
    public double PercentComplete { get; set; }

    /// <summary>
    ///     Gets or sets the estimated time remaining for completion.
    /// </summary>
    public TimeSpan EstimatedTimeRemaining { get; set; }

    /// <summary>
    ///     Gets or sets the total number of roles added so far.
    /// </summary>
    public int RolesAdded { get; set; }

    /// <summary>
    ///     Gets or sets the total number of roles removed so far.
    /// </summary>
    public int RolesRemoved { get; set; }

    /// <summary>
    ///     Gets or sets the number of users that encountered errors.
    /// </summary>
    public int ErrorCount { get; set; }
}

/// <summary>
///     Represents the result of a complete role synchronization operation.
/// </summary>
public class RoleSyncResult
{
    /// <summary>
    ///     Gets or sets the guild ID where synchronization occurred.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the start time of the synchronization.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    ///     Gets or sets the completion time of the synchronization.
    /// </summary>
    public DateTime? CompletionTime { get; set; }

    /// <summary>
    ///     Gets or sets the estimated completion time.
    /// </summary>
    public DateTime EstimatedCompletion { get; set; }

    /// <summary>
    ///     Gets or sets the total number of users that should be processed.
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    ///     Gets or sets the number of users that were successfully processed.
    /// </summary>
    public int ProcessedUsers { get; set; }

    /// <summary>
    ///     Gets or sets the number of users that were skipped.
    /// </summary>
    public int SkippedUsers { get; set; }

    /// <summary>
    ///     Gets or sets the number of users that encountered errors.
    /// </summary>
    public int ErrorUsers { get; set; }

    /// <summary>
    ///     Gets or sets the total number of roles that were added.
    /// </summary>
    public int RolesAdded { get; set; }

    /// <summary>
    ///     Gets or sets the total number of roles that were removed.
    /// </summary>
    public int RolesRemoved { get; set; }

    /// <summary>
    ///     Gets the duration of the synchronization operation.
    /// </summary>
    public TimeSpan Duration
    {
        get
        {
            return CompletionTime?.Subtract(StartTime) ?? TimeSpan.Zero;
        }
    }
}

/// <summary>
///     Represents the result of a single user's role synchronization.
/// </summary>
public class UserRoleSyncResult
{
    /// <summary>
    ///     Gets or sets the user ID that was synchronized.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the user's current XP level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    ///     Gets or sets the number of roles that were added to the user.
    /// </summary>
    public int RolesAdded { get; set; }

    /// <summary>
    ///     Gets or sets the number of roles that were removed from the user.
    /// </summary>
    public int RolesRemoved { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the synchronization was processed successfully.
    /// </summary>
    public bool ProcessedSuccessfully { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether an error occurred during synchronization.
    /// </summary>
    public bool HasError { get; set; }

    /// <summary>
    ///     Gets or sets the error message if an error occurred.
    /// </summary>
    public string? ErrorMessage { get; set; }
}