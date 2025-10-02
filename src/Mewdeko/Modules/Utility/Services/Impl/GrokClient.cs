using System.ClientModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Runtime.CompilerServices;
using DataModel;
using OpenAI;
using OpenAI.Chat;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     OpenAI API client implementation using the official OpenAI SDK.
/// </summary>
public class GrokClient : IAiClient
{
    /// <summary>
    ///     Gets the AI provider type for this client.
    /// </summary>
    public AiService.AiProvider Provider
    {
        get
        {
            return AiService.AiProvider.Grok;
        }
    }

    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClaudeClient" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public GrokClient(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <summary>
    ///     Streams a response from the Grok model.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the AI response.</returns>
    public Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model,
    string apiKey, CancellationToken cancellationToken = default)
    {
        // Configure OpenAI SDK to point to xAI's endpoint
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.x.ai/v1"),
        };

        var apiKeyCredential = new ApiKeyCredential(apiKey);
        var client = new ChatClient(model, apiKeyCredential, options);

        // Convert AiMessage (our internal message type) to OpenAI.ChatMessage format
        var chatMessages = messages.Select<AiMessage, ChatMessage>(m => m.Role switch
        {
            "user" => new UserChatMessage(m.Content),
            "assistant" => new AssistantChatMessage(m.Content),
            "system" => new SystemChatMessage(m.Content),
            _ => throw new ArgumentException($"Unknown role: {m.Role}")
        }).ToList();

        async IAsyncEnumerable<string> StreamWithFallback([EnumeratorCancellation] CancellationToken ct)
        {
            // Preflight: check if chat/completions is available; if 404, fall back to Responses API
            var shouldUseResponses = await IsChatCompletionsNotFoundAsync(messages, model, apiKey, ct);
            if (shouldUseResponses)
            {
                await foreach (var json in StreamViaResponsesApiAsync(messages, model, apiKey, ct))
                {
                    yield return json;
                }
                yield break;
            }

            var completionStream = client.CompleteChatStreamingAsync(chatMessages, cancellationToken: ct);
            await foreach (var update in completionStream.WithCancellation(ct))
            {
                var text = update.ContentUpdate.FirstOrDefault()?.Text ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    yield return string.Empty;
                    continue;
                }

                var dataObj = new
                {
                    delta = new { text = text },
                };
                var json = JsonSerializer.Serialize(dataObj);
                yield return json;
            }

            // After streaming is done, fetch usage and emit a final usage chunk
            var usage = await FetchGrokUsageAsync(messages, model, apiKey, ct)
                       ?? await FetchGrokUsageViaResponsesAsync(messages, model, apiKey, ct);

            if (usage != null)
            {
                var usageObj = new
                {
                    usage = new
                    {
                        prompt_tokens = usage.Value.InputTokens,
                        completion_tokens = usage.Value.OutputTokens,
                        total_tokens = usage.Value.TotalTokens
                    }
                };
                var usageJson = JsonSerializer.Serialize(usageObj);
                yield return usageJson;
            }
        }

        return Task.FromResult<IAsyncEnumerable<string>>(StreamWithFallback(cancellationToken));
    }

    // Helper to fetch usage stats after streaming finishes
    private async Task<(int InputTokens, int OutputTokens, int TotalTokens)?> FetchGrokUsageAsync(
        IEnumerable<AiMessage> messages, string model, string apiKey, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                model,
                messages = messages.Select(m => new
                {
                    role = m.Role,
                    content = m.Content
                }).ToArray(),
                stream = false
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.ai/v1/chat/completions")
            {
                Content = content
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Serilog.Log.Error("Failed to fetch Grok usage: {Status} - {Error}", response.StatusCode, error);
                // If chat/completions is not found, try Responses API
                if ((int)response.StatusCode == 404)
                {
                    return await FetchGrokUsageViaResponsesAsync(messages, model, apiKey, cancellationToken);
                }
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("usage", out var usageElem))
            {
                var parsed = ParseUsageObject(usageElem);
                if (parsed != null)
                    return parsed;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Exception fetching Grok usage");
        }
        return null;
    }

    // Responses API streaming fallback
    private async IAsyncEnumerable<string> StreamViaResponsesApiAsync(
        IEnumerable<AiMessage> messages,
        string model,
        string apiKey,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var requestBody = new
        {
            model,
            // Provide both shapes to maximize compatibility
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            input = messages.Select(m => new
            {
                role = m.Role,
                content = new object[]
                {
                    new { type = "input_text", text = m.Content }
                }
            }).ToArray(),
            stream = true
        };

        var jsonRequest = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.ai/v1/responses")
        {
            Content = content
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Fallback to /messages if Responses API is not found
            if ((int)response.StatusCode == 404)
            {
                await foreach (var json in StreamViaMessagesApiAsync(messages, model, apiKey, cancellationToken))
                {
                    yield return json;
                }
                yield break;
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            Serilog.Log.Error("Grok Responses API streaming failed: {Status} - {Error}", response.StatusCode, error);
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
                break;

            if (line.StartsWith("data:"))
            {
                var payload = line.Substring("data:".Length).Trim();
                if (string.IsNullOrWhiteSpace(payload) || payload == "[DONE]")
                    continue;

                if (TryExtractDeltaOrUsage(payload, out var deltaJson, out var usageJson))
                {
                    if (deltaJson is not null)
                        yield return deltaJson;
                    if (usageJson is not null)
                        yield return usageJson;
                }
            }
        }
    }

    // Messages API streaming fallback (Anthropic-compatible style)
    private async IAsyncEnumerable<string> StreamViaMessagesApiAsync(
        IEnumerable<AiMessage> messages,
        string model,
        string apiKey,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var requestBody = new
        {
            model,
            messages = messages.Select(m => new
            {
                role = m.Role,
                content = new object[]
                {
                    new { type = "text", text = m.Content }
                }
            }).ToArray(),
            stream = true,
            max_tokens = 1024
        };

        var jsonRequest = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.ai/v1/messages")
        {
            Content = content
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            Serilog.Log.Error("Grok Messages API streaming failed: {Status} - {Error}", response.StatusCode, error);
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
                break;

            if (line.StartsWith("data:"))
            {
                var payload = line.Substring("data:".Length).Trim();
                if (string.IsNullOrWhiteSpace(payload) || payload == "[DONE]")
                    continue;

                if (TryExtractDeltaOrUsage(payload, out var deltaJson, out var usageJson))
                {
                    if (deltaJson is not null)
                        yield return deltaJson;
                    if (usageJson is not null)
                        yield return usageJson;
                }
            }
        }
    }

    private static string TryExtractTextDelta(JsonElement root)
    {
        // Shape 1: { delta: { text: "..." } }
        if (root.TryGetProperty("delta", out var delta))
        {
            if (delta.ValueKind == JsonValueKind.Object && delta.TryGetProperty("text", out var textElem))
                return textElem.GetString() ?? string.Empty;

            if (delta.ValueKind == JsonValueKind.String)
                return delta.GetString() ?? string.Empty;
        }

        // Shape 2: { type: "response.output_text.delta", delta: "..." }
        if (root.TryGetProperty("type", out var typeElem))
        {
            var type = typeElem.GetString();
            if (!string.IsNullOrEmpty(type) && type.Contains("delta", StringComparison.OrdinalIgnoreCase))
            {
                if (root.TryGetProperty("delta", out var d2) && d2.ValueKind == JsonValueKind.String)
                    return d2.GetString() ?? string.Empty;
            }
        }

        // Shape 3: { message: { content: [ { type: "output_text.delta", text: "..." } ] } }
        if (root.TryGetProperty("message", out var messageElem) &&
            messageElem.TryGetProperty("content", out var contentElem) &&
            contentElem.ValueKind == JsonValueKind.Array && contentElem.GetArrayLength() > 0)
        {
            var first = contentElem[0];
            if (first.ValueKind == JsonValueKind.Object)
            {
                if (first.TryGetProperty("text", out var textElem3))
                    return textElem3.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static (int InputTokens, int OutputTokens, int TotalTokens)? ParseUsageObject(JsonElement usageElem)
    {
        try
        {
            int input = 0, output = 0, total = 0;

            // Support both OpenAI-style and Responses-style field names
            if (usageElem.TryGetProperty("prompt_tokens", out var promptElem))
                input = promptElem.GetInt32();
            if (usageElem.TryGetProperty("input_tokens", out var inputElem))
                input = inputElem.GetInt32();

            if (usageElem.TryGetProperty("completion_tokens", out var completionElem))
                output = completionElem.GetInt32();
            if (usageElem.TryGetProperty("output_tokens", out var outputElem))
                output = outputElem.GetInt32();

            if (usageElem.TryGetProperty("total_tokens", out var totalElem))
                total = totalElem.GetInt32();
            else
                total = input + output;

            return (input, output, total);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryExtractDeltaOrUsage(string payload, out string? deltaJson, out string? usageJson)
    {
        deltaJson = null;
        usageJson = null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var text = TryExtractTextDelta(root);
            if (!string.IsNullOrEmpty(text))
            {
                deltaJson = JsonSerializer.Serialize(new { delta = new { text } });
            }
            else if (root.TryGetProperty("usage", out var usageElem))
            {
                var usage = ParseUsageObject(usageElem);
                if (usage != null)
                {
                    usageJson = JsonSerializer.Serialize(new
                    {
                        usage = new
                        {
                            prompt_tokens = usage.Value.InputTokens,
                            completion_tokens = usage.Value.OutputTokens,
                            total_tokens = usage.Value.TotalTokens
                        }
                    });
                }
            }

            return deltaJson != null || usageJson != null;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsChatCompletionsNotFoundAsync(
        IEnumerable<AiMessage> messages,
        string model,
        string apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                stream = false,
                max_tokens = 1
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.ai/v1/chat/completions")
            {
                Content = content
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);
            return (int)response.StatusCode == 404;
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 404)
        {
            return true;
        }
        catch
        {
            // For other errors, assume endpoint exists (e.g., 400 due to validation)
            return false;
        }
    }

    private async Task<(int InputTokens, int OutputTokens, int TotalTokens)?> FetchGrokUsageViaResponsesAsync(
        IEnumerable<AiMessage> messages, string model, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                input = messages.Select(m => new
                {
                    role = m.Role,
                    content = new object[]
                    {
                        new { type = "input_text", text = m.Content }
                    }
                }).ToArray(),
                stream = false
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.ai/v1/responses")
            {
                Content = content
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 404)
                {
                    return await FetchGrokUsageViaMessagesAsync(messages, model, apiKey, cancellationToken);
                }
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Serilog.Log.Error("Failed to fetch Grok usage (Responses API): {Status} - {Error}", response.StatusCode, error);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("usage", out var usageElem))
            {
                var parsed = ParseUsageObject(usageElem);
                if (parsed != null)
                    return parsed;
            }

            // Some responses might nest usage under "response"
            if (root.TryGetProperty("response", out var responseElem) &&
                responseElem.TryGetProperty("usage", out var usageElem2))
            {
                var parsed = ParseUsageObject(usageElem2);
                if (parsed != null)
                    return parsed;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Exception fetching Grok usage via Responses API");
        }

        return null;
    }

    private async Task<(int InputTokens, int OutputTokens, int TotalTokens)?> FetchGrokUsageViaMessagesAsync(
        IEnumerable<AiMessage> messages, string model, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                model,
                messages = messages.Select(m => new
                {
                    role = m.Role,
                    content = new object[]
                    {
                        new { type = "text", text = m.Content }
                    }
                }).ToArray(),
                stream = false,
                max_tokens = 1
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.ai/v1/messages")
            {
                Content = content
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Serilog.Log.Error("Failed to fetch Grok usage (Messages API): {Status} - {Error}", response.StatusCode, error);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("usage", out var usageElem))
            {
                var parsed = ParseUsageObject(usageElem);
                if (parsed != null)
                    return parsed;
            }

            if (root.TryGetProperty("response", out var responseElem) &&
                responseElem.TryGetProperty("usage", out var usageElem2))
            {
                var parsed = ParseUsageObject(usageElem2);
                if (parsed != null)
                    return parsed;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Exception fetching Grok usage via Messages API");
        }

        return null;
    }

    private static bool IsNotFound(Exception ex)
    {
        // Match common exception types to detect 404 without binding to specific SDK internals
        if (ex is HttpRequestException httpEx && (int?)httpEx.StatusCode == 404)
            return true;

        // ClientResultException from System.ClientModel can contain Status
        if (ex is ClientResultException cre)
        {
            try
            {
                var statusProp = cre.GetType().GetProperty("Status");
                if (statusProp != null)
                {
                    var statusVal = statusProp.GetValue(cre);
                    if (statusVal is int i && i == 404)
                        return true;
                }
            }
            catch { }
        }

        // Fallback: check message text
        return ex.Message.Contains("404", StringComparison.Ordinal);
    }
}