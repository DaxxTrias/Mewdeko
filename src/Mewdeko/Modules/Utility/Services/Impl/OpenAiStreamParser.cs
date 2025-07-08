using System.Text.Json;
using Serilog;

namespace Mewdeko.Modules.Utility.Services.Impl
{
    /// <summary>
    ///     Parses streaming responses from OpenAI's GPT-4o Chat Completion API.
    /// </summary>
    public class OpenAiStreamParser : IAiStreamParser
    {
        /// <inheritdoc />
        public string ParseDelta(string json, AiService.AiProvider provider)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check for the choices array and at least one choice
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];

                    // Extract content from the delta if present
                    if (firstChoice.TryGetProperty("delta", out var delta))
                    {
                        // Some delta chunks may not have content (e.g. role-only delta)
                        if (delta.TryGetProperty("content", out var content))
                        {
                            return content.GetString() ?? string.Empty;
                        }
                    }
                }

                // No content found in this chunk
                return string.Empty;
            }
            catch (Exception ex)
            {
                // Log and suppress any parsing errors to keep stream handling robust
                Log.Error(ex, "Error parsing OpenAI stream delta: {Json}", json);
                return string.Empty;
            }
        }

        /// <inheritdoc />
        public bool IsStreamFinished(string json, AiService.AiProvider provider)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("finish_reason", out var finishReason))
                {
                    // finish_reason is null for intermediate chunks, and a string (e.g. "stop") when done
                    var reason = finishReason.ValueKind == JsonValueKind.Null ? null : finishReason.GetString();
                    if (!string.IsNullOrEmpty(reason))
                    {
                        Log.Information($"Stream finished with reason: {reason}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking if OpenAI stream is finished: {Json}", json);
                return false;
            }
        }

        /// <inheritdoc />
        public (int InputTokens, int OutputTokens, int TotalTokens)? ParseUsage(string json, AiService.AiProvider provider)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Look for a usage object in the root (present in the final chunk if usage stats are enabled)
                if (root.TryGetProperty("usage", out var usageElem) && usageElem.ValueKind == JsonValueKind.Object)
                {
                    int promptTokens = 0;
                    int completionTokens = 0;
                    int totalTokens = 0;

                    if (usageElem.TryGetProperty("prompt_tokens", out var prompt))
                        promptTokens = prompt.GetInt32();
                    if (usageElem.TryGetProperty("completion_tokens", out var completion))
                        completionTokens = completion.GetInt32();
                    if (usageElem.TryGetProperty("total_tokens", out var total))
                        totalTokens = total.GetInt32();
                    else
                        totalTokens = promptTokens + completionTokens;

                    return (promptTokens, completionTokens, totalTokens);
                }

                // No usage info in this chunk
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error parsing OpenAI stream usage: {Json}", json);
                return null;
            }
        }
    }
}
