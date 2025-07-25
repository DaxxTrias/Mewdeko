﻿using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Highlights.Services;

namespace Mewdeko.Modules.Highlights;

/// <summary>
///     Module for managing highlights.
/// </summary>
/// <param name="interactivity">The embed pagination service</param>
/// <param name="svcs">The service provider</param>
/// <param name="dbFactory">The db context provider</param>
public class Highlights(InteractiveService interactivity, IServiceProvider svcs, IDataConnectionFactory dbFactory)
    : MewdekoModuleBase<HighlightsService>
{
    /// <summary>
    ///     The actions available for the highlight command.
    /// </summary>
    public enum HighlightActions
    {
        /// <summary>
        ///     Adds a highlight.
        /// </summary>
        Add,

        /// <summary>
        ///     Lists current highlights
        /// </summary>
        List,

        /// <summary>
        ///     Deletes a highlight.
        /// </summary>
        Delete,

        /// <summary>
        ///     Removes a highlight.
        /// </summary>
        Remove,

        /// <summary>
        ///     Attempts to match a highlight to a phrase
        /// </summary>
        Match,

        /// <summary>
        ///     Toggles whether highlights ignore a user or channel
        /// </summary>
        ToggleIgnore,

        /// <summary>
        ///     Toggles whether highlights are enabled
        /// </summary>
        Toggle
    }

    /// <summary>
    ///     Adds, lists, removes, or matches highlights.
    /// </summary>
    /// <param name="action">
    ///     <see cref="HighlightActions" />
    /// </param>
    /// <param name="words">Parameters for the selected action</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Highlight(HighlightActions action, [Remainder] string words = null)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var highlights = await (dbContext.Highlights.Where(x => x.GuildId == ctx.Guild.Id && x.UserId == ctx.User.Id))
            .ToListAsync();
        switch (action)
        {
            case HighlightActions.Add:
                if (string.IsNullOrWhiteSpace(words))
                    return;
                if (highlights.Count > 0 && highlights.Any(x => x.UserId == ctx.User.Id))
                {
                    if (highlights.Select(x => x.Word.ToLower()).Contains(words.ToLower()))
                    {
                        await ctx.Channel.SendErrorAsync(Strings.HighlightAlreadyExists(ctx.Guild.Id), Config)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await Service.AddHighlight(ctx.Guild.Id, ctx.User.Id, words).ConfigureAwait(false);
                        await ctx.Channel.SendConfirmAsync(Strings.HighlightAdded(ctx.Guild.Id, Format.Code(words)))
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    await Service.AddHighlight(ctx.Guild.Id, ctx.User.Id, words).ConfigureAwait(false);
                    await ctx.Channel.SendConfirmAsync(Strings.HighlightAdded(ctx.Guild.Id, Format.Code(words)))
                        .ConfigureAwait(false);
                }

                break;
            case HighlightActions.List:
                var highlightsForUser = highlights.Where(x => x.UserId == ctx.User.Id).ToList();
                if (highlightsForUser.Count == 0)
                {
                    await ctx.Channel.SendErrorAsync(Strings.HighlightNoHighlights(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(highlightsForUser.Count() / 10)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(paginator, Context.Channel,
                    TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    var highlightsEnumerable = highlightsForUser.Skip(page * 10).Take(10);
                    return new PageBuilder().WithOkColor()
                        .WithTitle(Strings.HighlightListTitle(ctx.Guild.Id, highlightsForUser.Count))
                        .WithDescription(string.Join("\n",
                            highlightsEnumerable.Select(x => $"{highlightsForUser.IndexOf(x) + 1}. {x.Word}")));
                }

                break;
            case HighlightActions.Remove:
            case HighlightActions.Delete:
                if (string.IsNullOrWhiteSpace(words))
                    return;
                highlightsForUser = highlights.Where(x => x.UserId == ctx.User.Id).ToList();
                if (highlightsForUser.Count == 0)
                {
                    await ctx.Channel.SendErrorAsync(Strings.HighlightCannotDelete(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (int.TryParse(words, out var number))
                {
                    var todelete = highlightsForUser.ElementAt(number - 1);
                    if (todelete is null)
                    {
                        await ctx.Channel.SendErrorAsync(Strings.HighlightNotExist(ctx.Guild.Id), Config)
                            .ConfigureAwait(false);
                        return;
                    }

                    await Service.RemoveHighlight(todelete).ConfigureAwait(false);
                    await ctx.Channel
                        .SendConfirmAsync(Strings.HighlightRemoved(ctx.Guild.Id, Format.Code(todelete.Word)))
                        .ConfigureAwait(false);
                    return;
                }

                if (!highlightsForUser.Select(x => x.Word).Contains(words))
                {
                    await ctx.Channel.SendErrorAsync(Strings.HighlightNotFound(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                await Service.RemoveHighlight(highlightsForUser.Find(x => x.Word == words)).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(Strings.HighlightRemoved(ctx.Guild.Id, Format.Code(words)))
                    .ConfigureAwait(false);
                break;
            case HighlightActions.Match:
                if (string.IsNullOrWhiteSpace(words))
                    return;
                highlightsForUser = highlights.Where(x => x.UserId == ctx.User.Id).ToList();
                if (highlightsForUser.Count == 0)
                {
                    await ctx.Channel.SendErrorAsync(Strings.HighlightNoMatches(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                var matched = highlightsForUser.Where(x => words.ToLower().Contains(x.Word.ToLower()));
                if (!matched.Any())
                {
                    await ctx.Channel.SendErrorAsync(Strings.HighlightNoMatchFound(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory1)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(matched.Count() / 10)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(paginator, Context.Channel,
                    TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory1(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    var highlightsEnumerable = matched.Skip(page * 10).Take(10);
                    return new PageBuilder().WithOkColor()
                        .WithTitle(Strings.HighlightListTitle(ctx.Guild.Id, highlightsForUser.Count))
                        .WithDescription(string.Join("\n",
                            highlightsEnumerable.Select(x => $"{highlightsForUser.IndexOf(x) + 1}. {x.Word}")));
                }

                break;

            case HighlightActions.ToggleIgnore:
                if (string.IsNullOrWhiteSpace(words))
                    return;
                var reader1 = new ChannelTypeReader<ITextChannel>();
                var result = await reader1.ReadAsync(ctx, words, svcs).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    var reader2 = new UserTypeReader<IUser>();
                    var result1 = await reader2.ReadAsync(ctx, words, null).ConfigureAwait(false);
                    var host = (IUser)result1.BestMatch;
                    if (host.Username is null)
                    {
                        await ctx.Channel.SendErrorAsync(Strings.HighlightUserChannelNotFound(ctx.Guild.Id), Config)
                            .ConfigureAwait(false);
                        return;
                    }

                    if (await Service.ToggleIgnoredUser(ctx.Guild.Id, ctx.User.Id, host.Id.ToString())
                            .ConfigureAwait(false))
                    {
                        await ctx.Channel
                            .SendConfirmAsync(Strings.HighlightIgnoredUserAdded(ctx.Guild.Id, host.Mention))
                            .ConfigureAwait(false);
                        return;
                    }

                    await ctx.Channel.SendConfirmAsync(Strings.HighlightIgnoredUserRemoved(ctx.Guild.Id, host.Mention))
                        .ConfigureAwait(false);

                    return;
                }

                var channel = (ITextChannel)result.BestMatch;

                if (await Service.ToggleIgnoredChannel(ctx.Guild.Id, ctx.User.Id, channel.Id.ToString())
                        .ConfigureAwait(false))
                {
                    await ctx.Channel
                        .SendConfirmAsync(Strings.HighlightIgnoredChannelAdded(ctx.Guild.Id, channel.Mention))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel
                        .SendConfirmAsync(Strings.HighlightIgnoredChannelRemoved(ctx.Guild.Id, channel.Mention))
                        .ConfigureAwait(false);
                }

                break;

            case HighlightActions.Toggle:
                if (string.IsNullOrWhiteSpace(words))
                    return;
                if (!bool.TryParse(words, out var enabled))
                {
                    await ctx.Channel.SendErrorAsync(Strings.HighlightToggleInvalid(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                await Service.ToggleHighlights(ctx.Guild.Id, ctx.User.Id, enabled).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(enabled
                        ? Strings.HighlightEnabled(ctx.Guild.Id)
                        : Strings.HighlightDisabled(ctx.Guild.Id))
                    .ConfigureAwait(false);
                break;
        }
    }
}