using Mewdeko.Modules.Patreon.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for handling Patreon OAuth operations
/// </summary>
[ApiController]
[Route("botapi/patreon")]
[EnableRateLimiting("AuthPolicy")]
public class PatreonController : ControllerBase
{
    private readonly IBotCredentials credentials;
    private readonly PatreonApiClient patreonApiClient;
    private readonly PatreonService patreonService;

    /// <summary>
    ///     Initializes a new instance of the PatreonController class.
    /// </summary>
    /// <param name="patreonService">The Patreon service.</param>
    /// <param name="patreonApiClient">The Patreon API client.</param>
    /// <param name="credentials">The bot credentials.</param>
    public PatreonController(
        PatreonService patreonService,
        PatreonApiClient patreonApiClient,
        IBotCredentials credentials)
    {
        this.patreonService = patreonService;
        this.patreonApiClient = patreonApiClient;
        this.credentials = credentials;
    }

    /// <summary>
    ///     Generates OAuth authorization URL for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>OAuth authorization URL</returns>
    [HttpGet("oauth/url")]
    [EnableRateLimiting("BasicPolicy")]
    public IActionResult GetOAuthUrl([FromQuery] ulong guildId)
    {
        try
        {
            if (string.IsNullOrEmpty(credentials.PatreonClientId))
            {
                Log.Warning("Patreon OAuth URL requested but no client ID configured");
                return BadRequest(new
                {
                    error = "Patreon integration not configured"
                });
            }

            var redirectUri = $"{credentials.PatreonBaseUrl}/dashboard/patreon";
            var state = $"{guildId}:{Guid.NewGuid()}";

            var authUrl = patreonApiClient.GetAuthorizationUrl(
                credentials.PatreonClientId,
                redirectUri,
                state);

            Log.Information("Generated Patreon OAuth URL for guild {GuildId}", guildId);

            return Ok(new PatreonOAuthResponse
            {
                AuthorizationUrl = authUrl, State = state
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating Patreon OAuth URL for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Handles the OAuth callback from Patreon after user authorization.
    /// </summary>
    /// <param name="code">The authorization code provided by Patreon.</param>
    /// <param name="state">The state parameter returned from Patreon, containing the guild ID.</param>
    /// <param name="error">An error parameter, if the authorization failed.</param>
    /// <returns>A result indicating the success or failure of the OAuth process.</returns>
    [HttpGet("oauth/callback")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> OAuthCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        try
        {
            if (!string.IsNullOrEmpty(error))
            {
                Log.Warning("Patreon OAuth callback received error: {Error}", error);
                return BadRequest(new
                {
                    error = $"OAuth error: {error}"
                });
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                Log.Warning("Patreon OAuth callback missing required parameters");
                return BadRequest(new
                {
                    error = "Missing required parameters"
                });
            }

            var stateParts = state.Split(':');
            if (stateParts.Length != 2 || !ulong.TryParse(stateParts[0], out var guildId))
            {
                Log.Warning("Invalid state parameter in Patreon OAuth callback: {State}", state);
                return BadRequest(new
                {
                    error = "Invalid state parameter"
                });
            }

            if (string.IsNullOrEmpty(credentials.PatreonClientId) ||
                string.IsNullOrEmpty(credentials.PatreonClientSecret))
            {
                Log.Error("Patreon OAuth callback but credentials not configured");
                return StatusCode(500, new
                {
                    error = "Patreon integration not configured"
                });
            }

            var redirectUri = $"{credentials.PatreonBaseUrl}/dashboard/patreon";

            var tokenResponse = await patreonApiClient.ExchangeCodeForTokenAsync(
                code,
                credentials.PatreonClientId,
                credentials.PatreonClientSecret,
                redirectUri);

            if (tokenResponse == null)
            {
                Log.Error("Failed to exchange Patreon OAuth code for tokens for guild {GuildId}", guildId);
                return StatusCode(500, new
                {
                    error = "Failed to exchange authorization code"
                });
            }

            var campaignsResponse = await patreonApiClient.GetCampaignsAsync(tokenResponse.AccessToken);
            if (campaignsResponse?.Data == null || campaignsResponse.Data.Count == 0)
            {
                Log.Warning("No Patreon campaigns found for the authenticated user in guild {GuildId}", guildId);
                return BadRequest(new
                {
                    error =
                        "No Patreon campaigns found for this user. Ensure you are logging in with the creator account."
                });
            }

            var campaignId = campaignsResponse.Data.First().Id;

            var success = await patreonService.StoreOAuthTokensAsync(
                guildId,
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                campaignId,
                tokenResponse.ExpiresIn);

            if (!success)
            {
                Log.Error("Failed to store Patreon OAuth tokens for guild {GuildId}", guildId);
                return StatusCode(500, new
                {
                    error = "Failed to store OAuth tokens"
                });
            }

            _ = Task.Run(() => patreonService.SyncAllAsync(guildId));

            Log.Information("Successfully completed Patreon OAuth for guild {GuildId} with campaign {CampaignId}",
                guildId, campaignId);
            return Ok(new PatreonOAuthCallbackResponse
            {
                Success = true,
                Message = "Patreon integration configured successfully",
                GuildId = guildId,
                CampaignId = campaignId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling Patreon OAuth callback");
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets the OAuth state information for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>OAuth state information</returns>
    [HttpGet("oauth/status")]
    [EnableRateLimiting("BasicPolicy")]
    public async Task<IActionResult> GetOAuthStatus([FromQuery] ulong guildId)
    {
        try
        {
            var config = await patreonService.GetPatreonOAuthConfig(guildId);
            var hasValidTokens = !string.IsNullOrEmpty(config.accessToken) &&
                                 !string.IsNullOrEmpty(config.campaignId);

            return Ok(new PatreonOAuthStatusResponse
            {
                IsConfigured = hasValidTokens,
                CampaignId = hasValidTokens ? config.campaignId : null,
                LastSync = null, // Not tracking sync time in database yet
                TokenExpiry = config.tokenExpiry
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon OAuth status for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets comprehensive analytics for a guild's Patreon integration
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Patreon analytics data</returns>
    [HttpGet("analytics")]
    [EnableRateLimiting("BasicPolicy")]
    public async Task<IActionResult> GetAnalytics([FromQuery] ulong guildId)
    {
        try
        {
            var config = await patreonService.GetPatreonOAuthConfig(guildId);
            if (string.IsNullOrEmpty(config.accessToken))
            {
                return BadRequest(new
                {
                    error = "Patreon not configured for this guild"
                });
            }

            var analytics = await patreonService.GetAnalyticsAsync(guildId);
            return Ok(analytics);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon analytics for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets active supporters for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>List of active supporters</returns>
    [HttpGet("supporters")]
    [EnableRateLimiting("BasicPolicy")]
    public async Task<IActionResult> GetSupporters([FromQuery] ulong guildId)
    {
        try
        {
            var config = await patreonService.GetPatreonOAuthConfig(guildId);
            if (string.IsNullOrEmpty(config.accessToken))
            {
                return BadRequest(new
                {
                    error = "Patreon not configured for this guild"
                });
            }

            var supporters = await patreonService.GetActiveSupportersAsync(guildId);
            return Ok(supporters);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon supporters for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets Patreon configuration for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Patreon configuration</returns>
    [HttpGet("config")]
    [EnableRateLimiting("BasicPolicy")]
    public async Task<IActionResult> GetConfig([FromQuery] ulong guildId)
    {
        try
        {
            var config = await patreonService.GetPatreonConfig(guildId);
            return Ok(config);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon config for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Updates Patreon configuration for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="request">Configuration update request</param>
    /// <returns>Updated configuration</returns>
    [HttpPost("config")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> UpdateConfig([FromQuery] ulong guildId,
        [FromBody] PatreonConfigUpdateRequest request)
    {
        try
        {
            var config = await patreonService.GetPatreonOAuthConfig(guildId);
            if (string.IsNullOrEmpty(config.accessToken))
            {
                return BadRequest(new
                {
                    error = "Patreon not configured for this guild"
                });
            }

            if (request.ChannelId.HasValue)
            {
                await patreonService.SetPatreonChannel(guildId, request.ChannelId.Value);
            }

            if (request.Message != null)
            {
                await patreonService.SetPatreonMessage(guildId, request.Message);
            }

            if (request.AnnouncementDay.HasValue)
            {
                await patreonService.SetAnnouncementDay(guildId, request.AnnouncementDay.Value);
            }

            if (request.ToggleAnnouncements.HasValue && request.ToggleAnnouncements.Value)
            {
                await patreonService.TogglePatreonAnnouncements(guildId);
            }

            if (request.ToggleRoleSync.HasValue && request.ToggleRoleSync.Value)
            {
                await patreonService.ToggleRoleSyncAsync(guildId);
            }

            var updatedConfig = await patreonService.GetPatreonConfig(guildId);
            return Ok(updatedConfig);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating Patreon config for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets Patreon tiers for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>List of Patreon tiers</returns>
    [HttpGet("tiers")]
    [EnableRateLimiting("BasicPolicy")]
    public async Task<IActionResult> GetTiers([FromQuery] ulong guildId)
    {
        try
        {
            var config = await patreonService.GetPatreonOAuthConfig(guildId);
            if (string.IsNullOrEmpty(config.accessToken))
            {
                return BadRequest(new
                {
                    error = "Patreon not configured for this guild"
                });
            }

            var tiers = await patreonService.GetTiersAsync(guildId);
            return Ok(tiers);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon tiers for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Gets Patreon goals for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>List of Patreon goals</returns>
    [HttpGet("goals")]
    [EnableRateLimiting("BasicPolicy")]
    public async Task<IActionResult> GetGoals([FromQuery] ulong guildId)
    {
        try
        {
            var config = await patreonService.GetPatreonOAuthConfig(guildId);
            if (string.IsNullOrEmpty(config.accessToken))
            {
                return BadRequest(new
                {
                    error = "Patreon not configured for this guild"
                });
            }

            var goals = await patreonService.GetGoalsAsync(guildId);
            return Ok(goals);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon goals for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Triggers manual operations for Patreon integration
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="request">Operation request</param>
    /// <returns>Operation result</returns>
    [HttpPost("operations")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> TriggerOperation([FromQuery] ulong guildId,
        [FromBody] PatreonOperationRequest request)
    {
        try
        {
            var config = await patreonService.GetPatreonOAuthConfig(guildId);
            if (string.IsNullOrEmpty(config.accessToken))
            {
                return BadRequest(new
                {
                    error = "Patreon not configured for this guild"
                });
            }

            switch (request.Operation.ToLowerInvariant())
            {
                case "sync_all":
                    await patreonService.SyncAllAsync(guildId);
                    return Ok(new
                    {
                        message = "Full Patreon data sync completed successfully."
                    });
                case "sync":
                    await patreonService.UpdateSupportersAsync(guildId);
                    return Ok(new
                    {
                        message = "Supporters synced successfully"
                    });

                case "refresh_token":
                    var refreshed = await patreonService.RefreshTokenAsync(guildId);
                    return Ok(new
                    {
                        message = refreshed != null ? "Token refreshed successfully" : "Failed to refresh token"
                    });

                case "manual_announcement":
                    await patreonService.TriggerManualAnnouncement(guildId);
                    return Ok(new
                    {
                        message = "Manual announcement sent"
                    });

                case "sync_roles":
                    await patreonService.SyncAllRolesAsync(guildId);
                    return Ok(new
                    {
                        message = "Roles synced for all supporters"
                    });

                default:
                    return BadRequest(new
                    {
                        error = "Invalid operation"
                    });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing Patreon operation {Operation} for guild {GuildId}", request.Operation,
                guildId);
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Maps a Patreon tier to a Discord role
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="request">Tier mapping request</param>
    /// <returns>Mapping result</returns>
    [HttpPost("tiers/map")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> MapTierToRole([FromQuery] ulong guildId,
        [FromBody] PatreonTierMappingRequest request)
    {
        try
        {
            var config = await patreonService.GetPatreonOAuthConfig(guildId);
            if (string.IsNullOrEmpty(config.accessToken))
            {
                return BadRequest(new
                {
                    error = "Patreon not configured for this guild"
                });
            }

            await patreonService.MapTierToRoleAsync(guildId, request.TierId, request.RoleId);
            return Ok(new
            {
                message = "Tier mapped to role successfully"
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error mapping Patreon tier {TierId} to role {RoleId} for guild {GuildId}",
                request.TierId, request.RoleId, guildId);
            return StatusCode(500, new
            {
                error = "Internal server error"
            });
        }
    }
}

/// <summary>
///     Response model for OAuth URL generation
/// </summary>
public class PatreonOAuthResponse
{
    /// <summary>
    ///     The OAuth authorization URL
    /// </summary>
    public string AuthorizationUrl { get; set; } = null!;

    /// <summary>
    ///     The state parameter for the OAuth flow
    /// </summary>
    public string State { get; set; } = null!;
}

/// <summary>
///     Response model for OAuth callback
/// </summary>
public class PatreonOAuthCallbackResponse
{
    /// <summary>
    ///     Whether the OAuth flow was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Success or error message
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    ///     Discord guild ID
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Patreon campaign ID
    /// </summary>
    public string? CampaignId { get; set; }
}

/// <summary>
///     Response model for OAuth status
/// </summary>
public class PatreonOAuthStatusResponse
{
    /// <summary>
    ///     Whether Patreon integration is configured for this guild
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>
    ///     Patreon campaign ID if configured
    /// </summary>
    public string? CampaignId { get; set; }

    /// <summary>
    ///     Last time supporters were synced
    /// </summary>
    public DateTime? LastSync { get; set; }

    /// <summary>
    ///     When the OAuth token expires
    /// </summary>
    public DateTime? TokenExpiry { get; set; }
}

/// <summary>
///     Request model for updating Patreon configuration
/// </summary>
public class PatreonConfigUpdateRequest
{
    /// <summary>
    ///     Discord channel ID for announcements
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     Custom announcement message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     Day of month for announcements (1-31)
    /// </summary>
    public int? AnnouncementDay { get; set; }

    /// <summary>
    ///     Whether to toggle announcements on/off
    /// </summary>
    public bool? ToggleAnnouncements { get; set; }

    /// <summary>
    ///     Whether to toggle role sync on/off
    /// </summary>
    public bool? ToggleRoleSync { get; set; }
}

/// <summary>
///     Request model for Patreon operations
/// </summary>
public class PatreonOperationRequest
{
    /// <summary>
    ///     Operation to perform (sync, refresh_token, manual_announcement, sync_roles)
    /// </summary>
    public string Operation { get; set; } = null!;
}

/// <summary>
///     Request model for mapping Patreon tiers to Discord roles
/// </summary>
public class PatreonTierMappingRequest
{
    /// <summary>
    ///     Patreon tier ID
    /// </summary>
    public string TierId { get; set; } = null!;

    /// <summary>
    ///     Discord role ID
    /// </summary>
    public ulong RoleId { get; set; }
}