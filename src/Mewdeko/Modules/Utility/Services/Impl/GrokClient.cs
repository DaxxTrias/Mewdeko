using System.ClientModel;
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
        await Task.CompletedTask;

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

        //return completionUpdates.Select(update => update.ContentUpdate.FirstOrDefault()?.Text ?? "");
        // Transform the streaming updates into JSON strings for the parser
        // Each update’s first content piece is wrapped in a JSON line prefixed with 'data:'.
        return completionStream.Select(update =>
        {
            string text = update.ContentUpdate.FirstOrDefault()?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            // Form a JSON chunk similar to OpenAI’s streaming format with a delta
            var dataObj = new
            {
                delta = new { text = text },
                // Optionally include a usage placeholder or other fields if needed
                // (usage will be updated in final chunk or outside this loop)
            };
            string json = JsonSerializer.Serialize(dataObj);
            return json;  // We can include "data: " prefix if our parser expects it
        });
    }
}