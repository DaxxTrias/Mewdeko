namespace Mewdeko.Modules.Currency.Models;

/// <summary>
///     Represents a response from the Open Trivia Database API.
/// </summary>
public class OpenTDBResponse
{
    /// <summary>
    ///     Gets or sets the response code.
    /// </summary>
    public int response_code { get; set; }

    /// <summary>
    ///     Gets or sets the results array.
    /// </summary>
    public OpenTDBQuestion[]? results { get; set; }
}