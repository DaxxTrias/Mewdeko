using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Octokit;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Images;
using OpenAI_API.Models;
using Serilog;
using StackExchange.Redis;
using TwitchLib.Api.Helix;
using Embed = Discord.Embed;
using Image = Discord.Image;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
/// Service for owner-only commands.
/// </summary>
public class OwnerOnlyService : ILateExecutor, IReadyExecutor, INService
{
    private readonly Mewdeko bot;
    private readonly BotConfigService bss;

    private readonly IDataCache cache;
    private int currentStatusNum;
    private readonly DiscordSocketClient client;
    private readonly CommandHandler cmdHandler;
    private readonly IBotCredentials creds;
    private readonly DbService db;
    private readonly IHttpClientFactory httpFactory;
    private readonly Replacer rep;
    private readonly IBotStrings strings;
    private readonly GuildSettingsService guildSettings;
    private readonly ConcurrentDictionary<ulong, Conversation> conversations = new();

#pragma warning disable CS8714
    private ConcurrentDictionary<ulong?, ConcurrentDictionary<int, Timer>> autoCommands =
#pragma warning restore CS8714
        new();

    private ImmutableDictionary<ulong, IDMChannel> ownerChannels =
        new Dictionary<ulong, IDMChannel>().ToImmutableDictionary();

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerOnlyService"/> class.
    /// This service handles owner-only commands and functionalities for the bot.
    /// </summary>
    /// <param name="client">The Discord client used for interacting with the Discord API.</param>
    /// <param name="cmdHandler">Handles command processing and execution.</param>
    /// <param name="db">Provides access to the database for data persistence.</param>
    /// <param name="strings">Provides access to localized bot strings.</param>
    /// <param name="creds">Contains the bot's credentials and configuration.</param>
    /// <param name="cache">Provides caching functionalities.</param>
    /// <param name="factory">Factory for creating instances of <see cref="HttpClient"/>.</param>
    /// <param name="bss">Service for accessing bot configuration settings.</param>
    /// <param name="phProviders">A collection of providers for placeholder values.</param>
    /// <param name="bot">Reference to the main bot instance.</param>
    /// <param name="guildSettings">Service for accessing guild-specific settings.</param>
    /// <param name="handler">Event handler for subscribing to bot events.</param>
    /// <remarks>
    /// The constructor subscribes to message received events and sets up periodic tasks for rotating statuses
    /// and checking for updates. It also listens for commands to leave guilds or reload images via Redis subscriptions.
    /// </remarks>
    public OwnerOnlyService(DiscordSocketClient client, CommandHandler cmdHandler, DbService db,
        IBotStrings strings, IBotCredentials creds, IDataCache cache, IHttpClientFactory factory,
        BotConfigService bss, IEnumerable<IPlaceholderProvider> phProviders, Mewdeko bot,
        GuildSettingsService guildSettings, EventHandler handler)
    {
        var redis = cache.Redis;
        this.cmdHandler = cmdHandler;
        this.db = db;
        this.strings = strings;
        this.client = client;
        this.creds = creds;
        this.cache = cache;
        this.bot = bot;
        this.guildSettings = guildSettings;
        var imgs = cache.LocalImages;
        httpFactory = factory;
        this.bss = bss;
        handler.MessageReceived += OnMessageReceived;
        if (client.ShardId == 0)
        {
            rep = new ReplacementBuilder()
                .WithClient(client)
                .WithProviders(phProviders)
                .Build();

            _ = Task.Run(RotatingStatuses);
        }

        var sub = redis.GetSubscriber();
        if (this.client.ShardId == 0)
        {
            sub.Subscribe($"{this.creds.RedisKey()}_reload_images",
                delegate { imgs.Reload(); }, CommandFlags.FireAndForget);
        }

        sub.Subscribe($"{this.creds.RedisKey()}_leave_guild", async (_, v) =>
        {
            try
            {
                var guildStr = v.ToString()?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(guildStr))
                    return;
                var server = this.client.Guilds.FirstOrDefault(g => g.Id.ToString() == guildStr) ??
                             this.client.Guilds.FirstOrDefault(g => g.Name.Trim().ToUpperInvariant() == guildStr);

                if (server == null)
                    return;

                if (server.OwnerId != this.client.CurrentUser.Id)
                {
                    await server.LeaveAsync().ConfigureAwait(false);
                    Log.Information("Left server {ServerName} [{ServerId}]", server.Name, server.Id);
                }
                else
                {
                    await server.DeleteAsync().ConfigureAwait(false);
                    Log.Information("Deleted server {ServerName} [{ServerId}]", server.Name, server.Id);
                }
            }
            catch
            {
                // ignored
            }
        }, CommandFlags.FireAndForget);

        _ = CheckUpdateTimer();
        handler.GuildMemberUpdated += QuarantineCheck;
    }

    private async Task QuarantineCheck(Cacheable<SocketGuildUser, ulong> args, SocketGuildUser arsg2)
    {
        if (!args.HasValue)
            return;

        if (args.Id != client.CurrentUser.Id)
            return;

        var value = args.Value;

        if (value.Roles is null)
            return;


        if (!bss.Data.QuarantineNotification)
            return;

        if (!Equals(value.Roles, arsg2.Roles))
        {
            var quarantineRole = value.Guild.Roles.FirstOrDefault(x => x.Name == "Quarantine");
            if (quarantineRole is null)
                return;

            if (value.Roles.All(x => x.Id != quarantineRole.Id) && arsg2.Roles.Any(x => x.Id == quarantineRole.Id))
            {
                if (bss.Data.ForwardToAllOwners)
                {
                    foreach (var i in creds.OwnerIds)
                    {
                        var user = await client.Rest.GetUserAsync(i);
                        if (user is null) continue;
                        var channel = await user.CreateDMChannelAsync();
                        await channel.SendMessageAsync(
                            $"Quarantined in {value.Guild.Name} [{value.Guild.Id}]");
                    }
                }
                else
                {
                    var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                    if (user is not null)
                    {
                        var channel = await user.CreateDMChannelAsync();
                        await channel.SendMessageAsync(
                            $"Quarantined in {value.Guild.Name} [{value.Guild.Id}]");
                    }
                }
            }
        }
    }

    private async Task CheckUpdateTimer()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(bss.Data.CheckUpdateInterval));
        do
        {
            var github = new GitHubClient(new ProductHeaderValue("Mewdeko"));
            var redis = cache.Redis.GetDatabase();
            switch (bss.Data.CheckForUpdates)
            {
                case UpdateCheckType.Release:
                    var latestRelease = await github.Repository.Release.GetLatest("SylveonDeko", "Mewdeko");
                    var eb = new EmbedBuilder()
                        .WithAuthor($"New Release found: {latestRelease.TagName}",
                            "https://seeklogo.com/images/G/github-logo-5F384D0265-seeklogo.com.png",
                            latestRelease.HtmlUrl)
                        .WithDescription(
                            $"- If on Windows, you can download the new release [here]({latestRelease.Assets[0].BrowserDownloadUrl})\n" +
                            $"- If running source just run the `{bss.Data.Prefix}update command and the bot will do the rest for you.`")
                        .WithOkColor();
                    var list = await redis.StringGetAsync($"{creds.RedisKey()}_ReleaseList");
                    if (!list.HasValue)
                    {
                        await redis.StringSetAsync($"{creds.RedisKey()}_ReleaseList",
                            JsonConvert.SerializeObject(latestRelease));
                        Log.Information("Setting latest release to {ReleaseTag}", latestRelease.TagName);
                    }
                    else
                    {
                        var release = JsonConvert.DeserializeObject<Release>(list);
                        if (release.TagName != latestRelease.TagName)
                        {
                            Log.Information("New release found: {ReleaseTag}", latestRelease.TagName);
                            await redis.StringSetAsync($"{creds.RedisKey()}_ReleaseList",
                                JsonConvert.SerializeObject(latestRelease));
                            if (bss.Data.ForwardToAllOwners)
                            {
                                foreach (var i in creds.OwnerIds)
                                {
                                    var user = await client.Rest.GetUserAsync(i);
                                    if (user is null) continue;
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(embed: eb.Build());
                                }
                            }
                            else
                            {
                                var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                                if (user is not null)
                                {
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(embed: eb.Build());
                                }
                            }
                        }
                    }

                    break;
                case UpdateCheckType.Commit:
                    var latestCommit =
                        await github.Repository.Commit.Get("SylveonDeko", "Mewdeko", bss.Data.UpdateBranch);
                    if (latestCommit is null)
                    {
                        Log.Warning(
                            "Failed to get latest commit, make sure you have the correct branch set in bot.yml");
                        break;
                    }

                    var redisCommit = await redis.StringGetAsync($"{creds.RedisKey()}_CommitList");
                    if (!redisCommit.HasValue)
                    {
                        await redis.StringSetAsync($"{creds.RedisKey()}_CommitList",
                            latestCommit.Sha);
                        Log.Information("Setting latest commit to {CommitSha}", latestCommit.Sha);
                    }
                    else
                    {
                        if (redisCommit.ToString() != latestCommit.Sha)
                        {
                            Log.Information("New commit found: {CommitSha}", latestCommit.Sha);
                            await redis.StringSetAsync($"{creds.RedisKey()}_CommitList",
                                latestCommit.Sha);
                            if (bss.Data.ForwardToAllOwners)
                            {
                                foreach (var i in creds.OwnerIds)
                                {
                                    var user = await client.Rest.GetUserAsync(i);
                                    if (user is null) continue;
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(
                                        $"New commit found: {latestCommit.Sha}\n{latestCommit.HtmlUrl}");
                                }
                            }
                            else
                            {
                                var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                                if (user is not null)
                                {
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(
                                        $"New commit found: {latestCommit.Sha}\n{latestCommit.HtmlUrl}");
                                }
                            }
                        }
                    }

                    break;
                case UpdateCheckType.None:
                    break;
                default:
                    Log.Error("Invalid UpdateCheckType {UpdateCheckType}", bss.Data.CheckForUpdates);
                    break;
            }
        } while (await timer.WaitForNextTickAsync());
    }

    private async Task OnMessageReceived(SocketMessage args)
    {
        var isDebugMode = false;
        if (args.Channel is not IGuildChannel guildChannel)
            return;
        var prefix = await guildSettings.GetPrefix(guildChannel.GuildId);
        if (args.Content.StartsWith(prefix))
            return;
        if (bss.Data.ChatGptKey is null or "" || bss.Data.ChatGptChannel is 0)
            return;
        if (args.Author.IsBot)
            return;
        if (args.Channel.Id != bss.Data.ChatGptChannel && args.Channel.Id != bss.Data.ChatGptChannel2)
            return;
        if (args is not IUserMessage usrMsg)
            return;

        //bad hackfix to separate handling of nightly vs stable
#if DEBUG
        isDebugMode = true;
#endif

        try
        {
            var api = new OpenAIAPI(bss.Data.ChatGptKey);

            if (args.Content is ".deletesession" && !isDebugMode)
            {
                if (conversations.TryRemove(args.Author.Id, out _))
                {
                    await usrMsg.SendConfirmReplyAsync("Session deleted");
                    return;
                }
                else
                {
                    await usrMsg.SendConfirmReplyAsync("No session to delete");
                    return;
                }
            }
            else if (args.Content is ",deletesesssion" && isDebugMode)
            {
                if (conversations.TryRemove(args.Author.Id, out _))
                {
                    await usrMsg.SendConfirmReplyAsync("Session deleted");
                    return;
                }

                await usrMsg.SendConfirmReplyAsync("No session to delete");
                return;
            }

            await using var uow = db.GetDbContext();
            (Database.Models.OwnerOnly actualItem, bool added) toUpdate = uow.OwnerOnly.Any()
                    ? (await uow.OwnerOnly.FirstOrDefaultAsync(), false)
                    : (new Database.Models.OwnerOnly
                    {
                        GptTokensUsed = 0
                    }, true);


            if (!args.Content.StartsWith("!frog") && !isDebugMode)
                return;

            else if (!args.Content.StartsWith("-frog") && isDebugMode)
                return;


            Log.Information("ChatGPT request from {Author}: | ({AuthorId}): | {Content}", args.Author, args.Author.Id, args.Content);

            // lower any capitalization in message content
            var loweredContents = args.Content.ToLower();

            // Remove the prefix from the message content being sent to gpt
            var gptprompt = loweredContents.Substring("frog ".Length).Trim();

            // Split the message content into words and take only the first two for checking.
            var words = args.Content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).ToList();
            var scannedWords = words.Select(w => w.ToLower()).ToList();

            if (scannedWords.Contains("image"))
            {
                try
                {
                    await usrMsg.Channel.SendMessageAsync("Dall-E disabled.");
                    return;
                }
                catch
                {
                    throw;
                }
                var authorName = args.Author.ToString();
                var prompt = args.Content.Substring("frog image ".Length).Trim();
                if (string.IsNullOrEmpty(prompt))
                {
                    await usrMsg.Channel.SendMessageAsync("Please provide a prompt for the image.");
                    return;
                }

                IUserMessage placeholderMessage = null;
                try
                {
                    // Send a placeholder message directly using the bot's client
                    placeholderMessage = await usrMsg.SendConfirmReplyAsync($"{bss.Data.LoadingEmote} Generating image...");

                    // Generate the image
                    var images = await api.ImageGenerations.CreateImageAsync(new ImageGenerationRequest
                    {
                        Prompt = prompt,  // prompt (text string)
                        NumOfImages = 1, // dall-e2 can provide multiple images, e3 does not support this currently
                        Size = ImageSize._1792x1024, // resolution of the generated images (256x256 | 512x512 | 1024x1024 | 1792x1024) dall-e3 cannot use images below 1024x1024
                        Model = Model.DALLE3, // model (model for this req. defaults to dall-e2
                        User = authorName, // user: author of post, this can be used to help openai detect abuse and rule breaking
                        ResponseFormat = ImageResponseFormat.Url // the format the images can be returned as. must be url or b64_json
                        // quality: by default images are generated at standard, but on e3 you can use HD
                    });

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
                    if (images.Data.Count > 0)
                    {
                        var imageUrl = images.Data[0].Url; // Assuming images.Data[0] contains the URL
                        var embed = new EmbedBuilder()
                            .WithImageUrl(imageUrl)
                            .Build();
                        await placeholderMessage.ModifyAsync(msg =>
                        {
                            msg.Content = ""; // Clearing the content
                            msg.Embed = embed;
                        });
                    }
                    else
                    {
                        await placeholderMessage.ModifyAsync(msg => msg.Content = "No image generated.");
                    }
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
                    await usrMsg.SendErrorReplyAsync("An error occurred while processing your request. Please try again later.");
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
                    await usrMsg.SendErrorReplyAsync($"Failed to generate image due to an unexpected error. Please try again later. Error code: **{ex.HResult}**");
                }
                return;
            }

            if (scannedWords.Contains("scan"))
            {
                try
                {
                    //todo: dep support has been enabled, finish this when ef model port done
                    // https://github.com/OkGoDoIt/OpenAI-API-dotnet/commit/b824ac5b50027af48aa8ea02bf1bc40fac36f390#diff-ba720258629043138df0c8ebea494853e88e2517638a615c4a9c4fdc84a2a168
                    await usrMsg.Channel.SendMessageAsync("Not Yet Implemented.");
                    return;
                }
                catch
                {
                    throw;
                }
            }

            if (!conversations.TryGetValue(args.Author.Id, out var conversation))
            {
                conversation = StartNewConversation(args.Author, api);
                conversations.TryAdd(args.Author.Id, conversation);
            }

            conversation.AppendUserInput(gptprompt);

            var loadingMsg = await usrMsg.SendConfirmReplyAsync($"{bss.Data.LoadingEmote} Awaiting response...");
            await StreamResponseAndUpdateEmbedAsync(conversation, loadingMsg, uow, toUpdate, args.Author);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Error in ChatGPT");
            await usrMsg.SendErrorReplyAsync("Something went wrong, please try again later.");
        }
    }

    public class OpenAiErrorResponse
    {
        [JsonProperty("error")]
        public OpenAiError Error { get; set; }
    }

    public class OpenAiError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    private Conversation StartNewConversation(SocketUser user, IOpenAIAPI api, SocketMessage args = null)
    {
        var modelToUse = bss.Data.ChatGptModel switch
        {
            "gpt4-turbo" => Model.GPT4_Turbo,
            "gpt-4-0613" => Model.GPT4_32k_Context,
            "gpt4" or "gpt-4" => Model.GPT4,
            "gpt3" => Model.ChatGPTTurbo,
            _ => Model.ChatGPTTurbo
        };

        if (bss.Data.ChatGptModel == "gpt-4o")
        {
            modelToUse.ModelID = "gpt-4o";
        }
        //else if (bss.Data.ChatGptModel == "o1-preview")
        //{
        //    modelToUse.ModelID = "o1-preview";
        //}

        var chat = api.Chat.CreateConversation(new ChatRequest
        {
            MaxTokens = bss.Data.ChatGptMaxTokens,
            Temperature = bss.Data.ChatGptTemperature,
            Model = modelToUse
        });
        chat.AppendSystemMessage(bss.Data.ChatGptInitPrompt);
        chat.AppendSystemMessage($"The user's name is {user}.");
        return chat;
    }

    private static async Task StreamResponseAndUpdateEmbedAsync(Conversation conversation, IUserMessage loadingMsg,
        MewdekoContext uow, (Database.Models.OwnerOnly actualItem, bool added) toUpdate, SocketUser author)
    {
        var responseBuilder = new StringBuilder();
        var lastUpdate = DateTimeOffset.UtcNow;

        await conversation.StreamResponseFromChatbotAsync(async partialResponse =>
        {
            responseBuilder.Append(partialResponse);
            if (!((DateTimeOffset.UtcNow - lastUpdate).TotalSeconds >= 1))
                return;
            lastUpdate = DateTimeOffset.UtcNow;
            var embeds = BuildEmbeds(responseBuilder.ToString(), author, toUpdate.actualItem.GptTokensUsed,
                conversation);
            await loadingMsg.ModifyAsync(m => m.Embeds = embeds.ToArray());
        });

        var finalResponse = responseBuilder.ToString();
        if (conversation.MostRecentApiResult.Usage != null)
        {
            toUpdate.actualItem.GptTokensUsed += conversation.MostRecentApiResult.Usage.TotalTokens;
        }

        if (toUpdate.added)
            uow.OwnerOnly.Add(toUpdate.actualItem);
        else
            uow.OwnerOnly.Update(toUpdate.actualItem);
        await uow.SaveChangesAsync();

        var finalEmbeds = BuildEmbeds(finalResponse, author, toUpdate.actualItem.GptTokensUsed, conversation);
        await loadingMsg.ModifyAsync(m => m.Embeds = finalEmbeds.ToArray());
    }

    private static List<Embed> BuildEmbeds(string response, IUser requester, int totalTokensUsed,
        Conversation conversation)
    {
        var embeds = new List<Embed>();
        var partIndex = 0;
        while (partIndex < response.Length)
        {
            var length = Math.Min(4096, response.Length - partIndex);
            var description = response.Substring(partIndex, length);
            var embedBuilder = new EmbedBuilder()
                .WithDescription(description)
                .WithOkColor();

            if (partIndex == 0)
                embedBuilder.WithAuthor("ChatGPT",
                    "https://seeklogo.com/images/C/chatgpt-logo-02AFA704B5-seeklogo.com.png");

            if (partIndex + length == response.Length)
                embedBuilder.WithFooter(
                    $"Requested by {requester.Username}");
                    //$"Requested by {requester.Username} | Response Tokens: {conversation.MostRecentApiResult.Usage?.TotalTokens} | Total Used: {totalTokensUsed}");

            embeds.Add(embedBuilder.Build());
            partIndex += length;
        }

        return embeds;
    }


    /// <summary>
    /// Resets the count of used GPT tokens to zero in the database. This is typically called to clear the token usage count at the start of a new billing period or when manually resetting the token count.
    /// </summary>
    public async Task ClearUsedTokens()
    {
        await using var uow = db.GetDbContext();
        var val = await uow.OwnerOnly.FirstOrDefaultAsync();
        if (val is null)
            return;
        val.GptTokensUsed = 0;
        uow.OwnerOnly.Update(val);
        await uow.SaveChangesAsync();
    }

    /// <summary>
    /// Forwards direct messages (DMs) received by the bot to the owners' DMs. This allows bot owners to monitor and respond to user messages directly.
    /// </summary>
    /// <param name="discordSocketClient">The Discord client through which the message was received.</param>
    /// <param name="guild">The guild associated with the message, if any.</param>
    /// <param name="msg">The message that was received and is to be forwarded.</param>
    /// <remarks>
    /// The method checks if the message was sent in a DM channel and forwards it to all owners if the setting is enabled.
    /// Attachments are also forwarded. Errors in sending messages to any owner are logged but not thrown.
    /// </remarks>
    public async Task LateExecute(DiscordSocketClient discordSocketClient, IGuild guild, IUserMessage msg)
    {
        var bs = bss.Data;
        if (msg.Channel is IDMChannel && bss.Data.ForwardMessages && ownerChannels.Count > 0)
        {
            var title = $"{strings.GetText("dm_from")} [{msg.Author}]({msg.Author.Id})";

            var attachamentsTxt = strings.GetText("attachments");

            var toSend = msg.Content;

            if (msg.Attachments.Count > 0)
            {
                toSend +=
                    $"\n\n{Format.Code(attachamentsTxt)}:\n{string.Join("\n", msg.Attachments.Select(a => a.ProxyUrl))}";
            }

            if (bs.ForwardToAllOwners)
            {
                var allOwnerChannels = ownerChannels.Values;

                foreach (var ownerCh in allOwnerChannels.Where(ch => ch.Recipient.Id != msg.Author.Id))
                {
                    try
                    {
                        await ownerCh.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                    }
                    catch
                    {
                        Log.Warning("Can't contact owner with id {0}", ownerCh.Recipient.Id);
                    }
                }
            }
            else
            {
                var firstOwnerChannel = ownerChannels.Values.First();
                if (firstOwnerChannel.Recipient.Id != msg.Author.Id)
                {
                    try
                    {
                        await firstOwnerChannel.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
    }

    /// <summary>
    /// Initializes required services and loads configurations when the bot is ready. This includes setting up automatic commands based on their configured intervals and creating direct message channels for the bot owners.
    /// </summary>
    /// <remarks>
    /// This method is typically called once when the bot starts and is ready to receive and process messages. It prepares the bot for operation by loading necessary configurations and establishing connections.
    /// </remarks>
    public async Task OnReadyAsync()
    {
        await using var uow = db.GetDbContext();

        autoCommands =
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval >= 5)
                .AsEnumerable()
                .GroupBy(x => x.GuildId)
                .ToDictionary(x => x.Key,
                    y => y.ToDictionary(x => x.Id, TimerFromAutoCommand)
                        .ToConcurrent())
                .ToConcurrent();

        foreach (var cmd in uow.AutoCommands.AsNoTracking().Where(x => x.Interval == 0))
        {
            try
            {
                await ExecuteCommand(cmd).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        if (client.ShardId == 0)
        {
            var channels = await Task.WhenAll(creds.OwnerIds.Select(id =>
            {
                var user = client.GetUser(id);
                return user == null ? Task.FromResult<IDMChannel?>(null) : user.CreateDMChannelAsync();
            })).ConfigureAwait(false);

            ownerChannels = channels.Where(x => x is not null)
                .ToDictionary(x => x.Recipient.Id, x => x)
                .ToImmutableDictionary();

            if (ownerChannels.Count == 0)
            {
                Log.Warning(
                    "No owner channels created! Make sure you've specified the correct OwnerId in the credentials.json file and invited the bot to a Discord server");
            }
            else
            {
                Log.Information(
                    $"Created {ownerChannels.Count} out of {creds.OwnerIds.Length} owner message channels.");
            }
        }
    }

    private async Task RotatingStatuses()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                if (!bss.Data.RotateStatuses)
                    continue;

                IReadOnlyList<RotatingPlayingStatus> rotatingStatuses;
                var uow = db.GetDbContext();
                await using (uow.ConfigureAwait(false))
                {
                    rotatingStatuses = uow.RotatingStatus.AsNoTracking().OrderBy(x => x.Id).ToList();
                }

                if (rotatingStatuses.Count == 0)
                    continue;

                var playingStatus = currentStatusNum >= rotatingStatuses.Count
                    ? rotatingStatuses[currentStatusNum = 0]
                    : rotatingStatuses[currentStatusNum++];

                var statusText = rep.Replace(playingStatus.Status);
                await bot.SetGameAsync(statusText, playingStatus.Type).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Rotating playing status errored: {ErrorMessage}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Removes a playing status from the rotating statuses list based on its index.
    /// </summary>
    /// <param name="index">The zero-based index of the status to remove.</param>
    /// <returns>The status that was removed, or null if the index was out of bounds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="index"/> is less than 0.</exception>
    public async Task<string?> RemovePlayingAsync(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        await using var uow = db.GetDbContext();
        var toRemove = await uow.RotatingStatus
            .AsQueryable()
            .AsNoTracking()
            .Skip(index)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (toRemove is null)
            return null;

        uow.Remove(toRemove);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return toRemove.Status;
    }

    /// <summary>
    /// Adds a new playing status to the list of rotating statuses.
    /// </summary>
    /// <param name="t">The type of activity for the status (e.g., playing, streaming).</param>
    /// <param name="status">The text of the status to display.</param>
    /// <returns>A task that represents the asynchronous add operation.</returns>
    public async Task AddPlaying(ActivityType t, string status)
    {
        await using var uow = db.GetDbContext();
        var toAdd = new RotatingPlayingStatus
        {
            Status = status,
            Type = t
        };
        uow.Add(toAdd);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Toggles the rotation of playing statuses on or off.
    /// </summary>
    /// <returns>True if rotation is enabled after the toggle, false otherwise.</returns>
    public bool ToggleRotatePlaying()
    {
        var enabled = false;
        bss.ModifyConfig(bs => enabled = bs.RotateStatuses = !bs.RotateStatuses);
        return enabled;
    }

    /// <summary>
    /// Retrieves the current list of rotating playing statuses.
    /// </summary>
    /// <returns>A read-only list of <see cref="RotatingPlayingStatus"/> representing the current rotating statuses.</returns>
    public IReadOnlyList<RotatingPlayingStatus> GetRotatingStatuses()
    {
        using var uow = db.GetDbContext();
        return uow.RotatingStatus.AsNoTracking().ToList();
    }

    private Timer TimerFromAutoCommand(AutoCommand x) =>
        new(async obj => await ExecuteCommand((AutoCommand)obj).ConfigureAwait(false),
            x,
            x.Interval * 1000,
            x.Interval * 1000);

    private async Task ExecuteCommand(AutoCommand cmd)
    {
        try
        {
            if (cmd.GuildId is null)
                return;
            var guildShard = (int)((cmd.GuildId.Value >> 22) % (ulong)creds.TotalShards);
            if (guildShard != client.ShardId)
                return;
            var prefix = await guildSettings.GetPrefix(cmd.GuildId.Value);
            //if someone already has .die as their startup command, ignore it
            if (cmd.CommandText.StartsWith($"{prefix}die", StringComparison.InvariantCulture))
                return;
            await cmdHandler.ExecuteExternal(cmd.GuildId, cmd.ChannelId, cmd.CommandText).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in SelfService ExecuteCommand");
        }
    }

    /// <summary>
    /// Adds a new auto command to the database and schedules it if necessary.
    /// </summary>
    /// <param name="cmd">The auto command to be added.</param>
    /// <remarks>
    /// If the command's interval is 5 seconds or more, it's also scheduled to be executed periodically according to its interval.
    /// </remarks>
    public void AddNewAutoCommand(AutoCommand cmd)
    {
        using (var uow = db.GetDbContext())
        {
            uow.AutoCommands.Add(cmd);
            uow.SaveChanges();
        }

        if (cmd.Interval >= 5)
        {
            var autos = autoCommands.GetOrAdd(cmd.GuildId, new ConcurrentDictionary<int, Timer>());
            autos.AddOrUpdate(cmd.Id, _ => TimerFromAutoCommand(cmd), (_, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return TimerFromAutoCommand(cmd);
            });
        }
    }

    /// <summary>
    /// Sets the default prefix for bot commands.
    /// </summary>
    /// <param name="prefix">The new prefix to be set.</param>
    /// <returns>The newly set prefix.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="prefix"/> is null or whitespace.</exception>
    public string SetDefaultPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));

        bss.ModifyConfig(bs => bs.Prefix = prefix);

        return prefix;
    }

    /// <summary>
    /// Retrieves a list of auto commands set to execute at bot startup (interval of 0).
    /// </summary>
    /// <returns>A list of startup auto commands.</returns>
    public IEnumerable<AutoCommand> GetStartupCommands()
    {
        using var uow = db.GetDbContext();
        return
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval == 0)
                .OrderBy(x => x.Id)
                .ToList();
    }

    /// <summary>
    /// Retrieves a list of auto commands with an interval of 5 seconds or more.
    /// </summary>
    /// <returns>A list of auto commands set to execute periodically.</returns>
    public IEnumerable<AutoCommand> GetAutoCommands()
    {
        using var uow = db.GetDbContext();
        return
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval >= 5)
                .OrderBy(x => x.Id)
                .ToList();
    }

    /// <summary>
    /// Instructs the bot to leave a guild based on the guild's identifier or name.
    /// </summary>
    /// <param name="guildStr">The guild identifier or name.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task LeaveGuild(string guildStr)
    {
        var sub = cache.Redis.GetSubscriber();
        return sub.PublishAsync($"{creds.RedisKey()}_leave_guild", guildStr);
    }

    /// <summary>
    /// Attempts to restart the bot using the configured restart command.
    /// </summary>
    /// <returns>True if the command to restart the bot is not null or whitespace and the bot is restarted; otherwise, false.</returns>
    public bool RestartBot()
    {
        var cmd = creds.RestartCommand;
        if (string.IsNullOrWhiteSpace(cmd.Cmd))
            return false;

        Restart();
        return true;
    }

    /// <summary>
    /// Removes a startup command (a command with an interval of 0) at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the startup command to remove.</param>
    /// <param name="cmd">Out parameter that returns the removed auto command if the operation succeeds.</param>
    /// <returns>True if a command was found and removed; otherwise, false.</returns>
    public bool RemoveStartupCommand(int index, out AutoCommand cmd)
    {
        using var uow = db.GetDbContext();
        cmd = uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval == 0)
            .Skip(index)
            .FirstOrDefault();

        if (cmd != null)
        {
            uow.Remove(cmd);
            uow.SaveChanges();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes an auto command based on its index in the collection of commands with an interval of 5 seconds or more.
    /// </summary>
    /// <param name="index">The zero-based index of the command to remove.</param>
    /// <param name="cmd">Outputs the removed <see cref="AutoCommand"/> if the method returns true.</param>
    /// <returns>True if a command was successfully found and removed; otherwise, false.</returns>
    public bool RemoveAutoCommand(int index, out AutoCommand cmd)
    {
        using var uow = db.GetDbContext();
        cmd = uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval >= 5)
            .Skip(index)
            .FirstOrDefault();

        if (cmd == null)
            return false;
        uow.Remove(cmd);
        if (autoCommands.TryGetValue(cmd.GuildId, out var autos))
        {
            if (autos.TryRemove(cmd.Id, out var timer))
                timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        uow.SaveChanges();
        return true;
    }

    /// <summary>
    /// Sets a new avatar for the bot by downloading an image from a specified URL.
    /// </summary>
    /// <param name="img">The URL of the image to set as the new avatar.</param>
    /// <returns>True if the avatar was successfully updated; otherwise, false.</returns>
    public async Task<bool> SetAvatar(string img)
    {
        if (string.IsNullOrWhiteSpace(img))
            return false;

        if (!Uri.IsWellFormedUriString(img, UriKind.Absolute))
            return false;

        var uri = new Uri(img);

        using var http = httpFactory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (!sr.IsImage())
            return false;

        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await client.CurrentUser.ModifyAsync(u => u.Avatar = new Image(imgStream)).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Clears all startup commands from the database.
    /// </summary>
    public void ClearStartupCommands()
    {
        using var uow = db.GetDbContext();
        var toRemove = uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval == 0);

        uow.AutoCommands.RemoveRange(toRemove);
        uow.SaveChanges();
    }

    /// <summary>
    /// Reloads images from a source, typically used for refreshing local or cached resources.
    /// </summary>
    public void ReloadImages()
    {
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_reload_images", "");
    }

    /// <summary>
    /// Instructs the bot to shut down.
    /// </summary>
    public void Die()
    {
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_die", "", CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Restarts the bot by invoking a system command.
    /// </summary>
    public void Restart()
    {
        Process.Start(creds.RestartCommand.Cmd, creds.RestartCommand.Args);
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_die", "", CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Restarts a specific bot shard.
    /// </summary>
    /// <param name="shardId">The ID of the shard to restart.</param>
    /// <returns>True if the shard ID is valid and the shard is restarted; otherwise, false.</returns>
    public bool RestartShard(int shardId)
    {
        if (shardId < 0 || shardId >= creds.TotalShards)
            return false;

        var pub = cache.Redis.GetSubscriber();
        pub.Publish($"{creds.RedisKey()}_shardcoord_stop",
            JsonConvert.SerializeObject(shardId),
            CommandFlags.FireAndForget);

        return true;
    }

    /// <summary>
    /// Toggles the bot's message forwarding feature.
    /// </summary>
    /// <returns>True if message forwarding is enabled after the toggle; otherwise, false.</returns>
    public bool ForwardMessages()
    {
        var isForwarding = false;
        bss.ModifyConfig(config => isForwarding = config.ForwardMessages = !config.ForwardMessages);

        return isForwarding;
    }

    /// <summary>
    /// Toggles whether the bot forwards messages to all owners or just the primary owner.
    /// </summary>
    /// <returns>True if forwarding to all owners is enabled after the toggle; otherwise, false.</returns>
    public bool ForwardToAll()
    {
        var isToAll = false;
        bss.ModifyConfig(config => isToAll = config.ForwardToAllOwners = !config.ForwardToAllOwners);
        return isToAll;
    }
}