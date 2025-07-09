using System.IO;
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

    /// <summary>
    ///     Streams a response from the OpenAI model.
    /// </summary>
    /// <param name="messages">The conversation history.</param>
    /// <param name="model">The model identifier to use.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A stream containing the AI response.</returns>
    public async Task<IAsyncEnumerable<string>> StreamResponseAsync(
        IEnumerable<AiMessage> messages,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var openAiMessages = messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new { role = m.Role.ToLowerInvariant(), content = m.Content });

        var payload = new
        {
            model,
            messages = openAiMessages,
            stream = true
        };

        request.Content = new StringContent(
        JsonSerializer.Serialize(payload),
        Encoding.UTF8,
        "application/json");

        //var payloadJson = JsonSerializer.Serialize(payload);
        //// Add debug logging for the outgoing payload
        //Serilog.Log.Information("OpenAI Payload: {Payload}", payloadJson);

        //request.Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");

        // Use the static method to get the streaming JSON chunks
        var stream = StreamChatCompletionsAsync(httpClient, request);

        // Optionally, you can wrap this in Task.FromResult to match the signature
        return await Task.FromResult(stream);
    }

    /// <summary>
    ///     pre-parses the response from OpenAI's chat completions endpoint to filter out non-JSON elements
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    public static async IAsyncEnumerable<string> StreamChatCompletionsAsync(HttpClient httpClient, HttpRequestMessage request)
    {
        // Send request with streaming response
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync();
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
    }
}