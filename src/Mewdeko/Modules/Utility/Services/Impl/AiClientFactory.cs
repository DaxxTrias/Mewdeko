using System.Net.Http;

namespace Mewdeko.Modules.Utility.Services.Impl;

/// <summary>
/// Factory for creating AI clients and their corresponding stream parsers.
/// </summary>
public class AiClientFactory : IAiClientFactory
{
    private readonly Dictionary<AiService.AiProvider, (IAiClient Client, IAiStreamParser Parser)> clients;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiClientFactory"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public AiClientFactory(IHttpClientFactory httpClientFactory)
    {
        clients = new Dictionary<AiService.AiProvider, (IAiClient Client, IAiStreamParser Parser)>
        {
            [AiService.AiProvider.Claude] = (new ClaudeClient(httpClientFactory), new ClaudeStreamParser()),
            [AiService.AiProvider.Groq] = (new GroqClient(httpClientFactory), new GroqStreamParser())
        };
    }

    /// <inheritdoc />
    public (IAiClient Client, IAiStreamParser Parser) Create(AiService.AiProvider provider)
    {
        if (!clients.TryGetValue(provider, out var client))
            throw new NotSupportedException($"Provider {provider} not supported");

        return client;
    }
}