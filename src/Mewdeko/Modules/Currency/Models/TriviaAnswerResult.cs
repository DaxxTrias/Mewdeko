using Embed = Discord.Embed;

namespace Mewdeko.Modules.Currency.Models;

/// <summary>
///     Represents the result of processing a trivia answer.
/// </summary>
/// <param name="IsCorrect">Whether the answer was correct.</param>
/// <param name="GameCompleted">Whether the trivia chain game was completed successfully.</param>
/// <param name="GameFailed">Whether the trivia chain game failed.</param>
/// <param name="UpdatedState">The updated trivia chain state, if applicable.</param>
/// <param name="ResultMessage">A message describing the result.</param>
/// <param name="ResultEmbed">The embed to display for the result.</param>
/// <param name="NextComponents">The components for the next interaction, if applicable.</param>
public record TriviaAnswerResult(
    bool IsCorrect,
    bool GameCompleted,
    bool GameFailed,
    TriviaChainState? UpdatedState,
    string ResultMessage,
    Embed? ResultEmbed,
    MessageComponent? NextComponents
);