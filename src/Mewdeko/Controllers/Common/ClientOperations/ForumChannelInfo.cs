namespace Mewdeko.Controllers.Common.ClientOperations;

/// <summary>
///     Detailed information about a forum channel
/// </summary>
public class ForumChannelInfo
{
    /// <summary>
    ///     The forum channel ID
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The forum channel name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The forum channel topic/description
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    ///     Available tags for this forum
    /// </summary>
    public List<ForumTagInfo> Tags { get; set; } = new();

    /// <summary>
    ///     Active threads in this forum
    /// </summary>
    public List<ThreadInfo> ActiveThreads { get; set; } = new();

    /// <summary>
    ///     Total number of threads (active + archived)
    /// </summary>
    public int TotalThreadCount { get; set; }

    /// <summary>
    ///     Whether the forum requires tags
    /// </summary>
    public bool RequiresTags { get; set; }

    /// <summary>
    ///     Maximum number of active threads
    /// </summary>
    public int? MaxActiveThreads { get; set; }

    /// <summary>
    ///     Default auto archive duration for threads
    /// </summary>
    public int? DefaultAutoArchiveDuration { get; set; }
}

/// <summary>
///     Information about a forum tag
/// </summary>
public class ForumTagInfo
{
    /// <summary>
    ///     The tag ID
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The tag name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The tag emoji (if any)
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    ///     Whether this tag is moderated
    /// </summary>
    public bool IsModerated { get; set; }
}

/// <summary>
///     Information about a forum thread
/// </summary>
public class ThreadInfo
{
    /// <summary>
    ///     The thread ID
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The thread name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Tags applied to this thread
    /// </summary>
    public List<ulong> AppliedTags { get; set; } = new();

    /// <summary>
    ///     Thread creator ID
    /// </summary>
    public ulong CreatorId { get; set; }

    /// <summary>
    ///     When the thread was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Message count in the thread
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    ///     Whether the thread is archived
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    ///     Whether the thread is locked
    /// </summary>
    public bool IsLocked { get; set; }
}