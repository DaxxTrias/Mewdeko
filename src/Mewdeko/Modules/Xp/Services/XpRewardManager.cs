using System.Text.Json;
using LinqToDB;
using DataModel;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Models;

using Serilog;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Manages XP rewards and notifications.
/// </summary>
public class XpRewardManager : INService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ICurrencyService currencyService;
    private readonly XpCacheManager cacheManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpRewardManager"/> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="currencyService">The currency service.</param>
    /// <param name="cacheManager">The cache manager.</param>
    public XpRewardManager(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        ICurrencyService currencyService,
        XpCacheManager cacheManager)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.currencyService = currencyService;
        this.cacheManager = cacheManager;
    }

    /// <summary>
    ///     Gets the role reward for a specific level.
    /// </summary>
    /// <param name="dbFactory">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level.</param>
    /// <returns>The role reward for the specified level, or null if none exists.</returns>
    public async Task<XpRoleReward?> GetRoleRewardForLevelAsync(MewdekoDb db, ulong guildId, int level)
    {
        // Create a cache key for Redis
        var cacheKey = $"xp:rewards:{guildId}:role:{level}";

        // Get Redis database from cache manager
        var redis = cacheManager.GetRedisDatabase();

        // Try to get from Redis
        var cachedValue = await redis.StringGetAsync(cacheKey);

        if (cachedValue.HasValue)
        {
            // Deserialize the JSON string back to XpRoleReward object
            return JsonSerializer.Deserialize<XpRoleReward>(cachedValue);
        }

        // Get from database if not in cache using LinqToDB
        var reward = await db.XpRoleRewards
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Level == level);

        if (reward == null) return null;

        // Serialize the object to JSON and store in Redis
        var serializedReward = JsonSerializer.Serialize(reward);
        await redis.StringSetAsync(cacheKey, serializedReward, TimeSpan.FromMinutes(30));

        return reward;
    }

    /// <summary>
    ///     Gets the currency reward for a specific level.
    /// </summary>
    /// <param name="dbFactory">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level.</param>
    /// <returns>The currency reward for the specified level, or null if none exists.</returns>
    public async Task<XpCurrencyReward?> GetCurrencyRewardForLevelAsync(MewdekoDb db, ulong guildId, int level)
    {
        // Check cache first
        var cacheKey = $"xp:rewards:{guildId}:currency:{level}";
        var redis = cacheManager.GetRedisDatabase();

        // Try to get from Redis
        var cachedValue = await redis.StringGetAsync(cacheKey);

        if (cachedValue.HasValue)
        {
            // Deserialize the JSON string back to XpCurrencyReward object
            return JsonSerializer.Deserialize<XpCurrencyReward>(cachedValue);
        }

        // Get from database if not in cache using LinqToDB
        var reward = await db.XpCurrencyRewards
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Level == level);

        if (reward == null) return null;

        // Serialize the object to JSON and store in Redis
        var serializedReward = JsonSerializer.Serialize(reward);
        await redis.StringSetAsync(cacheKey, serializedReward, TimeSpan.FromMinutes(30));

        return reward;
    }

    /// <summary>
    ///     Sets a role reward for a specific level.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level to set the reward for.</param>
    /// <param name="roleId">The role ID to award, or null to remove the reward.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetRoleRewardAsync(ulong guildId, int level, ulong? roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get existing reward using LinqToDB
        var existingReward = await db.XpRoleRewards
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Level == level);

        if (roleId.HasValue)
        {
            if (existingReward != null)
            {
                existingReward.RoleId = roleId.Value;
                // Update using LinqToDB
                await db.UpdateAsync(existingReward);
            }
            else
            {
                var newReward = new XpRoleReward
                {
                    GuildId = guildId,
                    Level = level,
                    RoleId = roleId.Value
                };
                // Insert using LinqToDB
                await db.InsertAsync(newReward);
            }
        }
        else if (existingReward != null)
        {
            // Delete using LinqToDB
            await db.XpRoleRewards
                .Where(x => x.Id == existingReward.Id)
                .DeleteAsync();
        }

        // Clear cache
        await cacheManager.GetRedisDatabase().KeyDeleteAsync($"xp:rewards:{guildId}:role:{level}");
    }

    /// <summary>
    ///     Sets a currency reward for a specific level.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level to set the reward for.</param>
    /// <param name="amount">The amount of currency to award, or 0 to remove the reward.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetCurrencyRewardAsync(ulong guildId, int level, long amount)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get existing reward using LinqToDB
        var existingReward = await db.XpCurrencyRewards
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Level == level);

        if (amount > 0)
        {
            if (existingReward != null)
            {
                existingReward.Amount = amount;
                // Update using LinqToDB
                await db.UpdateAsync(existingReward);
            }
            else
            {
                var newReward = new XpCurrencyReward
                {
                    GuildId = guildId,
                    Level = level,
                    Amount = amount
                };
                // Insert using LinqToDB
                await db.InsertAsync(newReward);
            }
        }
        else if (existingReward != null)
        {
            // Delete using LinqToDB
            await db.XpCurrencyRewards
                .Where(x => x.Id == existingReward.Id)
                .DeleteAsync();
        }

        // Clear cache
        await cacheManager.GetRedisDatabase().KeyDeleteAsync($"xp:rewards:{guildId}:currency:{level}");
    }

    /// <summary>
    ///     Sends XP level-up notifications.
    /// </summary>
    /// <param name="notifications">The list of notifications to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendNotificationsAsync(List<XpNotification> notifications)
    {
        if (notifications.Count == 0)
            return;

        foreach (var notification in notifications)
        {
            try
            {
                var guild = client.GetGuild(notification.GuildId);
                var user = guild?.GetUser(notification.UserId);

                if (guild == null || user == null)
                    continue;

                // Get notification message template
                var settings = await cacheManager.GetGuildXpSettingsAsync(notification.GuildId);
                var messageTemplate = settings.LevelUpMessage;

                // Format the message
                var formattedMessage = messageTemplate
                    .Replace("{UserMention}", user.Mention)
                    .Replace("{UserName}", user.Username)
                    .Replace("{Level}", notification.Level.ToString())
                    .Replace("{Server}", guild.Name)
                    .Replace("{Guild}", guild.Name);

                if (!string.IsNullOrEmpty(notification.Sources))
                {
                    formattedMessage += $" (Source: {notification.Sources})";
                }

                // Send the notification
                if (notification.NotificationType == XpNotificationType.Dm)
                {
                    var dmChannel = await user.CreateDMChannelAsync();
                    if (dmChannel != null)
                    {
                        await dmChannel.SendMessageAsync(
                            embed: new EmbedBuilder()
                                .WithColor(Color.Green)
                                .WithDescription(formattedMessage)
                                .WithTitle("Level Up!")
                                .Build()
                        );
                    }
                }
                else // Channel
                {
                    var channel = guild.GetTextChannel(notification.ChannelId);
                    var curUser = guild.GetUser(client.CurrentUser.Id);
                    var perms = curUser.GetPermissions(channel);
                    if (channel != null && perms.Has(ChannelPermission.SendMessages))
                    {
                        await channel.SendMessageAsync(
                            embed: new EmbedBuilder()
                                .WithColor(Color.Green)
                                .WithDescription(formattedMessage)
                                .Build()
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending XP notification for {UserId} in {GuildId}",
                    notification.UserId, notification.GuildId);
            }
        }
    }

    /// <summary>
    ///     Grants role rewards to users.
    /// </summary>
    /// <param name="rewards">The list of role rewards to grant.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task GrantRoleRewardsAsync(List<RoleRewardItem> rewards)
    {
        if (rewards.Count == 0)
            return;

        // Get unique guilds
        var guildIds = rewards.Select(r => r.GuildId).Distinct().ToList();

        foreach (var guildId in guildIds)
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                continue;

            // Get guild settings to check exclusivity
            var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);

            // Process rewards by user
            var userRewards = rewards.Where(r => r.GuildId == guildId).GroupBy(r => r.UserId);

            foreach (var userGroup in userRewards)
            {
                var userId = userGroup.Key;
                var user = guild.GetUser(userId);

                if (user == null)
                    continue;

                try
                {
                    // Apply exclusive role rewards if configured
                    if (settings.ExclusiveRoleRewards)
                    {
                        await ProcessExclusiveRoleRewardsAsync(guild, user, userGroup);
                    }
                    else
                    {
                        // Just add all role rewards
                        foreach (var reward in userGroup)
                        {
                            var role = guild.GetRole(reward.RoleId);
                            if (role != null && !user.Roles.Any(r => r.Id == role.Id))
                            {
                                await user.AddRoleAsync(role);
                                await Task.Delay(100); // Avoid rate limiting
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error granting role reward to {UserId} in {GuildId}", userId, guildId);
                }
            }
        }
    }

    /// <summary>
    ///     Processes exclusive role rewards, removing old rewards and adding new ones.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="user">The user.</param>
    /// <param name="userRewards">The user's rewards.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ProcessExclusiveRoleRewardsAsync(
        SocketGuild guild,
        SocketGuildUser user,
        IGrouping<ulong, RoleRewardItem> userRewards)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get all role rewards for the guild using LinqToDB
        var allRewardRoles = await db.XpRoleRewards
            .Where(r => r.GuildId == guild.Id)
            .Select(r => r.RoleId)
            .ToListAsync();

        // Remove old reward roles first
        foreach (var roleId in user.Roles.Where(r => allRewardRoles.Contains(r.Id)).Select(r => r.Id))
        {
            var role = guild.GetRole(roleId);
            if (role != null)
            {
                await user.RemoveRoleAsync(role);
                await Task.Delay(100); // Avoid rate limiting
            }
        }

        // Get highest level reward in this batch
        var highestReward = await GetHighestLevelRewardAsync(db, guild.Id, userRewards);

        if (highestReward != null)
        {
            var role = guild.GetRole(highestReward.RoleId);
            if (role != null)
            {
                await user.AddRoleAsync(role);
            }
        }
    }

    /// <summary>
    ///     Gets the highest level reward for a user from a group of rewards.
    /// </summary>
    /// <param name="dbFactory">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userRewards">The user's rewards.</param>
    /// <returns>The highest level reward.</returns>
    private static async Task<RoleRewardItem?> GetHighestLevelRewardAsync(
        MewdekoDb db,
        ulong guildId,
        IGrouping<ulong, RoleRewardItem> userRewards)
    {
        var rewardRoleLevels = new Dictionary<ulong, int>();

        foreach (var reward in userRewards)
        {
            // Get role reward using LinqToDB
            var roleReward = await db.XpRoleRewards
                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == reward.RoleId);

            if (roleReward != null)
            {
                rewardRoleLevels[reward.RoleId] = roleReward.Level;
            }
        }

        if (rewardRoleLevels.Count == 0)
            return null;

        var highestRoleId = rewardRoleLevels.OrderByDescending(x => x.Value).First().Key;

        return userRewards.FirstOrDefault(r => r.RoleId == highestRoleId);
    }

    /// <summary>
    ///     Grants currency rewards to users.
    /// </summary>
    /// <param name="rewards">The list of currency rewards to grant.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task GrantCurrencyRewardsAsync(List<CurrencyRewardItem> rewards)
    {
        if (rewards.Count == 0)
            return;

        foreach (var reward in rewards)
        {
            try
            {
                // Use the currency service to award currency
                await currencyService.AddUserBalanceAsync(reward.UserId, reward.Amount, reward.GuildId);

                // Add transaction record for the reward
                await currencyService.AddTransactionAsync(
                    reward.UserId,
                    reward.Amount,
                    $"XP Level-up reward",
                    reward.GuildId);

                Log.Information("Awarded {Amount} currency to {UserId} in {GuildId}",
                    reward.Amount, reward.UserId, reward.GuildId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error granting currency reward to {UserId} in {GuildId}",
                    reward.UserId, reward.GuildId);
            }
        }
    }
}