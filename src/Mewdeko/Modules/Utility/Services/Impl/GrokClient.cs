using System.ClientModel;
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

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.x.ai/v1"),
        };

        var apiKeyCredential = new ApiKeyCredential(apiKey);

        var client = new ChatClient(model, apiKeyCredential, options);

        var chatMessages = messages.Select<AiMessage, ChatMessage>(m => m.Role switch
        {
            "user" => new UserChatMessage(m.Content),
            "assistant" => new AssistantChatMessage(m.Content),
            "system" => new SystemChatMessage(m.Content),
            _ => throw new ArgumentException($"Unknown role: {m.Role}")
        }).ToList();

        var completionUpdates = client.CompleteChatStreamingAsync(chatMessages, cancellationToken: cancellationToken);

        return completionUpdates.Select(update => update.ContentUpdate.FirstOrDefault()?.Text ?? "");
    }
}