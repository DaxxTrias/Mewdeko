using System.Text.Json;
using Serilog;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
/// Parses streaming responses from Claude's API.
/// </summary>
public class ClaudeStreamParser : IAiStreamParser
{
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

            // Handle content_block_delta events for text
            if (eventType == "content_block_delta")
            {
                // Check if index exists
                if (!root.TryGetProperty("index", out _))
                {
                    Log.Warning("Missing required 'index' field in content_block_delta");
                    return "";
                }

                // Check if delta exists and extract content
                if (root.TryGetProperty("delta", out var deltaProperty))
                {
                    if (deltaProperty.TryGetProperty("type", out var deltaTypeProperty))
                    {
                        var deltaType = deltaTypeProperty.GetString();

                        if (deltaType == "text_delta" && deltaProperty.TryGetProperty("text", out var textProperty))
                        {
                            var text = textProperty.GetString() ?? "";
                            Log.Information($"Extracted text: {text}");
                            return text;
                        }
                    }
                }
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
                int inputTokens = 0;
                int outputTokens = 0;

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
                int inputTokens = 0;
                int outputTokens = 0;

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
                    return true;
                }

                // Or a message_delta with stop_reason
                if (eventType == "message_delta" &&
                    root.TryGetProperty("delta", out var deltaProperty) &&
                    deltaProperty.TryGetProperty("stop_reason", out _))
                {
                    Log.Information("Stream finished: received message_delta with stop_reason");
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error checking if Claude stream is finished");
            return false;
        }
    }
}