using System.Text;
using Fergun.Interactive;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Helpers for surfacing message edit history on deleted-message snipes.
/// </summary>
public static class SnipeEditHistoryFormatter
{
    private const int FieldValueMaxLength = 1024;
    private const int MaxEntriesShown = 3;
    private const int ContentPreviewLength = 240;

    /// <summary>
    ///     Adds an edit-history field to a deleted-message embed, when edit history is available.
    /// </summary>
    /// <param name="embed">The embed to mutate.</param>
    /// <param name="snipe">The deleted-message snipe.</param>
    /// <param name="sanitize">Whether to sanitize captured message content before display.</param>
    public static void AddDeletedEditHistoryField(EmbedBuilder embed, SnipeStore snipe, bool sanitize = false)
    {
        var value = BuildField(snipe, sanitize);
        if (!string.IsNullOrWhiteSpace(value))
            embed.AddField("Edited Before Delete", value);
    }

    /// <summary>
    ///     Adds an edit-history field to a deleted-message page, when edit history is available.
    /// </summary>
    /// <param name="page">The page builder to mutate.</param>
    /// <param name="snipe">The deleted-message snipe.</param>
    /// <param name="sanitize">Whether to sanitize captured message content before display.</param>
    public static void AddDeletedEditHistoryField(PageBuilder page, SnipeStore snipe, bool sanitize = false)
    {
        var value = BuildField(snipe, sanitize);
        if (!string.IsNullOrWhiteSpace(value))
            page.AddField("Edited Before Delete", value);
    }

    private static string? BuildField(SnipeStore snipe, bool sanitize)
    {
        if (snipe.Edited)
            return null;

        if (snipe.EditHistory is not { Count: > 0 } history)
            return null;

        var entries = history
            .TakeLast(MaxEntriesShown)
            .ToList();

        var sb = new StringBuilder();
        sb.Append("**").Append(history.Count).Append("** edit")
            .Append(history.Count == 1 ? string.Empty : "s")
            .AppendLine(" captured before deletion.");

        foreach (var entry in entries)
        {
            var content = string.IsNullOrWhiteSpace(entry.Message)
                ? "[No text content]"
                : entry.Message;

            if (sanitize)
                content = Format.Sanitize(content);

            var timestamp = entry.DateAdded == default
                ? "unknown time"
                : TimestampTag.FromDateTimeOffset(new DateTimeOffset(entry.DateAdded, TimeSpan.Zero),
                    TimestampTagStyles.ShortTime).ToString();

            var line = $"{timestamp}: {content.TrimTo(ContentPreviewLength)}";
            if (sb.Length + line.Length + 1 > FieldValueMaxLength)
                break;

            sb.AppendLine(line);
        }

        if (history.Count > entries.Count)
        {
            var suffix = $"...and {history.Count - entries.Count} earlier edit(s).";
            if (sb.Length + suffix.Length + 1 <= FieldValueMaxLength)
                sb.AppendLine(suffix);
        }

        return sb.ToString().Trim();
    }
}
