using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using DataModel;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     OpenAI API client implementation using the official OpenAI SDK.
/// </summary>
public class OpenAiClient : IAiClient
{
    // Define OpenAI model context windows from the documentation
    private static readonly Dictionary<string, int> ModelContextLimits = new()
    {
        {
            "gpt-4", 8192
        },
        {
            "gpt-4o", 128000
        },
        {
            "gpt-4.1", 1047576
        },
    };

    /// <summary>
    ///     Gets the AI provider type for this client.
    /// </summary>
    public AiService.AiProvider Provider
    {
        get
        {
            return AiService.AiProvider.OpenAi;
        }
    }

    private readonly HttpClient httpClient = new();

    // No custom public exceptions; map to HttpRequestException with StatusCode to simplify handling upstream

    /// <summary>
    ///     Streams a response from the OpenAI model.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the AI response.</returns>
    public Task<IAsyncEnumerable<string>> StreamResponseAsync(
        IEnumerable<AiMessage> messages,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var openAiMessages = messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new { role = m.Role.ToLowerInvariant(), content = m.Content });

        var payload = new
        {
            model,
            messages = openAiMessages,
            stream = true,
            stream_options = new
            {
                include_usage = true
            }
        };

        var payloadJson = JsonSerializer.Serialize(payload);

        async IAsyncEnumerable<string> StreamWithUsage()
        {
            int? promptTokens = null, completionTokens = null, totalTokens = null;
            var usageEmitted = false;
            await foreach (var json in StreamChatCompletionsAsync(httpClient, apiKey, payloadJson, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    // Try to parse usage from the chunk if present
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        Serilog.Log.Debug("Received chunk: {Json}", json);

                        if (doc.RootElement.TryGetProperty("usage", out var usageElem))
                        {
                            if (usageElem.TryGetProperty("prompt_tokens", out var promptElem))
                                promptTokens = promptElem.GetInt32();
                            if (usageElem.TryGetProperty("completion_tokens", out var completionElem))
                                completionTokens = completionElem.GetInt32();
                            if (usageElem.TryGetProperty("total_tokens", out var totalElem))
                                totalTokens = totalElem.GetInt32();
                            usageEmitted = true;
                        }
                    }
                    catch { /* ignore parse errors, just stream the chunk */ }
                    yield return json;
                }
            }

            // If usage was not found, make a non-streaming request to get usage
            if (!usageEmitted)
            {
                var usage = await FetchOpenAiUsageAsync(openAiMessages.ToList(), model, apiKey, cancellationToken);
                if (usage != null)
                {
                    promptTokens = usage.Value.PromptTokens;
                    completionTokens = usage.Value.CompletionTokens;
                    totalTokens = usage.Value.TotalTokens;
                }
            }

            // Always emit a final usage chunk
            var usageObj = new
            {
                usage = new
                {
                    prompt_tokens = promptTokens ?? 0,
                    completion_tokens = completionTokens ?? 0,
                    total_tokens = totalTokens ?? ((promptTokens ?? 0) + (completionTokens ?? 0))
                }
            };
            var usageJson = JsonSerializer.Serialize(usageObj);
            yield return usageJson;
        }

        return Task.FromResult<IAsyncEnumerable<string>>(StreamWithUsage());
    }

    // Helper to fetch usage stats after streaming finishes
    private async Task<(int PromptTokens, int CompletionTokens, int TotalTokens)?> FetchOpenAiUsageAsync(
        IEnumerable<object> messages, string model, string apiKey, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model,
            messages,
            stream = false
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        if (root.TryGetProperty("usage", out var usageElem))
        {
            int prompt = 0, completion = 0, total = 0;
            if (usageElem.TryGetProperty("prompt_tokens", out var promptElem))
                prompt = promptElem.GetInt32();
            if (usageElem.TryGetProperty("completion_tokens", out var completionElem))
                completion = completionElem.GetInt32();
            if (usageElem.TryGetProperty("total_tokens", out var totalElem))
                total = totalElem.GetInt32();
            else
                total = prompt + completion;
            return (prompt, completion, total);
        }
        return null;
    }

    /// <summary>
    ///     pre-parses the response from OpenAI's chat completions endpoint to filter out non-JSON elements
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    public static async IAsyncEnumerable<string> StreamChatCompletionsAsync(HttpClient httpClient, string apiKey, string payloadJson, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;  // skip empty lines (SSE heartbeat or spacing)

                    if (line.StartsWith("data: [DONE]"))
                        break;    // end of stream detected – stop iteration

                    if (!line.StartsWith("data: "))
                        continue; // skip any line that doesn't begin with the expected prefix

                    // Trim the "data: " prefix to isolate the JSON content
                    var json = line.Substring("data: ".Length).Trim();
                    if (string.IsNullOrWhiteSpace(json))
                        continue;  // skip if nothing after prefix (just in case)

                    // Add debug logging for each chunk received
                    //Serilog.Log.Information("OpenAI Stream Chunk: {Chunk}", json);

                    yield return json;  // yield the clean JSON string for parsing
                }
                yield break;
            }

            // Handle non-success
            var status = response.StatusCode;
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Try to extract OpenAI error payload
            string? errorCode = null;
            string? errorMessage = null;
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    errorMessage = err.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
                    errorCode = err.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
                }
            }
            catch { /* ignore parse errors */ }

            if (status == HttpStatusCode.TooManyRequests)
            {
                // Respect Retry-After when present
                int? retryAfterSeconds = null;
                if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
                    retryAfterSeconds = (int)Math.Ceiling(delta.TotalSeconds);
                else if (response.Headers.RetryAfter?.Date is DateTimeOffset when)
                    retryAfterSeconds = (int)Math.Ceiling((when - DateTimeOffset.UtcNow).TotalSeconds);

                // If last attempt, throw clean exception
                if (attempt == maxAttempts)
                {
                    var message = errorMessage ?? "OpenAI rate limit hit (429).";
                    if (retryAfterSeconds.HasValue)
                        message += $" retry_after_seconds={retryAfterSeconds.Value}";
                    throw new HttpRequestException(message, null, HttpStatusCode.TooManyRequests);
                }

                // Backoff with jitter
                var backoff = ComputeBackoffWithJitter(attempt, retryAfterSeconds);
                Serilog.Log.Warning("OpenAI 429 received. Backing off {DelayMs}ms (attempt {Attempt}/{Max}). Code={Code}", (int)backoff.TotalMilliseconds, attempt, maxAttempts, errorCode);
                await Task.Delay(backoff, cancellationToken);
                continue;
            }

            // Quota exhausted is typically 429 or 403 with code "insufficient_quota"
            if (string.Equals(errorCode, "insufficient_quota", StringComparison.OrdinalIgnoreCase))
            {
                var statusForQuota = status == 0 ? HttpStatusCode.Forbidden : status; // default to 403
                throw new HttpRequestException(errorMessage ?? "OpenAI insufficient_quota", null, statusForQuota);
            }

            // Other errors: do not spam stack traces here; throw a concise HttpRequestException with status code
            throw new HttpRequestException(errorMessage ?? $"OpenAI request failed: {(int)status} {status}", null, status);
        }
    }

    private static TimeSpan ComputeBackoffWithJitter(int attempt, int? retryAfterSeconds)
    {
        if (retryAfterSeconds.HasValue && retryAfterSeconds.Value > 0)
        {
            // Apply small jitter on top of server-provided delay
            var jitterMs = Random.Shared.Next(250, 750);
            return TimeSpan.FromSeconds(retryAfterSeconds.Value) + TimeSpan.FromMilliseconds(jitterMs);
        }

        // Exponential backoff with full jitter: base 1s, cap ~20s
        var maxDelayMs = (int)Math.Min(20000, Math.Pow(2, attempt) * 1000);
        var delayMs = Random.Shared.Next(500, maxDelayMs);
        return TimeSpan.FromMilliseconds(delayMs);
    }
}