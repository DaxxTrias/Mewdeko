using Mewdeko.Modules.Moderation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing moderation actions (warnings, punishments, etc.)
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ModerationController(
    UserPunishService userPunishService,
    DiscordShardedClient client) : Controller
{
    /// <summary>
    ///     Gets all warnings for a guild
    /// </summary>
    [HttpGet("warnings")]
    public async Task<IActionResult> GetWarnings(ulong guildId)
    {
        var warnings = await userPunishService.GetAllWarnings(guildId);
        return Ok(warnings);
    }

    /// <summary>
    ///     Gets warnings for a specific user
    /// </summary>
    [HttpGet("warnings/user/{userId}")]
    public async Task<IActionResult> GetUserWarnings(ulong guildId, ulong userId)
    {
        var warnings = await userPunishService.UserWarnings(guildId, userId);
        return Ok(warnings);
    }

    /// <summary>
    ///     Gets recent moderation activity (just returns the warnings for now)
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentActivity(ulong guildId, int limit = 20)
    {
        var warnings = await userPunishService.GetAllWarnings(guildId);
        var recentWarnings = warnings.Take(limit).ToArray();
        return Ok(recentWarnings);
    }

    /// <summary>
    ///     Gets warning punishment settings for a guild
    /// </summary>
    [HttpGet("punishments")]
    public async Task<IActionResult> GetWarningPunishments(ulong guildId)
    {
        var punishments = await userPunishService.WarnPunishList(guildId);
        return Ok(punishments);
    }

    /// <summary>
    ///     Gets the warn log channel for a guild
    /// </summary>
    [HttpGet("warnlog-channel")]
    public async Task<IActionResult> GetWarnlogChannel(ulong guildId)
    {
        var channelId = await userPunishService.GetWarnlogChannel(guildId);
        return Ok(new
        {
            channelId
        });
    }
}