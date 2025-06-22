using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for message count statistics and analytics
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class MessageCountController(DiscordShardedClient client, MessageCountService messageCountService) : Controller
{
    /// <summary>
    ///     Gets daily message statistics for the guild
    /// </summary>
    [HttpGet("daily")]
    public async Task<IActionResult> GetDailyMessageStats(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var (counts, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.Guild, guildId, guildId);

        if (!enabled)
            return Ok(new
            {
                enabled = false, dailyMessages = 0, totalMessages = 0
            });

        // Calculate messages from the last 24 hours
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var dailyMessages = counts.Where(c => c.DateAdded >= yesterday).Sum(c => (long)c.Count);
        var totalMessages = counts.Sum(c => (long)c.Count);

        return Ok(new
        {
            enabled = true, dailyMessages, totalMessages, lastUpdated = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Gets message count statistics for a specific channel
    /// </summary>
    [HttpGet("channel/{channelId}")]
    public async Task<IActionResult> GetChannelMessageStats(ulong guildId, ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var channel = guild.GetTextChannel(channelId);
        if (channel == null) return NotFound("Channel not found");

        var (counts, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.Channel, channelId, guildId);

        if (!enabled)
            return Ok(new
            {
                enabled = false, totalMessages = 0
            });

        var totalMessages = counts.Sum(c => (long)c.Count);
        var dailyMessages = counts.Where(c => c.DateAdded >= DateTime.UtcNow.AddDays(-1)).Sum(c => (long)c.Count);

        return Ok(new
        {
            enabled = true,
            channelId = channelId.ToString(),
            channelName = channel.Name,
            totalMessages,
            dailyMessages,
            lastUpdated = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Gets message count statistics for a specific user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserMessageStats(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var (counts, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.User, userId, guildId);

        if (!enabled)
            return Ok(new
            {
                enabled = false, totalMessages = 0
            });

        var totalMessages = counts.Sum(c => (long)c.Count);
        var dailyMessages = counts.Where(c => c.DateAdded >= DateTime.UtcNow.AddDays(-1)).Sum(c => (long)c.Count);

        return Ok(new
        {
            enabled = true,
            userId = userId.ToString(),
            totalMessages,
            dailyMessages,
            lastUpdated = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Gets top users by message count
    /// </summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetMessageLeaderboard(ulong guildId, [FromQuery] int limit = 10)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var (counts, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.Guild, guildId, guildId);

        if (!enabled)
            return Ok(new
            {
                enabled = false, leaderboard = Array.Empty<object>()
            });

        var userGroups = counts.GroupBy(c => c.UserId)
            .Select(g => new
            {
                UserId = g.Key.ToString(),
                TotalMessages = g.Sum(x => (long)x.Count),
                DailyMessages = g.Where(x => x.DateAdded >= DateTime.UtcNow.AddDays(-1)).Sum(x => (long)x.Count)
            })
            .OrderByDescending(u => u.TotalMessages)
            .Take(limit)
            .ToList();

        return Ok(new
        {
            enabled = true, leaderboard = userGroups, lastUpdated = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Gets message count status for the guild
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetMessageCountStatus(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var (_, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.Guild, guildId, guildId);

        return Ok(new
        {
            enabled
        });
    }
}