using System.ClientModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
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
    public async Task<IAsyncEnumerable<string>> StreamResponseAsync(IEnumerable<AiMessage> messages, string model,
    string apiKey, CancellationToken cancellationToken = default)
    {
        // Configure OpenAI SDK to point to xAI's endpoint
        var options = new OpenAIClientOptions
        {
            // openai and grok are very similar api wise, only really need to change endpoint afaik
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

        // Call the OpenAI-compatible streaming chat completion
        var completionStream = client.CompleteChatStreamingAsync(chatMessages, cancellationToken: cancellationToken);

        // Buffer the streamed content so we can fetch usage after the stream
        async IAsyncEnumerable<string> StreamWithUsage()
        {
            var streamedChunks = new List<string>();
            await foreach (var update in completionStream.WithCancellation(cancellationToken))
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
                streamedChunks.Add(json);
                yield return json;
            }

            // After streaming is done, fetch usage and emit a final usage chunk
            var usage = await FetchGrokUsageAsync(messages, model, apiKey, cancellationToken);
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

        return StreamWithUsage();
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
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("usage", out var usageElem))
            {
                int input = 0, output = 0, total = 0;
                if (usageElem.TryGetProperty("prompt_tokens", out var promptElem))
                    input = promptElem.GetInt32();
                if (usageElem.TryGetProperty("completion_tokens", out var completionElem))
                    output = completionElem.GetInt32();
                if (usageElem.TryGetProperty("total_tokens", out var totalElem))
                    total = totalElem.GetInt32();
                else
                    total = input + output;
                return (input, output, total);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Exception fetching Grok usage");
        }
        return null;
    }
}