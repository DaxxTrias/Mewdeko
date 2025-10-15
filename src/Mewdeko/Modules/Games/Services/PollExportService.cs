using System.IO;
using System.Text;
using System.Text.Json;
using Mewdeko.Modules.Games.Common;
using Poll = DataModel.Poll;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
/// Service for exporting poll data to various formats.
/// </summary>
public class PollExportService : INService
{
    private readonly ILogger<PollExportService> logger;
    private readonly PollService pollService;

    /// <summary>
    /// Initializes a new instance of the PollExportService class.
    /// </summary>
    /// <param name="pollService">The poll service.</param>
    /// <param name="logger">The logger instance.</param>
    public PollExportService(PollService pollService, ILogger<PollExportService> logger)
    {
        this.pollService = pollService;
        this.logger = logger;
    }

    /// <summary>
    /// Exports poll data to CSV format.
    /// </summary>
    /// <param name="pollId">The ID of the poll to export.</param>
    /// <returns>The CSV data as a byte array.</returns>
    public async Task<byte[]?> ExportToCsvAsync(int pollId)
    {
        try
        {
            var poll = await pollService.GetPollAsync(pollId);
            if (poll == null) return null;

            var stats = await pollService.GetPollStatsAsync(pollId);
            if (stats == null) return null;

            var csv = new StringBuilder();

            // Header information
            csv.AppendLine("Poll Export - CSV Format");
            csv.AppendLine($"Export Date,{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            csv.AppendLine($"Poll ID,{poll.Id}");
            csv.AppendLine($"Guild ID,{poll.GuildId}");
            csv.AppendLine($"Channel ID,{poll.ChannelId}");
            csv.AppendLine($"Creator ID,{poll.CreatorId}");
            csv.AppendLine($"Question,\"{EscapeCsvValue(poll.Question)}\"");
            csv.AppendLine($"Type,{(PollType)poll.Type}");
            csv.AppendLine($"Created At,{poll.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            csv.AppendLine($"Expires At,{(poll.ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never")} UTC");
            csv.AppendLine($"Closed At,{(poll.ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A")} UTC");
            csv.AppendLine($"Is Active,{poll.IsActive}");
            csv.AppendLine($"Total Votes,{stats.TotalVotes}");
            csv.AppendLine($"Unique Voters,{stats.UniqueVoters}");
            csv.AppendLine();

            // Poll options and results
            csv.AppendLine("Poll Options and Results");
            csv.AppendLine("Option Index,Option Text,Vote Count,Vote Percentage,Emote,Color");

            foreach (var option in poll.PollOptions.OrderBy(o => o.Index))
            {
                var voteCount = stats.OptionVotes.GetValueOrDefault(option.Index, 0);
                var percentage = stats.TotalVotes > 0 ? (double)voteCount / stats.TotalVotes * 100 : 0;

                csv.AppendLine($"{option.Index}," +
                               $"\"{EscapeCsvValue(option.Text)}\"," +
                               $"{voteCount}," +
                               $"{percentage:F2}," +
                               $"\"{EscapeCsvValue(option.Emote ?? "")}\"," +
                               $"\"{EscapeCsvValue(option.Color ?? "")}\"");
            }

            csv.AppendLine();

            // Vote history (if not anonymous)
            var settings = poll.Settings != null ? JsonSerializer.Deserialize<PollSettings>(poll.Settings) : null;
            if (settings?.IsAnonymous != true)
            {
                csv.AppendLine("Vote History");
                csv.AppendLine("User ID,Username,Selected Options,Voted At,Is Anonymous");

                foreach (var vote in stats.VoteHistory.OrderBy(v => v.VotedAt))
                {
                    var optionIndices = string.Join(";", vote.OptionIndices);
                    csv.AppendLine($"{vote.UserId}," +
                                   $"\"{EscapeCsvValue(vote.Username ?? "Unknown")}\"," +
                                   $"\"{optionIndices}\"," +
                                   $"{vote.VotedAt:yyyy-MM-dd HH:mm:ss} UTC," +
                                   $"{vote.IsAnonymous}");
                }
            }
            else
            {
                csv.AppendLine("Vote History");
                csv.AppendLine("Vote history is not available for anonymous polls.");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export poll {PollId} to CSV", pollId);
            return null;
        }
    }

    /// <summary>
    /// Exports poll data to JSON format.
    /// </summary>
    /// <param name="pollId">The ID of the poll to export.</param>
    /// <returns>The JSON data as a byte array.</returns>
    public async Task<byte[]?> ExportToJsonAsync(int pollId)
    {
        try
        {
            var poll = await pollService.GetPollAsync(pollId);
            if (poll == null) return null;

            var stats = await pollService.GetPollStatsAsync(pollId);
            if (stats == null) return null;

            var exportData = new
            {
                ExportInfo = new
                {
                    ExportDate = DateTime.UtcNow, ExportFormat = "JSON", ExportVersion = "1.0"
                },
                Poll = new
                {
                    poll.Id,
                    poll.GuildId,
                    poll.ChannelId,
                    poll.MessageId,
                    poll.CreatorId,
                    poll.Question,
                    Type = (PollType)poll.Type,
                    Settings = poll.Settings != null ? JsonSerializer.Deserialize<PollSettings>(poll.Settings) : null,
                    poll.CreatedAt,
                    poll.ExpiresAt,
                    poll.ClosedAt,
                    poll.IsActive,
                    Options = poll.PollOptions.OrderBy(o => o.Index).Select(opt => new
                    {
                        opt.Id,
                        opt.Index,
                        opt.Text,
                        opt.Color,
                        opt.Emote,
                        VoteCount = stats.OptionVotes.GetValueOrDefault(opt.Index, 0),
                        VotePercentage = stats.TotalVotes > 0
                            ? Math.Round(
                                (double)stats.OptionVotes.GetValueOrDefault(opt.Index, 0) / stats.TotalVotes * 100, 2)
                            : 0
                    }).ToList()
                },
                Statistics = new
                {
                    stats.TotalVotes,
                    stats.UniqueVoters,
                    stats.OptionVotes,
                    stats.AverageVoteTime,
                    stats.ParticipationRate,
                    VoteHistory = stats.VoteHistory.Select(vh => new
                    {
                        vh.UserId,
                        vh.Username,
                        vh.OptionIndices,
                        vh.VotedAt,
                        vh.IsAnonymous
                    }).ToList(),
                    VotesByHour = GetVotesByHour(stats.VoteHistory),
                    VotesByDayOfWeek = GetVotesByDayOfWeek(stats.VoteHistory)
                }
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return Encoding.UTF8.GetBytes(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export poll {PollId} to JSON", pollId);
            return null;
        }
    }

    /// <summary>
    /// Exports poll data to a simple text format suitable for reports.
    /// </summary>
    /// <param name="pollId">The ID of the poll to export.</param>
    /// <returns>The text data as a byte array.</returns>
    public async Task<byte[]?> ExportToTextAsync(int pollId)
    {
        try
        {
            var poll = await pollService.GetPollAsync(pollId);
            if (poll == null) return null;

            var stats = await pollService.GetPollStatsAsync(pollId);
            if (stats == null) return null;

            var text = new StringBuilder();

            // Header
            text.AppendLine("═══════════════════════════════════════════════════════════");
            text.AppendLine("                         POLL REPORT                        ");
            text.AppendLine("═══════════════════════════════════════════════════════════");
            text.AppendLine();

            // Basic information
            text.AppendLine("POLL INFORMATION");
            text.AppendLine("─────────────────────────────────────────────────────────");
            text.AppendLine($"Poll ID: {poll.Id}");
            text.AppendLine($"Question: {poll.Question}");
            text.AppendLine($"Type: {(PollType)poll.Type}");
            text.AppendLine($"Status: {(poll.IsActive ? "Active" : "Closed")}");
            text.AppendLine($"Created: {poll.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

            if (poll.ExpiresAt.HasValue)
                text.AppendLine($"Expires: {poll.ExpiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            else
                text.AppendLine("Expires: Never");

            if (poll.ClosedAt.HasValue)
                text.AppendLine($"Closed: {poll.ClosedAt.Value:yyyy-MM-dd HH:mm:ss} UTC");

            text.AppendLine();

            // Statistics
            text.AppendLine("VOTING STATISTICS");
            text.AppendLine("─────────────────────────────────────────────────────────");
            text.AppendLine($"Total Votes: {stats.TotalVotes}");
            text.AppendLine($"Unique Voters: {stats.UniqueVoters}");

            if (stats.AverageVoteTime.TotalMinutes > 0)
            {
                var avgTime = stats.AverageVoteTime.TotalHours >= 1
                    ? $"{stats.AverageVoteTime.TotalHours:F1} hours"
                    : $"{stats.AverageVoteTime.TotalMinutes:F1} minutes";
                text.AppendLine($"Average Vote Time: {avgTime}");
            }

            text.AppendLine();

            // Results
            text.AppendLine("POLL RESULTS");
            text.AppendLine("─────────────────────────────────────────────────────────");

            if (stats.TotalVotes > 0)
            {
                foreach (var option in poll.PollOptions.OrderBy(o => o.Index))
                {
                    var voteCount = stats.OptionVotes.GetValueOrDefault(option.Index, 0);
                    var percentage = (double)voteCount / stats.TotalVotes * 100;

                    text.AppendLine($"{option.Index + 1}. {option.Text}");
                    text.AppendLine($"   Votes: {voteCount} ({percentage:F1}%)");

                    // Simple bar chart
                    var barLength = Math.Max(1, (int)(percentage / 2)); // Scale to 50 chars max
                    var bar = new string('█', barLength);
                    text.AppendLine($"   {bar}");
                    text.AppendLine();
                }
            }
            else
            {
                text.AppendLine("No votes have been cast on this poll.");
                text.AppendLine();
            }

            // Export information
            text.AppendLine("EXPORT INFORMATION");
            text.AppendLine("─────────────────────────────────────────────────────────");
            text.AppendLine($"Export Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            text.AppendLine($"Export Format: Text Report");
            text.AppendLine();

            text.AppendLine("═══════════════════════════════════════════════════════════");
            text.AppendLine("                      END OF REPORT                        ");
            text.AppendLine("═══════════════════════════════════════════════════════════");

            return Encoding.UTF8.GetBytes(text.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export poll {PollId} to text", pollId);
            return null;
        }
    }

    /// <summary>
    /// Exports multiple polls to a summary CSV format.
    /// </summary>
    /// <param name="guildId">The guild ID to export polls for.</param>
    /// <param name="includeInactive">Whether to include inactive polls.</param>
    /// <returns>The CSV data as a byte array.</returns>
    public async Task<byte[]?> ExportGuildPollsSummaryAsync(ulong guildId, bool includeInactive = false)
    {
        try
        {
            var polls = includeInactive
                ? await GetAllPollsForGuild(guildId)
                : await pollService.GetActivePollsAsync(guildId);

            if (polls.Count == 0) return null;

            var csv = new StringBuilder();

            // Header
            csv.AppendLine("Guild Polls Summary Export");
            csv.AppendLine($"Guild ID,{guildId}");
            csv.AppendLine($"Export Date,{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            csv.AppendLine($"Total Polls,{polls.Count}");
            csv.AppendLine($"Include Inactive,{includeInactive}");
            csv.AppendLine();

            // Column headers
            csv.AppendLine("Poll ID,Question,Type,Status,Created At,Closed At,Total Votes,Unique Voters,Top Option");

            foreach (var poll in polls.OrderByDescending(p => p.CreatedAt))
            {
                var stats = await pollService.GetPollStatsAsync(poll.Id);
                var topOption = GetTopOption(poll, stats);

                csv.AppendLine($"{poll.Id}," +
                               $"\"{EscapeCsvValue(poll.Question)}\"," +
                               $"{(PollType)poll.Type}," +
                               $"{(poll.IsActive ? "Active" : "Closed")}," +
                               $"{poll.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                               $"{(poll.ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "")}," +
                               $"{stats?.TotalVotes ?? 0}," +
                               $"{stats?.UniqueVoters ?? 0}," +
                               $"\"{EscapeCsvValue(topOption)}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export guild polls summary for guild {GuildId}", guildId);
            return null;
        }
    }

    /// <summary>
    /// Gets the filename for an export based on the poll and format.
    /// </summary>
    /// <param name="poll">The poll being exported.</param>
    /// <param name="format">The export format.</param>
    /// <returns>The filename for the export.</returns>
    public static string GetExportFilename(Poll poll, string format)
    {
        var safeQuestion = SanitizeFilename(poll.Question);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return $"poll-{poll.Id}-{safeQuestion}-{timestamp}.{format.ToLower()}";
    }

    /// <summary>
    /// Gets the filename for a guild summary export.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="format">The export format.</param>
    /// <returns>The filename for the export.</returns>
    public static string GetGuildSummaryFilename(ulong guildId, string format)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return $"guild-{guildId}-polls-summary-{timestamp}.{format.ToLower()}";
    }

    #region Private Methods

    /// <summary>
    /// Escapes a value for use in CSV format.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value.</returns>
    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\"", "\"\"");
    }

    /// <summary>
    /// Sanitizes a string for use as a filename.
    /// </summary>
    /// <param name="filename">The filename to sanitize.</param>
    /// <returns>The sanitized filename.</returns>
    private static string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "untitled";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(filename.Where(c => !invalidChars.Contains(c)).ToArray());

        // Limit length and remove extra spaces
        sanitized = sanitized.Trim().Replace("  ", " ");
        if (sanitized.Length > 50)
            sanitized = sanitized[..47] + "...";

        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized;
    }

    /// <summary>
    /// Gets votes grouped by hour of day.
    /// </summary>
    /// <param name="voteHistory">The vote history.</param>
    /// <returns>Dictionary of hour to vote count.</returns>
    private static Dictionary<int, int> GetVotesByHour(List<VoteHistoryEntry> voteHistory)
    {
        return voteHistory
            .GroupBy(v => v.VotedAt.Hour)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Gets votes grouped by day of week.
    /// </summary>
    /// <param name="voteHistory">The vote history.</param>
    /// <returns>Dictionary of day of week to vote count.</returns>
    private static Dictionary<string, int> GetVotesByDayOfWeek(List<VoteHistoryEntry> voteHistory)
    {
        return voteHistory
            .GroupBy(v => v.VotedAt.DayOfWeek.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Gets the top option for a poll.
    /// </summary>
    /// <param name="poll">The poll.</param>
    /// <param name="stats">The poll statistics.</param>
    /// <returns>The text of the top option.</returns>
    private static string GetTopOption(Poll poll, PollStats? stats)
    {
        if (stats == null || stats.OptionVotes.Count == 0)
            return "No votes";

        var topOptionIndex = stats.OptionVotes.OrderByDescending(kvp => kvp.Value).First().Key;
        var topOption = poll.PollOptions.FirstOrDefault(o => o.Index == topOptionIndex);

        return topOption?.Text ?? "Unknown";
    }

    /// <summary>
    /// Gets all polls for a guild (placeholder - would need to extend ModernPollService).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A list of all polls for the guild.</returns>
    private async Task<List<Poll>> GetAllPollsForGuild(ulong guildId)
    {
        // This would require extending ModernPollService to get all polls, not just active ones
        // For now, return active polls as a placeholder
        return await pollService.GetActivePollsAsync(guildId);
    }

    #endregion
}