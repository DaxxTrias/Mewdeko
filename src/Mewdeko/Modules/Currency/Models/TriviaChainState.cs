namespace Mewdeko.Modules.Currency.Models;

/// <summary>
///     Represents the state of a trivia chain game for a user.
/// </summary>
/// <param name="UserId">The user ID of the player.</param>
/// <param name="GuildId">The guild ID where the game is being played.</param>
/// <param name="BetAmount">The amount bet for this trivia chain.</param>
/// <param name="Category">The trivia category.</param>
/// <param name="ChainLength">The current chain length.</param>
/// <param name="CurrentMultiplier">The current multiplier for winnings.</param>
/// <param name="TotalWinnings">The total winnings accumulated so far.</param>
/// <param name="CurrentQuestion">The current question being asked.</param>
/// <param name="CurrentOptions">The current answer options.</param>
/// <param name="CorrectAnswer">The correct answer for the current question.</param>
/// <param name="CreatedAt">When the trivia chain was created.</param>
public record TriviaChainState(
    ulong UserId,
    ulong GuildId,
    long BetAmount,
    string Category,
    int ChainLength,
    double CurrentMultiplier,
    long TotalWinnings,
    string CurrentQuestion,
    string[] CurrentOptions,
    string CorrectAnswer,
    DateTime CreatedAt
);