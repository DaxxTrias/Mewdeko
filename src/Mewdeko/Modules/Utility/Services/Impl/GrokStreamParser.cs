using System;
using System.Text.Json;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility.Services.Impl
{
    /// <summary>
    /// Parser for streaming responses from xAI's Grok model (implements IAiStreamParser).
    /// </summary>
    public class GrokStreamParser : IAiStreamParser
    {
        /// <inheritdoc/>
        public string ParseDelta(string json, AiService.AiProvider provider)
        {
            // Ensure this parser is used for Grok only.
            if (provider != AiService.AiProvider.Grok)
                return string.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            // Remove SSE prefix if present (e.g., "data: ...").
            if (json.StartsWith("data:"))
            {
                json = json.Substring("data:".Length).Trim();
            }
            // Check for SSE done sentinel.
            if (json == "[DONE]")
                return string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                // Grok’s streaming JSON has a "delta" object with text content (similar to OpenAI’s delta).
                if (root.TryGetProperty("delta", out var deltaElem))
                {
                    // The text content might be under "text" (as per Grok wrapper) or "content" (if mimicking OpenAI).
                    if (deltaElem.ValueKind == JsonValueKind.Object)
                    {
                        if (deltaElem.TryGetProperty("text", out var textElem))
                        {
                            var text = textElem.GetString();
                            return string.IsNullOrEmpty(text) ? string.Empty : text;
                        }
                        if (deltaElem.TryGetProperty("content", out var contentElem))
                        {
                            var content = contentElem.GetString();
                            return string.IsNullOrEmpty(content) ? string.Empty : content;
                        }
                    }
                }
                // Also handle OpenAI-like structure if present (choices -> delta -> content).
                if (root.TryGetProperty("choices", out var choicesElem) && choicesElem.ValueKind == JsonValueKind.Array)
                {
                    // Get the first choice object
                    var firstChoice = choicesElem[0];
                    if (firstChoice.ValueKind == JsonValueKind.Object &&
                        firstChoice.TryGetProperty("delta", out var choiceDelta))
                    {
                        if (choiceDelta.TryGetProperty("content", out var contentElem))
                        {
                            var content = contentElem.GetString();
                            return string.IsNullOrEmpty(content) ? string.Empty : content;
                        }
                        if (choiceDelta.TryGetProperty("text", out var textElem))
                        {
                            var text = textElem.GetString();
                            return string.IsNullOrEmpty(text) ? string.Empty : text;
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                // Malformed JSON encountered – log and ignore this chunk
                Console.WriteLine($"GrokStreamParser: JSON parse error: {ex.Message}");
            }
            return string.Empty;
        }

        /// <inheritdoc/>
        public (int InputTokens, int OutputTokens, int TotalTokens)? ParseUsage(string json, AiService.AiProvider provider)
        {
            if (provider != AiService.AiProvider.Grok)
                return null;
            if (string.IsNullOrWhiteSpace(json))
                return null;
            // Strip "data:" prefix if present
            if (json.StartsWith("data:"))
            {
                json = json.Substring("data:".Length).Trim();
            }
            // Ignore [DONE] sentinel for usage
            if (json == "[DONE]")
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("usage", out var usageElem))
                {
                    int input = 0, output = 0, total = 0;
                    // If usage has detailed breakdown (like OpenAI's prompt/completion tokens)
                    if (usageElem.TryGetProperty("prompt_tokens", out var promptElem))
                        input = promptElem.GetInt32();
                    if (usageElem.TryGetProperty("completion_tokens", out var completionElem))
                        output = completionElem.GetInt32();
                    if (usageElem.TryGetProperty("total_tokens", out var totalElem))
                        total = totalElem.GetInt32();
                    else
                        total = input + output;
                    // Return usage tuple
                    return (input, output, total);
                }
            }
            catch (JsonException)
            {
                // If usage JSON is malformed, ignore it
            }
            return null;
        }

        /// <inheritdoc/>
        public bool IsStreamFinished(string json, AiService.AiProvider provider)
        {
            if (provider != AiService.AiProvider.Grok)
                return false;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            // Trim SSE prefix and whitespace
            var content = json;
            if (content.StartsWith("data:"))
            {
                content = content.Substring("data:".Length).Trim();
            }
            // If we receive the SSE termination signal
            if (content == "[DONE]")
                return true;

            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                // Check for an explicit finish reason (OpenAI-style) indicating end of stream
                if (root.TryGetProperty("choices", out var choicesElem) && choicesElem.ValueKind == JsonValueKind.Array)
                {
                    var firstChoice = choicesElem[0];
                    if (firstChoice.TryGetProperty("finish_reason", out var finishElem) &&
                        finishElem.ValueKind == JsonValueKind.String)
                    {
                        var reason = finishElem.GetString();
                        if (!string.IsNullOrEmpty(reason) && reason != "null")
                            return true;
                    }
                }
                // Alternatively, if usage info appears (usually final chunk), treat as finished
                if (root.TryGetProperty("usage", out _))
                {
                    return true;
                }
            }
            catch (JsonException)
            {
                // If we cannot parse it, assume not finished (to continue streaming)
            }
            return false;
        }
    }
}
