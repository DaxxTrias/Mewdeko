using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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

/// <summary>
///     Essential guild information model for dashboard
/// </summary>
public class GuildInfoModel
{
    /// <summary>
    ///     The guild ID
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The guild name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The guild icon hash
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    ///     The full guild icon URL
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    ///     The guild banner hash
    /// </summary>
    public string? Banner { get; set; }

    /// <summary>
    ///     The full guild banner URL
    /// </summary>
    public string? BannerUrl { get; set; }

    /// <summary>
    ///     The guild description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Total member count
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    ///     Premium tier (boost level)
    /// </summary>
    public int PremiumTier { get; set; }

    /// <summary>
    ///     Guild owner ID
    /// </summary>
    public ulong OwnerId { get; set; }

    /// <summary>
    ///     When the guild was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}