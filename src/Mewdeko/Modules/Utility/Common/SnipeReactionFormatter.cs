using System.Text;
using Fergun.Interactive;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Helpers for surfacing the reactor snapshot captured by
///     <see cref="ReactionTrackingService" /> on snipe-style embeds.
/// </summary>
/// <remarks>
///     The snapshot is in-memory only and bounded by a TTL, so reactor data is best-effort:
///     reactions on snipes older than the tracker's retention window (or captured before the
///     bot was running) will simply not be displayed.
/// </remarks>
public static class SnipeReactionFormatter
{
    /// <summary>
    ///     Maximum number of individual reactor mentions rendered before truncating with a "...and N more" suffix.
    /// </summary>
    private const int MaxReactorsShown = 25;

    /// <summary>Discord embed field value hard limit.</summary>
    private const int FieldValueMaxLength = 1024;

    /// <summary>
    ///     Adds a "Reactors" field to <paramref name="embed" /> describing who reacted to the
    ///     sniped message, when a snapshot is available.
    /// </summary>
    /// <param name="embed">The embed to mutate.</param>
    /// <param name="tracker">The reaction tracker service to query.</param>
    /// <param name="messageId">The original Discord message id.</param>
    /// <param name="messageTimestamp">The original message timestamp, used to compute reaction delays.</param>
    public static void AddReactorsField(EmbedBuilder embed, ReactionTrackingService tracker,
        ulong messageId, DateTimeOffset messageTimestamp)
    {
        var value = BuildField(tracker, messageId, messageTimestamp);
        if (!string.IsNullOrEmpty(value))
            embed.AddField("Reactors", value);
    }

    /// <summary>
    ///     Adds a "Reactors" field to <paramref name="page" /> describing who reacted to the
    ///     sniped message, when a snapshot is available.
    /// </summary>
    /// <param name="page">The page builder to mutate.</param>
    /// <param name="tracker">The reaction tracker service to query.</param>
    /// <param name="messageId">The original Discord message id.</param>
    /// <param name="messageTimestamp">The original message timestamp, used to compute reaction delays.</param>
    public static void AddReactorsField(PageBuilder page, ReactionTrackingService tracker,
        ulong messageId, DateTimeOffset messageTimestamp)
    {
        var value = BuildField(tracker, messageId, messageTimestamp);
        if (!string.IsNullOrEmpty(value))
            page.AddField("Reactors", value);
    }

    private static string? BuildField(ReactionTrackingService tracker, ulong messageId,
        DateTimeOffset messageTimestamp)
    {
        if (!tracker.TryGetReactors(messageId, out var snapshot) || snapshot is null)
            return null;

        var total = snapshot.Reactors.Count;
        if (total == 0)
            return null;

        var ordered = snapshot.Reactors
            .OrderBy(kvp => kvp.Value)
            .Take(MaxReactorsShown)
            .ToList();

        var sb = new StringBuilder();
        sb.Append("**").Append(total).Append("** reactor").Append(total == 1 ? string.Empty : "s").AppendLine(":");

        foreach (var (userId, reactionTime) in ordered)
        {
            var delay = reactionTime - messageTimestamp.UtcDateTime;
            // Only show delay when it's plausible (positive, < 1h). Older reactions or clock skew skip the suffix.
            var delayText = delay.TotalSeconds is >= 0 and < 3600
                ? $" `+{delay.TotalSeconds:F0}s`"
                : string.Empty;

            sb.Append("<@").Append(userId).Append('>').AppendLine(delayText);
        }

        if (total > MaxReactorsShown)
            sb.Append("...and ").Append(total - MaxReactorsShown).AppendLine(" more");

        var result = sb.ToString();
        return result.Length > FieldValueMaxLength
            ? result[..FieldValueMaxLength]
            : result;
    }
}
