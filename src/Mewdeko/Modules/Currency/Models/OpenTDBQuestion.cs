namespace Mewdeko.Modules.Currency.Models;

/// <summary>
///     Represents a trivia question from the Open Trivia Database API.
/// </summary>
public class OpenTDBQuestion
{
    /// <summary>
    ///     Gets or sets the question category.
    /// </summary>
    public string category { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the question type.
    /// </summary>
    public string type { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the difficulty level.
    /// </summary>
    public string difficulty { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the question text.
    /// </summary>
    public string question { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the correct answer.
    /// </summary>
    public string correct_answer { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the incorrect answers.
    /// </summary>
    public string[] incorrect_answers { get; set; } = Array.Empty<string>();
}