using Mewdeko.Controllers.Common.Administration;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing guild administration settings (auto-assign roles, protection, etc.)
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class AdministrationController(
    AutoAssignRoleService autoAssignRoleService,
    ProtectionService protectionService,
    SelfAssignedRolesService selfAssignedRolesService,
    DiscordShardedClient client) : Controller
{
    /// <summary>
    ///     Gets auto-assign role settings for normal users
    /// </summary>
    [HttpGet("auto-assign-roles")]
    public async Task<IActionResult> GetAutoAssignRoles(ulong guildId)
    {
        var normalRoles = await autoAssignRoleService.TryGetNormalRoles(guildId);
        var botRoles = await autoAssignRoleService.TryGetBotRoles(guildId);

        return Ok(new
        {
            normalRoles = normalRoles.ToList(), botRoles = botRoles.ToList()
        });
    }

    /// <summary>
    ///     Sets auto-assign roles for normal users
    /// </summary>
    [HttpPost("auto-assign-roles/normal")]
    public async Task<IActionResult> SetAutoAssignRoles(ulong guildId, [FromBody] List<ulong> roleIds)
    {
        await autoAssignRoleService.SetAarRolesAsync(guildId, roleIds);
        return Ok();
    }

    /// <summary>
    ///     Sets auto-assign roles for bots
    /// </summary>
    [HttpPost("auto-assign-roles/bots")]
    public async Task<IActionResult> SetBotAutoAssignRoles(ulong guildId, [FromBody] List<ulong> roleIds)
    {
        await autoAssignRoleService.SetAabrRolesAsync(guildId, roleIds);
        return Ok();
    }

    /// <summary>
    ///     Toggles an auto-assign role for normal users
    /// </summary>
    [HttpPost("auto-assign-roles/normal/{roleId}/toggle")]
    public async Task<IActionResult> ToggleAutoAssignRole(ulong guildId, ulong roleId)
    {
        var result = await autoAssignRoleService.ToggleAarAsync(guildId, roleId);
        return Ok(result);
    }

    /// <summary>
    ///     Toggles an auto-assign role for bots
    /// </summary>
    [HttpPost("auto-assign-roles/bots/{roleId}/toggle")]
    public async Task<IActionResult> ToggleBotAutoAssignRole(ulong guildId, ulong roleId)
    {
        var result = await autoAssignRoleService.ToggleAabrAsync(guildId, roleId);
        return Ok(result);
    }

    /// <summary>
    ///     Gets current protection settings status
    /// </summary>
    [HttpGet("protection/status")]
    public async Task<IActionResult> GetProtectionStatus(ulong guildId)
    {
        await Task.CompletedTask;

        // Get protection stats using the GetAntiStats method
        var (antiSpamStats, antiRaidStats, antiAltStats, antiMassMentionStats) =
            protectionService.GetAntiStats(guildId);

        return Ok(new
        {
            antiRaid = new
            {
                enabled = antiRaidStats != null,
                userThreshold = antiRaidStats?.AntiRaidSettings?.UserThreshold ?? 0,
                seconds = antiRaidStats?.AntiRaidSettings?.Seconds ?? 0,
                action = antiRaidStats?.AntiRaidSettings?.Action ?? 0,
                punishDuration = antiRaidStats?.AntiRaidSettings?.PunishDuration ?? 0,
                usersCount = antiRaidStats?.UsersCount ?? 0
            },
            antiSpam = new
            {
                enabled = antiSpamStats != null,
                messageThreshold = antiSpamStats?.AntiSpamSettings?.MessageThreshold ?? 0,
                action = antiSpamStats?.AntiSpamSettings?.Action ?? 0,
                muteTime = antiSpamStats?.AntiSpamSettings?.MuteTime ?? 0,
                roleId = antiSpamStats?.AntiSpamSettings?.RoleId ?? 0,
                userCount = antiSpamStats?.UserStats?.Count ?? 0
            },
            antiAlt = new
            {
                enabled = antiAltStats != null,
                minAge = antiAltStats?.MinAge ?? "",
                action = antiAltStats?.Action ?? 0,
                actionDuration = antiAltStats?.ActionDurationMinutes ?? 0,
                roleId = antiAltStats?.RoleId ?? 0,
                counter = antiAltStats?.Counter ?? 0
            },
            antiMassMention = new
            {
                enabled = antiMassMentionStats != null,
                mentionThreshold = antiMassMentionStats?.AntiMassMentionSettings?.MentionThreshold ?? 0,
                maxMentionsInTimeWindow = antiMassMentionStats?.AntiMassMentionSettings?.MaxMentionsInTimeWindow ?? 0,
                timeWindowSeconds = antiMassMentionStats?.AntiMassMentionSettings?.TimeWindowSeconds ?? 0,
                action = antiMassMentionStats?.AntiMassMentionSettings?.Action ?? 0,
                muteTime = antiMassMentionStats?.AntiMassMentionSettings?.MuteTime ?? 0,
                roleId = antiMassMentionStats?.AntiMassMentionSettings?.RoleId ?? 0,
                ignoreBots = antiMassMentionStats?.AntiMassMentionSettings?.IgnoreBots ?? false,
                userCount = antiMassMentionStats?.UserStats?.Count ?? 0
            }
        });
    }

    /// <summary>
    ///     Starts anti-raid protection
    /// </summary>
    [HttpPost("protection/anti-raid/start")]
    public async Task<IActionResult> StartAntiRaid(ulong guildId, [FromBody] AntiRaidRequest request)
    {
        var result = await protectionService.StartAntiRaidAsync(guildId, request.UserThreshold, request.Seconds,
            request.Action, request.MinutesDuration);
        if (result == null)
            return BadRequest("Failed to start anti-raid protection");

        return Ok(result);
    }

    /// <summary>
    ///     Stops anti-raid protection
    /// </summary>
    [HttpPost("protection/anti-raid/stop")]
    public async Task<IActionResult> StopAntiRaid(ulong guildId)
    {
        var result = await protectionService.TryStopAntiRaid(guildId);
        return Ok(new
        {
            success = result
        });
    }

    /// <summary>
    ///     Starts anti-spam protection
    /// </summary>
    [HttpPost("protection/anti-spam/start")]
    public async Task<IActionResult> StartAntiSpam(ulong guildId, [FromBody] AntiSpamRequest request)
    {
        var result = await protectionService.StartAntiSpamAsync(guildId, request.MessageCount, request.Action,
            request.PunishDurationMinutes, request.RoleId);
        if (result == null)
            return BadRequest("Failed to start anti-spam protection");

        return Ok(result);
    }

    /// <summary>
    ///     Stops anti-spam protection
    /// </summary>
    [HttpPost("protection/anti-spam/stop")]
    public async Task<IActionResult> StopAntiSpam(ulong guildId)
    {
        var result = await protectionService.TryStopAntiSpam(guildId);
        return Ok(new
        {
            success = result
        });
    }

    /// <summary>
    ///     Gets self-assignable roles
    /// </summary>
    [HttpGet("self-assignable-roles")]
    public async Task<IActionResult> GetSelfAssignableRoles(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var roles = await selfAssignedRolesService.GetRoles(guild);
        return Ok(roles);
    }

    /// <summary>
    ///     Adds a self-assignable role
    /// </summary>
    [HttpPost("self-assignable-roles/{roleId}")]
    public async Task<IActionResult> AddSelfAssignableRole(ulong guildId, ulong roleId,
        [FromBody] AddSelfAssignableRoleRequest? request = null)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var role = guild.GetRole(roleId);
        if (role == null)
            return NotFound("Role not found");

        var group = request?.Group ?? 0;
        var success = await selfAssignedRolesService.AddNew(guildId, role, group);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Removes a self-assignable role
    /// </summary>
    [HttpDelete("self-assignable-roles/{roleId}")]
    public async Task<IActionResult> RemoveSelfAssignableRole(ulong guildId, ulong roleId)
    {
        var success = await selfAssignedRolesService.RemoveSar(guildId, roleId);
        return Ok(new
        {
            success
        });
    }
}