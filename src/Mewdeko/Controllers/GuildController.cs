using Mewdeko.Controllers.Common.Guild;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for guild/server information endpoints
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
public class GuildController : ControllerBase
{
    private readonly DiscordShardedClient client;
    private readonly ILogger<GuildController> logger;

    /// <summary>
    ///     Initializes a new instance of the GuildController class
    /// </summary>
    /// <param name="client">The Discord sharded client</param>
    /// <param name="logger">The logger instance</param>
    public GuildController(DiscordShardedClient client, ILogger<GuildController> logger)
    {
        this.client = client;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets essential guild information for dashboard display
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>Guild information for dashboard theming and display</returns>
    [HttpGet("{guildId}/info")]
    public IActionResult GetGuildInfo(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound($"Guild with ID {guildId} not found");

            var guildInfo = new GuildInfoModel
            {
                Id = guild.Id,
                Name = guild.Name,
                Icon = guild.IconId,
                IconUrl =
                    guild.IconId != null
                        ? $"https://cdn.discordapp.com/icons/{guild.Id}/{guild.IconId}.{(guild.IconId.StartsWith("a_") ? "gif" : "png")}"
                        : null,
                Banner = guild.BannerId,
                BannerUrl =
                    guild.BannerId != null
                        ? $"https://cdn.discordapp.com/banners/{guild.Id}/{guild.BannerId}.{(guild.BannerId.StartsWith("a_") ? "gif" : "png")}"
                        : null,
                Description = guild.Description,
                MemberCount = guild.MemberCount,
                PremiumTier = (int)guild.PremiumTier,
                OwnerId = guild.OwnerId,
                CreatedAt = guild.CreatedAt
            };

            return Ok(guildInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving guild info for {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }
}