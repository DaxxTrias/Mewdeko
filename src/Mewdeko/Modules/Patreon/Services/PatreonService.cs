using System.Threading;
using DataModel;
using LinqToDB;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Patreon.Common;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Mewdeko.Modules.Patreon.Services;

/// <summary>
/// Service for managing Patreon integration and monthly announcements.
/// </summary>
public class PatreonService : BackgroundService, INService, IReadyExecutor
{
    private readonly PatreonApiClient apiClient;
    private readonly DiscordShardedClient client;
    private readonly IBotCredentials creds;
    private readonly IDataConnectionFactory db;
    private readonly GuildSettingsService guildSettings;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    /// Initializes a new instance of the PatreonService class.
    /// </summary>
    /// <param name="client">The Discord sharded client.</param>
    /// <param name="db">The database connection factory.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="strings">The localized bot strings.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="apiClient">The Patreon API client.</param>
    public PatreonService(
        DiscordShardedClient client,
        IDataConnectionFactory db,
        GuildSettingsService guildSettings,
        GeneratedBotStrings strings,
        IBotCredentials creds,
        PatreonApiClient apiClient)
    {
        this.client = client;
        this.db = db;
        this.guildSettings = guildSettings;
        this.strings = strings;
        this.creds = creds;
        this.apiClient = apiClient;
    }

    /// <summary>
    /// Initializes the service when the bot is ready
    /// </summary>
    public async Task OnReadyAsync()
    {
        Log.Information("PatreonService ready - checking for overdue announcements");

        // Check for any overdue announcements on startup
        await CheckForOverdueAnnouncements();
    }

    /// <summary>
    /// Background service execution for monthly announcements
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckMonthlyAnnouncements();

                // Check every hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in PatreonService background task");

                // Wait 5 minutes before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Sets the Patreon announcement channel for a guild
    /// </summary>
    public async Task<bool> SetPatreonChannel(ulong guildId, ulong channelId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        await using var uow = await db.CreateConnectionAsync();
        config.PatreonChannelId = channelId;
        config.PatreonEnabled = channelId != 0; // Enable if channel is set, disable if set to 0

        await guildSettings.UpdateGuildConfig(guildId, config);

        Log.Information("Patreon channel set to {ChannelId} for guild {GuildId}", channelId, guildId);
        return true;
    }

    /// <summary>
    /// Sets a custom announcement message for a guild
    /// </summary>
    public async Task SetPatreonMessage(ulong guildId, string? message)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        await using var uow = await db.CreateConnectionAsync();
        config.PatreonMessage = string.IsNullOrWhiteSpace(message) || message == "-" ? null : message;

        await guildSettings.UpdateGuildConfig(guildId, config);

        Log.Information("Patreon message updated for guild {GuildId}", guildId);
    }

    /// <summary>
    /// Sets the day of the month for announcements
    /// </summary>
    public async Task<bool> SetAnnouncementDay(ulong guildId, int day)
    {
        if (day < 1 || day > 28) // Max 28 to avoid issues with February
            return false;

        var config = await guildSettings.GetGuildConfig(guildId);

        await using var uow = await db.CreateConnectionAsync();
        config.PatreonAnnouncementDay = day;

        await guildSettings.UpdateGuildConfig(guildId, config);

        Log.Information("Patreon announcement day set to {Day} for guild {GuildId}", day, guildId);
        return true;
    }

    /// <summary>
    /// Toggles Patreon announcements for a guild
    /// </summary>
    public async Task<bool> TogglePatreonAnnouncements(ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        await using var uow = await db.CreateConnectionAsync();
        config.PatreonEnabled = !config.PatreonEnabled;

        await guildSettings.UpdateGuildConfig(guildId, config);

        Log.Information("Patreon announcements {Status} for guild {GuildId}",
            config.PatreonEnabled ? "enabled" : "disabled", guildId);

        return config.PatreonEnabled;
    }

    /// <summary>
    /// Gets the current Patreon configuration for a guild
    /// </summary>
    public async Task<(ulong channelId, string? message, int day, bool enabled, DateTime? lastAnnouncement)>
        GetPatreonConfig(ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        return (config.PatreonChannelId, config.PatreonMessage, config.PatreonAnnouncementDay,
            config.PatreonEnabled, config.PatreonLastAnnouncement);
    }

    /// <summary>
    /// Gets the extended Patreon configuration for a guild including OAuth details
    /// </summary>
    public async Task<(ulong channelId, string? message, int day, bool enabled, DateTime? lastAnnouncement,
        string? accessToken, string? refreshToken, string? campaignId, DateTime? tokenExpiry)> GetPatreonOAuthConfig(
        ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        return (config.PatreonChannelId, config.PatreonMessage, config.PatreonAnnouncementDay,
            config.PatreonEnabled, config.PatreonLastAnnouncement, config.PatreonAccessToken,
            config.PatreonRefreshToken, config.PatreonCampaignId, config.PatreonTokenExpiry);
    }

    /// <summary>
    /// Manually triggers a Patreon announcement for a guild
    /// </summary>
    public async Task<bool> TriggerManualAnnouncement(ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        if (config.PatreonChannelId == 0)
            return false;

        var guild = client.GetGuild(guildId);
        if (guild == null)
            return false;

        var channel = guild.GetTextChannel(config.PatreonChannelId);
        if (channel == null)
            return false;

        await SendPatreonAnnouncement(guild, channel, config, true);
        return true;
    }

    /// <summary>
    /// Checks for monthly announcements that need to be sent
    /// </summary>
    private async Task CheckMonthlyAnnouncements()
    {
        var now = DateTime.UtcNow;
        var currentDay = now.Day;

        await using var uow = await db.CreateConnectionAsync();

        // Get all guilds that have Patreon enabled and should announce today
        var configs = await uow.GuildConfigs
            .Where(x => x.PatreonEnabled &&
                        x.PatreonChannelId != 0 &&
                        x.PatreonAnnouncementDay == currentDay &&
                        (x.PatreonLastAnnouncement == null ||
                         x.PatreonLastAnnouncement.Value.Month != now.Month ||
                         x.PatreonLastAnnouncement.Value.Year != now.Year))
            .ToListAsync();

        foreach (var config in configs)
        {
            try
            {
                await ProcessGuildAnnouncement(config);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing Patreon announcement for guild {GuildId}", config.GuildId);
            }
        }
    }

    /// <summary>
    /// Checks for overdue announcements on startup
    /// </summary>
    private async Task CheckForOverdueAnnouncements()
    {
        var now = DateTime.UtcNow;

        await using var uow = await db.CreateConnectionAsync();

        // Get guilds that should have announced this month but haven't
        var configs = await uow.GuildConfigs
            .Where(x => x.PatreonEnabled &&
                        x.PatreonChannelId != 0 &&
                        x.PatreonAnnouncementDay <= now.Day &&
                        (x.PatreonLastAnnouncement == null ||
                         x.PatreonLastAnnouncement.Value.Month != now.Month ||
                         x.PatreonLastAnnouncement.Value.Year != now.Year))
            .ToListAsync();

        Log.Information("Found {Count} overdue Patreon announcements", configs.Count);

        foreach (var config in configs)
        {
            try
            {
                await ProcessGuildAnnouncement(config);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing overdue Patreon announcement for guild {GuildId}", config.GuildId);
            }
        }
    }

    /// <summary>
    /// Processes a single guild's Patreon announcement
    /// </summary>
    private async Task ProcessGuildAnnouncement(GuildConfig config)
    {
        var guild = client.GetGuild(config.GuildId);
        if (guild == null)
        {
            Log.Warning("Guild {GuildId} not found for Patreon announcement", config.GuildId);
            return;
        }

        var channel = guild.GetTextChannel(config.PatreonChannelId);
        if (channel == null)
        {
            Log.Warning("Patreon channel {ChannelId} not found in guild {GuildId}",
                config.PatreonChannelId, config.GuildId);
            return;
        }

        await SendPatreonAnnouncement(guild, channel, config, false);
    }

    /// <summary>
    /// Sends the actual Patreon announcement message
    /// </summary>
    private async Task SendPatreonAnnouncement(IGuild guild, ITextChannel channel, GuildConfig config, bool isManual)
    {
        try
        {
            var socketGuild = guild as SocketGuild;
            var perms = socketGuild.CurrentUser.GetPermissions(channel);

            if (!perms.SendMessages)
            {
                Log.Warning("No permission to send messages in Patreon channel {ChannelId} for guild {GuildId}",
                    channel.Id, guild.Id);
                return;
            }

            // Prepare the message
            string message;
            if (!string.IsNullOrWhiteSpace(config.PatreonMessage))
            {
                // Use custom message with replacements
                var replacer = new ReplacementBuilder()
                    .WithServer(client, guild as SocketGuild)
                    .WithChannel(channel)
                    .WithOverride("%month%", () => DateTime.UtcNow.ToString("MMMM"))
                    .WithOverride("%year%", () => DateTime.UtcNow.Year.ToString())
                    .WithOverride("%patreon.link%", () => "https://patreon.com")
                    .Build();

                message = replacer.Replace(config.PatreonMessage);
            }
            else
            {
                // Use default message
                message =
                    $"🎉 It's a new month on Patreon! Thank you to all our amazing supporters for making this community possible. Check out the latest rewards and consider supporting us!";
            }

            // Send the message
            if (SmartEmbed.TryParse(message, guild.Id, out var embed, out var plainText, out var components))
            {
                await channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
            }
            else
            {
                await channel.SendMessageAsync(message);
            }

            // Update last announcement time
            await using var uow = await db.CreateConnectionAsync();
            config.PatreonLastAnnouncement = DateTime.UtcNow;
            await guildSettings.UpdateGuildConfig(guild.Id, config);

            Log.Information("Patreon announcement sent for guild {GuildId} in channel {ChannelId} (Manual: {IsManual})",
                guild.Id, channel.Id, isManual);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending Patreon announcement for guild {GuildId}", guild.Id);
        }
    }

    /// <summary>
    /// Gets statistics about Patreon announcements
    /// </summary>
    public async Task<(int enabledGuilds, int totalAnnouncements)> GetPatreonStats()
    {
        await using var uow = await db.CreateConnectionAsync();

        var enabledGuilds = await uow.GuildConfigs
            .CountAsync(x => x.PatreonEnabled && x.PatreonChannelId != 0);

        var totalAnnouncements = await uow.GuildConfigs
            .CountAsync(x => x.PatreonLastAnnouncement != null);

        return (enabledGuilds, totalAnnouncements);
    }

    /// <summary>
    /// Fetches and updates supporter data for a guild from Patreon API
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Number of supporters updated, or -1 if failed</returns>
    public async Task<int> UpdateSupportersAsync(ulong guildId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();
            var guildConfig = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (guildConfig?.PatreonAccessToken == null || guildConfig.PatreonCampaignId == null)
            {
                Log.Warning("No Patreon access token or campaign ID found for guild {GuildId}", guildId);
                return -1;
            }

            // Check if token needs refreshing
            if (guildConfig.PatreonTokenExpiry <= DateTime.UtcNow.AddMinutes(5))
            {
                var refreshedToken = await RefreshTokenAsync(guildId);
                if (refreshedToken == null)
                {
                    Log.Error("Failed to refresh Patreon token for guild {GuildId}", guildId);
                    return -1;
                }

                guildConfig.PatreonAccessToken = refreshedToken.AccessToken;
                guildConfig.PatreonRefreshToken = refreshedToken.RefreshToken;
                guildConfig.PatreonTokenExpiry = DateTime.UtcNow.AddSeconds(refreshedToken.ExpiresIn);

                await uow.GuildConfigs
                    .Where(x => x.GuildId == guildId)
                    .UpdateAsync(x => new GuildConfig
                    {
                        PatreonAccessToken = guildConfig.PatreonAccessToken,
                        PatreonRefreshToken = guildConfig.PatreonRefreshToken,
                        PatreonTokenExpiry = guildConfig.PatreonTokenExpiry
                    });
            }

            var supporters = new List<PatreonMember>();
            string? cursor = null;

            // Fetch all supporters with pagination
            do
            {
                var response = await apiClient.GetCampaignMembersAsync(
                    guildConfig.PatreonAccessToken,
                    guildConfig.PatreonCampaignId,
                    cursor);

                if (response?.Data == null)
                {
                    Log.Warning("Failed to fetch supporters for guild {GuildId}", guildId);
                    break;
                }

                supporters.AddRange(response.Data);
                cursor = response.Links?.Next;
            } while (!string.IsNullOrEmpty(cursor));

            // Update database with supporter data
            var updatedCount = 0;
            foreach (var supporter in supporters)
            {
                try
                {
                    var existingSupporter = await uow.PatreonSupporters
                        .FirstOrDefaultAsync(x => x.GuildId == guildId && x.PatreonUserId == supporter.Id);

                    if (existingSupporter == null)
                    {
                        // Create new supporter record
                        var newSupporter = new PatreonSupporter
                        {
                            GuildId = guildId,
                            PatreonUserId = supporter.Id,
                            DiscordUserId = 0, // Will be linked later via commands
                            FullName = supporter.FullName ?? "Unknown",
                            Email = supporter.Email,
                            TierId = GetCurrentTierId(supporter),
                            AmountCents = supporter.CurrentlyEntitledAmountCents ?? 0,
                            PatronStatus = supporter.PatronStatus ?? "unknown",
                            PledgeRelationshipStart = ParseDateTime(supporter.PledgeRelationshipStart),
                            LastChargeDate = ParseDateTime(supporter.LastChargeDate),
                            LastChargeStatus = supporter.LastChargeStatus,
                            LifetimeAmountCents = supporter.LifetimeSupportCents ?? 0,
                            CurrentlyEntitledAmountCents = supporter.CurrentlyEntitledAmountCents ?? 0,
                            LastUpdated = DateTime.UtcNow
                        };

                        await uow.InsertAsync(newSupporter);
                        updatedCount++;
                    }
                    else
                    {
                        // Update existing supporter record
                        await uow.PatreonSupporters
                            .Where(x => x.Id == existingSupporter.Id)
                            .UpdateAsync(x => new PatreonSupporter
                            {
                                FullName = supporter.FullName ?? existingSupporter.FullName,
                                Email = supporter.Email ?? existingSupporter.Email,
                                TierId = GetCurrentTierId(supporter),
                                AmountCents = supporter.CurrentlyEntitledAmountCents ?? 0,
                                PatronStatus = supporter.PatronStatus ?? existingSupporter.PatronStatus,
                                PledgeRelationshipStart =
                                    ParseDateTime(supporter.PledgeRelationshipStart) ??
                                    existingSupporter.PledgeRelationshipStart,
                                LastChargeDate =
                                    ParseDateTime(supporter.LastChargeDate) ?? existingSupporter.LastChargeDate,
                                LastChargeStatus = supporter.LastChargeStatus ?? existingSupporter.LastChargeStatus,
                                LifetimeAmountCents =
                                    supporter.LifetimeSupportCents ?? existingSupporter.LifetimeAmountCents,
                                CurrentlyEntitledAmountCents =
                                    supporter.CurrentlyEntitledAmountCents ??
                                    existingSupporter.CurrentlyEntitledAmountCents,
                                LastUpdated = DateTime.UtcNow
                            });
                        updatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error updating supporter {SupporterId} for guild {GuildId}", supporter.Id, guildId);
                }
            }

            // Mark inactive supporters (not in current API response)
            var supporterIds = supporters.Select(s => s.Id).ToList();
            await uow.PatreonSupporters
                .Where(x => x.GuildId == guildId && !supporterIds.Contains(x.PatreonUserId))
                .UpdateAsync(x => new PatreonSupporter
                {
                    PatronStatus = "former_patron", LastUpdated = DateTime.UtcNow
                });

            Log.Information("Updated {Count} supporters for guild {GuildId}", updatedCount, guildId);
            return updatedCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating supporters for guild {GuildId}", guildId);
            return -1;
        }
    }

    /// <summary>
    /// Refreshes Patreon access token for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>New token response, or null if failed</returns>
    public async Task<PatreonTokenResponse?> RefreshTokenAsync(ulong guildId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();
            var guildConfig = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (guildConfig?.PatreonRefreshToken == null)
            {
                Log.Warning("No Patreon refresh token found for guild {GuildId}", guildId);
                return null;
            }

            var tokenResponse = await apiClient.RefreshTokenAsync(guildConfig.PatreonRefreshToken,
                creds.PatreonClientId, creds.PatreonClientSecret);
            if (tokenResponse != null)
            {
                await uow.GuildConfigs
                    .Where(x => x.GuildId == guildId)
                    .UpdateAsync(x => new GuildConfig
                    {
                        PatreonAccessToken = tokenResponse.AccessToken,
                        PatreonRefreshToken = tokenResponse.RefreshToken,
                        PatreonTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                    });

                Log.Information("Successfully refreshed Patreon token for guild {GuildId}", guildId);
            }

            return tokenResponse;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error refreshing Patreon token for guild {GuildId}", guildId);
            return null;
        }
    }

    /// <summary>
    /// Gets active supporters for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>List of active supporters</returns>
    public async Task<List<PatreonSupporter>> GetActiveSupportersAsync(ulong guildId)
    {
        await using var uow = await db.CreateConnectionAsync();
        return await uow.PatreonSupporters
            .Where(x => x.GuildId == guildId && x.PatronStatus == "active_patron")
            .OrderByDescending(x => x.AmountCents)
            .ToListAsync();
    }

    /// <summary>
    /// Gets Patreon tiers configured for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>List of configured tiers</returns>
    public async Task<List<PatreonTier>> GetTiersAsync(ulong guildId)
    {
        await using var uow = await db.CreateConnectionAsync();
        return await uow.PatreonTiers
            .Where(x => x.GuildId == guildId && x.IsActive)
            .OrderBy(x => x.AmountCents)
            .ToListAsync();
    }

    /// <summary>
    /// Gets Patreon goals for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>List of goals</returns>
    public async Task<List<PatreonGoal>> GetGoalsAsync(ulong guildId)
    {
        await using var uow = await db.CreateConnectionAsync();
        return await uow.PatreonGoals
            .Where(x => x.GuildId == guildId && x.IsActive)
            .OrderBy(x => x.AmountCents)
            .ToListAsync();
    }

    /// <summary>
    /// Links a Discord user to a Patreon supporter
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="discordUserId">Discord user ID</param>
    /// <param name="patreonUserId">Patreon user ID</param>
    /// <returns>True if linked successfully</returns>
    public async Task<bool> LinkUserAsync(ulong guildId, ulong discordUserId, string patreonUserId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();
            var supporter = await uow.PatreonSupporters
                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.PatreonUserId == patreonUserId);

            if (supporter == null)
            {
                return false;
            }

            await uow.PatreonSupporters
                .Where(x => x.Id == supporter.Id)
                .UpdateAsync(x => new PatreonSupporter
                {
                    DiscordUserId = discordUserId
                });

            // Trigger role sync for this user if enabled
            var config = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
            if (config?.PatreonRoleSync == true)
            {
                _ = Task.Run(async () => await SyncUserRolesAsync(guildId, discordUserId));
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error linking user {DiscordUserId} to Patreon {PatreonUserId} for guild {GuildId}",
                discordUserId, patreonUserId, guildId);
            return false;
        }
    }

    /// <summary>
    /// Synchronizes Discord roles for all linked Patreon supporters in a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Number of users updated</returns>
    public async Task<int> SyncAllRolesAsync(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
            {
                Log.Warning("Guild {GuildId} not found for role sync", guildId);
                return 0;
            }

            await using var uow = await db.CreateConnectionAsync();

            // Check if role sync is enabled
            var config = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
            if (config?.PatreonRoleSync != true)
            {
                Log.Information("Patreon role sync is disabled for guild {GuildId}", guildId);
                return 0;
            }

            // Get all linked supporters
            var supporters = await uow.PatreonSupporters
                .Where(x => x.GuildId == guildId && x.DiscordUserId != 0 && x.PatronStatus == "active_patron")
                .ToListAsync();

            // Get tier mappings
            var tiers = await uow.PatreonTiers
                .Where(x => x.GuildId == guildId && x.IsActive && x.DiscordRoleId != 0)
                .ToListAsync();

            if (!tiers.Any())
            {
                Log.Warning("No Patreon tier role mappings configured for guild {GuildId}", guildId);
                return 0;
            }

            var updatedCount = 0;
            foreach (var supporter in supporters)
            {
                try
                {
                    if (await SyncUserRolesAsync(guildId, supporter.DiscordUserId))
                    {
                        updatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error syncing roles for user {UserId} in guild {GuildId}", supporter.DiscordUserId,
                        guildId);
                }
            }

            Log.Information("Synchronized roles for {Count} users in guild {GuildId}", updatedCount, guildId);
            return updatedCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error syncing all roles for guild {GuildId}", guildId);
            return 0;
        }
    }

    /// <summary>
    /// Synchronizes Discord roles for a specific user based on their Patreon support
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="discordUserId">Discord user ID</param>
    /// <returns>True if roles were updated</returns>
    public async Task<bool> SyncUserRolesAsync(ulong guildId, ulong discordUserId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null) return false;

            var user = guild.GetUser(discordUserId);
            if (user == null) return false;

            await using var uow = await db.CreateConnectionAsync();

            // Get supporter data
            var supporter = await uow.PatreonSupporters
                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.DiscordUserId == discordUserId);

            // Get all tier mappings
            var tiers = await uow.PatreonTiers
                .Where(x => x.GuildId == guildId && x.IsActive && x.DiscordRoleId != 0)
                .OrderByDescending(x => x.AmountCents)
                .ToListAsync();

            if (!tiers.Any()) return false;

            // Get current Patreon-managed roles
            var patreonRoleIds = tiers.Select(t => t.DiscordRoleId).ToHashSet();
            var currentPatreonRoles = user.Roles.Where(r => patreonRoleIds.Contains(r.Id)).ToList();

            // Determine target role based on supporter status and amount
            IRole? targetRole = null;
            if (supporter?.PatronStatus == "active_patron" && supporter.AmountCents > 0)
            {
                // Find the highest tier the user qualifies for
                var qualifyingTier = tiers.FirstOrDefault(t => supporter.AmountCents >= t.AmountCents);
                if (qualifyingTier != null)
                {
                    targetRole = guild.GetRole(qualifyingTier.DiscordRoleId);
                }
            }

            // Remove roles that should no longer be assigned
            var rolesToRemove = currentPatreonRoles.Where(r => targetRole == null || r.Id != targetRole.Id).ToList();
            foreach (var role in rolesToRemove)
            {
                try
                {
                    await user.RemoveRoleAsync(role);
                    Log.Information("Removed Patreon role {RoleName} from user {UserId} in guild {GuildId}",
                        role.Name, discordUserId, guildId);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to remove role {RoleId} from user {UserId} in guild {GuildId}",
                        role.Id, discordUserId, guildId);
                }
            }

            // Add target role if not already assigned
            if (targetRole != null && !user.Roles.Any(r => r.Id == targetRole.Id))
            {
                try
                {
                    await user.AddRoleAsync(targetRole);
                    Log.Information("Added Patreon role {RoleName} to user {UserId} in guild {GuildId}",
                        targetRole.Name, discordUserId, guildId);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to add role {RoleId} to user {UserId} in guild {GuildId}",
                        targetRole.Id, discordUserId, guildId);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error syncing roles for user {UserId} in guild {GuildId}", discordUserId, guildId);
            return false;
        }
    }

    /// <summary>
    /// Maps a Patreon tier to a Discord role
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="tierId">Patreon tier ID</param>
    /// <param name="roleId">Discord role ID</param>
    /// <returns>True if mapping was successful</returns>
    public async Task<bool> MapTierToRoleAsync(ulong guildId, string tierId, ulong roleId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();

            var tier = await uow.PatreonTiers
                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.TierId == tierId);

            if (tier == null)
            {
                Log.Warning("Patreon tier {TierId} not found for guild {GuildId}", tierId, guildId);
                return false;
            }

            await uow.PatreonTiers
                .Where(x => x.Id == tier.Id)
                .UpdateAsync(x => new PatreonTier
                {
                    DiscordRoleId = roleId
                });

            Log.Information("Mapped Patreon tier {TierId} to Discord role {RoleId} for guild {GuildId}",
                tierId, roleId, guildId);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error mapping tier {TierId} to role {RoleId} for guild {GuildId}",
                tierId, roleId, guildId);
            return false;
        }
    }

    /// <summary>
    /// Toggles role synchronization for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>New role sync status</returns>
    public async Task<bool> ToggleRoleSyncAsync(ulong guildId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();
            var config = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (config == null) return false;

            var newStatus = !config.PatreonRoleSync;
            await uow.GuildConfigs
                .Where(x => x.GuildId == guildId)
                .UpdateAsync(x => new GuildConfig
                {
                    PatreonRoleSync = newStatus
                });

            Log.Information("Patreon role sync {Status} for guild {GuildId}",
                newStatus ? "enabled" : "disabled", guildId);
            return newStatus;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling role sync for guild {GuildId}", guildId);
            return false;
        }
    }

    /// <summary>
    /// Gets the current tier ID for a supporter from included relationships
    /// </summary>
    /// <param name="supporter">Patreon supporter data</param>
    /// <returns>Current tier ID or null</returns>
    private static string? GetCurrentTierId(PatreonMember supporter)
    {
        // This would need to be implemented based on the relationships data structure
        // For now, return null as a placeholder
        return null;
    }

    /// <summary>
    /// Gets detailed analytics for Patreon supporters
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Analytics data</returns>
    public async Task<PatreonAnalytics> GetAnalyticsAsync(ulong guildId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();

            var supporters = await uow.PatreonSupporters
                .Where(x => x.GuildId == guildId)
                .ToListAsync();

            var activeSupporters = supporters.Where(s => s.PatronStatus == "active_patron").ToList();
            var formerSupporters = supporters.Where(s => s.PatronStatus == "former_patron").ToList();

            var totalRevenue = activeSupporters.Sum(s => s.AmountCents) / 100.0;
            var averageSupport = activeSupporters.Any() ? activeSupporters.Average(s => s.AmountCents) / 100.0 : 0;
            var lifetimeRevenue = supporters.Sum(s => s.LifetimeAmountCents) / 100.0;

            // Group by amount ranges
            var tierGroups = activeSupporters
                .GroupBy(s => GetTierGroup(s.AmountCents))
                .ToDictionary(g => g.Key, g => g.Count());

            // Monthly growth (approximate based on pledge start dates)
            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;
            var newThisMonth = supporters.Count(s =>
                s.PledgeRelationshipStart?.Month == currentMonth &&
                s.PledgeRelationshipStart?.Year == currentYear);

            return new PatreonAnalytics
            {
                TotalSupporters = supporters.Count,
                ActiveSupporters = activeSupporters.Count,
                FormerSupporters = formerSupporters.Count,
                LinkedSupporters = supporters.Count(s => s.DiscordUserId != 0),
                TotalMonthlyRevenue = totalRevenue,
                AverageSupport = averageSupport,
                LifetimeRevenue = lifetimeRevenue,
                NewSupportersThisMonth = newThisMonth,
                TierDistribution = tierGroups,
                TopSupporters = activeSupporters
                    .OrderByDescending(s => s.AmountCents)
                    .Take(5)
                    .Select(s => new TopSupporter
                    {
                        Name = s.FullName, Amount = s.AmountCents / 100.0, IsLinked = s.DiscordUserId != 0
                    })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting analytics for guild {GuildId}", guildId);
            return new PatreonAnalytics();
        }
    }

    /// <summary>
    /// Gets tier group name based on amount
    /// </summary>
    /// <param name="amountCents">Amount in cents</param>
    /// <returns>Tier group name</returns>
    private static string GetTierGroup(int amountCents)
    {
        var amount = amountCents / 100.0;
        return amount switch
        {
            < 5 => "$1-$4",
            < 10 => "$5-$9",
            < 25 => "$10-$24",
            < 50 => "$25-$49",
            < 100 => "$50-$99",
            _ => "$100+"
        };
    }

    /// <summary>
    /// Updates supporter recognition features like thank you messages
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="channelId">Channel to send thank you messages</param>
    /// <returns>Number of thank you messages sent</returns>
    public async Task<int> SendSupporterRecognitionAsync(ulong guildId, ulong channelId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null) return 0;

            var channel = guild.GetTextChannel(channelId);
            if (channel == null) return 0;

            await using var uow = await db.CreateConnectionAsync();

            // Get supporters who joined in the last 7 days
            var cutoffDate = DateTime.UtcNow.AddDays(-7);
            var newSupporters = await uow.PatreonSupporters
                .Where(x => x.GuildId == guildId &&
                            x.PatronStatus == "active_patron" &&
                            x.PledgeRelationshipStart >= cutoffDate)
                .ToListAsync();

            var messagesSent = 0;
            foreach (var supporter in newSupporters.Take(5)) // Limit to prevent spam
            {
                try
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("🎉 New Patreon Supporter!")
                        .WithDescription($"Let's welcome **{supporter.FullName}** to our Patreon family!")
                        .WithColor(0xF96854)
                        .AddField("Monthly Support", $"${supporter.AmountCents / 100.0:F2}", true)
                        .AddField("Started Supporting",
                            supporter.PledgeRelationshipStart?.ToString("MMMM dd, yyyy") ?? "Recently", true)
                        .WithThumbnailUrl("https://c5.patreon.com/external/logo/logomark_color_on_white.png");

                    if (supporter.DiscordUserId != 0)
                    {
                        var user = guild.GetUser(supporter.DiscordUserId);
                        if (user != null)
                        {
                            embed.AddField("Discord User", user.Mention, true);
                        }
                    }

                    await channel.SendMessageAsync(embed: embed.Build());
                    messagesSent++;

                    // Small delay to prevent rate limiting
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to send recognition message for supporter {SupporterId}",
                        supporter.PatreonUserId);
                }
            }

            Log.Information("Sent {Count} supporter recognition messages in guild {GuildId}", messagesSent, guildId);
            return messagesSent;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending supporter recognition for guild {GuildId}", guildId);
            return 0;
        }
    }

    /// <summary>
    /// Parses ISO datetime string to DateTime
    /// </summary>
    /// <param name="dateString">ISO datetime string</param>
    /// <returns>Parsed DateTime or null</returns>
    private static DateTime? ParseDateTime(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
            return null;

        return DateTime.TryParse(dateString, out var result) ? result : null;
    }

    /// <summary>
    /// Stores OAuth tokens and campaign information for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="accessToken">Patreon access token</param>
    /// <param name="refreshToken">Patreon refresh token</param>
    /// <param name="campaignId">Patreon campaign ID</param>
    /// <param name="expiresIn">Token expiration time in seconds</param>
    /// <returns>True if stored successfully</returns>
    public async Task<bool> StoreOAuthTokensAsync(ulong guildId, string accessToken, string refreshToken,
        string campaignId, int expiresIn)
    {
        try
        {
            var config = await guildSettings.GetGuildConfig(guildId);

            await using var uow = await db.CreateConnectionAsync();
            config.PatreonAccessToken = accessToken;
            config.PatreonRefreshToken = refreshToken;
            config.PatreonCampaignId = campaignId;
            config.PatreonTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

            await guildSettings.UpdateGuildConfig(guildId, config);

            Log.Information("Stored Patreon OAuth tokens for guild {GuildId} with campaign {CampaignId}",
                guildId, campaignId);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error storing Patreon OAuth tokens for guild {GuildId}", guildId);
            return false;
        }
    }
}

/// <summary>
/// Patreon analytics data
/// </summary>
public class PatreonAnalytics
{
    /// <summary>
    /// Total number of supporters (all statuses)
    /// </summary>
    public int TotalSupporters { get; set; }

    /// <summary>
    /// Number of active supporters
    /// </summary>
    public int ActiveSupporters { get; set; }

    /// <summary>
    /// Number of former supporters
    /// </summary>
    public int FormerSupporters { get; set; }

    /// <summary>
    /// Number of supporters linked to Discord
    /// </summary>
    public int LinkedSupporters { get; set; }

    /// <summary>
    /// Total monthly revenue from active supporters
    /// </summary>
    public double TotalMonthlyRevenue { get; set; }

    /// <summary>
    /// Average support amount
    /// </summary>
    public double AverageSupport { get; set; }

    /// <summary>
    /// Total lifetime revenue from all supporters
    /// </summary>
    public double LifetimeRevenue { get; set; }

    /// <summary>
    /// Number of new supporters this month
    /// </summary>
    public int NewSupportersThisMonth { get; set; }

    /// <summary>
    /// Distribution of supporters by tier groups
    /// </summary>
    public Dictionary<string, int> TierDistribution { get; set; } = new();

    /// <summary>
    /// Top 5 supporters
    /// </summary>
    public List<TopSupporter> TopSupporters { get; set; } = new();
}

/// <summary>
/// Top supporter information
/// </summary>
public class TopSupporter
{
    /// <summary>
    /// Supporter name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Monthly support amount
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Whether supporter is linked to Discord
    /// </summary>
    public bool IsLinked { get; set; }
}