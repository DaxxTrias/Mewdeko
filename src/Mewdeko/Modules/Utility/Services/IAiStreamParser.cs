namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Interface for parsing streaming responses from AI providers.
/// </summary>
public interface IAiStreamParser
{
    /// <summary>
    ///     Parses a delta update from the stream.
    /// </summary>
    /// <param name="json">The JSON response from the AI provider.</param>
    /// <param name="provider">The AI provider type.</param>
    /// <returns>The parsed text delta.</returns>
    public string ParseDelta(string json, AiService.AiProvider provider);

    /// <summary>
    ///     Parses token usage information from the stream.
    /// </summary>
    /// <param name="json">The JSON response from the AI provider.</param>
    /// <param name="provider">The AI provider type.</param>
    /// <returns>A tuple containing input, output, and total token counts.</returns>
    public (int InputTokens, int OutputTokens, int TotalTokens)? ParseUsage(string json, AiService.AiProvider provider);

    /// <summary>
    ///     Determines if the stream has finished.
    /// </summary>
    /// <param name="json">The JSON response from the AI provider.</param>
    /// <param name="provider">The AI provider type.</param>
    /// <returns>True if the stream is finished, false otherwise.</returns>
    public bool IsStreamFinished(string json, AiService.AiProvider provider);

    /// <summary>
    ///     Checks if the stream has finished and returns both status and stop reason.
    /// </summary>
    /// <param name="json">The JSON response from the AI provider.</param>
    /// <param name="provider">The AI provider type.</param>
    /// <returns>A tuple containing whether the stream is finished and the stop reason.</returns>
    public (bool IsFinished, string StopReason) CheckStreamFinished(string json, AiService.AiProvider provider);
}