﻿using System.Globalization;
using System.Text.RegularExpressions;
using Discord.Net;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services.strings;
using Mewdeko.Services.Strings;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Permissions.Services;

/// <summary>
///     Provides services for filtering messages in guilds based on predefined rules, including word filters, link filters,
///     and invite filters.
/// </summary>
public class FilterService : IEarlyBehavior, INService
{
    private readonly AdministrationService ass;

    private readonly DiscordShardedClient client;
    private readonly BotConfig config;
    private readonly CultureInfo? cultureInfo = new("en-US");
    private readonly DbContextProvider dbProvider;
    private readonly GuildSettingsService gss;
    private readonly UserPunishService userPunServ;

    /// <summary>
    ///     Initializes a new instance of the FilterService with necessary dependencies for filtering operations.
    /// </summary>
    /// <remarks>
    ///     On initialization, this service loads filtering configurations from the database and subscribes to necessary events
    ///     for real-time monitoring and filtering of messages across all guilds the bot is part of.
    /// </remarks>
    public FilterService(DiscordShardedClient client, DbContextProvider dbProvider, IPubSub pubSub,
        UserPunishService upun2, GeneratedBotStrings strng, AdministrationService ass,
        GuildSettingsService gss, EventHandler eventHandler, BotConfig config)
    {
        this.dbProvider = dbProvider;
        this.client = client;
        userPunServ = upun2;
        Strings = strng;
        this.ass = ass;
        this.gss = gss;
        this.config = config;

        eventHandler.MessageUpdated += (_, newMsg, channel) =>
        {
            var guild = (channel as ITextChannel)?.Guild;

            if (guild == null || newMsg is not IUserMessage usrMsg)
                return Task.CompletedTask;

            return RunBehavior(null, guild, usrMsg);
        };
    }

    /// <summary>
    ///     Stores localized strings for bot messages.
    /// </summary>
    private GeneratedBotStrings Strings { get; }


    /// <summary>
    ///     Specifies the execution priority of this behavior in the pipeline.
    /// </summary>
    public int Priority
    {
        get
        {
            return -50;
        }
    }

    /// <summary>
    ///     Indicates the type of behavior this service represents.
    /// </summary>
    public ModuleBehaviorType BehaviorType
    {
        get
        {
            return ModuleBehaviorType.Blocker;
        }
    }

    /// <summary>
    ///     Orchestrates various filters, applying them to messages based on guild-specific configurations and global blacklist
    ///     settings.
    /// </summary>
    /// <param name="socketClient">The Discord client for interacting with the API.</param>
    /// <param name="guild">The guild where the message was posted.</param>
    /// <param name="msg">The user message to be checked against the filters.</param>
    /// <returns>A task that resolves to true if the message was acted upon due to a filter match; otherwise, false.</returns>
    public async Task<bool> RunBehavior(DiscordShardedClient socketClient, IGuild guild, IUserMessage msg)
    {
        return msg.Author is IGuildUser gu && !gu.RoleIds.Contains(await ass.GetStaffRole(guild.Id)) &&
               !gu.GuildPermissions.Administrator && (await FilterInvites(guild, msg).ConfigureAwait(false)
                                                      || await FilterWords(guild, msg).ConfigureAwait(false)
                                                      || await FilterLinks(guild, msg).ConfigureAwait(false)
                                                      || await FilterBannedWords(guild, msg).ConfigureAwait(false));
    }


    /// <summary>
    ///     Adds a word to the blacklist for a specified guild.
    /// </summary>
    /// <param name="id">The word to add to the blacklist.</param>
    /// <param name="id2">The ID of the guild for which the word is blacklisted.</param>
    public async Task WordBlacklist(string id, ulong id2)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var item = new AutoBanEntry
        {
            Word = id, GuildId = id2
        };
        dbContext.AutoBanWords.Add(item);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    ///     Removes a word from the blacklist for a specified guild.
    /// </summary>
    /// <param name="id">The word to remove from the blacklist.</param>
    /// <param name="id2">The ID of the guild from which the word is removed.</param>
    public async Task UnBlacklist(string id, ulong id2)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var toRemove = dbContext.AutoBanWords
            .FirstOrDefault(bi => bi.Word == id && bi.GuildId == id2);

        if (toRemove is not null)
            dbContext.AutoBanWords.Remove(toRemove);

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    ///     Retrieves the set of filtered words for a specific channel within a guild.
    /// </summary>
    /// <param name="channelId">The ID of the channel.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A set of filtered words for the channel, or null if no filters are set.</returns>
    public async Task<bool> FilterChannel(ulong channelId, ulong guildId)
    {
        var config = await gss.GetGuildConfig(guildId);
        return config.FilterWordsChannelIds.Any(x => x.ChannelId == channelId);
    }

    /// <summary>
    ///     Retrieves the number of warnings a guild has set for invite violations.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The number of warnings set for invite violations in the guild.</returns>
    public async Task<int> GetInvWarn(ulong id)
    {
        return (await gss.GetGuildConfig(id)).invwarn;
    }

    /// <summary>
    ///     Sets the number of warnings for invite violations in a guild.
    /// </summary>
    /// <param name="guild">The guild for which to set the warning count.</param>
    /// <param name="yesnt">The warning count to set.</param>
    public async Task InvWarn(IGuild guild, string yesnt)
    {
        var yesno = -1;
        yesno = yesnt switch
        {
            "y" => 1,
            "n" => 0,
            _ => yesno
        };


        await using var dbContext = await dbProvider.GetContextAsync();
        {
            await using var db = await dbProvider.GetContextAsync();
            var gc = await db.ForGuildId(guild.Id, set => set);
            gc.invwarn = yesno;
            await gss.UpdateGuildConfig(guild.Id, gc);
        }
    }

    /// <summary>
    ///     Retrieves the number of warnings a guild has set for filtered word violations.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The number of warnings set for filtered word violations in the guild.</returns>
    public async Task<int> GetFw(ulong id)
    {
        return (await gss.GetGuildConfig(id)).fwarn;
    }

    /// <summary>
    ///     Sets the number of warnings for filtered word violations in a guild.
    /// </summary>
    /// <param name="guild">The guild for which to set the warning count.</param>
    /// <param name="yesnt">The warning count to set.</param>
    public async Task SetFwarn(IGuild guild, string yesnt)
    {
        var yesno = -1;
        yesno = yesnt switch
        {
            "y" => 1,
            "n" => 0,
            _ => yesno
        };


        await using var dbContext = await dbProvider.GetContextAsync();
        {
            await using var db = await dbProvider.GetContextAsync();
            var gc = await db.ForGuildId(guild.Id, set => set);
            gc.fwarn = yesno;
            await gss.UpdateGuildConfig(guild.Id, gc);
        }
    }

    /// <summary>
    ///     Clears all filtered words for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which to clear filtered words.</param>
    public async Task ClearFilteredWords(ulong guildId)
    {
        await using var db = await dbProvider.GetContextAsync();
        var gc = await db.ForGuildId(guildId,
            set => set.Include(x => x.FilteredWords)
                .Include(x => x.FilterWordsChannelIds));

        gc.FilterWords = false;
        gc.FilteredWords.Clear();
        gc.FilterWordsChannelIds.Clear();

        await gss.UpdateGuildConfig(guildId, gc);
    }

    /// <summary>
    ///     Retrieves the set of filtered words for an entire server.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A set of filtered words for the server, or null if no filters are set.</returns>
    public async Task<HashSet<string>?> FilteredWordsForServer(ulong guildId)
    {
        var config = await gss.GetGuildConfig(guildId);
        return config.FilteredWords.Select(x => x.Word).ToHashSet();
    }


    /// <summary>
    ///     Filters messages containing banned words and takes appropriate action.
    /// </summary>
    /// <param name="guild">The guild in which the message was posted.</param>
    /// <param name="msg">The message to check for banned words.</param>
    /// <returns>True if the message contained banned words and was acted upon; otherwise, false.</returns>
    private async Task<bool> FilterBannedWords(IGuild? guild, IUserMessage? msg)
    {
        if (guild is null)
            return false;
        if (msg is null)
            return false;
        await using var dbContext = await dbProvider.GetContextAsync();

        var blacklist = dbContext.AutoBanWords.ToLinqToDB().Where(x => x.GuildId == guild.Id);
        foreach (var i in blacklist)
        {
            Regex regex;
            try
            {
                regex = new Regex(i.Word, RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
            }
            catch
            {
                Log.Error("Invalid regex, removing.: {IWord}", i.Word);

                dbContext.AutoBanWords.Remove(i);
                await dbContext.SaveChangesAsync();
                return false;
            }

            var match = regex.Match(msg.Content.ToLower()).Value;
            if (!regex.IsMatch(msg.Content.ToLower())) continue;
            try
            {
                await msg.DeleteAsync().ConfigureAwait(false);
                var defaultMessage = Strings.BanDm(guild.Id, Format.Bold(guild.Name), Strings.AutobanWordDetected(guild.Id, i));
                var embed = await userPunServ.GetBanUserDmEmbed(client, guild as SocketGuild,
                    await guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false), msg.Author as IGuildUser,
                    defaultMessage,
                    $"Banned for saying autoban word {match}", null).ConfigureAwait(false);
                await (await msg.Author.CreateDMChannelAsync().ConfigureAwait(false)).SendMessageAsync(embed.Item2,
                        embeds: embed.Item1, components: embed.Item3.Build())
                    .ConfigureAwait(false);
                await guild.AddBanAsync(msg.Author,options: new RequestOptions {
                    AuditLogReason = Strings.AutobanReason(guild.Id, match)
                }).ConfigureAwait(false);
                return true;
            }
            catch
            {
                try
                {
                    await guild.AddBanAsync(msg.Author, 1, options: new RequestOptions
                    {
                        AuditLogReason = $"AutoBan word detected: {match}"
                    }).ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    Log.Error(Strings.AutobanError(guild.Id, msg.Channel.Name));
                    return false;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Filters messages containing specified words and takes appropriate action.
    /// </summary>
    /// <param name="guild">The guild in which the message was posted.</param>
    /// <param name="usrMsg">The message to check for specified words.</param>
    /// <returns>True if the message contained specified words and was acted upon; otherwise, false.</returns>
    public async Task<bool> FilterWords(IGuild? guild, IUserMessage? usrMsg)
    {
        if (guild is null || usrMsg?.Author is null)
            return false;

        var channelId = usrMsg.Channel.Id;
        var guildId = guild.Id;

        if (!await ShouldFilterChannel(channelId, guildId))
            return false;

        var filteredWords = await GetFilteredWordsForServer(guildId);
        if (filteredWords.Count == 0)
            return false;

        var lowerContent = usrMsg.Content.ToLower();
        foreach (var word in filteredWords)
        {
            if (await IsWordMatched(word, lowerContent, guildId))
            {
                return await HandleFilteredWord(usrMsg, guild, word);
            }
        }

        return false;
    }

    private async Task<bool> ShouldFilterChannel(ulong channelId, ulong guildId)
    {
        var config = await gss.GetGuildConfig(guildId);
        return config.FilterWordsChannelIds.Any(x => x.ChannelId == channelId);
    }

    private async Task<HashSet<string>> GetFilteredWordsForServer(ulong guildId)
    {
        var config = await gss.GetGuildConfig(guildId);
        return config.FilteredWords.Select(x => x.Word).ToHashSet();
    }

    private async Task<bool> IsWordMatched(string word, string content, ulong guildId)
    {
        try
        {
            var regex = new Regex(word, RegexOptions.Compiled | RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(250));
            return regex.IsMatch(content);
        }
        catch (ArgumentException)
        {
            await RemoveInvalidRegex(word, guildId);
            return false;
        }
    }

    private async Task RemoveInvalidRegex(string word, ulong guildId)
    {
        Log.Error("Invalid regex, removing: {Word}", word);
        await using var dbContext = await dbProvider.GetContextAsync();
        var config = await dbContext.ForGuildId(guildId, set => set.Include(gc => gc.FilteredWords));
        var removed = config.FilteredWords.FirstOrDefault(fw =>
            fw.Word.Trim().Equals(word, StringComparison.InvariantCultureIgnoreCase));
        if (removed != null)
        {
            dbContext.Remove(removed);
            await gss.UpdateGuildConfig(guildId, config);
        }
    }

    private async Task<bool> HandleFilteredWord(IUserMessage usrMsg, IGuild guild, string word)
    {
        try
        {
            await usrMsg.DeleteAsync();
            if (await GetFw(guild.Id) == 0) return true;
            await userPunServ.Warn(guild, usrMsg.Author.Id, client.CurrentUser, "Warned for Filtered Word");
            var user = await usrMsg.Author.CreateDMChannelAsync();
            await user.SendErrorAsync(Strings.FilteredWordWarning(guild.Id, Format.Code(word)), config);
            return true;
        }
        catch (HttpException ex)
        {
            Log.Warning(ex, Strings.FilterError(guild.Id, usrMsg.Channel.Id));
            return false;
        }
    }

    /// <summary>
    ///     Filters messages containing invites and takes appropriate action.
    /// </summary>
    /// <param name="guild">The guild in which the message was posted.</param>
    /// <param name="usrMsg">The message to check for invites.</param>
    /// <returns>True if the message contained invites and was acted upon; otherwise, false.</returns>
    private async Task<bool> FilterInvites(IGuild? guild, IUserMessage? usrMsg)
    {
        if (guild is null || usrMsg?.Author is null)
            return false;

        var servConfig = await gss.GetGuildConfig(guild.Id);

        var shouldFilter = servConfig.FilterInvites &&
                           (servConfig.FilterInvitesChannelIds.Count == 0 || servConfig.FilterInvitesChannelIds
                               .Select(x => x.ChannelId).Contains(usrMsg.Channel.Id)) &&
                           usrMsg.Content.IsDiscordInvite();

        if (!shouldFilter)
            return false;

        try
        {
            await usrMsg.DeleteAsync();

            if (await GetInvWarn(guild.Id) == 0) return true;
            await userPunServ.Warn(guild, usrMsg.Author.Id, client.CurrentUser, Strings.InviteWarnReason(guild.Id));

            var userDmChannel = await usrMsg.Author.CreateDMChannelAsync();
            await userDmChannel.SendErrorAsync(Strings.InviteWarning(guild.Id), config);

            return true;
        }
        catch (HttpException ex)
        {
            Log.Warning(ex, Strings.InviteFilterError(guild.Id, usrMsg.Channel.Id));
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, Strings.InviteFilterUnexpected(guild.Id, usrMsg.Channel.Id));
            return false;
        }
    }

    /// <summary>
    ///     Filters links from messages based on guild configuration.
    /// </summary>
    /// <param name="guild">The guild where the message was sent.</param>
    /// <param name="usrMsg">The user message to check for links.</param>
    /// <returns>True if a link was filtered, false otherwise.</returns>
    private async Task<bool> FilterLinks(IGuild? guild, IUserMessage? usrMsg)
    {
        if (guild is null || usrMsg is null)
            return false;

        var servConfig = await gss.GetGuildConfig(guild.Id);

        var shouldFilter = (servConfig.FilterLinksChannelIds.Any(x => x.ChannelId == usrMsg.Channel.Id) ||
                            servConfig.FilterLinks)
                           && usrMsg.Content.TryGetUrlPath(out _);

        if (!shouldFilter)
            return false;

        try
        {
            await usrMsg.DeleteAsync();
            return true;
        }
        catch (HttpException ex)
        {
            Log.Warning(ex, Strings.LinkFilterError(guild.Id, usrMsg.Channel.Id));
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, Strings.LinkFilterUnexpected(guild.Id, usrMsg.Channel.Id));
            return false;
        }
    }
}