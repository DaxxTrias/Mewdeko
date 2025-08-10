namespace Mewdeko.Modules.Reputation.Common;

/// <summary>
///     DTO for reputation analytics summary.
/// </summary>
public class RepAnalyticsSummary
{
    /// <summary>
    ///     Guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Server name.
    /// </summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>
    ///     Total reputation given in the server.
    /// </summary>
    public long TotalRepGiven { get; set; }

    /// <summary>
    ///     Total unique users who have given rep.
    /// </summary>
    public int UniqueGivers { get; set; }

    /// <summary>
    ///     Total unique users who have received rep.
    /// </summary>
    public int UniqueReceivers { get; set; }

    /// <summary>
    ///     Average reputation per user.
    /// </summary>
    public decimal AverageRepPerUser { get; set; }

    /// <summary>
    ///     Most active hour of day (0-23).
    /// </summary>
    public int PeakHour { get; set; }

    /// <summary>
    ///     Most active day of week (0-6).
    /// </summary>
    public int PeakDayOfWeek { get; set; }

    /// <summary>
    ///     User retention rate (% active after receiving rep).
    /// </summary>
    public decimal RetentionRate { get; set; }

    /// <summary>
    ///     Date range for these metrics.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    ///     End date for metrics range.
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    ///     Channel activity breakdown.
    /// </summary>
    public List<RepChannelActivity> ChannelActivity { get; set; } = new();

    /// <summary>
    ///     Reputation type distribution.
    /// </summary>
    public Dictionary<string, int> RepTypeDistribution { get; set; } = new();
}

/// <summary>
///     Channel activity data for analytics.
/// </summary>
public class RepChannelActivity
{
    /// <summary>
    ///     Channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Channel name.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    ///     Total reputation given in this channel.
    /// </summary>
    public int TotalRep { get; set; }

    /// <summary>
    ///     Number of transactions in this channel.
    /// </summary>
    public int TransactionCount { get; set; }

    /// <summary>
    ///     Average rep per transaction.
    /// </summary>
    public decimal AverageRep { get; set; }

    /// <summary>
    ///     Top users in this channel.
    /// </summary>
    public List<RepUserActivity> TopUsers { get; set; } = new();
}

/// <summary>
///     User activity data for analytics.
/// </summary>
public class RepUserActivity
{
    /// <summary>
    ///     User ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    ///     Total reputation given by this user.
    /// </summary>
    public int RepGiven { get; set; }

    /// <summary>
    ///     Total reputation received by this user.
    /// </summary>
    public int RepReceived { get; set; }

    /// <summary>
    ///     Number of transactions by this user.
    /// </summary>
    public int TransactionCount { get; set; }
}