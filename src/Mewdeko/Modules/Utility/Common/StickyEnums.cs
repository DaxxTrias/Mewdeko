namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Specifies the trigger mode for sticky messages.
/// </summary>
public enum StickyTriggerMode
{
    /// <summary>
    ///     Standard time-based interval posting.
    /// </summary>
    TimeInterval = 0,

    /// <summary>
    ///     Display when channel becomes active (X messages in Y time).
    /// </summary>
    OnActivity = 1,

    /// <summary>
    ///     Display during quiet periods (no messages for X time).
    /// </summary>
    OnNoActivity = 2,

    /// <summary>
    ///     Display immediately and maintain bottom position.
    /// </summary>
    Immediate = 3,

    /// <summary>
    ///     Display after every N messages are posted.
    /// </summary>
    AfterMessages = 4
}

/// <summary>
///     Represents a time-based condition for sticky message display.
/// </summary>
public class TimeCondition
{
    /// <summary>
    ///     Start time in HH:mm format (24-hour).
    /// </summary>
    public string? StartTime { get; set; }

    /// <summary>
    ///     End time in HH:mm format (24-hour).
    /// </summary>
    public string? EndTime { get; set; }

    /// <summary>
    ///     Days of week when this condition applies (0=Sunday, 6=Saturday).
    /// </summary>
    public int[]? DaysOfWeek { get; set; }

    /// <summary>
    ///     Whether this condition is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Custom name for this time condition.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Checks if the current time matches this condition.
    /// </summary>
    /// <param name="currentTime">Current time in the guild's timezone.</param>
    /// <returns>True if the condition matches the current time.</returns>
    public bool IsActiveAt(DateTime currentTime)
    {
        if (!Enabled)
            return false;

        // Check day of week
        if (DaysOfWeek != null && DaysOfWeek.Length > 0)
        {
            var dayOfWeek = (int)currentTime.DayOfWeek;
            if (!DaysOfWeek.Contains(dayOfWeek))
                return false;
        }

        // Check time range
        if (!string.IsNullOrWhiteSpace(StartTime) && !string.IsNullOrWhiteSpace(EndTime))
        {
            if (TimeSpan.TryParse(StartTime, out var start) && TimeSpan.TryParse(EndTime, out var end))
            {
                var currentTimeOfDay = currentTime.TimeOfDay;

                // Handle overnight time ranges (e.g., 23:00 to 02:00)
                if (start > end)
                {
                    return currentTimeOfDay >= start || currentTimeOfDay <= end;
                }

                return currentTimeOfDay >= start && currentTimeOfDay <= end;
            }
        }

        return true;
    }
}

/// <summary>
///     Represents forum tag-based conditions for sticky messages.
/// </summary>
public class ForumTagCondition
{
    /// <summary>
    ///     Tag IDs that must be present for the sticky to appear.
    /// </summary>
    public ulong[]? RequiredTags { get; set; }

    /// <summary>
    ///     Tag IDs that prevent the sticky from appearing.
    /// </summary>
    public ulong[]? ExcludedTags { get; set; }

    /// <summary>
    ///     Whether this condition is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Checks if the given forum post tags match this condition.
    /// </summary>
    /// <param name="postTags">The tags on the forum post.</param>
    /// <returns>True if the condition allows the sticky to be displayed.</returns>
    public bool IsValidForTags(IEnumerable<ulong> postTags)
    {
        if (!Enabled)
            return false;

        var tags = postTags.ToArray();

        // Check required tags
        if (RequiredTags != null && RequiredTags.Length > 0)
        {
            if (!RequiredTags.All(required => tags.Contains(required)))
                return false;
        }

        // Check excluded tags
        if (ExcludedTags != null && ExcludedTags.Length > 0)
        {
            if (ExcludedTags.Any(excluded => tags.Contains(excluded)))
                return false;
        }

        return true;
    }
}

/// <summary>
///     Tracks sticky messages created in individual threads.
/// </summary>
public class ThreadStickyTracker
{
    /// <summary>
    ///     Dictionary mapping thread IDs to their sticky message IDs.
    /// </summary>
    public Dictionary<ulong, ulong> ThreadMessageIds { get; set; } = new();

    /// <summary>
    ///     When this tracker was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Adds or updates a thread sticky message ID.
    /// </summary>
    public void SetThreadStickyMessage(ulong threadId, ulong messageId)
    {
        ThreadMessageIds[threadId] = messageId;
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    ///     Removes a thread from tracking.
    /// </summary>
    public void RemoveThread(ulong threadId)
    {
        ThreadMessageIds.Remove(threadId);
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    ///     Gets the sticky message ID for a thread.
    /// </summary>
    public ulong? GetThreadStickyMessage(ulong threadId)
    {
        return ThreadMessageIds.TryGetValue(threadId, out var messageId) ? messageId : null;
    }

    /// <summary>
    ///     Gets all tracked thread IDs.
    /// </summary>
    public IEnumerable<ulong> GetTrackedThreads()
    {
        return ThreadMessageIds.Keys;
    }
}