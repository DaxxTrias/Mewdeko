using System.Net.Http;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
///     Factory for creating AI clients and their corresponding stream parsers.
/// </summary>
public class AiClientFactory : IAiClientFactory
{
    private readonly Dictionary<AiService.AiProvider, (IAiClient Client, IAiStreamParser Parser)> clients;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiClientFactory" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public AiClientFactory(IHttpClientFactory httpClientFactory)
    {
        clients = new Dictionary<AiService.AiProvider, (IAiClient Client, IAiStreamParser Parser)>
        {
            [AiService.AiProvider.Claude] = (new ClaudeClient(httpClientFactory), new ClaudeStreamParser()),
            [AiService.AiProvider.Groq] = (new GroqClient(httpClientFactory), new GroqStreamParser()),
            [AiService.AiProvider.Grok] = (new GrokClient(httpClientFactory), new GrokStreamParser()),
            [AiService.AiProvider.OpenAi] = (new OpenAiClient(), new OpenAiStreamParser()),
        };
    }

    /// <inheritdoc />
    public (IAiClient Client, IAiStreamParser Parser) Create(AiService.AiProvider provider)
    {
        if (!Enum.IsDefined(provider))
            throw new NotSupportedException($"Provider {provider} is not a valid AI provider.");

        if (!clients.TryGetValue(provider, out var entry))
            throw new NotSupportedException($"Provider {provider} not supported");

        if (entry.Client is null || entry.Parser is null)
            throw new InvalidOperationException($"Provider {provider} is registered with an invalid client/parser.");

        if (entry.Client.Provider != provider)
        {
            throw new InvalidOperationException(
                $"Provider registry mismatch. Requested {provider} but client reports {entry.Client.Provider}.");
        }

        return entry;
    }
}