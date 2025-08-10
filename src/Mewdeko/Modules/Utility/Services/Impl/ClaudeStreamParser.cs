using System.Text.Json;
using Serilog;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     Parses streaming responses from Claude's API.
/// </summary>
public class ClaudeStreamParser : IAiStreamParser
{
    private readonly HashSet<string> webSearchPhrases = new()
    {
        "Let me search",
        "I'll search",
        "I'll help you",
        "Let me find",
        "I'll look for",
        "Let me look",
        "Searching for"
    };

    private int currentBlockIndex = -1;
    private string currentTextBuffer = "";

    private bool isCollectingPreToolText;

    /// <inheritdoc />
    public string ParseDelta(string json, AiService.AiProvider provider)
    {
        try
        {
            // Parse the JSON to get the event type
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProperty))
                return "";

            var eventType = typeProperty.GetString();

            // Track content block starts
            if (eventType == "content_block_start")
            {
                if (root.TryGetProperty("index", out var indexProp))
                {
                    currentBlockIndex = indexProp.GetInt32();

                    // First text block might contain tool explanation
                    if (currentBlockIndex == 0 &&
                        root.TryGetProperty("content_block", out var contentBlock) &&
                        contentBlock.TryGetProperty("type", out var blockType) &&
                        blockType.GetString() == "text")
                    {
                        isCollectingPreToolText = true;
                        currentTextBuffer = "";
                        Log.Information("Started collecting potential pre-tool text");
                    }
                }

                return "";
            }

            // Track content block stops
            if (eventType == "content_block_stop")
            {
                if (isCollectingPreToolText)
                {
                    // If we collected text that looks like tool explanation, don't emit it
                    if (ContainsWebSearchPhrase(currentTextBuffer))
                    {
                        Log.Information($"Suppressed tool explanation text: {currentTextBuffer}");
                        currentTextBuffer = "";
                        isCollectingPreToolText = false;
                        return "";
                    }
                    else
                    {
                        // It wasn't tool explanation, emit the buffered text
                        var buffered = currentTextBuffer;
                        currentTextBuffer = "";
                        isCollectingPreToolText = false;
                        return buffered;
                    }
                }

                return "";
            }

            // Handle content_block_delta events for text
            if (eventType == "content_block_delta")
            {
                // Check if delta exists and extract content
                if (root.TryGetProperty("delta", out var deltaProperty))
                {
                    if (deltaProperty.TryGetProperty("type", out var deltaTypeProperty))
                    {
                        var deltaType = deltaTypeProperty.GetString();

                        if (deltaType == "text_delta" && deltaProperty.TryGetProperty("text", out var textProperty))
                        {
                            var text = textProperty.GetString() ?? "";

                            // If we're collecting pre-tool text, buffer it
                            if (isCollectingPreToolText)
                            {
                                currentTextBuffer += text;

                                // Check if we've seen enough to determine if it's tool explanation
                                if (currentTextBuffer.Length > 100 ||
                                    currentTextBuffer.Contains(".") ||
                                    currentTextBuffer.Contains("\n"))
                                {
                                    if (ContainsWebSearchPhrase(currentTextBuffer))
                                    {
                                        Log.Information(
                                            $"Detected and suppressing tool explanation: {currentTextBuffer}");
                                        currentTextBuffer = "";
                                        isCollectingPreToolText = false;
                                        return "";
                                    }
                                }

                                // Don't emit yet, continue buffering
                                return "";
                            }

                            Log.Information($"Extracted text: {text}");
                            return text;
                        }
                    }
                }
            }

            // Handle tool_use events (client tools like get_user_info)
            if (eventType == "tool_use")
            {
                // If we were collecting text and see tool use, that text was tool explanation
                if (isCollectingPreToolText && !string.IsNullOrEmpty(currentTextBuffer))
                {
                    Log.Information($"Suppressed tool explanation due to tool use: {currentTextBuffer}");
                    currentTextBuffer = "";
                    isCollectingPreToolText = false;
                }

                // Log that we're using a tool but don't return text
                if (root.TryGetProperty("name", out var nameProperty))
                {
                    Log.Information($"Client tool use: {nameProperty.GetString()}");
                }

                return "";
            }

            // Handle server_tool_use events (e.g., web search)
            if (eventType == "server_tool_use")
            {
                // If we were collecting text and see tool use, that text was tool explanation
                if (isCollectingPreToolText && !string.IsNullOrEmpty(currentTextBuffer))
                {
                    Log.Information($"Suppressed tool explanation due to tool use: {currentTextBuffer}");
                    currentTextBuffer = "";
                    isCollectingPreToolText = false;
                }

                // Log that we're using a tool but don't return text
                if (root.TryGetProperty("name", out var nameProperty))
                {
                    Log.Information($"Server tool use: {nameProperty.GetString()}");
                }

                return "";
            }

            // Handle tool_result events
            if (eventType == "tool_result")
            {
                // Log that we received tool results but don't return text
                // The actual response will come in subsequent text blocks
                Log.Information("Received tool result");
                return "";
            }

            // Handle web_search_tool_result events
            if (eventType == "web_search_tool_result")
            {
                // Log that we received search results but don't return text
                // The actual response will come in subsequent text blocks
                Log.Information("Received web search results");
                return "";
            }

            return "";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error parsing Claude stream delta");
            return "";
        }
    }

    /// <inheritdoc />
    public (int InputTokens, int OutputTokens, int TotalTokens)? ParseUsage(string json, AiService.AiProvider provider)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check for message_delta with usage information
            if (root.TryGetProperty("type", out var typeProperty) &&
                typeProperty.GetString() == "message_delta" &&
                root.TryGetProperty("usage", out var usageProperty))
            {
                var inputTokens = 0;
                var outputTokens = 0;

                if (usageProperty.TryGetProperty("input_tokens", out var inputProperty))
                    inputTokens = inputProperty.GetInt32();

                if (usageProperty.TryGetProperty("output_tokens", out var outputProperty))
                    outputTokens = outputProperty.GetInt32();

                return (inputTokens, outputTokens, inputTokens + outputTokens);
            }

            // Also check message_start which can contain initial usage
            if (root.TryGetProperty("type", out typeProperty) &&
                typeProperty.GetString() == "message_start" &&
                root.TryGetProperty("message", out var messageProperty) &&
                messageProperty.TryGetProperty("usage", out usageProperty))
            {
                var inputTokens = 0;
                var outputTokens = 0;

                if (usageProperty.TryGetProperty("input_tokens", out var inputProperty))
                    inputTokens = inputProperty.GetInt32();

                if (usageProperty.TryGetProperty("output_tokens", out var outputProperty))
                    outputTokens = outputProperty.GetInt32();

                return (inputTokens, outputTokens, inputTokens + outputTokens);
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error parsing Claude stream usage");
            return null;
        }
    }

    /// <inheritdoc />
    public bool IsStreamFinished(string json, AiService.AiProvider provider)
    {
        var result = CheckStreamFinished(json, provider);
        return result.IsFinished;
    }

    /// <summary>
    ///     Checks if the stream is finished and returns both status and stop reason
    /// </summary>
    public (bool IsFinished, string StopReason) CheckStreamFinished(string json, AiService.AiProvider provider)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeProperty))
            {
                var eventType = typeProperty.GetString();

                // The message_stop event signals the end of the stream
                if (eventType == "message_stop")
                {
                    Log.Information("Stream finished: received message_stop event");
                    return (true, "end_turn");
                }

                // Or a message_delta with stop_reason
                if (eventType == "message_delta" &&
                    root.TryGetProperty("delta", out var deltaProperty) &&
                    deltaProperty.TryGetProperty("stop_reason", out var stopReasonProperty))
                {
                    var stopReason = stopReasonProperty.GetString();
                    Log.Information($"Stream finished: received message_delta with stop_reason: {stopReason}");
                    return (true, stopReason);
                }
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error checking if Claude stream is finished");
            return (false, null);
        }
    }

    private bool ContainsWebSearchPhrase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        var lowerText = text.ToLower();
        return webSearchPhrases.Any(phrase => lowerText.Contains(phrase.ToLower()));
    }
}