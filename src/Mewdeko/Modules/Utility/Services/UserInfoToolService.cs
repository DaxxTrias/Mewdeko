using System.Text.Json;
using LinqToDB;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service for providing user information to Claude AI tools
/// </summary>
public class UserInfoToolService : INService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly XpService xpService;

    /// <summary>
    ///     Initializes a new instance of the UserInfoToolService
    /// </summary>
    /// <param name="dbFactory">Database connection factory</param>
    /// <param name="xpService">XP service for user stats</param>
    /// <param name="client">Discord client for guild/user access</param>
    public UserInfoToolService(IDataConnectionFactory dbFactory, XpService xpService, DiscordShardedClient client)
    {
        this.dbFactory = dbFactory;
        this.xpService = xpService;
        this.client = client;
    }

    /// <summary>
    ///     Gets comprehensive user information for the specified user in a guild
    /// </summary>
    /// <param name="guildId">Guild ID</param>
    /// <param name="userId">User ID</param>
    /// <returns>JSON string containing user information</returns>
    public async Task<string> GetUserInfoAsync(ulong guildId, ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get guild and user objects
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return JsonSerializer.Serialize(new
                {
                    error = "Guild not found"
                });

            var guildUser = guild.GetUser(userId);
            var userInfo = new Dictionary<string, object>();

            // Basic Discord info
            if (guildUser != null)
            {
                userInfo["discord"] = new
                {
                    username = guildUser.Username,
                    display_name = guildUser.DisplayName,
                    nickname = guildUser.Nickname,
                    is_bot = guildUser.IsBot,
                    joined_at = guildUser.JoinedAt?.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    account_created = guildUser.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    avatar_url = guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl(),
                    roles = guildUser.Roles.Where(r => r.Id != guildId).Select(r => r.Name).ToArray()
                };
            }
            else
            {
                userInfo["discord"] = new
                {
                    error = "User not found in guild"
                };
            }

            // XP Information
            try
            {
                var xpStats = await xpService.GetUserXpStatsAsync(guildId, userId);
                var rank = await xpService.GetUserRankAsync(guildId, userId);

                userInfo["xp"] = new
                {
                    total_xp = xpStats.TotalXp,
                    level = xpStats.Level,
                    rank,
                    xp_for_next_level = xpStats.RequiredXp,
                    xp_progress = xpStats.LevelXp,
                    bonus_xp = xpStats.BonusXp
                };
            }
            catch
            {
                userInfo["xp"] = new
                {
                    error = "No XP data found"
                };
            }

            // Profile Information
            var discordUser = await db.DiscordUsers.FirstOrDefaultAsync(x => x.UserId == userId);
            if (discordUser != null)
            {
                userInfo["profile"] = new
                {
                    bio = discordUser.Bio,
                    pronouns = discordUser.Pronouns,
                    birthday = discordUser.Birthday?.ToString("yyyy-MM-dd"),
                    timezone = discordUser.BirthdayTimezone,
                    zodiac_sign = discordUser.ZodiacSign,
                    switch_friend_code = discordUser.SwitchFriendCode,
                    profile_privacy = discordUser.ProfilePrivacy,
                    stats_opt_out = discordUser.StatsOptOut
                };
            }

            // Currency Information
            var balance =
                await db.GuildUserBalances.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
            if (balance != null)
            {
                userInfo["currency"] = new
                {
                    balance = balance.Balance
                };
            }

            // Moderation Information (warnings, etc.)
            var warnings = await db.Warnings.Where(x => x.GuildId == guildId && x.UserId == userId).ToListAsync();
            var activeWarnings = warnings.Where(w => !w.Forgiven).ToList();

            userInfo["moderation"] = new
            {
                total_warnings = warnings.Count,
                active_warnings = activeWarnings.Count,
                recent_warnings = activeWarnings.Take(5).Select(w => new
                {
                    reason = w.Reason,
                    date = w.DateAdded?.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    moderator_id = w.Moderator
                }).ToArray()
            };

            // Activity Information
            var guildXp = await db.GuildUserXps.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
            if (guildXp != null)
            {
                userInfo["activity"] = new
                {
                    last_activity = guildXp.LastActivity.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    last_level_up = guildXp.LastLevelUp.ToString("yyyy-MM-dd HH:mm:ss UTC")
                };
            }

            return JsonSerializer.Serialize(userInfo, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"Failed to get user info: {ex.Message}"
            });
        }
    }

    /// <summary>
    ///     Gets a simplified user lookup by username or mention
    /// </summary>
    /// <param name="guildId">Guild ID</param>
    /// <param name="userQuery">Username, display name, or user mention</param>
    /// <returns>JSON string containing user search results</returns>
    public async Task<string> FindUserAsync(ulong guildId, string userQuery)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return JsonSerializer.Serialize(new
                {
                    error = "Guild not found"
                });

            // Clean up the query (remove @ mentions, etc.)
            var cleanQuery = userQuery.Trim('<', '@', '!', '>');

            // Try to parse as user ID first
            if (ulong.TryParse(cleanQuery, out var userId))
            {
                return await GetUserInfoAsync(guildId, userId);
            }

            // Search by username/display name
            var users = guild.Users
                .Where(u => u.Username.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase) ||
                            (u.DisplayName?.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (u.Nickname?.Contains(cleanQuery, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(5)
                .ToList();

            if (!users.Any())
                return JsonSerializer.Serialize(new
                {
                    error = "No users found matching that query"
                });

            if (users.Count == 1)
                return await GetUserInfoAsync(guildId, users[0].Id);

            // Multiple matches - return list
            var matches = users.Select(u => new
            {
                user_id = u.Id.ToString(), username = u.Username, display_name = u.DisplayName, nickname = u.Nickname
            }).ToArray();

            return JsonSerializer.Serialize(new
            {
                multiple_matches = true,
                matches,
                message = "Multiple users found. Please be more specific or use a user ID."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"Search failed: {ex.Message}"
            });
        }
    }
}