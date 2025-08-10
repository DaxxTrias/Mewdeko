using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Reputation.Services;

/// <summary>
///     Service responsible for handling all reputation-related notifications (DM + channel).
/// </summary>
public class RepNotificationService : INService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<RepNotificationService> logger;

    // Cache for notification cooldowns
    private readonly ConcurrentDictionary<(ulong userId, ulong guildId), DateTime> notificationCooldowns = new();
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RepNotificationService" /> class.
    /// </summary>
    /// <param name="client">The Discord sharded client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="strings">The localized bot strings.</param>
    /// <param name="logger">The logger instance.</param>
    public RepNotificationService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings,
        ILogger<RepNotificationService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.logger = logger;
    }

    /// <summary>
    ///     Sends reputation notifications for a reputation change event.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="receiverId">The user who received reputation.</param>
    /// <param name="giverId">The user who gave reputation (0 for system events).</param>
    /// <param name="amount">The amount of reputation changed.</param>
    /// <param name="newTotal">The user's new total reputation.</param>
    /// <param name="repType">The type of reputation given.</param>
    /// <param name="reason">Optional reason for the reputation change.</param>
    /// <param name="isAnonymous">Whether the reputation was given anonymously.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SendReputationNotificationAsync(ulong guildId, ulong receiverId, ulong giverId,
        int amount, int newTotal, string repType, string? reason = null, bool isAnonymous = false)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get user notification settings
            var userSettings = await GetUserNotificationSettingsAsync(db, guildId, receiverId);

            // Check if user wants DM notifications
            if (userSettings.ReceiveDMs && ShouldSendNotification(userSettings, amount, newTotal))
            {
                await SendUserDmNotificationAsync(guildId, receiverId, giverId, amount, newTotal,
                    repType, reason, isAnonymous);
            }

            // Send milestone notifications if applicable
            await CheckAndSendMilestoneNotificationAsync(guildId, receiverId, newTotal, amount);

            // Send admin notifications for significant events
            if (Math.Abs(amount) >= 10 || newTotal % 100 == 0)
            {
                await SendAdminNotificationAsync(guildId, receiverId, giverId, amount, newTotal, repType, reason);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending reputation notifications for user {UserId} in guild {GuildId}",
                receiverId, guildId);
        }
    }

    /// <summary>
    ///     Sends a DM notification to the user who received reputation.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="receiverId">The user who received reputation.</param>
    /// <param name="giverId">The user who gave reputation (0 for system events).</param>
    /// <param name="amount">The amount of reputation changed.</param>
    /// <param name="newTotal">The user's new total reputation.</param>
    /// <param name="repType">The type of reputation given.</param>
    /// <param name="reason">Optional reason for the reputation change.</param>
    /// <param name="isAnonymous">Whether the reputation was given anonymously.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SendUserDmNotificationAsync(ulong guildId, ulong receiverId, ulong giverId,
        int amount, int newTotal, string repType, string? reason, bool isAnonymous)
    {
        try
        {
            // Check cooldown
            var cooldownKey = (receiverId, guildId);
            if (notificationCooldowns.TryGetValue(cooldownKey, out var lastNotification) &&
                DateTime.UtcNow - lastNotification < TimeSpan.FromMinutes(5))
            {
                return; // Skip notification due to cooldown
            }

            var guild = client.GetGuild(guildId);
            if (guild == null) return;

            var user = guild.GetUser(receiverId);
            if (user == null) return;

            var giver = giverId == 0 ? null : giverId == receiverId ? null : guild.GetUser(giverId);

            var embed = new EmbedBuilder()
                .WithColor(amount > 0 ? Color.Green : Color.Red)
                .WithTitle(strings.RepNotificationTitle(guildId))
                .WithDescription(BuildNotificationMessage(guildId, amount, newTotal, repType,
                    giver, isAnonymous, reason))
                .WithFooter(strings.RepFooterGuild(guildId, guild.Name))
                .WithTimestamp(DateTimeOffset.UtcNow);

            await user.SendMessageAsync(embed: embed.Build());

            // Update cooldown
            notificationCooldowns.TryAdd(cooldownKey, DateTime.UtcNow);

            logger.LogDebug("Sent DM notification to user {UserId} for {Amount} reputation",
                receiverId, amount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send DM notification to user {UserId}", receiverId);
        }
    }

    /// <summary>
    ///     Checks if milestone notifications should be sent and sends them.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="newTotal">The user's new total reputation.</param>
    /// <param name="amount">The amount that was added/removed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CheckAndSendMilestoneNotificationAsync(ulong guildId, ulong userId, int newTotal, int amount)
    {
        // Check for major milestones (50, 100, 250, 500, 1000, etc.)
        var milestones = new[]
        {
            50, 100, 250, 500, 1000, 2500, 5000, 10000
        };
        var previousTotal = newTotal - amount;

        foreach (var milestone in milestones)
        {
            if (newTotal >= milestone && previousTotal < milestone)
            {
                await SendMilestoneNotificationAsync(guildId, userId, milestone);
                break; // Only announce one milestone at a time
            }
        }
    }

    /// <summary>
    ///     Sends a milestone notification to the configured channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="milestone">The milestone reached.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SendMilestoneNotificationAsync(ulong guildId, ulong userId, int milestone)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var config = await db.RepConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (config?.NotificationChannel == null) return;

            var guild = client.GetGuild(guildId);
            if (guild == null) return;

            var channel = guild.GetTextChannel(config.NotificationChannel.Value);
            if (channel == null) return;

            var user = guild.GetUser(userId) ?? await ((IGuild)guild).GetUserAsync(userId);
            var username = user?.ToString() ?? $"Unknown User ({userId})";

            var message = strings.RepMilestone(guildId, username, milestone);

            var embed = new EmbedBuilder()
                .WithColor(Color.Gold)
                .WithTitle(strings.RepMilestoneTitle(guildId))
                .WithDescription(message)
                .WithThumbnailUrl(user?.GetAvatarUrl() ?? user?.GetDefaultAvatarUrl())
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await channel.SendMessageAsync(embed: embed);

            logger.LogInformation("Sent milestone notification for user {UserId} reaching {Milestone} reputation",
                userId, milestone);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send milestone notification for user {UserId}", userId);
        }
    }

    /// <summary>
    ///     Sends admin notifications for significant reputation events.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="receiverId">The user who received reputation.</param>
    /// <param name="giverId">The user who gave reputation (0 for system events).</param>
    /// <param name="amount">The amount of reputation changed.</param>
    /// <param name="newTotal">The user's new total reputation.</param>
    /// <param name="repType">The type of reputation given.</param>
    /// <param name="reason">Optional reason for the reputation change.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SendAdminNotificationAsync(ulong guildId, ulong receiverId, ulong giverId,
        int amount, int newTotal, string repType, string? reason)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var config = await db.RepConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (config?.NotificationChannel == null) return;

            var guild = client.GetGuild(guildId);
            if (guild == null) return;

            var channel = guild.GetTextChannel(config.NotificationChannel.Value);
            if (channel == null) return;

            var receiver = guild.GetUser(receiverId) ?? await ((IGuild)guild).GetUserAsync(receiverId);
            var giver = giverId == 0 ? null : guild.GetUser(giverId) ?? await ((IGuild)guild).GetUserAsync(giverId);

            var receiverName = receiver?.ToString() ?? $"Unknown User ({receiverId})";
            var giverName = giver?.ToString() ?? "System";

            var embed = new EmbedBuilder()
                .WithColor(amount > 0 ? Color.Green : Color.Red)
                .WithTitle(strings.RepActivityTitle(guildId))
                .AddField("User", receiverName, true)
                .AddField("Amount", $"{(amount > 0 ? "+" : "")}{amount}", true)
                .AddField("New Total", newTotal.ToString(), true)
                .AddField("Type", repType, true)
                .AddField("Given By", giverName, true)
                .AddField("Reason", reason ?? "No reason provided")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send admin notification for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Gets user notification settings, creating defaults if they don't exist.
    /// </summary>
    /// <param name="db">Database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The user's notification settings.</returns>
    private async Task<RepUserSettings> GetUserNotificationSettingsAsync(MewdekoDb db, ulong guildId, ulong userId)
    {
        var settings = await db.RepUserSettings
            .FirstOrDefaultAsync(x => x.UserId == userId && x.GuildId == guildId);

        if (settings == null)
        {
            settings = new RepUserSettings
            {
                UserId = userId,
                GuildId = guildId,
                ReceiveDMs = true,
                DMThreshold = 10,
                PublicHistory = true,
                DateAdded = DateTime.UtcNow
            };
            await db.InsertAsync(settings);
        }

        return settings;
    }

    /// <summary>
    ///     Determines if a notification should be sent based on user settings and thresholds.
    /// </summary>
    /// <param name="settings">User notification settings.</param>
    /// <param name="amount">The amount of reputation changed.</param>
    /// <param name="newTotal">The user's new total reputation.</param>
    /// <returns>True if notification should be sent.</returns>
    private bool ShouldSendNotification(RepUserSettings settings, int amount, int newTotal)
    {
        // Always notify for significant changes
        if (Math.Abs(amount) >= 10) return true;

        // Check if user has reached their notification threshold
        if (newTotal > 0 && newTotal % settings.DMThreshold == 0) return true;

        // Notify for negative reputation if enabled
        if (amount < 0) return true;

        return false;
    }

    /// <summary>
    ///     Builds the notification message for DM notifications.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="amount">The amount of reputation changed.</param>
    /// <param name="newTotal">The user's new total reputation.</param>
    /// <param name="repType">The type of reputation given.</param>
    /// <param name="giver">The user who gave the reputation (null if anonymous/system).</param>
    /// <param name="isAnonymous">Whether the reputation was given anonymously.</param>
    /// <param name="reason">Optional reason for the reputation change.</param>
    /// <returns>The formatted notification message.</returns>
    private string BuildNotificationMessage(ulong guildId, int amount, int newTotal, string repType,
        IUser? giver, bool isAnonymous, string? reason)
    {
        var sign = amount > 0 ? "+" : "";
        var giverText = isAnonymous ? "someone" : giver?.ToString() ?? "System";

        var message = strings.RepNotificationMessage(guildId, sign, amount, repType, giverText, newTotal);

        if (!string.IsNullOrEmpty(reason))
        {
            message += $"\n**Reason:** {reason}";
        }

        return message;
    }
}