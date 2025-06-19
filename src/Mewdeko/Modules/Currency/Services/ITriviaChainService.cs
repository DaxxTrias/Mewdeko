using Mewdeko.Modules.Currency.Models;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Service interface for managing trivia chain games.
/// </summary>
public interface ITriviaChainService : INService
{
    /// <summary>
    ///     Gets the trivia chain state for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The trivia chain state if it exists, otherwise null.</returns>
    TriviaChainState? GetTriviaChainState(ulong userId);

    /// <summary>
    ///     Creates a new trivia chain game for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="betAmount">The bet amount.</param>
    /// <param name="category">The trivia category.</param>
    /// <returns>The initial trivia chain state with the first question.</returns>
    Task<TriviaChainState> StartTriviaChainAsync(ulong userId, ulong guildId, long betAmount, string category);

    /// <summary>
    ///     Processes a trivia answer and updates the game state.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="answerIndex">The selected answer index.</param>
    /// <param name="chainState">The current trivia chain state.</param>
    /// <param name="currencyService">The currency service.</param>
    /// <returns>The result of processing the answer.</returns>
    Task<TriviaAnswerResult> ProcessTriviaAnswerAsync(IInteractionContext ctx, string answerIndex,
        TriviaChainState chainState, ICurrencyService currencyService);

    /// <summary>
    ///     Removes the trivia chain state for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    void RemoveTriviaChainState(ulong userId);
}