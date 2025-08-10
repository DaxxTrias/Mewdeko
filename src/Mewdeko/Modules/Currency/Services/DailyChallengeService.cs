using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Service for managing daily challenges in the currency system.
/// </summary>
public class DailyChallengeService : INService
{
    private readonly IDataConnectionFactory connectionFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DailyChallengeService" /> class.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory.</param>
    public DailyChallengeService(IDataConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    /// <summary>
    ///     Gets the current daily challenge for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The daily challenge or null if already completed.</returns>
    public async Task<DailyChallenge?> GetCurrentChallenge(ulong userId, ulong guildId)
    {
        await using var ctx = await connectionFactory.CreateConnectionAsync();
        var today = DateTime.UtcNow.Date;

        var existing = await ctx.DailyChallenges
            .FirstOrDefaultAsync(dc => dc.UserId == userId && dc.GuildId == guildId && dc.Date == today);

        if (existing != null)
        {
            return existing.IsCompleted ? null : existing;
        }

        var challenge = GenerateNewChallenge(userId, guildId, today);
        challenge.Id = await ctx.InsertWithInt32IdentityAsync(challenge);

        return challenge;
    }

    /// <summary>
    ///     Completes a daily challenge and awards the reward.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="challengeType">The type of challenge completed.</param>
    /// <returns>The reward amount.</returns>
    public async Task<long> CompleteChallenge(ulong userId, ulong guildId, DailyChallengeType challengeType)
    {
        await using var ctx = await connectionFactory.CreateConnectionAsync();
        var today = DateTime.UtcNow.Date;

        var challenge = await ctx.DailyChallenges
            .FirstOrDefaultAsync(dc => dc.UserId == userId && dc.GuildId == guildId &&
                                       dc.Date == today && dc.ChallengeType == challengeType);

        if (challenge == null || challenge.IsCompleted)
        {
            return 0;
        }

        challenge.Progress++;

        if (challenge.Progress >= challenge.RequiredAmount)
        {
            challenge.IsCompleted = true;
            challenge.CompletedAt = DateTime.UtcNow;
            await ctx.UpdateAsync(challenge);
            return challenge.RewardAmount;
        }

        await ctx.UpdateAsync(challenge);
        return 0;
    }

    /// <summary>
    ///     Gets the leaderboard for daily challenge completions.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>List of users and their completion counts.</returns>
    public async Task<List<(ulong UserId, int CompletionCount)>> GetLeaderboard(ulong guildId)
    {
        await using var ctx = await connectionFactory.CreateConnectionAsync();
        var thirtyDaysAgo = DateTime.UtcNow.Date.AddDays(-30);

        return await ctx.DailyChallenges
            .Where(dc => dc.GuildId == guildId && dc.Date >= thirtyDaysAgo && dc.IsCompleted)
            .GroupBy(dc => dc.UserId)
            .Select(g => new
            {
                UserId = g.Key, Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .Select(x => new ValueTuple<ulong, int>(x.UserId, x.Count))
            .ToListAsync();
    }

    private static DailyChallenge GenerateNewChallenge(ulong userId, ulong guildId, DateTime date)
    {
        var rand = new Random();
        var challengeTypes = Enum.GetValues<DailyChallengeType>();
        var challengeType = challengeTypes[rand.Next(challengeTypes.Length)];

        var (requiredAmount, baseReward, description) = challengeType switch
        {
            DailyChallengeType.PlayGames => (5, 100, "Play 5 currency games"),
            DailyChallengeType.WinGames => (3, 200, "Win 3 currency games"),
            DailyChallengeType.SpendCurrency => (500, 150, "Spend 500 currency on games"),
            DailyChallengeType.EarnCurrency => (300, 100, "Earn 300 currency from games"),
            DailyChallengeType.PlaySpecificGame => (3, 150, GetSpecificGameChallenge(rand)),
            _ => (1, 50, "Complete any game")
        };

        return new DailyChallenge
        {
            UserId = userId,
            GuildId = guildId,
            Date = date,
            ChallengeType = challengeType,
            Description = description,
            RequiredAmount = requiredAmount,
            RewardAmount = baseReward + rand.Next(0, 51), // Add up to 50 bonus
            Progress = 0,
            IsCompleted = false
        };
    }

    private static string GetSpecificGameChallenge(Random rand)
    {
        var games = new[]
        {
            "blackjack", "roulette", "slots", "crash", "keno", "plinko", "bingo", "memory", "trivia"
        };
        var game = games[rand.Next(games.Length)];
        return $"Play {game} 3 times";
    }
}

/// <summary>
///     Types of daily challenges.
/// </summary>
public enum DailyChallengeType
{
    /// <summary>Play any currency games.</summary>
    PlayGames,

    /// <summary>Win currency games.</summary>
    WinGames,

    /// <summary>Spend currency on games.</summary>
    SpendCurrency,

    /// <summary>Earn currency from games.</summary>
    EarnCurrency,

    /// <summary>Play a specific game type.</summary>
    PlaySpecificGame
}