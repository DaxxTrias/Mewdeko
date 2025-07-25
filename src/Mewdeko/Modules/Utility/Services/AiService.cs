using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Antlr4.Runtime.Misc;
using DataModel;
using LinqToDB;
using Mewdeko.Common.Configs;
using Mewdeko.Modules.Utility.Services.Impl;
using Mewdeko.Services.Strings;
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
    private readonly BotConfig botConfig;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IHttpClientFactory httpFactory;
    private readonly ILogger<AiService> logger;
    private readonly ConcurrentDictionary<AiProvider, List<AiModel>> modelCache;
    private readonly TimeSpan modelCacheExpiry = TimeSpan.FromHours(24);
    private readonly GeneratedBotStrings strings;
    private DateTime lastModelUpdate = DateTime.MinValue;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AiService" /> class.
    /// </summary>
    public AiService(IDataConnectionFactory dbFactory, IHttpClientFactory httpFactory,
        GeneratedBotStrings strings, BotConfig config, EventHandler handler, DiscordShardedClient client,
        ILogger<AiService> logger)
    {
        this.dbFactory = dbFactory;
        this.httpFactory = httpFactory;
        this.strings = strings;
        botConfig = config;
        this.client = client;
        this.logger = logger;
        aiClientFactory = new AiClientFactory(httpFactory);
        handler.Subscribe("MessageReceived", "AiService", HandleMessage);
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
        var isDebugMode = false;
        if (msg is not IUserMessage || msg.Author.IsBot) return;
        if (msg.Channel is not IGuildChannel guildChannel) return;

        var config = await GetOrCreateConfig(guildChannel.GuildId);
        if (!config.Enabled || config.ChannelId != msg.Channel.Id) return;

#if DEBUG
        isDebugMode = true;
#endif

        if (msg.Content == ".deletesession" && !isDebugMode)
        {
            await ClearConversation(guildChannel.GuildId, msg.Author.Id);
            await msg.Channel.SendConfirmAsync(strings.AiConversationDeleted(guildChannel.GuildId));
            return;
        }
        else if (msg.Content == ",deletesession" && isDebugMode)
        {
            await ClearConversation(guildChannel.GuildId, msg.Author.Id);
            await msg.Channel.SendConfirmAsync(strings.AiConversationDeleted(guildChannel.GuildId));
            return;
        }

        if (string.IsNullOrEmpty(config.ApiKey))
        {
            await msg.Channel.SendErrorAsync(strings.AiNoApiKey(guildChannel.GuildId, config.Provider), botConfig);
            return;
        }

        DiscordWebhookClient? webhook = null;
        ulong? webhookMessageId = null;
        IUserMessage? processingMessage = null;

        // Determine the prefix based on debug mode
        var prefixes = isDebugMode
            ? new[] { "-frog ", ",frog " }
            : new[] { "!frog ", ".frog " };

        // Find the matching prefix (if any)
        var prefix = prefixes.FirstOrDefault(p => msg.Content.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(config.WebhookUrl))
        {
            webhook = new DiscordWebhookClient(config.WebhookUrl);
            webhookMessageId = await webhook.SendMessageAsync(embeds:
            [
                new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(strings.AiProcessingRequest(guildChannel.GuildId, msg.Author.Mention))
                    .Build()
            ]);
        }
        else
        {
            // Only show processing message if a valid prefix was found and the message starts with it
            if (!string.IsNullOrEmpty(prefix) && msg.Content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                processingMessage = await msg.Channel.SendConfirmAsync(strings.AiProcessingRequest(guildChannel.GuildId, msg.Author.Mention));
            }

        }

        try
        {
            // lower any capitalization in message content
            var loweredContents = msg.Content.ToLower();

            // Split the message content into words and take only the first two for checking.
            var words = loweredContents.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).ToList();
            var scannedWords = words.Select(w => w.ToLower()).ToList();

            if (scannedWords.Contains("image"))
            {
                try
                {
                    await msg.Channel.SendMessageAsync("Dall-E disabled.");
                    return;
                }
                catch
                {
                    throw;
                }
                var authorName = msg.Author.ToString();
                var prompt = msg.Content.Substring("frog image ".Length).Trim();
                if (string.IsNullOrEmpty(prompt))
                {
                    await msg.Channel.SendMessageAsync("Please provide a prompt for the image.");
                    return;
                }

                IUserMessage placeholderMessage = null;
                try
                {
                    // Send a placeholder message directly using the bot's client
                    placeholderMessage = await msg.Channel.SendConfirmAsync($"Generating image...");

                    // Generate the image
                    //var images = await api.ImageGenerations.CreateImageAsync(new ImageGenerationRequest
                    //{
                    //    Prompt = prompt,  // prompt (text string)
                    //    NumOfImages = 1, // dall-e2 can provide multiple images, e3 does not support this currently
                    //    Size = ImageSize._1792x1024, // resolution of the generated images (256x256 | 512x512 | 1024x1024 | 1792x1024) dall-e3 cannot use images below 1024x1024
                    //    Model = Model.DALLE3, // model (model for this req. defaults to dall-e2
                    //    User = authorName, // user: author of post, this can be used to help openai detect abuse and rule breaking
                    //    ResponseFormat = ImageResponseFormat.Url // the format the images can be returned as. must be url or b64_json
                    //                                             // quality: by default images are generated at standard, but on e3 you can use HD
                    //});

                    /*
                    // if dall-e3 ever supports more then 1 image can use this code block instead
                    // Update the placeholder message with the images
                    if (images.Data.Count > 0)
                    {
                        var embeds = images.Data.Select(image => new EmbedBuilder().WithImageUrl(image.Url).Build()).ToArray(); // Convert to array

                        await placeholderMessage.ModifyAsync(msg =>
                        {
                            msg.Content = ""; // Clearing the content
                            msg.Embeds = new Optional<Embed[]>(embeds); // Wrap the array in an Optional
                        });
                    }
                    else
                    {
                        await placeholderMessage.ModifyAsync(msg => msg.Content = "No images were generated.");
                    }
                    */

                    // Update the placeholder message with the image
                    //if (images.Data.Count > 0)
                    //{
                    //    var imageUrl = images.Data[0].Url; // Assuming images.Data[0] contains the URL
                    //    var embed = new EmbedBuilder()
                    //        .WithImageUrl(imageUrl)
                    //        .Build();
                    //    await placeholderMessage.ModifyAsync(msg =>
                    //    {
                    //        msg.Content = ""; // Clearing the content
                    //        msg.Embed = embed;
                    //    });
                    //}
                    //else
                    //{
                    //    await placeholderMessage.ModifyAsync(msg => msg.Content = "No image generated.");
                    //}
                }
                catch (HttpRequestException httpEx)
                {
                    var content = httpEx.Message; // This is not the response content, but the exception message.
                    Log.Information("Exception message: {Message}", content);

                    // Log the full exception details for debugging
                    Log.Error(httpEx, "HttpRequestException occurred while processing the request.");

                    // Clean up the placeholder message if it was assigned
                    if (placeholderMessage != null)
                    {
                        await placeholderMessage.DeleteAsync();
                    }

                    // Notify the user of a generic error message
                    await msg.Channel.SendMessageAsync("An error occurred while processing your request. Please try again later.");
                }
                catch (Exception ex)
                {
                    // Log the error
                    Log.Error(ex, "Error generating image");

                    // Clean up the placeholder message if it was assigned
                    if (placeholderMessage != null)
                    {
                        await placeholderMessage.DeleteAsync();
                    }
                    await msg.Channel.SendMessageAsync($"Failed to generate image due to an unexpected error. Please try again later. Error code: **{ex.HResult}**");
                }
                return;
            }

            if (scannedWords.Contains("scan"))
            {
                try
                {
                    //todo: should impl this to help with anti bot security (fake support agents)
                    // https://github.com/OkGoDoIt/OpenAI-API-dotnet/commit/b824ac5b50027af48aa8ea02bf1bc40fac36f390#diff-ba720258629043138df0c8ebea494853e88e2517638a615c4a9c4fdc84a2a168
                    await msg.Channel.SendMessageAsync("Not Yet Implemented.");
                    return;
                }
                catch
                {
                    throw;
                }
            }

            if (!string.IsNullOrEmpty(prefix) && msg.Content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var userQuery = msg.Content.Substring(prefix.Length).Trim();

                logger.LogInformation("Processing AI request from {User} in {Channel}: {userQuery}",
                    msg.Author.Username, msg.Channel.Name, userQuery);

                // Pass userQuery to your AI logic instead of msg.Content
                await StreamResponse(config, webhookMessageId, msg, webhook, userQuery);
                await UpdateConfig(config);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error in AI processing");
            if (webhook != null && webhookMessageId.HasValue)
            {
                await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                {
                    x.Embeds = new[]
                    {
                        new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription(strings.AiErrorOccurred(guildChannel.GuildId, ex.Message))
                            .Build()
                    };

                });
            }
            else
            {
                await msg.Channel.SendErrorAsync(strings.AiErrorOccurred(guildChannel.GuildId, ex.Message), botConfig);
            }
        }
        finally
        {
            webhook?.Dispose();
            // Clean up the processing message if it exists and the response was successful
            if (processingMessage != null)
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
        DiscordWebhookClient? webhook, string userQuery = null)
    {
        
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

        var (aiClient, streamParser) = aiClientFactory.Create((AiProvider)config.Provider);
        var responseBuilder = new StringBuilder();
        var lastUpdate = DateTime.UtcNow;
        var tokenCount = 0;

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

        //logger.LogInformation("Sending to AI provider. Messages: {Json}", JsonSerializer.Serialize(messagesToSend));

        var stream = await aiClient.StreamResponseAsync(messagesToSend, config.Model, config.ApiKey);

        string lastRawJson = null;
        await foreach (var rawJson in stream)
        {
            // Ensure that rawJson is explicitly cast to a string before checking for null or empty
            if (rawJson is string jsonString && string.IsNullOrEmpty(jsonString)) continue;
            if (string.IsNullOrEmpty(rawJson)) continue;

            // Log raw response for debugging
            //logger.LogInformation($"{(AiProvider)config.Provider} raw response: {rawJson}");
            lastRawJson = rawJson; // Save the last chunk

            // IMPORTANT: Parse the delta to extract just the content
            var contentDelta = streamParser.ParseDelta(rawJson, (AiProvider)config.Provider);
            if (!string.IsNullOrEmpty(contentDelta))
            {
                responseBuilder.Append(contentDelta);
                //logger.LogInformation($"Added content delta: {contentDelta}");
            }

            // Check for usage information
            if (config.Provider == (int)AiProvider.Groq || config.Provider == (int)AiProvider.Claude)
            {
                var usage = streamParser.ParseUsage(rawJson, (AiProvider)config.Provider);
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

            // Check if stream is finished
            if (streamParser.IsStreamFinished(rawJson, (AiProvider)config.Provider))
            {
                logger.LogInformation("AI stream finished");
                break;
            }

            if (DateTime.UtcNow > timeout)
            {
                logger.LogWarning("AI stream timed out");
                break;
            }
        }

        // Grok and GPT give their usage stats on the final message, not every message like claude/groq
        if (lastRawJson != null && 
            (config.Provider == (int)AiProvider.OpenAi || config.Provider == (int)AiProvider.Grok))
        {
            var usage = streamParser.ParseUsage(lastRawJson, (AiProvider)config.Provider);
            if (usage.HasValue)
            {
                tokenCount = usage.Value.TotalTokens;
                logger.LogInformation($"Updated token count: {tokenCount}");
            }
        }

        await db.InsertAsync(new AiMessage
        {
            ConversationId = convId,
            Role = "assistant",
            Content = responseBuilder.ToString()
        });
        config.TokensUsed += tokenCount;
        await db.UpdateAsync(config);

        // Final update with completed response
        await UpdateMessageEmbed(true); // true = final update
        return;

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
                            await SendMultipartJsonEmbeds(responseChunks);
                            return;
                        }

                        // Single embed with JSON template - similar to original logic
                        var escapedContent = EscapeJsonString(aiResponse);
                        processedContent = initialTemplate.Replace("%airesponse%", escapedContent);

                        // Parse the JSON
                        var newEmbed = JsonSerializer.Deserialize<NewEmbed>(processedContent);

                        if (newEmbed != null && (newEmbed.Embeds?.Count > 0 || newEmbed.Embed != null))
                        {
                            var discordEmbeds = GetDiscordEmbeds(newEmbed);

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
                await SendMultipartRegularEmbeds(responseChunks);
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
                        var modifiedEmbeds = embedData.Select(embed =>
                        {
                            var builder = embed.ToEmbedBuilder();
                            if (builder.Footer is null || isFinalUpdate)
                            {
                                builder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                    tokenCount));
                            }

                            return builder.Build();
                        }).ToList();

                        await webhook.ModifyMessageAsync(webhookMessageId.Value, x =>
                        {
                            x.Content = plainText;
                            x.Embeds = modifiedEmbeds;
                        });
                    }
                    else
                    {
                        // For non-embed content, create a simple embed
                        var embedBuilder = new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription(processedContent)
                            .WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username, tokenCount));

                        AddProviderBranding(embedBuilder, (AiProvider)config.Provider);

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
                            var modifiedEmbeds = embedData.Select(embed =>
                            {
                                var builder = embed.ToEmbedBuilder();
                                if (builder.Footer is null || isFinalUpdate)
                                {
                                    builder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                        tokenCount));
                                }
                                // Add provider branding here
                                AddProviderBranding(builder, (AiProvider)config.Provider);

                                return builder.Build();
                            }).ToList();

                            // Send the initial message and store the reference
                            regularMessage = await userMsg.Channel.SendMessageAsync(
                                plainText,
                                embeds: modifiedEmbeds.ToArray(), allowedMentions: AllowedMentions.None);
                        }
                        else
                        {
                            // Create simple embed for first message
                            regularMessage = await userMsg.Channel.SendConfirmAsync(processedContent);
                        }
                    }
                    else
                    {
                        // Subsequent updates - modify existing message
                        if (SmartEmbed.TryParse(processedContent, config.GuildId, out var embedData, out var plainText,
                                out _))
                        {
                            // Use the parsed embed data for updates
                            var modifiedEmbeds = embedData.Select(embed =>
                            {
                                var builder = embed.ToEmbedBuilder();
                                if (builder.Footer is null || isFinalUpdate)
                                {
                                    builder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                                        tokenCount));
                                }

                                return builder.Build();
                            }).ToList();

                            // Update the existing message
                            await regularMessage.ModifyAsync(msg =>
                            {
                                msg.Content = plainText;
                                msg.Embeds = modifiedEmbeds.ToArray();
                            });
                        }
                        else
                        {
                            // Update with simple embed
                            var embedBuilder = new EmbedBuilder()
                                .WithOkColor()
                                .WithDescription(processedContent)
                                .WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username, tokenCount));

                            AddProviderBranding(embedBuilder, (AiProvider)config.Provider);

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
            async Task SendMultipartJsonEmbeds(List<string> chunks)
            {
                // For the initial message (or webhook update)
                var allEmbeds = new List<Embed>();

                for (var i = 0; i < chunks.Count; i++)
                {
                    try
                    {
                        // Create a new JSON embed for each chunk by using the template
                        var escapedChunk = EscapeJsonString(chunks[i]);
                        var jsonTemplate = initialTemplate.Replace("%airesponse%", escapedChunk);

                        var newEmbed = JsonSerializer.Deserialize<NewEmbed>(jsonTemplate);

                        if (newEmbed != null && (newEmbed.Embeds?.Count > 0 || newEmbed.Embed != null))
                        {
                            var discordEmbeds = new List<Embed>();

                            if (newEmbed.Embeds?.Count > 0)
                            {
                                // Add part indicator to first embed title or description if missing
                                if (i > 0 && newEmbed.Embeds[0] != null)
                                {
                                    // If there's a title, append to it, otherwise add to description
                                    if (!string.IsNullOrEmpty(newEmbed.Embeds[0].Title))
                                    {
                                        newEmbed.Embeds[0].Title =
                                            $"{newEmbed.Embeds[0].Title} (Part {i + 1}/{chunks.Count})";
                                    }
                                    else if (!string.IsNullOrEmpty(newEmbed.Embeds[0].Description))
                                    {
                                        // For the description, we'll prepend the part indicator
                                        newEmbed.Embeds[0].Description =
                                            $"**Part {i + 1}/{chunks.Count}**\n\n{newEmbed.Embeds[0].Description}";
                                    }
                                }

                                discordEmbeds.AddRange(NewEmbed.ToEmbedArray(newEmbed.Embeds));
                            }
                            else if (newEmbed.Embed != null)
                            {
                                // Add part indicator to single embed
                                if (i > 0)
                                {
                                    if (!string.IsNullOrEmpty(newEmbed.Embed.Title))
                                    {
                                        newEmbed.Embed.Title = $"{newEmbed.Embed.Title} (Part {i + 1}/{chunks.Count})";
                                    }
                                    else if (!string.IsNullOrEmpty(newEmbed.Embed.Description))
                                    {
                                        newEmbed.Embed.Description =
                                            $"**Part {i + 1}/{chunks.Count}**\n\n{newEmbed.Embed.Description}";
                                    }
                                }

                                discordEmbeds.AddRange(NewEmbed.ToEmbedArray(new List<global::Mewdeko.Common.Embed>
                                {
                                    newEmbed.Embed
                                }));
                            }

                            // Add to our collection
                            allEmbeds.AddRange(discordEmbeds);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Error creating JSON embed for chunk {i + 1}: {ex.Message}");

                        // Fallback for this chunk
                        var fallbackEmbed = new EmbedBuilder()
                            .WithOkColor()
                            .WithTitle($"Response (Part {i + 1}/{chunks.Count})")
                            .WithDescription(chunks[i])
                            .WithFooter(i == chunks.Count - 1
                                ? strings.AiResponseFooter(config.GuildId, userMsg.Author.Username, tokenCount)
                                : null)
                            .Build();

                        allEmbeds.Add(fallbackEmbed);
                    }
                }

                // Send or update the message with all embeds
                if (webhook != null && webhookMessageId.HasValue)
                {
                    await webhook.ModifyMessageAsync(webhookMessageId.Value, msg =>
                    {
                        msg.Content = null;
                        msg.Embeds = allEmbeds;
                    });
                }
                else if (regularMessage != null)
                {
                    await regularMessage.ModifyAsync(msg =>
                    {
                        msg.Content = null;
                        msg.Embeds = allEmbeds.ToArray();
                    });
                }
                else
                {
                    // First message
                    regularMessage = await userMsg.Channel.SendMessageAsync(
                        embeds: allEmbeds.ToArray(),
                        allowedMentions: AllowedMentions.None);
                }
            }

            // Helper method to send multiple standard embeds (non-JSON template)
            async Task SendMultipartRegularEmbeds(List<string> chunks)
            {
                // Create a collection of embeds
                var allEmbeds = new List<Embed>();

                for (var i = 0; i < chunks.Count; i++)
                {
                    var embedBuilder = new EmbedBuilder()
                        .WithOkColor();

                    // First part doesn't need a part indicator in the title
                    if (i == 0)
                    {
                        embedBuilder.WithDescription(chunks[i]);
                    }
                    else
                    {
                        embedBuilder.WithTitle($"Continued (Part {i + 1}/{chunks.Count})")
                            .WithDescription(chunks[i]);
                    }

                    // Add footer to last part only
                    if (i == chunks.Count - 1 || isFinalUpdate)
                    {
                        embedBuilder.WithFooter(strings.AiResponseFooter(config.GuildId, userMsg.Author.Username,
                            tokenCount));
                    }

                    allEmbeds.Add(embedBuilder.Build());
                }

                // Send or update with all embeds
                if (webhook != null && webhookMessageId.HasValue)
                {
                    await webhook.ModifyMessageAsync(webhookMessageId.Value, msg =>
                    {
                        msg.Content = null;
                        msg.Embeds = allEmbeds;
                    });
                }
                else if (regularMessage != null)
                {
                    await regularMessage.ModifyAsync(msg =>
                    {
                        msg.Content = null;
                        msg.Embeds = allEmbeds.ToArray();
                    });
                }
                else
                {
                    // First message
                    regularMessage = await userMsg.Channel.SendMessageAsync(
                        embeds: allEmbeds.ToArray(),
                        allowedMentions: AllowedMentions.None);
                }
            }
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
                lastChunk = lastChunk.Substring(0, maxChunkSize - 100);
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
            AiProvider.Claude => "https://assets-global.website-files.com/63f6c7b6b2b1e2b8e7e1e1e1/63f6c7b6b2b1e2b8e7e1e1e1_claude-logo.png",
            _ => string.Empty
        };
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