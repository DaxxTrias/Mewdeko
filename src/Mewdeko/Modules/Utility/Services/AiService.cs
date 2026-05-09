using System.Globalization;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Antlr4.Runtime.Misc;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Configs;
using Mewdeko.Modules.Utility.Services.Impl;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service that handles Ai-related functionality across different providers.
/// </summary>
public class AiService : INService
{
    /// <summary>
    ///     Defines the available AI providers.
    /// </summary>
    public enum AiProvider
    {
        /// <summary>
        ///     OpenAI's API provider.
        /// </summary>
        OpenAi,

        /// <summary>
        ///     Groq's API provider.
        /// </summary>
        Groq,

        /// <summary>
        ///     Anthropic's Claude API provider.
        /// </summary>
        Claude,

        /// <summary>
        ///     x.AI's Grok API provider.
        /// </summary>
        Grok
    }

    private readonly AiClientFactory aiClientFactory;
    private readonly BotConfigService botConfigService;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IHttpClientFactory httpFactory;
    private readonly ILogger<AiService> logger;
    private readonly ConcurrentDictionary<AiProvider, List<AiModel>> modelCache;
    private readonly ConcurrentDictionary<ulong, CleanupTrackedResponse> trackedResponseCleanupRequests = new();
    private readonly TimeSpan modelCacheExpiry = TimeSpan.FromHours(24);
    private readonly IServiceProvider serviceProvider;
    private readonly GeneratedBotStrings strings;
    private static readonly Emoji CleanupReaction = new("🗑️");
    private static readonly TimeSpan CleanupTrackingLifetime = TimeSpan.FromHours(12);
    private BotConfig BotConfig => botConfigService.Data;
    private static readonly Dictionary<string, AiProvider> ProviderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openai"] = AiProvider.OpenAi,
        ["open-ai"] = AiProvider.OpenAi,
        ["gpt"] = AiProvider.OpenAi,
        ["chatgpt"] = AiProvider.OpenAi,
        ["grok"] = AiProvider.Grok,
        ["xai"] = AiProvider.Grok,
        ["grokai"] = AiProvider.Grok,
        ["groq"] = AiProvider.Groq,
        ["claude"] = AiProvider.Claude,
        ["anthropic"] = AiProvider.Claude
    };
    private string currentToolId;
    private string currentToolInput = "";

    // Tool use tracking
    private string currentToolName;
    private bool isCollectingToolInput;
    private DateTime lastModelUpdate = DateTime.MinValue;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="httpFactory">The httpfactory factory.</param>
    /// <param name="strings">The localized strings service.</param>
    /// <param name="configService">The bot configuration service.</param>
    /// <param name="handler">The handler parameter.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    public AiService(IDataConnectionFactory dbFactory, IHttpClientFactory httpFactory,
        GeneratedBotStrings strings, BotConfigService configService, EventHandler handler, DiscordShardedClient client,
        ILogger<AiService> logger, IServiceProvider serviceProvider)
    {
        this.dbFactory = dbFactory;
        this.httpFactory = httpFactory;
        this.strings = strings;
        botConfigService = configService;
        this.client = client;
        this.logger = logger;
        this.serviceProvider = serviceProvider;
        aiClientFactory = new AiClientFactory(httpFactory);
        handler.Subscribe("MessageReceived", "AiService", HandleMessage);
        handler.Subscribe("ReactionAdded", "AiService", HandleAiCleanupReactionAdded);
        modelCache = new ConcurrentDictionary<AiProvider, List<AiModel>>();
    }


    /// <summary>
    ///     Gets or creates an Ai configuration for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The guild's Ai configuration.</returns>
    public async Task<GuildAiConfig> GetOrCreateConfig(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.GuildAiConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId)
               ?? new GuildAiConfig
               {
                   GuildId = guildId
               };
    }

    /// <summary>
    ///     Updates or creates a guild's Ai configuration.
    /// </summary>
    /// <param name="config">The configuration to update.</param>
    public async Task UpdateConfig(GuildAiConfig config)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        if (config.Id == 0)
            await db.InsertAsync(config);
        else
            await db.UpdateAsync(config);
    }

    /// <summary>
    ///     Gets all linked AI providers for a guild.
    /// </summary>
    public async Task<List<GuildAiProviderLink>> GetProviderLinks(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.GuildAiProviderLinks
            .Where(x => x.GuildId == guildId)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets a specific provider link for a guild.
    /// </summary>
    public async Task<GuildAiProviderLink?> GetProviderLink(ulong guildId, AiProvider provider)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.GuildAiProviderLinks
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Provider == (int)provider);
    }

    /// <summary>
    ///     Sets or updates the API key for a specific provider.
    /// </summary>
    public async Task SetProviderApiKey(ulong guildId, AiProvider provider, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be empty.", nameof(apiKey));

        await using var db = await dbFactory.CreateConnectionAsync();
        var now = DateTime.UtcNow;
        var normalizedKey = apiKey.Trim();
        var existing = await db.GuildAiProviderLinks
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Provider == (int)provider);

        if (existing is null)
        {
            var hasDefault = await db.GuildAiProviderLinks
                .AnyAsync(x => x.GuildId == guildId && x.IsDefault);

            await db.InsertAsync(new GuildAiProviderLink
            {
                GuildId = guildId,
                Provider = (int)provider,
                ApiKey = normalizedKey,
                IsEnabled = true,
                IsDefault = !hasDefault,
                DateAdded = now,
                DateUpdated = now
            });
        }
        else
        {
            existing.ApiKey = normalizedKey;
            existing.IsEnabled = true;
            existing.DateUpdated = now;
            await db.UpdateAsync(existing);
        }

        // Keep legacy single-provider fields synchronized for compatibility.
        var config = await db.GuildAiConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is null)
            return;

        if (config.Provider == (int)provider || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            config.Provider = (int)provider;
            config.ApiKey = normalizedKey;
            await db.UpdateAsync(config);
        }
    }

    /// <summary>
    ///     Sets or updates the default model for a specific provider.
    /// </summary>
    public async Task<bool> SetProviderModel(ulong guildId, AiProvider provider, string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = await db.GuildAiProviderLinks
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Provider == (int)provider);

        if (existing is null)
        {
            var legacy = await db.GuildAiConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
            if (legacy is null || legacy.Provider != (int)provider || string.IsNullOrWhiteSpace(legacy.ApiKey))
                return false;

            var hasDefault = await db.GuildAiProviderLinks
                .AnyAsync(x => x.GuildId == guildId && x.IsDefault);

            existing = new GuildAiProviderLink
            {
                GuildId = guildId,
                Provider = (int)provider,
                ApiKey = legacy.ApiKey!,
                IsEnabled = true,
                IsDefault = !hasDefault,
                DateAdded = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow
            };

            await db.InsertAsync(existing);

            existing = await db.GuildAiProviderLinks
                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Provider == (int)provider);
            if (existing is null)
                return false;
        }

        existing.DefaultModel = model;
        existing.DateUpdated = DateTime.UtcNow;
        await db.UpdateAsync(existing);

        // Keep legacy single-provider fields synchronized for compatibility.
        var config = await db.GuildAiConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is not null && config.Provider == (int)provider)
        {
            config.Model = model;
            await db.UpdateAsync(config);
        }

        return true;
    }

    /// <summary>
    ///     Sets the default provider for a guild.
    /// </summary>
    public async Task<bool> SetDefaultProvider(ulong guildId, AiProvider provider)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var links = await db.GuildAiProviderLinks
            .Where(x => x.GuildId == guildId)
            .ToListAsync();

        var target = links.FirstOrDefault(x => x.Provider == (int)provider);
        if (target is null)
            return false;

        foreach (var link in links)
        {
            var shouldBeDefault = link.Id == target.Id;
            if (link.IsDefault == shouldBeDefault)
                continue;

            link.IsDefault = shouldBeDefault;
            link.DateUpdated = DateTime.UtcNow;
            await db.UpdateAsync(link);
        }

        var config = await db.GuildAiConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (config is not null)
        {
            config.Provider = (int)provider;
            if (!string.IsNullOrWhiteSpace(target.DefaultModel))
                config.Model = target.DefaultModel;
            await db.UpdateAsync(config);
        }

        return true;
    }

    /// <summary>
    ///     Gets the API key for a provider from links, with legacy config fallback.
    /// </summary>
    public async Task<string?> GetProviderApiKey(ulong guildId, AiProvider provider)
    {
        var link = await GetProviderLink(guildId, provider);
        if (link is { IsEnabled: true } && !string.IsNullOrWhiteSpace(link.ApiKey))
            return link.ApiKey;

        var config = await GetOrCreateConfig(guildId);
        if (config.Provider == (int)provider && !string.IsNullOrWhiteSpace(config.ApiKey))
            return config.ApiKey;

        return null;
    }

    /// <summary>
    ///     Sets a custom embed template for AI responses in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the custom embed for.</param>
    /// <param name="customEmbed">The embed template, which can include %airesponse% placeholder.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetCustomEmbed(ulong guildId, string customEmbed)
    {
        var config = await GetOrCreateConfig(guildId);
        config.CustomEmbed = customEmbed == "-" ? "" : customEmbed;
        await UpdateConfig(config);
    }

    // Service method
    /// <summary>
    ///     Sets the webhook URL for AI responses in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="webhookUrl">The webhook URL, or null to disable webhooks.</param>
    public async Task SetWebhook(ulong guildId, string? webhookUrl)
    {
        var config = await GetOrCreateConfig(guildId);
        config.WebhookUrl = webhookUrl;
        await UpdateConfig(config);
    }

    private async Task HandleMessage(SocketMessage msg)
    {
        if (msg is not IUserMessage || msg.Author.IsBot)
            return;
        if (msg.Channel is not IGuildChannel guildChannel)
            return;

        var config = await GetOrCreateConfig(guildChannel.GuildId);
        if (!config.Enabled || config.ChannelId != msg.Channel.Id)
            return;

        if (IsDeleteSessionTrigger(msg.Content))
        {
            await ClearConversation(guildChannel.GuildId, msg.Author.Id);
            await msg.Channel.SendConfirmAsync(strings.AiConversationDeleted(guildChannel.GuildId));
            return;
        }

        var prefixes = GetConfiguredPrefixes();
        var prefix = prefixes.FirstOrDefault(p => msg.Content.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(prefix))
            return;

        var rawQuery = msg.Content.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            await msg.Channel.SendErrorAsync("Please provide a prompt after the AI trigger.", BotConfig);
            return;
        }

        // Keep existing built-in command words.
        var scannedWords = rawQuery
            .ToLowerInvariant()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .ToList();

        if (scannedWords.Contains("image"))
        {
            await msg.Channel.SendMessageAsync("Dall-E disabled.");
            return;
        }

        if (scannedWords.Contains("scan"))
        {
            await msg.Channel.SendMessageAsync("Not Yet Implemented.");
            return;
        }

        var routeResolution = await ResolveRouteAsync(config, rawQuery);
        if (!routeResolution.Success || routeResolution.Route is null)
        {
            await msg.Channel.SendErrorAsync(routeResolution.ErrorMessage ?? "Unable to resolve AI provider.", BotConfig);
            return;
        }

        DiscordWebhookClient? webhook = null;
        ulong? webhookMessageId = null;
        IUserMessage? processingMessage = null;

        if (!string.IsNullOrEmpty(config.WebhookUrl))
        {
            webhook = new DiscordWebhookClient(config.WebhookUrl);
            var processingEmbedBuilder = new EmbedBuilder()
                .WithOkColor()
                .WithDescription(strings.AiProcessingRequest(guildChannel.GuildId, msg.Author.Mention));
            AddProviderBranding(processingEmbedBuilder, routeResolution.Route.Provider);
            webhookMessageId = await webhook.SendMessageAsync(embeds:
            [
                processingEmbedBuilder.Build()
            ]);
        }
        else
        {
            processingMessage = await msg.Channel.SendConfirmAsync(strings.AiProcessingRequest(guildChannel.GuildId,
                msg.Author.Mention));
        }

        try
        {
            logger.LogInformation(
                "Processing AI request from {User} in {Channel} with provider {Provider} and model {Model}: {Query}",
                msg.Author.Username,
                msg.Channel.Name,
                routeResolution.Route.Provider,
                routeResolution.Route.Model,
                routeResolution.Prompt);

            await StreamResponse(config, webhookMessageId, msg, webhook, routeResolution.Prompt, routeResolution.Route);
            await UpdateConfig(config);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error in AI processing");
            if (webhook != null && webhookMessageId.HasValue)
            {
                await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                {
                    var errorEmbedBuilder = new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(strings.AiErrorOccurred(guildChannel.GuildId, ex.Message));
                    AddProviderBranding(errorEmbedBuilder, routeResolution.Route.Provider);
                    x.Embeds = new[]
                    {
                        errorEmbedBuilder.Build()
                    };
                });
            }
            else
            {
                await msg.Channel.SendErrorAsync(strings.AiErrorOccurred(guildChannel.GuildId, ex.Message), BotConfig);
            }
        }
        finally
        {
            webhook?.Dispose();
            if (processingMessage is not null)
            {
                try
                {
                    await processingMessage.DeleteAsync();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to delete processing message");
                }
            }
        }
    }

    private sealed record ResolvedAiRoute(AiProvider Provider, string ApiKey, string Model);

    private sealed record RouteResolutionResult(
        bool Success,
        string? ErrorMessage,
        string Prompt,
        ResolvedAiRoute? Route);

    private List<string> GetConfiguredPrefixes()
    {
        var configured = BotConfig.AiChatPrefixes?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length)
            .ToList();

        if (configured is { Count: > 0 } && !IsLegacyMixedPrefixSet(configured))
            return configured;

        return IsNightlyBranch()
            ? ["#frog ", ",frog "]
            : ["!frog ", ".frog "];
    }

    private bool IsDeleteSessionTrigger(string content)
    {
        var triggers = BotConfig.AiDeleteSessionTriggers?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (triggers is null || triggers.Count == 0 || IsLegacyMixedDeleteTriggerSet(triggers))
        {
            triggers = IsNightlyBranch()
                ? [",deletesession"]
                : [".deletesession"];
        }

        return triggers.Any(x => string.Equals(content, x, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsNightlyBranch()
    {
        var branch = BotConfig.UpdateBranch?.Trim();
        if (string.IsNullOrWhiteSpace(branch))
            return false;

        return branch.Equals("dev", StringComparison.OrdinalIgnoreCase)
               || branch.Equals("nightly", StringComparison.OrdinalIgnoreCase)
               || branch.Contains("dev", StringComparison.OrdinalIgnoreCase)
               || branch.Contains("nightly", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyMixedPrefixSet(List<string> prefixes)
    {
        var normalized = prefixes
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Prior mixed defaults from earlier refactor (caused cross-instance bleed).
        return normalized.SetEquals(
            ["!frog", ".frog", "#frog", ",frog", "-frog"]);
    }

    private static bool IsLegacyMixedDeleteTriggerSet(List<string> triggers)
    {
        var normalized = triggers
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return normalized.SetEquals([".deletesession", ",deletesession"]);
    }

    private static bool IsSupportedProvider(int providerValue)
    {
        return Enum.IsDefined(typeof(AiProvider), providerValue);
    }

    private static bool TryGetProviderFromAlias(string token, out AiProvider provider)
    {
        // Prevent numeric prompts (e.g., "3 is more than 2?") from being treated as enum values.
        if (IsNumericSelectorToken(token))
        {
            provider = default;
            return false;
        }

        if (ProviderAliases.TryGetValue(token, out provider))
            return true;

        return Enum.TryParse(token, true, out provider) && IsSupportedProvider((int)provider);
    }

    private static bool IsNumericSelectorToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token) && token.All(char.IsDigit);
    }

    private async Task<List<GuildAiProviderLink>> GetEffectiveProviderLinks(GuildAiConfig config)
    {
        var links = await GetProviderLinks(config.GuildId);
        var enabledLinks = links
            .Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.ApiKey))
            .ToList();

        // Legacy fallback path: old single-provider config still works without explicit link rows.
        if (IsSupportedProvider(config.Provider) && !string.IsNullOrWhiteSpace(config.ApiKey))
        {
            var hasLink = enabledLinks.Any(x => x.Provider == config.Provider);
            if (!hasLink)
            {
                enabledLinks.Add(new GuildAiProviderLink
                {
                    GuildId = config.GuildId,
                    Provider = config.Provider,
                    ApiKey = config.ApiKey!,
                    DefaultModel = config.Model,
                    IsEnabled = true,
                    IsDefault = enabledLinks.Count == 0
                });
            }
        }

        if (enabledLinks.Count > 0 && enabledLinks.All(x => !x.IsDefault))
        {
            var preferred = enabledLinks.FirstOrDefault(x => x.Provider == config.Provider) ?? enabledLinks[0];
            preferred.IsDefault = true;
        }

        return enabledLinks;
    }

    private async Task<RouteResolutionResult> ResolveRouteAsync(GuildAiConfig config, string rawQuery)
    {
        var links = await GetEffectiveProviderLinks(config);
        if (links.Count == 0)
        {
            return new RouteResolutionResult(false,
                "No AI provider is linked yet. Please link a provider key first (for example OpenAI or Grok).",
                rawQuery,
                null);
        }

        var parts = rawQuery.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        var selector = parts.Length > 0 ? parts[0].Trim() : string.Empty;
        var remainder = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        var hasNumericSelector = IsNumericSelectorToken(selector);

        var selectedByModel = hasNumericSelector
            ? null
            : links.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.DefaultModel) &&
                x.DefaultModel.Equals(selector, StringComparison.OrdinalIgnoreCase));

        if (selectedByModel is not null)
        {
            if (string.IsNullOrWhiteSpace(remainder))
                return new RouteResolutionResult(false, "Please include a prompt after the model selector.", rawQuery,
                    null);

            return new RouteResolutionResult(
                true,
                null,
                remainder,
                new ResolvedAiRoute((AiProvider)selectedByModel.Provider, selectedByModel.ApiKey,
                    selectedByModel.DefaultModel!));
        }

        if (!hasNumericSelector && TryGetProviderFromAlias(selector, out var selectedProvider))
        {
            var selectedByProvider = links.FirstOrDefault(x => x.Provider == (int)selectedProvider);
            if (selectedByProvider is null)
            {
                return new RouteResolutionResult(false,
                    $"Provider `{selector}` is not linked for this guild.",
                    rawQuery,
                    null);
            }

            if (string.IsNullOrWhiteSpace(selectedByProvider.DefaultModel))
            {
                return new RouteResolutionResult(false,
                    $"Provider `{selectedProvider}` is linked but has no default model configured.",
                    rawQuery,
                    null);
            }

            if (string.IsNullOrWhiteSpace(remainder))
            {
                return new RouteResolutionResult(false, "Please include a prompt after the provider selector.",
                    rawQuery,
                    null);
            }

            return new RouteResolutionResult(
                true,
                null,
                remainder,
                new ResolvedAiRoute((AiProvider)selectedByProvider.Provider, selectedByProvider.ApiKey,
                    selectedByProvider.DefaultModel!));
        }

        var looksLikeModelSelector =
            selector.StartsWith("gpt", StringComparison.OrdinalIgnoreCase) ||
            selector.StartsWith("grok", StringComparison.OrdinalIgnoreCase) ||
            selector.StartsWith("claude", StringComparison.OrdinalIgnoreCase) ||
            selector.StartsWith("llama", StringComparison.OrdinalIgnoreCase);

        if (looksLikeModelSelector && !string.IsNullOrWhiteSpace(remainder))
        {
            return new RouteResolutionResult(false,
                $"Model `{selector}` is not linked or not configured for this guild.",
                rawQuery,
                null);
        }

        var defaultLink = links.FirstOrDefault(x => x.IsDefault)
                          ?? links.FirstOrDefault(x => x.Provider == config.Provider)
                          ?? links[0];

        if (string.IsNullOrWhiteSpace(defaultLink.DefaultModel))
        {
            return new RouteResolutionResult(false,
                $"Default provider `{(AiProvider)defaultLink.Provider}` has no model configured.",
                rawQuery,
                null);
        }

        return new RouteResolutionResult(
            true,
            null,
            rawQuery,
            new ResolvedAiRoute((AiProvider)defaultLink.Provider, defaultLink.ApiKey, defaultLink.DefaultModel!));
    }

    /// <summary>
    ///     Clears an AI conversation and all associated messages for a specific guild and user
    /// </summary>
    /// <param name="guildId">The Discord guild identifier</param>
    /// <param name="userId">The Discord user identifier</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task ClearConversation(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var conversation = await db.AiConversations
            .LoadWithAsTable(x => x.AiMessages)
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (conversation != null)
        {
            try
            {
                // Delete all messages for this conversation
                await db.AiMessages
                    .Where(x => x.ConversationId == conversation.Id)
                    .DeleteAsync();

                // Delete the conversation itself
                await db.AiConversations
                    .Where(x => x.Id == conversation.Id)
                    .DeleteAsync();
            }
            catch (Exception e)
            {
                logger.LogError(e, "There was an issue deleting the conversation");
            }
        }
    }

    private async Task StreamResponse(GuildAiConfig config, ulong? webhookMessageId, SocketMessage userMsg,
        DiscordWebhookClient? webhook, string userQuery = null, ResolvedAiRoute? route = null,
        int accumulatedTokenCount = 0)
    {
        route ??= new ResolvedAiRoute((AiProvider)config.Provider, config.ApiKey ?? string.Empty, config.Model ?? string.Empty);
        if (!IsSupportedProvider((int)route.Provider))
            throw new InvalidOperationException($"Unsupported AI provider: {route.Provider}");
        if (string.IsNullOrWhiteSpace(route.ApiKey))
            throw new InvalidOperationException($"No API key linked for provider {route.Provider}.");
        if (string.IsNullOrWhiteSpace(route.Model))
            throw new InvalidOperationException($"No model configured for provider {route.Provider}.");

        var provider = route.Provider;
        var model = route.Model;
        var apiKey = route.ApiKey;

        await using var db = await dbFactory.CreateConnectionAsync();
        var conversation = await db.AiConversations
            .LoadWithAsTable(x => x.AiMessages)
            .FirstOrDefaultAsync(x => x.GuildId == config.GuildId && x.UserId == userMsg.Author.Id);

        var guildChannel = userMsg.Channel as SocketTextChannel;
        var replacer = new ReplacementBuilder()
            .WithChannel(userMsg.Channel)
            .WithUser(userMsg.Author)
            .WithServer(client, guildChannel.Guild)
            .WithClient(client)
            .Build();

        var sysPrompt = replacer.Replace(config.SystemPrompt);

        int convId;
        if (conversation == null)
        {
            if (!string.IsNullOrWhiteSpace(sysPrompt))
                conversation = new AiConversation
                {
                    GuildId = config.GuildId,
                    UserId = userMsg.Author.Id,
                    AiMessages =
                    [
                        new AiMessage
                        {
                            Role = "system", Content = sysPrompt
                        }
                    ]
                };
            else
                conversation = new AiConversation
                {
                    GuildId = config.GuildId,
                    UserId = userMsg.Author.Id,
                    AiMessages = []
                };
            convId = await db.InsertWithInt32IdentityAsync(conversation);
            conversation.Id = convId;
        }
        else
        {
            convId = conversation.Id;
        }

        // Create the user message object
        var userMessage = new AiMessage
        {
            ConversationId = convId,
            Role = "user",
            Content = userQuery
        };

        // Insert to database
        await db.InsertAsync(userMessage);

        // Maintain history size for all providers
        const int maxHistorySize = 5;

        // Create a list of all messages including the new user message
        var allMessages = conversation.AiMessages.ToList();
        allMessages.Add(userMessage);

        // Log all messages before trimming for debugging
        //logger.LogInformation("AI conversation messages before trimming (GuildId: {GuildId}, UserId: {UserId}):\n{Messages}",
        //    config.GuildId,
        //    userMsg.Author.Id,
        //    string.Join("\n---\n", allMessages.Select(m => $"[{m.Role}] (Id: {m.Id}) {m.Content}"))
        //);

        // Keep system message plus most recent messages
        var systemMessage = allMessages.FirstOrDefault(m => m.Role == "system");
        var nonSystemMessages = allMessages
            .Where(m => m.Role != "system")
            .OrderByDescending(m => m.Id)
            .Take(maxHistorySize)
            .ToList();

        var messagesToKeep = new HashSet<int>(nonSystemMessages.Select(m => m.Id));
        if (systemMessage != null)
            messagesToKeep.Add(systemMessage.Id);

        var toRemove = allMessages
            .Where(m => !messagesToKeep.Contains(m.Id))
            .ToList();

        if (toRemove.Any())
        {
            logger.LogInformation($"Removing {toRemove.Count} older messages from conversation to manage payload size");
            await db.AiMessages
                .Where(m =>
                    m.ConversationId == conversation.Id &&
                    !messagesToKeep.Contains(m.Id))
                .DeleteAsync();
        }

        // Create the final list of messages to send to the AI client
        var messagesToSend = allMessages
            .Where(m => messagesToKeep.Contains(m.Id))
            .OrderBy(m => m.Id)
            .ToList();

        var (aiClient, streamParser) = aiClientFactory.Create(provider);
        var responseBuilder = new StringBuilder();
        var lastUpdate = DateTime.UtcNow;
        var tokenCount = 0;
        var startingTotalTokensUsed = config.TokensUsed;

        // Store response message for regular (non-webhook) updates
        IUserMessage? regularMessage = null;

        // Create a replacer that will substitute %airesponse% with the current builder content
        replacer = new ReplacementBuilder()
            .WithChannel(userMsg.Channel)
            .WithUser(userMsg.Author)
            .WithServer(client, guildChannel.Guild)
            .WithClient(client)
            .WithOverride("%airesponse%", () => responseBuilder.ToString())
            .Build();

        // Process template once to maintain consistency
        string? initialTemplate = null;
        if (!string.IsNullOrEmpty(config.CustomEmbed))
        {
            // Pre-process the template, leaving %airesponse% placeholder intact
            initialTemplate = config.CustomEmbed;
        }

        var timeout = DateTime.UtcNow.AddMinutes(1); // 1-minute timeout as a safety

        // Filter out empty assistant messages
        messagesToSend = messagesToSend
            .Where(m => m.Role != "assistant" || !string.IsNullOrWhiteSpace(m.Content))
            .ToList();

        // Ensure last message is a user message (for current prompt)
        if (messagesToSend.Count == 0 || messagesToSend.Last().Role != "user")
        {
            messagesToSend.Add(new AiMessage
            {
                ConversationId = convId,
                Role = "user",
                Content = userQuery
            });
        }

        // Use tools if enabled and provider is Claude
        IAsyncEnumerable<string> stream;
        if (provider == AiProvider.Claude && aiClient is ClaudeClient claudeClient)
        {
            var enableWebSearch = config.WebSearchEnabled;
            var enableUserInfo = ShouldEnableUserInfoTool(userQuery);
            logger.LogInformation("Claude user info tool enabled: {Enabled}", enableUserInfo);

            stream = await claudeClient.StreamResponseAsync(messagesToSend, model, apiKey,
                enableWebSearch, enableUserInfo, guildChannel.Guild.Id);
        }
        else
        {
            stream = await aiClient.StreamResponseAsync(messagesToSend, model, apiKey);
        }

        string stopReason = null;
        var toolUseRequests = new List<ToolUseRequest>();

        string? lastRawJson = null;
        try
        {
            await foreach (var rawJson in stream)
            {
                // Ensure that rawJson is explicitly cast to a string before checking for null or empty
                if (rawJson is string jsonString && string.IsNullOrEmpty(jsonString)) continue;
                if (string.IsNullOrEmpty(rawJson)) continue;

                // Log raw response for debugging
                //logger.LogInformation($"{(AiProvider)config.Provider} raw response: {rawJson}");
                lastRawJson = rawJson; // Save the last chunk

                // IMPORTANT: Parse the delta to extract just the content
                var contentDelta = streamParser.ParseDelta(rawJson, provider);
                if (!string.IsNullOrEmpty(contentDelta))
                {
                    responseBuilder.Append(contentDelta);
                    //logger.LogInformation($"Added content delta: {contentDelta}");
                }

                // Check for usage information
                if (provider == AiProvider.Groq || provider == AiProvider.Claude || provider == AiProvider.Grok)
                {
                    var usage = streamParser.ParseUsage(rawJson, provider);
                    if (usage.HasValue)
                    {
                        tokenCount = usage.Value.TotalTokens;
                        logger.LogInformation($"Updated token count: {tokenCount}");
                    }
                }

                // Update UI more frequently during stream
                var now = DateTime.UtcNow;
                if ((now - lastUpdate).TotalSeconds >= 1 && !string.IsNullOrWhiteSpace(contentDelta))
                {
                    lastUpdate = now;
                    await UpdateMessageEmbed(false); // false = not final update
                }

                // Check for tool use requests
                HandleToolUseEventIfNeeded(rawJson, toolUseRequests);

                // Check if stream is finished and extract stop reason
                var streamFinishInfo = streamParser.CheckStreamFinished(rawJson, provider);
                if (streamFinishInfo.IsFinished)
                {
                    stopReason = streamFinishInfo.StopReason;
                    logger.LogInformation($"AI stream finished with stop reason: {stopReason}");
                    break;
                }

                if (DateTime.UtcNow > timeout)
                {
                    logger.LogWarning("AI stream timed out");
                    break;
                }
            }
        }
        catch (HttpRequestException hre) when (hre.StatusCode == HttpStatusCode.TooManyRequests)
        {
            // Graceful handling for OpenAI 429
            var friendly = "Rate limited by OpenAI. Please try again in a moment.";
            logger.LogWarning("OpenAI 429 received: {Message}. Tip: verify billing/credits; API tokens/credits may need replenishment.", hre.Message);

            if (webhook != null && webhookMessageId.HasValue)
            {
                await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                {
                    var errorEmbedBuilder = new EmbedBuilder().WithErrorColor().WithDescription(friendly);
                    AddProviderBranding(errorEmbedBuilder, provider);
                    x.Embeds = new[]
                    {
                        errorEmbedBuilder.Build()
                    };
                });
            }
            else
            {
                await userMsg.Channel.SendErrorAsync(friendly, BotConfig);
            }
            return;
        }
        catch (HttpRequestException hre) when (hre.StatusCode == HttpStatusCode.Forbidden || (hre.Message?.IndexOf("insufficient_quota", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            // Graceful handling for quota exceeded
            var friendly = "OpenAI API quota exceeded for this key. Please update billing or switch model.";
            logger.LogWarning("OpenAI quota error: {Message}. Tip: credits may be exhausted or tier downgraded; replenish tokens/credits or review billing.", hre.Message);

            if (webhook != null && webhookMessageId.HasValue)
            {
                await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                {
                    var errorEmbedBuilder = new EmbedBuilder().WithErrorColor().WithDescription(friendly);
                    AddProviderBranding(errorEmbedBuilder, provider);
                    x.Embeds = new[]
                    {
                        errorEmbedBuilder.Build()
                    };
                });
            }
            else
            {
                await userMsg.Channel.SendErrorAsync(friendly, BotConfig);
            }
            return;
        }

        // Grok and GPT give their usage stats on the final message, not every message like claude/groq
        if (lastRawJson != null && 
            (provider == AiProvider.OpenAi || provider == AiProvider.Grok))
        {
            logger.LogInformation("Parsing final usage from last raw JSON: {Json}", lastRawJson);
            var usage = streamParser.ParseUsage(lastRawJson, provider);
            if (usage.HasValue)
            {
                tokenCount = usage.Value.TotalTokens;
                logger.LogInformation($"Updated token count: {tokenCount}");
            }
        }

        // Handle tool use if the stream ended with tool_use stop reason
        if (stopReason == "tool_use" && toolUseRequests.Any())
        {
            logger.LogInformation($"Processing {toolUseRequests.Count} tool use requests");

            // Execute all tool requests and collect results
            var toolResults = new List<ToolResult>();
            foreach (var toolRequest in toolUseRequests)
            {
                var result = await ExecuteToolRequest(toolRequest, guildChannel.Guild.Id);
                toolResults.Add(result);
            }

            // Add the assistant message with tool use to conversation history
            // When Claude uses tools, the assistant message may have no text content
            var assistantContent = responseBuilder.ToString();
            logger.LogInformation(
                $"Assistant content length: {assistantContent?.Length ?? 0}, tool requests: {toolUseRequests.Count}");

            if (!string.IsNullOrWhiteSpace(assistantContent))
            {
                messagesToSend.Add(new AiMessage
                {
                    Role = "assistant", Content = assistantContent
                });
                logger.LogInformation("Added assistant message with content to conversation history");
            }
            else
            {
                logger.LogInformation("Skipping empty assistant message - Claude used tools without text content");
            }

            // Add tool results to conversation history
            var toolResultsContent = string.Join("\n", toolResults.Select(r =>
                $"<tool_result>\n<tool_use_id>{r.ToolUseId}</tool_use_id>\n{r.Content}\n</tool_result>"));

            messagesToSend.Add(new AiMessage
            {
                Role = "user", Content = toolResultsContent
            });

            // Continue the conversation with the tool results
            logger.LogInformation($"Continuing conversation with tool results. Total messages: {messagesToSend.Count}");
            for (var i = 0; i < messagesToSend.Count; i++)
            {
                var msg = messagesToSend[i];
                logger.LogInformation($"Message {i}: Role={msg.Role}, ContentLength={msg.Content?.Length ?? 0}");
            }

            await StreamResponse(config, webhookMessageId, userMsg, webhook, toolResultsContent, route,
                accumulatedTokenCount + tokenCount);
            return;
        }

        var requestTokenCount = accumulatedTokenCount + tokenCount;

        await db.InsertAsync(new AiMessage
        {
            ConversationId = convId,
            Role = "assistant",
            Content = responseBuilder.ToString()
        });
        config.TokensUsed += requestTokenCount;
        await db.UpdateAsync(config);

        // Final update with completed response
        await UpdateMessageEmbed(true); // true = final update
        await AttachCleanupReactionForRequester(userMsg.Author.Id, regularMessage, webhookMessageId, userMsg.Channel);
        return;

        string BuildTokenUsageFooter()
        {
            var currentRequestTokens = Math.Max(0, accumulatedTokenCount + tokenCount);
            var lifetimeTokens = Math.Max(0, startingTotalTokensUsed + currentRequestTokens);
            return $"{currentRequestTokens:N0} ({lifetimeTokens:N0})";
        }

        async Task UpdateMessageEmbed(bool isFinalUpdate)
        {
            string processedContent = null;
            var aiResponse = responseBuilder.ToString();
            const int maxEmbedSize = 4000;

            // Check if we need to split the response into multiple embeds
            var needsSplitting = aiResponse.Length > maxEmbedSize;
            var responseChunks = needsSplitting
                ? SplitLongText(aiResponse)
                : [aiResponse];

            // Handle JSON templates
            if (initialTemplate != null)
            {
                var isJsonEmbed = initialTemplate.TrimStart().StartsWith("{") &&
                                  (initialTemplate.Contains("\"embeds\"") || initialTemplate.Contains("\"embed\""));

                if (isJsonEmbed)
                {
                    // For JSON templates, try to create and send the embed(s)
                    try
                    {
                        if (needsSplitting)
                        {
                            // Handle multi-part response with JSON template
                            await SendMultipartJsonEmbeds(responseChunks, isFinalUpdate);
                            return;
                        }

                        // Single embed with JSON template - similar to original logic
                        var escapedContent = EscapeJsonString(aiResponse);
                        processedContent = initialTemplate.Replace("%airesponse%", escapedContent);

                        // Parse the JSON
                        var newEmbed = JsonSerializer.Deserialize<NewEmbed>(processedContent);

                        if (newEmbed != null && (newEmbed.Embeds?.Count > 0 || newEmbed.Embed != null))
                        {
                                var discordEmbeds = ApplyProviderBranding(GetDiscordEmbeds(newEmbed), provider);

                            if (discordEmbeds.Count > 0)
                            {
                                // Send the embed based on whether we're using webhooks or regular messages
                                if (webhook != null && webhookMessageId.HasValue)
                                {
                                    await webhook.ModifyMessageAsync(webhookMessageId.Value, msg =>
                                    {
                                        msg.Content = newEmbed.Content;
                                        msg.Embeds = discordEmbeds;
                                    });
                                    return;
                                }

                                if (regularMessage != null)
                                {
                                    await regularMessage.ModifyAsync(msg =>
                                    {
                                        msg.Content = newEmbed.Content;
                                        msg.Embeds = discordEmbeds.ToArray();
                                    });
                                    return;
                                }

                                regularMessage = await userMsg.Channel.SendMessageAsync(
                                    newEmbed.Content,
                                    embeds: discordEmbeds.ToArray(),
                                    allowedMentions: AllowedMentions.None);
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Error parsing JSON embed: {ex.Message}");
                        logger.LogWarning("Failed to parse JSON, falling back to standard embeds");
                        // Fall through to standard embed handling
                    }
                }
                else
                {
                    // For non-JSON templates
                    processedContent = replacer.Replace(initialTemplate);
                }
            }
            else
            {
                // No template, just use the response directly
                processedContent = aiResponse;
            }

            if (string.IsNullOrWhiteSpace(processedContent))
            {
                // If processedContent is blank, supply a default message so discord doesnt shit the bed with error 50035
                processedContent = "[AI did not respond]";
            }

            // If we get here, either we don't have a JSON embed, or JSON parsing failed
            // Fall back to the original handling or use the multi-part approach for long content
            processedContent = processedContent.EscapeWeirdStuff();

            if (needsSplitting)
            {
                // Send multiple regular embeds
                await SendMultipartRegularEmbeds(responseChunks, isFinalUpdate);
            }
            else
            {
                // Standard single embed - same as your original logic
                if (webhook != null && webhookMessageId.HasValue)
                {
                    // WEBHOOK CASE - fallback
                    if (SmartEmbed.TryParse(processedContent, config.GuildId, out var embedData, out var plainText,
                            out _))
                    {
                        // Use the parsed embed data
                        var modifiedEmbeds = (embedData ?? Array.Empty<Embed>()).Select(embed =>
                        {
                            var builder = embed.ToEmbedBuilder();
                            if (builder.Footer is null || isFinalUpdate)
                            {
                                builder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                    BuildTokenUsageFooter()));
                            }

                            return builder.Build();
                        }).ToList();
                        modifiedEmbeds = ApplyProviderBranding(modifiedEmbeds, provider);

                        if (modifiedEmbeds.Count > 0)
                        {
                            await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                            {
                                x.Content = plainText;
                                x.Embeds = modifiedEmbeds;
                            });
                        }
                        else
                        {
                            var embedBuilder = new EmbedBuilder()
                                .WithOkColor()
                                .WithDescription(plainText ?? processedContent)
                                .WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                    BuildTokenUsageFooter()));
                            AddProviderBranding(embedBuilder, provider);
                            var embed = embedBuilder.Build();
                            await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                            {
                                x.Content = null;
                                x.Embeds = new List<Embed> { embed };
                            });
                        }
                    }
                    else
                    {
                        // For non-embed content, create a simple embed
                        var embedBuilder = new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription(processedContent)
                            .WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                BuildTokenUsageFooter()));

                        AddProviderBranding(embedBuilder, provider);

                        var embed = embedBuilder.Build();

                        await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                        {
                            x.Content = null;
                            x.Embeds = new List<Embed>
                            {
                                embed
                            };
                        });
                    }
                }
                else
                {
                    // REGULAR (NON-WEBHOOK) CASE - fallback
                    if (regularMessage == null)
                    {
                        // First update - create new message
                        if (SmartEmbed.TryParse(processedContent, config.GuildId, out var embedData, out var plainText,
                                out _))
                        {
                            // Use the parsed embed data for the initial message
                            var modifiedEmbeds = (embedData ?? Array.Empty<Embed>()).Select(embed =>
                            {
                                var builder = embed.ToEmbedBuilder();
                                if (builder.Footer is null || isFinalUpdate)
                                {
                                    builder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                        BuildTokenUsageFooter()));
                                }
                                // Add provider branding here
                                AddProviderBranding(builder, provider);

                                return builder.Build();
                            }).ToList();
                            modifiedEmbeds = ApplyProviderBranding(modifiedEmbeds, provider);

                            // Send the initial message and store the reference
                            if (modifiedEmbeds.Count > 0)
                            {
                                regularMessage = await userMsg.Channel.SendMessageAsync(
                                    plainText,
                                    embeds: modifiedEmbeds.ToArray(), allowedMentions: AllowedMentions.None);
                            }
                            else
                            {
                                var embedBuilder = new EmbedBuilder()
                                    .WithOkColor()
                                    .WithDescription(plainText ?? processedContent)
                                    .WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                        BuildTokenUsageFooter()));
                                AddProviderBranding(embedBuilder, provider);
                                regularMessage = await userMsg.Channel.SendMessageAsync(
                                    embeds: new[] { embedBuilder.Build() }, allowedMentions: AllowedMentions.None);
                            }
                        }
                        else
                        {
                            // Create simple embed for first message
                            var embedBuilder = new EmbedBuilder()
                                .WithOkColor()
                                .WithDescription(processedContent)
                                .WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                    BuildTokenUsageFooter()));
                            AddProviderBranding(embedBuilder, provider);
                            regularMessage = await userMsg.Channel.SendMessageAsync(
                                embeds: new[] { embedBuilder.Build() }, allowedMentions: AllowedMentions.None);
                        }
                    }
                    else
                    {
                        // Subsequent updates - modify existing message
                        if (SmartEmbed.TryParse(processedContent, config.GuildId, out var embedData, out var plainText,
                                out _))
                        {
                            // Use the parsed embed data for updates
                            var modifiedEmbeds = (embedData ?? Array.Empty<Embed>()).Select(embed =>
                            {
                                var builder = embed.ToEmbedBuilder();
                                if (builder.Footer is null || isFinalUpdate)
                                {
                                    builder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                        BuildTokenUsageFooter()));
                                }

                                return builder.Build();
                            }).ToList();
                            modifiedEmbeds = ApplyProviderBranding(modifiedEmbeds, provider);

                            if (modifiedEmbeds.Count > 0)
                            {
                                // Update the existing message
                                await regularMessage.ModifyAsync(msg =>
                                {
                                    msg.Content = plainText;
                                    msg.Embeds = modifiedEmbeds.ToArray();
                                });
                            }
                            else
                            {
                                var embedBuilder = new EmbedBuilder()
                                    .WithOkColor()
                                    .WithDescription(plainText ?? processedContent)
                                    .WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                        BuildTokenUsageFooter()));
                                AddProviderBranding(embedBuilder, provider);
                                var embed = embedBuilder.Build();
                                await regularMessage.ModifyAsync(msg =>
                                {
                                    msg.Content = null;
                                    msg.Embed = embed;
                                });
                            }
                        }
                        else
                        {
                            // Update with simple embed
                            var embedBuilder = new EmbedBuilder()
                                .WithOkColor()
                                .WithDescription(processedContent)
                                .WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                    BuildTokenUsageFooter()));

                            AddProviderBranding(embedBuilder, provider);

                            var embed = embedBuilder.Build();

                            await regularMessage.ModifyAsync(msg =>
                            {
                                msg.Content = null;
                                msg.Embed = embed;
                            });
                        }
                    }
                }
            }

            // Helper method to extract Discord.NET embeds from a NewEmbed object
            List<Embed> GetDiscordEmbeds(NewEmbed newEmbed)
            {
                var discordEmbeds = new List<Embed>();

                if (newEmbed.Embeds?.Count > 0)
                {
                    discordEmbeds.AddRange(NewEmbed.ToEmbedArray(newEmbed.Embeds));
                }
                else if (newEmbed.Embed != null)
                {
                    discordEmbeds.AddRange(NewEmbed.ToEmbedArray(new List<global::Mewdeko.Common.Embed>
                    {
                        newEmbed.Embed
                    }));
                }

                return discordEmbeds;
            }

            // Helper method to send multiple embeds when using JSON templates
            async Task SendMultipartJsonEmbeds(List<string> chunks, bool isFinalUpdate)
            {
                // If not final, only render the first chunk into the main message to avoid exceeding limits
                var processAllChunks = isFinalUpdate;

                // Build the embed(s) for the first chunk using the template
                try
                {
                    var escapedChunk = EscapeJsonString(chunks[0]);
                    var jsonTemplate = initialTemplate.Replace("%airesponse%", escapedChunk);

                    var newEmbed = JsonSerializer.Deserialize<NewEmbed>(jsonTemplate);
                    if (newEmbed != null && (newEmbed.Embeds?.Count > 0 || newEmbed.Embed != null))
                    {
                        var firstMessageEmbeds = ApplyProviderBranding(GetDiscordEmbeds(newEmbed), provider);

                        if (webhook != null && webhookMessageId.HasValue)
                        {
                            await webhook.ModifyMessageAsync(webhookMessageId.Value, msg =>
                            {
                                msg.Content = newEmbed.Content;
                                msg.Embeds = firstMessageEmbeds;
                            });
                        }
                        else if (regularMessage != null)
                        {
                            await regularMessage.ModifyAsync(msg =>
                            {
                                msg.Content = newEmbed.Content;
                                msg.Embeds = firstMessageEmbeds.ToArray();
                            });
                        }
                        else
                        {
                            regularMessage = await userMsg.Channel.SendMessageAsync(
                                newEmbed.Content,
                                embeds: firstMessageEmbeds.ToArray(),
                                allowedMentions: AllowedMentions.None);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Error creating JSON embed for first chunk: {ex.Message}");

                    // Fallback for the first chunk
                    var fallbackEmbedBuilder = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle($"Response (Part 1/{chunks.Count})")
                        .WithDescription(chunks[0])
                        .WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                            BuildTokenUsageFooter()));
                    AddProviderBranding(fallbackEmbedBuilder, provider);
                    var fallbackEmbed = fallbackEmbedBuilder.Build();

                    if (webhook != null && webhookMessageId.HasValue)
                    {
                        await webhook.ModifyMessageAsync(webhookMessageId.Value, msg =>
                        {
                            msg.Content = null;
                            msg.Embeds = new List<Embed> { fallbackEmbed };
                        });
                    }
                    else if (regularMessage != null)
                    {
                        await regularMessage.ModifyAsync(msg =>
                        {
                            msg.Content = null;
                            msg.Embeds = new[] { fallbackEmbed };
                        });
                    }
                    else
                    {
                        regularMessage = await userMsg.Channel.SendMessageAsync(
                            embeds: new[] { fallbackEmbed },
                            allowedMentions: AllowedMentions.None);
                    }
                }

                // For non-final updates, don't send additional parts yet to avoid duplicates during streaming
                if (!processAllChunks)
                    return;

                // Send remaining chunks as separate follow-up messages (simple embeds to ensure safety)
                for (var i = 1; i < chunks.Count; i++)
                {
                    try
                    {
                        var embedBuilder = new EmbedBuilder()
                            .WithOkColor()
                            .WithTitle($"Continued (Part {i + 1}/{chunks.Count})")
                            .WithDescription(chunks[i]);

                        if (i == chunks.Count - 1)
                        {
                            embedBuilder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                BuildTokenUsageFooter()));
                        }

                        AddProviderBranding(embedBuilder, provider);
                        var embed = embedBuilder.Build();

                        if (webhook != null && webhookMessageId.HasValue)
                        {
                            // Send a new webhook message for each additional part
                            var followupMessageId = await webhook.SendMessageAsync(embeds: new[] { embed });
                            await AttachCleanupReactionForRequester(userMsg.Author.Id, null, followupMessageId,
                                userMsg.Channel);
                        }
                        else
                        {
                            var followupMessage = await userMsg.Channel.SendMessageAsync(
                                embeds: new[] { embed },
                                allowedMentions: AllowedMentions.None);
                            await TrackAndAttachCleanupReaction(followupMessage, userMsg.Author.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Error sending JSON multipart chunk {i + 1}: {ex.Message}");
                    }
                }
            }

            // Helper method to send multiple standard embeds (non-JSON template)
            async Task SendMultipartRegularEmbeds(List<string> chunks, bool isFinalUpdate)
            {
                // If not final, only show the first chunk in the main message to prevent hitting 6000 total per message
                var showAllChunks = isFinalUpdate;

                // Build the first embed
                var firstEmbedBuilder = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(chunks[0]);

                if (showAllChunks && chunks.Count == 1)
                {
                    firstEmbedBuilder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                        BuildTokenUsageFooter()));
                }

                AddProviderBranding(firstEmbedBuilder, provider);
                var firstEmbed = firstEmbedBuilder.Build();

                // Send/modify the main message with only the first chunk
                if (webhook != null && webhookMessageId.HasValue)
                {
                    await webhook.ModifyMessageAsync(webhookMessageId.Value, msg =>
                    {
                        msg.Content = null;
                        msg.Embeds = new List<Embed> { firstEmbed };
                    });
                }
                else if (regularMessage != null)
                {
                    await regularMessage.ModifyAsync(msg =>
                    {
                        msg.Content = null;
                        msg.Embeds = new[] { firstEmbed };
                    });
                }
                else
                {
                    regularMessage = await userMsg.Channel.SendMessageAsync(
                        embeds: new[] { firstEmbed },
                        allowedMentions: AllowedMentions.None);
                }

                // During streaming, stop here to avoid duplicate additional messages
                if (!showAllChunks)
                    return;

                // Send the remaining chunks as separate follow-up messages, one embed per message
                for (var i = 1; i < chunks.Count; i++)
                {
                    var embedBuilder = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle($"Continued (Part {i + 1}/{chunks.Count})")
                        .WithDescription(chunks[i]);

                    if (i == chunks.Count - 1)
                    {
                        embedBuilder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                            BuildTokenUsageFooter()));
                    }

                    AddProviderBranding(embedBuilder, provider);
                    var embed = embedBuilder.Build();

                    if (webhook != null && webhookMessageId.HasValue)
                    {
                        var followupMessageId = await webhook.SendMessageAsync(embeds: new[] { embed });
                        await AttachCleanupReactionForRequester(userMsg.Author.Id, null, followupMessageId,
                            userMsg.Channel);
                    }
                    else
                    {
                        var followupMessage = await userMsg.Channel.SendMessageAsync(
                            embeds: new[] { embed },
                            allowedMentions: AllowedMentions.None);
                        await TrackAndAttachCleanupReaction(followupMessage, userMsg.Author.Id);
                    }
                }
            }
        }
    }

    private sealed record CleanupTrackedResponse(ulong RequesterId, DateTimeOffset CreatedAt);

    private async Task HandleAiCleanupReactionAdded(
        Cacheable<IUserMessage, ulong> msg,
        Cacheable<IMessageChannel, ulong> chan,
        SocketReaction reaction)
    {
        _ = chan;
        if (reaction.UserId == client.CurrentUser.Id || !IsCleanupReaction(reaction.Emote))
            return;

        PruneTrackedCleanupRequests();
        if (!trackedResponseCleanupRequests.TryGetValue(reaction.MessageId, out var trackedResponse))
            return;

        if (trackedResponse.RequesterId != reaction.UserId)
            return;

        try
        {
            var targetMessage = msg.HasValue ? msg.Value : await msg.GetOrDownloadAsync().ConfigureAwait(false);
            if (targetMessage != null)
                await targetMessage.DeleteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to delete AI response message {MessageId} via cleanup reaction",
                reaction.MessageId);
        }
        finally
        {
            trackedResponseCleanupRequests.TryRemove(reaction.MessageId, out _);
        }
    }

    private static bool IsCleanupReaction(IEmote emote)
    {
        return emote.Name is "🗑️" or "🗑";
    }

    private async Task AttachCleanupReactionForRequester(
        ulong requesterId,
        IUserMessage? regularMessage,
        ulong? webhookMessageId,
        IMessageChannel responseChannel)
    {
        if (regularMessage != null)
        {
            await TrackAndAttachCleanupReaction(regularMessage, requesterId);
            return;
        }

        if (!webhookMessageId.HasValue)
            return;

        try
        {
            var webhookMessage = await responseChannel.GetMessageAsync(webhookMessageId.Value).ConfigureAwait(false);
            if (webhookMessage is IUserMessage userMessage)
                await TrackAndAttachCleanupReaction(userMessage, requesterId);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to fetch webhook AI message {MessageId} for cleanup reaction",
                webhookMessageId.Value);
        }
    }

    private async Task TrackAndAttachCleanupReaction(IUserMessage message, ulong requesterId)
    {
        PruneTrackedCleanupRequests();
        trackedResponseCleanupRequests[message.Id] = new CleanupTrackedResponse(requesterId, DateTimeOffset.UtcNow);

        try
        {
            await message.AddReactionAsync(CleanupReaction).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            trackedResponseCleanupRequests.TryRemove(message.Id, out _);
            logger.LogDebug(ex, "Failed to add cleanup reaction to AI response message {MessageId}", message.Id);
        }
    }

    private void PruneTrackedCleanupRequests()
    {
        if (trackedResponseCleanupRequests.IsEmpty)
            return;

        var cutoff = DateTimeOffset.UtcNow.Subtract(CleanupTrackingLifetime);
        foreach (var trackedEntry in trackedResponseCleanupRequests)
        {
            if (trackedEntry.Value.CreatedAt < cutoff)
                trackedResponseCleanupRequests.TryRemove(trackedEntry.Key, out _);
        }
    }

    /// <summary>
    ///     Properly escapes a string for use in JSON.
    /// </summary>
    /// <param name="input">The string to escape</param>
    /// <returns>JSON-safe escaped string</returns>
    private string EscapeJsonString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var escaped = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            switch (c)
            {
                case '\\': escaped.Append("\\\\"); break;
                case '"': escaped.Append("\\\""); break;
                case '\n': escaped.Append("\\n"); break;
                case '\r': escaped.Append("\\r"); break;
                case '\t': escaped.Append("\\t"); break;
                case '\b': escaped.Append("\\b"); break;
                case '\f': escaped.Append("\\f"); break;
                default:
                    // Check for control characters (0x00-0x1F)
                    if (c < 32)
                    {
                        escaped.Append($"\\u{(int)c:X4}");
                    }
                    else
                    {
                        escaped.Append(c);
                    }

                    break;
            }
        }

        return escaped.ToString();
    }

    /// <summary>
    ///     Splits a long text into multiple chunks suitable for Discord embeds.
    /// </summary>
    /// <param name="text">Text to split</param>
    /// <param name="maxChunkSize">Maximum size of each chunk</param>
    /// <param name="maxChunks">Maximum number of chunks to create</param>
    /// <returns>List of text chunks</returns>
    private List<string> SplitLongText(string text, int maxChunkSize = 4000, int maxChunks = 10)
    {
        var results = new List<string>();
        if (string.IsNullOrEmpty(text))
            return results;

        // If text is already within limits, return it as is
        if (text.Length <= maxChunkSize)
        {
            results.Add(text);
            return results;
        }

        var startIndex = 0;
        int endIndex;

        // Split text into chunks at natural boundaries
        while (startIndex < text.Length && results.Count < maxChunks)
        {
            // Determine the potential end of this chunk
            endIndex = Math.Min(startIndex + maxChunkSize, text.Length);

            // If this is the last chunk we can create due to maxChunks limit,
            // include as much as possible up to maxChunkSize
            if (results.Count == maxChunks - 1)
            {
                results.Add(text.Substring(startIndex, endIndex - startIndex));
                break;
            }

            // Try to find a natural breaking point - preferably at paragraph or sentence end
            if (endIndex < text.Length)
            {
                // Look for paragraph break
                var paragraphBreak = text.LastIndexOf("\n\n", endIndex - 1, endIndex - startIndex);
                if (paragraphBreak > startIndex + 100) // Ensure we have a reasonable chunk size
                {
                    endIndex = paragraphBreak + 2; // Include the double newline
                }
                else
                {
                    // Look for newline
                    var newLineBreak = text.LastIndexOf('\n', endIndex - 1, endIndex - startIndex);
                    if (newLineBreak > startIndex + 100)
                    {
                        endIndex = newLineBreak + 1; // Include the newline
                    }
                    else
                    {
                        // Look for sentence end
                        var sentenceBreak = -1;
                        foreach (var c in new[]
                                 {
                                     '.', '!', '?'
                                 })
                        {
                            var breakPoint = text.LastIndexOf(c, endIndex - 1, endIndex - startIndex);
                            if (breakPoint > sentenceBreak) sentenceBreak = breakPoint;
                        }

                        if (sentenceBreak > startIndex + 100)
                        {
                            endIndex = Math.Min(sentenceBreak + 1, text.Length);
                        }
                        else
                        {
                            // Look for space
                            var spaceBreak = text.LastIndexOf(' ', endIndex - 1, endIndex - startIndex);
                            if (spaceBreak > startIndex + 100)
                            {
                                endIndex = spaceBreak + 1;
                            }
                            // else we'll just cut at maxChunkSize
                        }
                    }
                }
            }

            // Add chunk to results
            results.Add(text.Substring(startIndex, endIndex - startIndex));
            startIndex = endIndex;
        }

        // If there's still text left but we hit the maxChunks limit, add an indicator
        if (startIndex < text.Length && results.Count >= maxChunks)
        {
            var lastChunk = results[results.Count - 1];
            if (lastChunk.Length > maxChunkSize - 100)
            {
                lastChunk = lastChunk[..(maxChunkSize - 100)];
            }

            results[results.Count - 1] = lastChunk + "\n\n... (Response truncated due to length)";
        }

        return results;
    }

    /// <summary>
    ///     Retrieves a list of AI models supported by the specified provider.
    /// </summary>
    /// <param name="provider">The AI provider to fetch models from (OpenAI, Groq, or Claude).</param>
    /// <param name="apiKey">The API key used to authenticate with the provider.</param>
    /// <returns>A list of supported AI models for the specified provider.</returns>
    /// <remarks>
    ///     This method caches results for 24 hours to minimize API calls. Models are fetched from:
    ///     - OpenAI: api.openai.com/v1/models
    ///     - Groq: api.groq.com/v1/models
    ///     - Claude: api.anthropic.com/v1/models
    /// </remarks>
    /// <exception cref="NotSupportedException">Thrown when an unsupported provider is specified.</exception>
    /// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
    public async Task<List<AiModel>> GetSupportedModels(AiProvider provider, string apiKey)
    {
        if (modelCache.TryGetValue(provider, out var models) &&
            DateTime.UtcNow - lastModelUpdate < modelCacheExpiry)
        {
            return models;
        }

        using var http = httpFactory.CreateClient();
        models = provider switch
        {
            AiProvider.OpenAi => await FetchOpenAiModels(http, apiKey),
            AiProvider.Groq => await FetchGroqModels(http, apiKey),
            AiProvider.Claude => await FetchClaudeModels(http, apiKey),
            AiProvider.Grok => await FetchGrokModels(http, apiKey),
            _ => throw new NotSupportedException($"Provider {provider} not supported")
        };

        modelCache.AddOrUpdate(provider, models, (_, _) => models);
        lastModelUpdate = DateTime.UtcNow;
        return models;
    }

    private async Task<List<AiModel>> FetchOpenAiModels(HttpClient http, string apiKey)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        try
        {
            var response = await http.GetAsync("https://api.openai.com/v1/models");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("OpenAI models fetch failed: {Status} {Content}", response.StatusCode, content);
                return new List<AiModel>();
            }

            var openAiResponse = JsonSerializer.Deserialize<OpenAiModelsResponse>(content);

            if (openAiResponse?.Data == null)
            {
                logger.LogError("OpenAI models fetch: Data field is null. Raw content: {Content}", content);
                return new List<AiModel>();
            }

            return openAiResponse.Data
                .Where(m => m.Id.StartsWith("gpt"))
                .Select(m => new AiModel
                {
                    Id = m.Id,
                    Name = FormatModelName(m.Id),
                    Provider = AiProvider.OpenAi
                })
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpenAI models fetch failed with exception");
            return new List<AiModel>();
        }
    }

    private async Task<List<AiModel>> FetchGrokModels(HttpClient http, string apiKey)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        try
        {
            // Call xAI's model list endpoint
            var response = await http.GetAsync("https://api.x.ai/v1/models");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Grok models fetch failed: {Status} {Content}", response.StatusCode, content);
                return new List<AiModel>();
            }
            // Parse the response JSON into a model list (reuse OpenAiModelsResponse if compatible)
            var grokResponse = JsonSerializer.Deserialize<OpenAiModelsResponse>(content);
            if (grokResponse?.Data == null)
            {
                logger.LogError("Grok models fetch: Data field is null. Raw content: {Content}", content);
                return new List<AiModel>();
            }
            // Map to internal AiModel list, filtering relevant model IDs
            return grokResponse.Data
                .Where(m => m.Id.StartsWith("grok"))  // Only take Grok models
                .Select(m => new AiModel
                {
                    Id       = m.Id,
                    Name     = FormatModelName(m.Id),   // e.g. "grok-4" -> "Grok 4"
                    Provider = AiProvider.Grok
                })
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Grok models fetch failed with exception");
            return new List<AiModel>();
        }
    }

    private async Task<List<AiModel>> FetchGroqModels(HttpClient http, string apiKey)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var response = await http.GetFromJsonAsync<GroqModelsResponse>("https://api.groq.com/openai/v1/models");

        return response?.Data
            .Select(m => new AiModel
            {
                Id = m.Id, Name = FormatModelName(m.Id), Provider = AiProvider.Groq
            })
            .ToList() ?? [];
    }

    private async Task<List<AiModel>> FetchClaudeModels(HttpClient http, string apiKey)
    {
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var response = await http.GetStringAsync("https://api.anthropic.com/v1/models");
        var data = JsonSerializer.Deserialize<ClaudeModelsResponse>(response);
        logger.LogInformation(response);

        return data.Data
            .Select(m => new AiModel
            {
                Id = m.Id, Name = FormatModelName(m.Id), Provider = AiProvider.Claude
            })
            .ToList();
    }

    private static string FormatModelName(string modelId)
    {
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
            modelId.Replace('-', ' ')
                .Replace('/', ' ')
                .Replace('_', ' '));
    }

    /// <summary>
    ///     Handles tool use events from Claude AI stream and accumulates tool requests.
    /// </summary>
    /// <param name="rawJson">The raw JSON from Claude's API</param>
    /// <param name="toolUseRequests">List to accumulate completed tool requests</param>
    private void HandleToolUseEventIfNeeded(string rawJson, List<ToolUseRequest> toolUseRequests)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProperty))
                return;

            var eventType = typeProperty.GetString();

            switch (eventType)
            {
                // Handle content_block_start with tool_use
                case "content_block_start":
                {
                    if (root.TryGetProperty("content_block", out var contentBlock) &&
                        contentBlock.TryGetProperty("type", out var blockType) &&
                        blockType.GetString() == "tool_use")
                    {
                        // Start of a tool use - extract name and ID
                        if (contentBlock.TryGetProperty("name", out var nameProperty) &&
                            contentBlock.TryGetProperty("id", out var idProperty))
                        {
                            currentToolName = nameProperty.GetString();
                            currentToolId = idProperty.GetString();
                            currentToolInput = "";
                            isCollectingToolInput = true;

                            logger.LogInformation($"Started tool use: {currentToolName} with ID: {currentToolId}");
                        }
                    }

                    return;
                }
                // Handle content_block_delta with input_json_delta
                case "content_block_delta" when isCollectingToolInput:
                {
                    if (root.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("type", out var deltaType) &&
                        deltaType.GetString() == "input_json_delta" &&
                        delta.TryGetProperty("partial_json", out var partialJson))
                    {
                        var jsonPart = partialJson.GetString();
                        currentToolInput += jsonPart;
                        logger.LogInformation($"Accumulated tool input: {currentToolInput}");
                    }

                    return;
                }
                // Handle content_block_stop - complete the tool request
                case "content_block_stop" when isCollectingToolInput:
                {
                    logger.LogInformation($"Tool input complete: {currentToolInput}");

                    if (!string.IsNullOrEmpty(currentToolName) && !string.IsNullOrEmpty(currentToolId))
                    {
                        toolUseRequests.Add(new ToolUseRequest
                        {
                            Id = currentToolId, Name = currentToolName, InputJson = currentToolInput
                        });
                    }

                    // Reset tool state
                    currentToolName = null;
                    currentToolId = null;
                    currentToolInput = "";
                    isCollectingToolInput = false;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling tool use event");
            // Reset tool state on error
            currentToolName = null;
            currentToolId = null;
            currentToolInput = "";
            isCollectingToolInput = false;
        }
    }

    /// <summary>
    ///     Executes a tool request and returns the result.
    /// </summary>
    private async Task<ToolResult> ExecuteToolRequest(ToolUseRequest toolRequest, ulong guildId)
    {
        try
        {
            logger.LogInformation($"Executing tool: {toolRequest.Name} with input: {toolRequest.InputJson}");

            if (toolRequest.Name == "get_user_info")
            {
                string toolResult;

                // Parse the tool input JSON
                if (!string.IsNullOrEmpty(toolRequest.InputJson))
                {
                    using var inputDoc = JsonDocument.Parse(toolRequest.InputJson);
                    var inputRoot = inputDoc.RootElement;

                    if (inputRoot.TryGetProperty("user_query", out var userQueryProperty))
                    {
                        var userQuery = userQueryProperty.GetString();
                        logger.LogInformation($"Looking up user: {userQuery}");

                        var userInfoService = serviceProvider.GetRequiredService<UserInfoToolService>();
                        toolResult = await userInfoService.FindUserAsync(guildId, userQuery);
                    }
                    else
                    {
                        toolResult = JsonSerializer.Serialize(new
                        {
                            error = "Missing required parameter: user_query"
                        });
                    }
                }
                else
                {
                    toolResult = JsonSerializer.Serialize(new
                    {
                        error = "Empty tool input"
                    });
                }

                logger.LogInformation($"Tool result for {toolRequest.Name}: {toolResult.Length} characters");

                return new ToolResult
                {
                    ToolUseId = toolRequest.Id, Content = toolResult, IsError = false
                };
            }

            logger.LogWarning($"Unknown tool requested: {toolRequest.Name}");
            return new ToolResult
            {
                ToolUseId = toolRequest.Id,
                Content = JsonSerializer.Serialize(new
                {
                    error = $"Unknown tool: {toolRequest.Name}"
                }),
                IsError = true
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error executing tool {toolRequest.Name}: {ex.Message}");

            return new ToolResult
            {
                ToolUseId = toolRequest.Id,
                Content = JsonSerializer.Serialize(new
                {
                    error = $"Tool execution failed: {ex.Message}"
                }),
                IsError = true
            };
        }
    }

    /// <summary>
    ///     Represents a tool use request from Claude
    /// </summary>
    private class ToolUseRequest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string InputJson { get; set; }
    }

    /// <summary>
    ///     Represents the result of a tool execution
    /// </summary>
    private class ToolResult
    {
        public string ToolUseId { get; set; }
        public string Content { get; set; }
        public bool IsError { get; set; }
    }

    /// <summary>
    ///     Gets the logo URL for the specified AI provider.
    /// </summary>
    /// <param name="provider">The AI provider.</param>
    /// <returns>The logo URL as a string.</returns>
    private static string GetProviderLogoUrl(AiProvider provider)
    {
        return provider switch
        {
            AiProvider.OpenAi => "https://seeklogo.com/images/C/chatgpt-logo-02AFA704B5-seeklogo.com.png",
            AiProvider.Grok => "https://images.seeklogo.com/logo-png/61/2/grok-logo-png_seeklogo-613403.png",
            AiProvider.Groq => "https://seeklogo.com/images/G/groq-logo-1B1B1B1B1B-seeklogo.com.png",
            AiProvider.Claude => "https://images.seeklogo.com/logo-png/55/1/claude-logo-png_seeklogo-554534.png",
            _ => string.Empty
        };
    }

    private List<Embed> ApplyProviderBranding(IEnumerable<Embed> embeds, AiProvider provider)
    {
        return embeds
            .Select(embed =>
            {
                var builder = embed.ToEmbedBuilder();
                AddProviderBranding(builder, provider);
                return builder.Build();
            })
            .ToList();
    }

    private bool ShouldEnableUserInfoTool(string? userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
            return false;

        // Recursive Claude tool-result turns should not re-enable lookup tools.
        if (userQuery.Contains("<tool_result>", StringComparison.OrdinalIgnoreCase))
            return false;

        if (userQuery.Contains("<@", StringComparison.Ordinal))
            return true;

        if (ContainsSnowflakeLikeId(userQuery))
            return true;

        var lowered = userQuery.ToLowerInvariant();

        // Require explicit Discord/member lookup intent to avoid false positives on normal knowledge prompts.
        string[] intentPhrases =
        [
            "discord user",
            "server member",
            "guild member",
            "member info",
            "user info",
            "userinfo",
            "user profile",
            "member profile",
            "find user",
            "lookup user",
            "look up user",
            "check user",
            "who is this user",
            "their xp",
            "their warnings",
            "their roles",
            "joined this server",
            "in this server"
        ];

        return intentPhrases.Any(lowered.Contains);
    }

    private static bool ContainsSnowflakeLikeId(string input)
    {
        var digitRun = 0;

        foreach (var ch in input)
        {
            if (char.IsDigit(ch))
            {
                digitRun++;
                if (digitRun >= 16)
                    return true;
            }
            else
            {
                digitRun = 0;
            }
        }

        return false;
    }

    private EmbedBuilder AddProviderBranding(EmbedBuilder builder, AiProvider provider)
    {
        var logoUrl = GetProviderLogoUrl(provider);
        if (!string.IsNullOrEmpty(logoUrl))
        {
            builder.WithAuthor(provider.ToString(), logoUrl);
        }
        else
        {
            builder.WithAuthor(provider.ToString());
        }
        return builder;
    }

    /// <summary>
    ///     Represents an Ai model with its metadata.
    /// </summary>
    public class AiModel
    {
        /// <summary>
        ///     Gets or sets the model identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///     Gets or sets the display name of the model.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Gets or sets the provider of this model.
        /// </summary>
        public AiProvider Provider { get; set; }
    }

    private record class OpenAiModelsResponse(
        [property: JsonPropertyName("data")] List<OpenAiModel> Data);

    private record class OpenAiModel(
        [property: JsonPropertyName("id")] string Id);

    private record class GroqModelsResponse(List<GroqModel> Data);

    private record class GroqModel(string Id);

    /// <summary>
    ///     Represents a Claude AI model from Anthropic's API.
    /// </summary>
    public class ClaudeModel
    {
        /// <summary>
        ///     Gets or sets the type identifier for this model. Always "model".
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        ///     Gets or sets the unique identifier for this model (e.g. "claude-3-opus-20240229").
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        ///     Gets or sets the human-readable name for this model (e.g. "Claude 3 Opus").
        /// </summary>
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        /// <summary>
        ///     Gets or sets the UTC timestamp when this model version was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    ///     Represents the response from Anthropic's models endpoint containing available Claude models.
    /// </summary>
    public class ClaudeModelsResponse
    {
        /// <summary>
        ///     Gets or sets the list of available Claude models.
        /// </summary>
        [JsonPropertyName("data")]
        public List<ClaudeModel> Data { get; set; }

        /// <summary>
        ///     Gets or sets whether there are additional models beyond this page of results.
        /// </summary>
        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }

        /// <summary>
        ///     Gets or sets the ID of the first model in this response.
        /// </summary>
        [JsonPropertyName("first_id")]
        public string FirstId { get; set; }

        /// <summary>
        ///     Gets or sets the ID of the last model in this response.
        /// </summary>
        [JsonPropertyName("last_id")]
        public string LastId { get; set; }
    }
}