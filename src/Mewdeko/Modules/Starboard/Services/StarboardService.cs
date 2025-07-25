﻿using DataModel;
using LinqToDB;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Starboard.Services;

/// <summary>
///     Service responsible for managing multiple starboards in Discord servers.
/// </summary>
public class StarboardService : INService, IReadyExecutor, IUnloadableService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<StarboardService> logger;
    private readonly GeneratedBotStrings strings;
    private List<DataModel.Starboard> starboardConfigs = [];

    private List<StarboardPost> starboardPosts = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="StarboardService" /> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="eventHandler">The event handler.</param>
    public StarboardService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        EventHandler eventHandler, GeneratedBotStrings strings, ILogger<StarboardService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.eventHandler = eventHandler;
        this.strings = strings;
        this.logger = logger;
        eventHandler.Subscribe("ReactionAdded", "StarboardService", OnReactionAddedAsync);
        eventHandler.Subscribe("MessageDeleted", "StarboardService", OnMessageDeletedAsync);
        eventHandler.Subscribe("ReactionRemoved", "StarboardService", OnReactionRemoveAsync);
        eventHandler.Subscribe("ReactionsCleared", "StarboardService", OnAllReactionsClearedAsync);
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        logger.LogInformation($"Starting {GetType()} Cache");
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        starboardPosts = await dbContext.StarboardPosts.ToListAsync();
        starboardConfigs = await dbContext.Starboards.ToListAsync();
        logger.LogInformation("Starboard Cache Ready");
    }

    /// <summary>
    ///     Unloads the service and unsubscribes from events.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("ReactionAdded", "StarboardService", OnReactionAddedAsync);
        eventHandler.Unsubscribe("MessageDeleted", "StarboardService", OnMessageDeletedAsync);
        eventHandler.Unsubscribe("ReactionRemoved", "StarboardService", OnReactionRemoveAsync);
        eventHandler.Unsubscribe("ReactionsCleared", "StarboardService", OnAllReactionsClearedAsync);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Creates a new starboard configuration for a guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channelId">The ID of the starboard channel.</param>
    /// <param name="emote">The emote to use for this starboard.</param>
    /// <param name="threshold">The number of reactions required.</param>
    /// <returns>The ID of the created starboard configuration.</returns>
    public async Task<int> CreateStarboard(IGuild guild, ulong channelId, string emote, int threshold)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var config = new DataModel.Starboard
        {
            GuildId = guild.Id,
            StarboardChannelId = channelId,
            Emote = emote,
            Threshold = threshold,
            CheckedChannels = "",
            UseBlacklist = false,
            AllowBots = false,
            RemoveOnDelete = true,
            RemoveOnReactionsClear = true,
            RemoveOnBelowThreshold = true,
            RepostThreshold = 0
        };

        await db.InsertAsync(config);
        starboardConfigs.Add(config);
        return config.Id;
    }

    /// <summary>
    ///     Deletes a starboard configuration.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <returns>True if the starboard was deleted, false otherwise.</returns>
    public async Task<bool> DeleteStarboard(IGuild guild, int starboardId)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.DeleteAsync(config);

        starboardConfigs.Remove(config);
        return true;
    }

    /// <summary>
    ///     Gets all starboard configurations for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of starboard configurations.</returns>
    public List<DataModel.Starboard> GetStarboards(ulong guildId)
        => starboardConfigs.Where(x => x.GuildId == guildId).ToList();

    /// <summary>
    ///     Gets recent starboard highlights for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="limit">The maximum number of highlights to return.</param>
    /// <returns>A list of recent starboard highlights.</returns>
    public async Task<List<StarboardHighlight>> GetRecentHighlights(ulong guildId, int limit = 5)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Get starboard configs for this guild
        var guildStarboards = starboardConfigs.Where(s => s.GuildId == guildId).ToList();
        if (guildStarboards.Count == 0)
            return new List<StarboardHighlight>();

        // Get recent starboard posts for this guild
        var recentPosts = await dbContext.StarboardPosts
            .Where(sp => guildStarboards.Select(gs => gs.Id).Contains(sp.StarboardConfigId))
            .OrderByDescending(sp => sp.DateAdded)
            .Take(limit * 2) // Get more than needed in case some messages are deleted
            .ToListAsync();

        var highlights = new List<StarboardHighlight>();
        var guild = client.GetGuild(guildId);

        if (guild == null)
            return highlights;

        // Process each post and try to get message content from Discord
        foreach (var post in recentPosts)
        {
            if (highlights.Count >= limit)
                break;

            try
            {
                var starboardConfig = guildStarboards.FirstOrDefault(s => s.Id == post.StarboardConfigId);
                if (starboardConfig == null) continue;

                var starboardChannel = guild.GetTextChannel(starboardConfig.StarboardChannelId);
                if (starboardChannel == null) continue;

                var starboardMessage = await starboardChannel.GetMessageAsync(post.PostId);
                if (starboardMessage == null) continue;

                // Parse star count from the starboard message - handle different emotes
                var starCount = 0;
                var emote = starboardConfig.Emote ?? "⭐";
                if (starboardMessage.Content.Contains(emote))
                {
                    var starText = starboardMessage.Content.Split(' ')[0];
                    if (int.TryParse(starText.Replace(emote, "").Trim(), out var parsedCount))
                        starCount = parsedCount;
                }

                // Try to get original message content and author info
                var originalContent = "Message content unavailable";
                var authorName = "Unknown User";
                var authorAvatarUrl = "";
                var imageUrl = "";

                // Extract content from starboard message embed or content
                if (starboardMessage.Embeds.Any())
                {
                    var embed = starboardMessage.Embeds.First();
                    originalContent = embed.Description ?? originalContent;
                    authorName = embed.Author?.Name ?? authorName;
                    authorAvatarUrl = embed.Author?.IconUrl ?? "";

                    // Check for images in embed
                    if (embed.Image.HasValue)
                        imageUrl = embed.Image.Value.Url;
                    else if (embed.Thumbnail.HasValue)
                        imageUrl = embed.Thumbnail.Value.Url;
                }

                highlights.Add(new StarboardHighlight
                {
                    MessageId = post.MessageId,
                    ChannelId = 0, // We'd need to store this in the DB to get it
                    StarCount = starCount,
                    Content = originalContent,
                    AuthorName = authorName,
                    AuthorAvatarUrl = authorAvatarUrl,
                    ImageUrl = imageUrl,
                    StarEmote = emote,
                    CreatedAt = post.DateAdded ?? DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                // Log error but continue processing other messages
                logger.LogWarning($"Failed to process starboard highlight for post {post.Id}: {ex.Message}");
            }
        }

        return highlights.OrderByDescending(h => h.StarCount).ToList();
    }

    /// <summary>
    ///     Sets whether bots are allowed to be starred for a specific starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="allowed">Whether bots are allowed.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetAllowBots(IGuild guild, int starboardId, bool allowed)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.AllowBots = allowed;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets whether to remove starred messages when the original is deleted.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="removeOnDelete">Whether to remove on delete.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRemoveOnDelete(IGuild guild, int starboardId, bool removeOnDelete)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.RemoveOnDelete = removeOnDelete;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets whether to remove starred messages when reactions are cleared.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="removeOnClear">Whether to remove on clear.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRemoveOnClear(IGuild guild, int starboardId, bool removeOnClear)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.RemoveOnReactionsClear = removeOnClear;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets whether to remove starred messages when they fall below threshold.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="removeBelowThreshold">Whether to remove below threshold.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRemoveBelowThreshold(IGuild guild, int starboardId, bool removeBelowThreshold)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.RemoveOnBelowThreshold = removeBelowThreshold;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets the repost threshold for a starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRepostThreshold(IGuild guild, int starboardId, int threshold)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.RepostThreshold = threshold;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets the star threshold for a starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetStarThreshold(IGuild guild, int starboardId, int threshold)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.Threshold = threshold;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Sets whether to use blacklist mode for channel checking.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="useBlacklist">Whether to use blacklist mode.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetUseBlacklist(IGuild guild, int starboardId, bool useBlacklist)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        config.UseBlacklist = useBlacklist;
        await db.UpdateAsync(config);

        return true;
    }

    /// <summary>
    ///     Toggles a channel in the starboard's check list.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="channelId">The channel ID to toggle.</param>
    /// <returns>A tuple containing whether the channel was added and the starboard configuration.</returns>
    public async Task<(bool WasAdded, DataModel.Starboard Config)> ToggleChannel(IGuild guild, int starboardId,
        string channelId)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return (false, null);

        var channels = config.CheckedChannels.Split(" ", StringSplitOptions.RemoveEmptyEntries).ToList();

        await using var db = await dbFactory.CreateConnectionAsync();
        if (!channels.Contains(channelId))
        {
            channels.Add(channelId);
            config.CheckedChannels = string.Join(" ", channels);
            await db.UpdateAsync(config);

            return (true, config);
        }

        channels.Remove(channelId);
        config.CheckedChannels = string.Join(" ", channels);
        await db.UpdateAsync(config);

        return (false, config);
    }


    private async Task AddStarboardPost(ulong messageId, ulong postId, int starboardId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var post = starboardPosts.Find(x => x.MessageId == messageId && x.StarboardConfigId == starboardId);
        if (post == null)
        {
            var toAdd = new StarboardPost
            {
                MessageId = messageId, PostId = postId, StarboardConfigId = starboardId
            };
            starboardPosts.Add(toAdd);
            await dbContext.InsertAsync(toAdd);

            return;
        }

        if (post.PostId == postId)
            return;

        starboardPosts.Remove(post);
        post.PostId = postId;
        await dbContext.UpdateAsync(post);
        starboardPosts.Add(post);
    }

    private async Task RemoveStarboardPost(ulong messageId, int starboardId)
    {
        var toRemove = starboardPosts.Find(x => x.MessageId == messageId && x.StarboardConfigId == starboardId);
        if (toRemove == null)
            return;

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        await dbContext.DeleteAsync(toRemove);
        starboardPosts.Remove(toRemove);
    }

    private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (!reaction.User.IsSpecified
            || reaction.User.Value.IsBot
            || !channel.HasValue
            || channel.Value is not ITextChannel textChannel)
            return;

        var guildStarboards = GetStarboards(textChannel.GuildId);
        if (!guildStarboards.Any())
            return;

        foreach (var starboard in guildStarboards)
        {
            await HandleReactionChange(message, channel, reaction, starboard, true);
        }
    }

    private async Task OnReactionRemoveAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (!reaction.User.IsSpecified
            || reaction.User.Value.IsBot
            || !channel.HasValue
            || channel.Value is not ITextChannel textChannel)
            return;

        var guildStarboards = GetStarboards(textChannel.GuildId);
        if (!guildStarboards.Any())
            return;

        foreach (var starboard in guildStarboards)
        {
            await HandleReactionChange(message, channel, reaction, starboard, false);
        }
    }

    private async Task HandleReactionChange(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction,
        DataModel.Starboard starboard,
        bool isAdd)
    {
        var textChannel = channel.Value as ITextChannel;
        var emote = starboard.Emote.ToIEmote();

        if (emote.Name == null || !Equals(reaction.Emote, emote))
            return;

        var starboardChannel = await textChannel.Guild.GetTextChannelAsync(starboard.StarboardChannelId);
        if (starboardChannel == null)
            return;

        IUserMessage newMessage;
        if (!message.HasValue)
            newMessage = await message.GetOrDownloadAsync();
        else
            newMessage = message.Value;

        if (newMessage == null)
            return;

        var gUser = await textChannel.Guild.GetUserAsync(client.CurrentUser.Id);
        var botPerms = gUser.GetPermissions(starboardChannel);

        if (!botPerms.Has(ChannelPermission.SendMessages))
            return;

        if (starboard.UseBlacklist)
        {
            if (!starboard.CheckedChannels.IsNullOrWhiteSpace() &&
                starboard.CheckedChannels.Split(" ").Contains(newMessage.Channel.Id.ToString()))
                return;
        }
        else
        {
            if (!starboard.CheckedChannels.IsNullOrWhiteSpace() &&
                !starboard.CheckedChannels.Split(" ").Contains(newMessage.Channel.ToString()))
                return;
        }

        string content;
        string imageurl;
        var component = new ComponentBuilder()
            .WithButton(url: newMessage.GetJumpUrl(), style: ButtonStyle.Link, label: "Jump To Message")
            .Build();

        if (newMessage.Author.IsBot)
        {
            if (!starboard.AllowBots)
                return;

            content = newMessage.Embeds.Count > 0
                ? newMessage.Embeds.Select(x => x.Description).FirstOrDefault()
                : newMessage.Content;
            imageurl = newMessage.Attachments.Count > 0
                ? newMessage.Attachments.FirstOrDefault().ProxyUrl
                : newMessage.Embeds?.Select(x => x.Image).FirstOrDefault()?.ProxyUrl;
        }
        else
        {
            content = newMessage.Content;
            imageurl = newMessage.Attachments?.FirstOrDefault()?.ProxyUrl;
        }

        if (content is null && imageurl is null)
            return;

        var emoteCount = await newMessage.GetReactionUsersAsync(emote, int.MaxValue).FlattenAsync();
        var count = emoteCount.Where(x => !x.IsBot);
        var enumerable = count as IUser[] ?? count.ToArray();
        var maybePost = starboardPosts.Find(x => x.MessageId == newMessage.Id && x.StarboardConfigId == starboard.Id);

        if (enumerable.Length < starboard.Threshold)
        {
            if (maybePost != null && starboard.RemoveOnBelowThreshold)
            {
                await RemoveStarboardPost(newMessage.Id, starboard.Id);
                try
                {
                    var post = await starboardChannel.GetMessageAsync(maybePost.PostId);
                    if (post != null)
                        await post.DeleteAsync();
                }
                catch
                {
                    // ignored
                }
            }

            return;
        }

        if (maybePost != null)
        {
            if (starboard.RepostThreshold > 0)
            {
                var messages = await starboardChannel.GetMessagesAsync(starboard.RepostThreshold).FlattenAsync();
                var post = messages.FirstOrDefault(x => x.Id == maybePost.PostId);

                if (post != null)
                {
                    var post2 = post as IUserMessage;
                    var eb1 = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(newMessage.Author)
                        .WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);

                    if (imageurl is not null)
                        eb1.WithImageUrl(imageurl);

                    await post2.ModifyAsync(x =>
                    {
                        x.Content = strings.StarboardMessage(textChannel.Guild.Id, emote, enumerable.Length,
                            textChannel.Mention);
                        x.Components = component;
                        x.Embed = eb1.Build();
                    });
                }
                else
                {
                    var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                    if (tryGetOldPost != null)
                    {
                        try
                        {
                            await tryGetOldPost.DeleteAsync();
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    var eb2 = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(newMessage.Author)
                        .WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);

                    if (imageurl is not null)
                        eb2.WithImageUrl(imageurl);

                    var msg1 = await starboardChannel.SendMessageAsync(
                        strings.StarboardMessage(textChannel.Guild.Id, emote, enumerable.Length, textChannel.Mention),
                        embed: eb2.Build(),
                        components: component);

                    await AddStarboardPost(message.Id, msg1.Id, starboard.Id);
                }
            }
            else
            {
                var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                if (tryGetOldPost != null)
                {
                    var toModify = tryGetOldPost as IUserMessage;
                    var eb1 = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(newMessage.Author)
                        .WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);

                    if (imageurl is not null)
                        eb1.WithImageUrl(imageurl);

                    await toModify.ModifyAsync(x =>
                    {
                        x.Content = strings.StarboardMessage(textChannel.Guild.Id, emote, enumerable.Length,
                            textChannel.Mention);
                        x.Components = component;
                        x.Embed = eb1.Build();
                    });
                }
                else
                {
                    var eb2 = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(newMessage.Author)
                        .WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);

                    if (imageurl is not null)
                        eb2.WithImageUrl(imageurl);

                    var msg1 = await starboardChannel.SendMessageAsync(
                        strings.StarboardMessage(textChannel.Guild.Id, emote, enumerable.Length, textChannel.Mention),
                        embed: eb2.Build(),
                        components: component);

                    await AddStarboardPost(message.Id, msg1.Id, starboard.Id);
                }
            }
        }
        else
        {
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithAuthor(newMessage.Author)
                .WithDescription(content)
                .WithTimestamp(newMessage.Timestamp);

            if (imageurl is not null)
                eb.WithImageUrl(imageurl);

            var msg = await starboardChannel.SendMessageAsync(
                strings.StarboardMessage(textChannel.Guild.Id, emote, enumerable.Length, textChannel.Mention),
                embed: eb.Build(),
                components: component);

            await AddStarboardPost(message.Id, msg.Id, starboard.Id);
        }
    }

    private async Task OnMessageDeletedAsync(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (!arg1.HasValue || !arg2.HasValue)
            return;

        var msg = arg1.Value;
        var chan = arg2.Value;
        if (chan is not ITextChannel channel)
            return;

        var permissions = (await channel.Guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(channel);
        if (!permissions.ManageMessages)
            return;

        var posts = starboardPosts.Where(x => x.MessageId == msg.Id);
        foreach (var post in posts)
        {
            var config = starboardConfigs.FirstOrDefault(x => x.Id == post.StarboardConfigId);
            if (config?.RemoveOnDelete != true)
                continue;

            var starboardChannel = await channel.Guild.GetTextChannelAsync(config.StarboardChannelId);
            if (starboardChannel == null)
                continue;

            try
            {
                var starboardMessage = await starboardChannel.GetMessageAsync(post.PostId);
                if (starboardMessage != null)
                    await starboardMessage.DeleteAsync();
            }
            catch
            {
                // ignored
            }

            await RemoveStarboardPost(msg.Id, config.Id);
        }
    }

    private async Task OnAllReactionsClearedAsync(Cacheable<IUserMessage, ulong> arg1,
        Cacheable<IMessageChannel, ulong> arg2)
    {
        if (!arg2.HasValue || arg2.Value is not ITextChannel channel)
            return;

        IUserMessage msg;
        if (!arg1.HasValue)
            msg = await arg1.GetOrDownloadAsync();
        else
            msg = arg1.Value;

        if (msg == null)
            return;

        var posts = starboardPosts.Where(x => x.MessageId == msg.Id);
        foreach (var post in posts)
        {
            var config = starboardConfigs.FirstOrDefault(x => x.Id == post.StarboardConfigId);
            if (config?.RemoveOnReactionsClear != true)
                continue;

            var starboardChannel = await channel.Guild.GetTextChannelAsync(config.StarboardChannelId);
            if (starboardChannel == null)
                continue;

            try
            {
                var starboardMessage = await starboardChannel.GetMessageAsync(post.PostId);
                if (starboardMessage != null)
                    await starboardMessage.DeleteAsync();
            }
            catch
            {
                // ignored
            }

            await RemoveStarboardPost(msg.Id, config.Id);
        }
    }
}

/// <summary>
///     Represents a starboard highlight for dashboard display
/// </summary>
public class StarboardHighlight
{
    /// <summary>
    ///     The original message ID
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     The channel ID where the message was posted
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The number of stars this message has
    /// </summary>
    public int StarCount { get; set; }

    /// <summary>
    ///     The content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    ///     The name of the message author
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    ///     The avatar URL of the message author
    /// </summary>
    public string? AuthorAvatarUrl { get; set; }

    /// <summary>
    ///     The URL of any image attached to the message
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     The star emote used for this starboard
    /// </summary>
    public string StarEmote { get; set; } = "⭐";

    /// <summary>
    ///     When the message was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}