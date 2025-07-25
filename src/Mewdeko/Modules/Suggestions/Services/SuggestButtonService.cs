﻿using Discord.Interactions;
using Mewdeko.Common.Modals;

namespace Mewdeko.Modules.Suggestions.Services;

/// <summary>
///     Handles button interactions for suggestions, including voting and starting discussion threads.
/// </summary>
public class SuggestButtonService : MewdekoSlashSubmodule<SuggestionsService>
{
    /// <summary>
    ///     Updates the vote count for a suggestion based on user interaction with emote buttons.
    /// </summary>
    /// <param name="number">The number associated with the emote button, representing a specific suggestion option.</param>
    /// <remarks>
    ///     This method allows users to vote on suggestions using emote buttons. Users can change their vote or remove it
    ///     entirely.
    /// </remarks>
    [ComponentInteraction("emotebutton:*")]
    public async Task UpdateCount(string number)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var componentData = ComponentBuilder.FromMessage(componentInteraction.Message);
        if (!int.TryParse(number, out var emoteNum)) return;
        var changed = false;
        var pickedEmote = await Service.GetPickedEmote(componentInteraction.Message.Id, ctx.User.Id);
        if (pickedEmote == emoteNum)
        {
            if (await PromptUserConfirmAsync(Strings.RemoveVoteConfirm(ctx.Guild.Id), ctx.User.Id, true, false))
            {
                changed = true;
                await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.VoteRemoved(ctx.Guild.Id));
            }
            else
            {
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.VoteNotRemoved(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }
        }

        if (pickedEmote != 0 && pickedEmote != emoteNum)
        {
            if (!await PromptUserConfirmAsync(Strings.ChangeVoteConfirm(ctx.Guild.Id), ctx.User.Id, true, false))
            {
                await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.VoteNotChanged(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.VoteChanged(ctx.Guild.Id));
        }

        await Service.UpdatePickedEmote(componentInteraction.Message.Id, ctx.User.Id, changed ? 0 : emoteNum)
            .ConfigureAwait(false);
        var suggest = await Service.GetSuggestByMessage(componentInteraction.Message.Id);
        var builder = new ComponentBuilder();
        var rows = componentData.ActionRows;
        var buttons = rows.ElementAt(0).Components;
        var count = 0;
        foreach (var i in buttons.Select(x => x as ButtonComponent))
        {
            ++count;
            var splitNum = int.Parse(i.CustomId.Split(":")[1]);
            if (splitNum == emoteNum && !changed)
            {
                await Service.UpdateEmoteCount(componentInteraction.Message.Id, emoteNum).ConfigureAwait(false);
                var label = int.Parse(i.Label);
                builder.WithButton((label + 1).ToString(), $"emotebutton:{emoteNum}",
                    emote: await Service.GetSuggestMote(ctx.Guild, emoteNum),
                    style: await Service.GetButtonStyle(ctx.Guild, emoteNum));
                continue;
            }

            if (splitNum == pickedEmote)
            {
                await Service.UpdateEmoteCount(componentInteraction.Message.Id, splitNum, true).ConfigureAwait(false);
                var label = int.Parse(i.Label);
                builder.WithButton((label - 1).ToString(), $"emotebutton:{splitNum}",
                    emote: await Service.GetSuggestMote(ctx.Guild, splitNum),
                    style: await Service.GetButtonStyle(ctx.Guild, splitNum));
                continue;
            }

            builder.WithButton(i.Label,
                $"emotebutton:{count}", await Service.GetButtonStyle(ctx.Guild, count),
                await Service.GetSuggestMote(ctx.Guild, count));
        }

        if (await Service.GetThreadType(ctx.Guild) == 1)
        {
            builder.WithButton("Join/Create Public Discussion", $"publicsuggestthread:{suggest.SuggestionId}",
                ButtonStyle.Secondary, row: 1);
        }

        if (await Service.GetThreadType(ctx.Guild) == 2)
        {
            builder.WithButton("Join/Create Private Discussion",
                $"privatesuggestthread:{suggest.SuggestionId}", ButtonStyle.Secondary, row: 1);
        }

        await componentInteraction.Message.ModifyAsync(x => x.Components = builder.Build()).ConfigureAwait(false);
    }


    /// <summary>
    ///     Starts or joins a public discussion thread for a suggestion.
    /// </summary>
    /// <param name="suggestnum">The unique identifier of the suggestion for which to start or join the discussion thread.</param>
    /// <remarks>
    ///     This method checks if a public discussion thread already exists for the suggestion and either joins it or creates a
    ///     new one.
    /// </remarks>
    [ComponentInteraction("publicsuggestthread:*")]
    public async Task PublicThreadStartOrJoin(string suggestnum)
    {
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var suggest = await Service.GetSuggestByMessage(componentInteraction.Message.Id);
        if (await Service.GetThreadType(ctx.Guild) is 0 or 2)
            return;
        var channel = await ctx.Guild.GetTextChannelAsync(await Service.GetSuggestionChannel(ctx.Guild.Id))
            .ConfigureAwait(false);
        if (await Service.GetThreadByMessage(suggest.MessageId) is 0)
        {
            var threadChannel = await channel.CreateThreadAsync($"Suggestion #{suggestnum} Discussion",
                    message: componentInteraction.Message)
                .ConfigureAwait(false);
            var user = await ctx.Guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
            if (user is not null)
                await threadChannel.AddUserAsync(user).ConfigureAwait(false);
            await threadChannel.AddUserAsync(ctx.User as IGuildUser).ConfigureAwait(false);
            await Service.AddThreadChannel(componentInteraction.Message.Id, threadChannel.Id).ConfigureAwait(false);
            await ctx.Interaction.SendEphemeralConfirmAsync($"{threadChannel.Mention} has been created!")
                .ConfigureAwait(false);
            return;
        }

        var thread = await ctx.Guild.GetThreadChannelAsync(await Service.GetThreadByMessage(suggest.MessageId))
            .ConfigureAwait(false);
        await ctx.Interaction.SendEphemeralErrorAsync($"There is already a thread open. {thread.Mention}", Config)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Starts or joins a private discussion thread for a suggestion.
    /// </summary>
    /// <param name="suggestnum">The unique identifier of the suggestion for which to start or join the discussion thread.</param>
    /// <remarks>
    ///     Similar to <see cref="PublicThreadStartOrJoin" />, but for private threads. It ensures that discussion threads are
    ///     accessible only to relevant members.
    /// </remarks>
    [ComponentInteraction("privatesuggestthread:*")]
    public async Task PrivateThreadStartOrJoin(string suggestnum)
    {
        var componentInteraction = ctx.Interaction as IComponentInteraction;
        var suggest = await Service.GetSuggestByMessage(componentInteraction.Message.Id);
        if (await Service.GetThreadType(ctx.Guild) is 0 or 1)
            return;
        var channel = await ctx.Guild.GetTextChannelAsync(await Service.GetSuggestionChannel(ctx.Guild.Id))
            .ConfigureAwait(false);
        if (await Service.GetThreadByMessage(suggest.MessageId) is 0)
        {
            var threadChannel = await channel.CreateThreadAsync($"Suggestion #{suggestnum} Discussion",
                    ThreadType.PrivateThread, message: componentInteraction.Message)
                .ConfigureAwait(false);
            var user = await ctx.Guild.GetUserAsync(suggest.UserId).ConfigureAwait(false);
            if (user is not null)
                await threadChannel.AddUserAsync(user).ConfigureAwait(false);
            await threadChannel.AddUserAsync(ctx.User as IGuildUser).ConfigureAwait(false);
            await Service.AddThreadChannel(componentInteraction.Message.Id, threadChannel.Id).ConfigureAwait(false);
            await ctx.Interaction.SendEphemeralConfirmAsync($"{threadChannel.Mention} has been created!")
                .ConfigureAwait(false);
            return;
        }

        var thread = await ctx.Guild.GetThreadChannelAsync(await Service.GetThreadByMessage(suggest.MessageId))
            .ConfigureAwait(false);
        await ctx.Interaction.SendEphemeralErrorAsync($"There is already a thread open. {thread.Mention}", Config)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Initiates the process for a user to send a suggestion through a modal interaction.
    /// </summary>
    /// <remarks>
    ///     This method prompts the user with a modal to input their suggestion, which is then processed accordingly.
    /// </remarks>
    [ComponentInteraction("suggestbutton")]
    public Task SendSuggestModal()
    {
        return ctx.Interaction.RespondWithModalAsync<SuggestionModal>("suggest.sendsuggestion",
            null,
            x => x.UpdateTextInput("suggestion", async s
                => s.WithMaxLength(Math.Min(4000, await Service.GetMaxLength(ctx.Guild.Id)))
                    .WithMinLength(Math.Min(await Service.GetMinLength(ctx.Guild.Id), 4000))));
    }
}