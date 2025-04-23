using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mewdeko.Modules.Starboard.Services;

namespace Mewdeko.Controllers;

/// <summary>
/// Controller for managing starboard configurations in guilds
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class StarboardController : Controller
{
    private readonly StarboardService starboardService;
    private readonly DiscordShardedClient client;

    /// <summary>
    /// Initializes a new instance of the StarboardController
    /// </summary>
    public StarboardController(StarboardService starboardService, DiscordShardedClient client)
    {
        this.starboardService = starboardService;
        this.client = client;
    }

    /// <summary>
    /// Gets all starboard configurations for a guild
    /// </summary>
    [HttpGet("all")]
    public IActionResult GetStarboards(ulong guildId)
    {
        var starboards = starboardService.GetStarboards(guildId);
        return Ok(starboards);
    }

    /// <summary>
    /// Creates a new starboard for a guild
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateStarboard(ulong guildId, [FromBody] StarboardCreateRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var starboardId = await starboardService.CreateStarboard(guild, request.ChannelId, request.Emote, request.Threshold);
        return Ok(starboardId);
    }

    /// <summary>
    /// Deletes a starboard configuration
    /// </summary>
    [HttpDelete("{starboardId}")]
    public async Task<IActionResult> DeleteStarboard(ulong guildId, int starboardId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var success = await starboardService.DeleteStarboard(guild, starboardId);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok();
    }

    /// <summary>
    /// Sets whether bots are allowed to be starred for a specific starboard
    /// </summary>
    [HttpPost("{starboardId}/allow-bots")]
    public async Task<IActionResult> SetAllowBots(ulong guildId, int starboardId, [FromBody] bool allowed)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var success = await starboardService.SetAllowBots(guild, starboardId, allowed);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(allowed);
    }

    /// <summary>
    /// Sets whether to remove starred messages when the original is deleted
    /// </summary>
    [HttpPost("{starboardId}/remove-on-delete")]
    public async Task<IActionResult> SetRemoveOnDelete(ulong guildId, int starboardId, [FromBody] bool removeOnDelete)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var success = await starboardService.SetRemoveOnDelete(guild, starboardId, removeOnDelete);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(removeOnDelete);
    }

    /// <summary>
    /// Sets whether to remove starred messages when reactions are cleared
    /// </summary>
    [HttpPost("{starboardId}/remove-on-clear")]
    public async Task<IActionResult> SetRemoveOnClear(ulong guildId, int starboardId, [FromBody] bool removeOnClear)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var success = await starboardService.SetRemoveOnClear(guild, starboardId, removeOnClear);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(removeOnClear);
    }

    /// <summary>
    /// Sets whether to remove starred messages when they fall below threshold
    /// </summary>
    [HttpPost("{starboardId}/remove-below-threshold")]
    public async Task<IActionResult> SetRemoveBelowThreshold(ulong guildId, int starboardId, [FromBody] bool removeBelowThreshold)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var success = await starboardService.SetRemoveBelowThreshold(guild, starboardId, removeBelowThreshold);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(removeBelowThreshold);
    }

    /// <summary>
    /// Sets the repost threshold for a starboard
    /// </summary>
    [HttpPost("{starboardId}/repost-threshold")]
    public async Task<IActionResult> SetRepostThreshold(ulong guildId, int starboardId, [FromBody] int threshold)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var success = await starboardService.SetRepostThreshold(guild, starboardId, threshold);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(threshold);
    }

    /// <summary>
    /// Sets the star threshold for a starboard
    /// </summary>
    [HttpPost("{starboardId}/star-threshold")]
    public async Task<IActionResult> SetStarThreshold(ulong guildId, int starboardId, [FromBody] int threshold)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var success = await starboardService.SetStarThreshold(guild, starboardId, threshold);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(threshold);
    }

    /// <summary>
    /// Sets whether to use blacklist mode for channel checking
    /// </summary>
    [HttpPost("{starboardId}/use-blacklist")]
    public async Task<IActionResult> SetUseBlacklist(ulong guildId, int starboardId, [FromBody] bool useBlacklist)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var success = await starboardService.SetUseBlacklist(guild, starboardId, useBlacklist);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(useBlacklist);
    }

    /// <summary>
    /// Toggles a channel in the starboard's check list
    /// </summary>
    [HttpPost("{starboardId}/toggle-channel")]
    public async Task<IActionResult> ToggleChannel(ulong guildId, int starboardId, [FromBody] ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var result = await starboardService.ToggleChannel(guild, starboardId, channelId.ToString());
        if (result.Config == null)
            return NotFound("Starboard configuration not found");

        return Ok(new { wasAdded = result.WasAdded, config = result.Config });
    }
}

/// <summary>
/// Request model for creating a new starboard
/// </summary>
public class StarboardCreateRequest
{
    /// <summary>
    /// The channel ID where starred messages will be posted
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// The emote to use for this starboard
    /// </summary>
    public string Emote { get; set; } = "⭐";

    /// <summary>
    /// The number of reactions required to post a message
    /// </summary>
    public int Threshold { get; set; } = 1;
}