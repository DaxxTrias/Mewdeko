﻿using DataModel;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    /// <summary>
    ///     Provides commands for managing word filters and automatic bans within guilds.
    /// </summary>
    [Group]
    public class FilterCommands(IDataConnectionFactory dbFactory, InteractiveService serv, GuildSettingsService gss)
        : MewdekoSubmodule<FilterService>
    {
        /// <summary>
        ///     Toggles a word on or off the automatic ban list for the current guild.
        /// </summary>
        /// <param name="word">The word to toggle on the auto ban list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     If the word is currently on the list, this command removes it, effectively unblacklisting the word.
        ///     If the word is not on the list, it adds the word, automatically banning any user who uses it.
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .AutoBanWord "example" - Toggles the word "example" on or off the auto ban list.
        /// </example>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task AutoBanWord([Remainder] string word)
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var exists = await db.AutoBanWords
                .AnyAsync(x => x.Word == word && x.GuildId == ctx.Guild.Id);

            if (exists)
            {
                await Service.UnBlacklist(word, ctx.Guild.Id);
                await ctx.Channel.SendConfirmAsync(Strings.AutobanWordRemoved(ctx.Guild.Id, Format.Code(word)))
                    .ConfigureAwait(false);
            }
            else
            {
                await Service.WordBlacklist(word, ctx.Guild.Id);
                await ctx.Channel.SendConfirmAsync(Strings.AutobanWordAdded(ctx.Guild.Id, Format.Code(word)))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Displays a paginated list of all words on the automatic ban list for the current guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     Requires Administrator permission to execute.
        ///     Uses an interactive paginator to navigate through the list of banned words.
        /// </remarks>
        /// <example>
        ///     .AutoBanWordList - Shows the paginated list of auto ban words.
        /// </example>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task AutoBanWordList()
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var words = db.AutoBanWords.Where(x => x.GuildId == ctx.Guild.Id);
            var count = await words.CountAsync();

            if (count == 0)
            {
                await ctx.Channel.SendErrorAsync(Strings.NoAutobanWordsSet(ctx.Guild.Id), Config).ConfigureAwait(false);
            }
            else
            {
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(count / 10)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                    .ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    var wordList = await words
                        .Select(x => x.Word)
                        .Skip(page * 10)
                        .Take(10)
                        .ToListAsync();

                    return new PageBuilder().WithTitle(Strings.AutobanWordsTitle(ctx.Guild.Id))
                        .WithDescription(string.Join("\n", wordList))
                        .WithOkColor();
                }
            }
        }

        /// <summary>
        ///     Enables or disables warnings for filtered words in the current guild.
        /// </summary>
        /// <param name="yesnt">A string indicating whether to enable ("y") or disable ("n") the warning.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .FWarn "y" - Enables warnings for filtered words.
        ///     .FWarn "n" - Disables warnings for filtered words.
        /// </example>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task FWarn(string yesnt)
        {
            await Service.SetFwarn(ctx.Guild, yesnt[..1].ToLower()).ConfigureAwait(false);
            switch (await Service.GetFw(ctx.Guild.Id))
            {
                case 1:
                    await ctx.Channel.SendConfirmAsync(Strings.WarnFilteredWordEnabled(ctx.Guild.Id))
                        .ConfigureAwait(false);
                    break;
                case 0:
                    await ctx.Channel.SendConfirmAsync(Strings.WarnFilteredWordDisabled(ctx.Guild.Id))
                        .ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        ///     Enables or disables warnings for invite links posted in the current guild.
        /// </summary>
        /// <param name="yesnt">A string indicating whether to enable ("y") or disable ("n") the warning.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .InvWarn "y" - Enables warnings for invite links.
        ///     .InvWarn "n" - Disables warnings for invite links.
        /// </example>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task InvWarn(string yesnt)
        {
            await Service.InvWarn(ctx.Guild, yesnt[..1].ToLower()).ConfigureAwait(false);
            switch (await Service.GetInvWarn(ctx.Guild.Id))
            {
                case 1:
                    await ctx.Channel.SendConfirmAsync(Strings.WarnInviteEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
                case 0:
                    await ctx.Channel.SendConfirmAsync(Strings.WarnInviteDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        ///     Clears all filtered words for the current guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     This command removes all words from the filtered words list, effectively disabling word filtering until new words
        ///     are added.
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .FwClear - Clears the filtered words list.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task FwClear()
        {
            await Service.ClearFilteredWords(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.FwCleared(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the server-wide invite link filter on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     When enabled, posting invite links to other Discord servers will be automatically blocked.
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .SrvrFilterInv - Toggles the server-wide invite filter.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SrvrFilterInv()
        {
            var channel = (ITextChannel)ctx.Channel;

            var config = await gss.GetGuildConfig(channel.Guild.Id);
            config.FilterInvites = !config.FilterInvites;
            await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);

            if (config.FilterInvites)
            {
                await ReplyConfirmAsync(Strings.InviteFilterServerOn(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.InviteFilterServerOff(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles the invite link filter for a specific channel on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     This command allows you to enable or disable invite link filtering on a per-channel basis.
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .ChnlFilterInv - Toggles the invite filter for the current channel.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ChnlFilterInv()
        {
            var channel = (ITextChannel)ctx.Channel;
            var guildId = channel.Guild.Id;
            var channelId = channel.Id;

            await using var db = await dbFactory.CreateConnectionAsync();

            // Check if filter exists
            var exists = await db.FilterInvitesChannelIds
                .AnyAsync(fc => fc.GuildId == guildId && fc.ChannelId == channelId);

            if (!exists)
            {
                // Add new filter
                await db.InsertAsync(new FilterInvitesChannelId
                {
                    GuildId = guildId, ChannelId = channelId
                });

                await ReplyConfirmAsync(Strings.InviteFilterChannelOn(guildId)).ConfigureAwait(false);
            }
            else
            {
                // Remove existing filter
                await db.FilterInvitesChannelIds
                    .Where(fc => fc.GuildId == guildId && fc.ChannelId == channelId)
                    .DeleteAsync();

                await ReplyConfirmAsync(Strings.InviteFilterChannelOff(guildId)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles the server-wide link filter on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     When enabled, posting any links will be automatically blocked server-wide.
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .SrvrFilterLin - Toggles the server-wide link filter.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SrvrFilterLin()
        {
            var channel = (ITextChannel)ctx.Channel;

            var config = await gss.GetGuildConfig(channel.Guild.Id);
            config.FilterLinks = !config.FilterLinks;
            await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);

            if (config.FilterLinks)
            {
                await ReplyConfirmAsync(Strings.LinkFilterServerOn(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.LinkFilterServerOff(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles the link filter for a specific channel on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     This command allows you to enable or disable link filtering on a per-channel basis.
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .ChnlFilterLin - Toggles the link filter for the current channel.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ChnlFilterLin()
        {
            var channel = (ITextChannel)ctx.Channel;
            var guildId = channel.Guild.Id;
            var channelId = channel.Id;

            await using var db = await dbFactory.CreateConnectionAsync();

            // Check if filter exists
            var exists = await db.FilterLinksChannelIds
                .AnyAsync(fc => fc.GuildId == guildId && fc.ChannelId == channelId);

            if (!exists)
            {
                // Add new filter
                await db.InsertAsync(new FilterLinksChannelId
                {
                    GuildId = guildId, ChannelId = channelId
                });

                await ReplyConfirmAsync(Strings.LinkFilterChannelOn(guildId)).ConfigureAwait(false);
            }
            else
            {
                // Remove existing filter
                await db.FilterLinksChannelIds
                    .Where(fc => fc.GuildId == guildId && fc.ChannelId == channelId)
                    .DeleteAsync();

                await ReplyConfirmAsync(Strings.LinkFilterChannelOff(guildId)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles the server-wide word filter on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     When enabled, specified words will be automatically blocked server-wide.
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .SrvrFilterWords - Toggles the server-wide word filter.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SrvrFilterWords()
        {
            var channel = (ITextChannel)ctx.Channel;

            var config = await gss.GetGuildConfig(channel.Guild.Id);
            config.FilterWords = !config.FilterWords;
            await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);

            if (config.FilterWords)
            {
                await ReplyConfirmAsync(Strings.WordFilterServerOn(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.WordFilterServerOff(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles the word filter for a specific channel on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     This command allows you to enable or disable word filtering on a per-channel basis.
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .ChnlFilterWords - Toggles the word filter for the current channel.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ChnlFilterWords()
        {
            var channel = (ITextChannel)ctx.Channel;
            var guildId = channel.Guild.Id;
            var channelId = channel.Id;

            await using var db = await dbFactory.CreateConnectionAsync();

            // Check if filter exists
            var exists = await db.FilterWordsChannelIds
                .AnyAsync(fc => fc.GuildId == guildId && fc.ChannelId == channelId);

            if (!exists)
            {
                // Add new filter
                await db.InsertAsync(new FilterWordsChannelId
                {
                    GuildId = guildId, ChannelId = channelId
                });

                await ReplyConfirmAsync(Strings.WordFilterChannelOn(guildId)).ConfigureAwait(false);
            }
            else
            {
                // Remove existing filter
                await db.FilterWordsChannelIds
                    .Where(fc => fc.GuildId == guildId && fc.ChannelId == channelId)
                    .DeleteAsync();

                await ReplyConfirmAsync(Strings.WordFilterChannelOff(guildId)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Adds or removes a word from the filtered words list in the current guild.
        /// </summary>
        /// <param name="word">The word to toggle on the filtered words list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     If the word is currently on the list, this command removes it, effectively unfiltering the word.
        ///     If the word is not on the list, it adds the word to the list.
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .FilterWord "example" - Toggles the word "example" on or off the filtered words list.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task FilterWord([Remainder] string? word)
        {
            var channel = (ITextChannel)ctx.Channel;
            var guildId = channel.Guild.Id;

            word = word?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(word))
                return;

            await using var db = await dbFactory.CreateConnectionAsync();

            // Check if word exists
            var exists = await db.FilteredWords
                .AnyAsync(fw => fw.GuildId == guildId &&
                                fw.Word.Trim().ToLowerInvariant() == word);

            if (!exists)
            {
                // Add new filter word
                await db.InsertAsync(new FilteredWord
                {
                    GuildId = guildId, Word = word
                });

                await ReplyConfirmAsync(Strings.FilterWordAdd(guildId, Format.Code(word))).ConfigureAwait(false);
            }
            else
            {
                // Remove existing filter word
                await db.FilteredWords
                    .Where(fw => fw.GuildId == guildId &&
                                 fw.Word.Trim().ToLowerInvariant() == word)
                    .DeleteAsync();

                await ReplyConfirmAsync(Strings.FilterWordRemove(guildId, Format.Code(word))).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all words currently on the filtered words list for the current guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        ///     Uses an interactive paginator to navigate through the list of filtered words.
        ///     Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        ///     .LstFilterWords - Shows the paginated list of filtered words.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task LstFilterWords()
        {
            var channel = (ITextChannel)ctx.Channel;
            var guildId = channel.Guild.Id;

            await using var db = await dbFactory.CreateConnectionAsync();

            // Get filtered words
            var filteredWords = db.FilteredWords
                .Where(fw => fw.GuildId == guildId)
                .Select(fw => fw.Word);

            var count = await filteredWords.CountAsync();
            var words = await filteredWords.ToArrayAsync();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithTitle(Strings.FilterWordList(guildId))
                    .WithDescription(
                        string.Join("\n", words.Skip(page * 10).Take(10)))
                    .WithOkColor();
            }
        }
    }
}