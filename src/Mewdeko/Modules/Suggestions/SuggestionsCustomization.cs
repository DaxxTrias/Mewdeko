﻿using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;

public partial class Suggestions
{
    /// <summary>
    ///     Defines the color options for suggestion buttons.
    /// </summary>
    public enum ButtonType
    {
        /// <summary>
        ///     Blurple-colored button.
        /// </summary>
        Blurple = 1,

        /// <summary>
        ///     Blue-colored button.
        /// </summary>
        Blue = 1,

        /// <summary>
        ///     Grey-colored button.
        /// </summary>
        Grey = 2,

        /// <summary>
        ///     Grey-colored button.
        /// </summary>
        Gray = 2,

        /// <summary>
        ///     Green-colored button.
        /// </summary>
        Green = 3,

        /// <summary>
        ///     Red-colored button.
        /// </summary>
        Red = 4
    }

    /// <summary>
    ///     Sets the threshold for reposting suggestions.
    /// </summary>
    public enum RepostThreshold
    {
        /// <summary>
        ///     Reposting suggestions is disabled.
        /// </summary>
        Disabled = 0,

        /// <summary>
        ///     Suggestions can be reposted after five denials.
        /// </summary>
        Five = 5,

        /// <summary>
        ///     Suggestions can be reposted after ten denials.
        /// </summary>
        Ten = 10,

        /// <summary>
        ///     Suggestions can be reposted after fifteen denials.
        /// </summary>
        Fifteen = 15
    }

    /// <summary>
    ///     Specifies the mode for emotes used in suggestions.
    /// </summary>
    public enum SuggestEmoteModeEnum
    {
        /// <summary>
        ///     Emotes are used as reactions to suggestions.
        /// </summary>
        Reactions = 0,

        /// <summary>
        ///     Emotes are displayed as buttons for interaction with suggestions.
        /// </summary>
        Buttons = 1
    }

    /// <summary>
    ///     Defines the type of threads used for suggestions.
    /// </summary>
    public enum SuggestThreadType
    {
        /// <summary>
        ///     No thread is used for suggestions.
        /// </summary>
        None = 0,

        /// <summary>
        ///     Public threads are used for suggestions.
        /// </summary>
        Public = 1,

        /// <summary>
        ///     Private threads are used for suggestions.
        /// </summary>
        Private = 2
    }

    /// <summary>
    ///     Customization commands for the Suggestions module.
    /// </summary>
    [Group]
    public class SuggestionsCustomization : MewdekoModuleBase<SuggestionsService>
    {
        /// <summary>
        ///     Sets or updates the suggestion message.
        /// </summary>
        /// <param name="embed">The message or embed code. Use "-" to reset to the default message.</param>
        /// <remarks>
        ///     Allows customization of the suggestion message. Providing "-" will reset the message to its default appearance.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SuggestMessage([Remainder] string embed)
        {
            if (embed == "-")
            {
                await Service.SetSuggestionMessage(ctx.Guild, embed).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(Strings.SuggestionsDefaultLook(ctx.Guild.Id));
                return;
            }

            await Service.SetSuggestionMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.SuggestionMessageUpdated(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets the minimum length for suggestions.
        /// </summary>
        /// <param name="length">The minimum number of characters a suggestion must have.</param>
        /// <remarks>
        ///     Establishes a minimum character count for suggestions to ensure they are sufficiently detailed.
        ///     Requires Administrator permissions. The length cannot be set to more than 2048 characters.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task MinSuggestionLength(int length)
        {
            if (length >= 2048)
            {
                await ctx.Channel.SendErrorAsync(Strings.SuggestionLengthInvalid(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetMinLength(ctx.Guild, length).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.MinLengthSet(ctx.Guild.Id, length)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the mode for suggestion emotes, either as reactions or buttons.
        /// </summary>
        /// <param name="mode">The mode to use for suggestion emotes.</param>
        /// <remarks>
        ///     Configures how users can interact with suggestions, through traditional reactions or interactive buttons.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SuggestMotesMode(SuggestEmoteModeEnum mode)
        {
            await Service.SetEmoteMode(ctx.Guild, (int)mode).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.EmoteModeSet(ctx.Guild.Id, mode)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the color of the suggestion button.
        /// </summary>
        /// <param name="type">The color type for the suggestion button.</param>
        /// <remarks>
        ///     Changes the appearance of the suggestion button to the selected color. Affects all suggestion messages moving
        ///     forward.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SuggestButtonColor(ButtonType type)
        {
            await Service.SetSuggestButtonColor(ctx.Guild, (int)type).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.SuggestButtonColorSet(ctx.Guild.Id, type.ToString()))
                .ConfigureAwait(false);
            await Service.UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the color of specific emote buttons on suggestions.
        /// </summary>
        /// <param name="num">The button number to change.</param>
        /// <param name="type">The color type to apply to the button.</param>
        /// <remarks>
        ///     Allows customization of individual button colors on the suggestion messages, enhancing visual distinction.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SuggestMoteColor(int num, ButtonType type)
        {
            await Service.SetButtonType(ctx.Guild, num, (int)type).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.SuggestButtonTypeSet(ctx.Guild.Id, num, type))
                .ConfigureAwait(false);
            await Service.UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the maximum length for suggestions.
        /// </summary>
        /// <param name="length">The maximum number of characters a suggestion can have.</param>
        /// <remarks>
        ///     Establishes a maximum character count for suggestions to prevent excessively long submissions.
        ///     Requires Administrator permissions. The length must be greater than 0.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task MaxSuggestionLength(int length)
        {
            if (length <= 0)
            {
                await ctx.Channel.SendErrorAsync(Strings.SuggestionLengthInvalid(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetMaxLength(ctx.Guild, length).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.MaxLengthSet(ctx.Guild.Id, length)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets or updates the accept message for suggestions.
        /// </summary>
        /// <param name="embed">The message or embed code. Use "-" to reset to the default message.</param>
        /// <remarks>
        ///     Customizes the message displayed when a suggestion is accepted. Providing "-" will reset the message to its default
        ///     appearance.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AcceptMessage([Remainder] string embed)
        {
            if (embed == "-")
            {
                await Service.SetAcceptMessage(ctx.Guild, embed).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(Strings.AcceptedSuggestionsDefaultLook(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetAcceptMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.AcceptedMessageUpdated(ctx.Guild.Id))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel where accepted suggestions are posted.
        /// </summary>
        /// <param name="channel">The channel for posting accepted suggestions. If null, the feature is disabled.</param>
        /// <remarks>
        ///     Designates a specific channel for showcasing accepted suggestions. Setting the channel to null disables this
        ///     feature.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AcceptChannel(ITextChannel? channel = null)
        {
            await Service.SetAcceptChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
            if (channel is null)
                await ctx.Channel.SendConfirmAsync(Strings.ImplementedSuggestionsDefaultLook(ctx.Guild.Id));
            else
                await ctx.Channel.SendConfirmAsync(Strings.AcceptChannelSet(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel where denied suggestions are posted.
        /// </summary>
        /// <param name="channel">The channel for posting denied suggestions. If null, the feature is disabled.</param>
        /// <remarks>
        ///     Designates a specific channel for showcasing denied suggestions. Setting the channel to null disables this feature.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task DenyChannel(ITextChannel? channel = null)
        {
            await Service.SetDenyChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
            if (channel is null)
                await ctx.Channel.SendConfirmAsync(Strings.DenyChannelDisabled(ctx.Guild.Id));
            else
                await ctx.Channel.SendConfirmAsync(Strings.DenyChannelSet(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel where considered suggestions are posted.
        /// </summary>
        /// <param name="channel">The channel for posting considered suggestions. If null, the feature is disabled.</param>
        /// <remarks>
        ///     Designates a specific channel for showcasing suggestions that are currently being considered. Setting the channel
        ///     to null disables this feature.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ConsiderChannel(ITextChannel? channel = null)
        {
            await Service.SetConsiderChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
            if (channel is null)
                await ctx.Channel.SendConfirmAsync(Strings.ConsiderChannelDisabled(ctx.Guild.Id));
            else
                await ctx.Channel.SendConfirmAsync(Strings.ConsiderChannelSet(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel where implemented suggestions are posted.
        /// </summary>
        /// <param name="channel">The channel for posting implemented suggestions. If null, the feature is disabled.</param>
        /// <remarks>
        ///     Designates a specific channel for showcasing suggestions that have been implemented. Setting the channel to null
        ///     disables this feature.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ImplementChannel(ITextChannel? channel = null)
        {
            await Service.SetImplementChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
            if (channel is null)
                await ctx.Channel.SendConfirmAsync(Strings.ImplementChannelDisabled(ctx.Guild.Id));
            else
                await ctx.Channel.SendConfirmAsync(Strings.ImplementChannelSet(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets or updates the implement message for suggestions.
        /// </summary>
        /// <param name="embed">The message or embed code. Use "-" to reset to the default message.</param>
        /// <remarks>
        ///     Customizes the message displayed when a suggestion is marked as implemented. Providing "-" will reset the message
        ///     to its default appearance.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ImplementMessage([Remainder] string embed)
        {
            if (embed == "-")
            {
                await Service.SetImplementMessage(ctx.Guild, embed).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(Strings.ImplementedSuggestionsDefaultLook(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetImplementMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.ImplementedMessageUpdated(ctx.Guild.Id))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the type of threads used in suggestions.
        /// </summary>
        /// <param name="type">The type of thread to use for new suggestions.</param>
        /// <remarks>
        ///     Allows selection between no threads, public threads, or private threads for suggestion discussions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SuggestThreadsType(SuggestThreadType type)
        {
            await Service.SetSuggestThreadsType(ctx.Guild, (int)type).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.SuggestionThreadsTypeSet(ctx.Guild.Id, type))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the suggestion button channel.
        /// </summary>
        /// <param name="channel">The channel to set as the suggestion button channel.</param>
        /// <remarks>
        ///     Specifies a channel where the suggestion button will be updated or created. Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SuggestButtonChannel(ITextChannel channel)
        {
            await Service.SetSuggestButtonChannel(ctx.Guild, channel.Id).ConfigureAwait(false);
            await Service
                .UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild), true)
                .ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.SuggestButtonChannelSet(ctx.Guild.Id, channel.Mention))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets or updates the suggest button message.
        /// </summary>
        /// <param name="toSet">The message to set for the suggest button. Use "-" to reset to default.</param>
        /// <remarks>
        ///     Customizes the message associated with the suggest button. Providing "-" will reset the message to its default
        ///     appearance.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SuggestButtonMessage([Remainder] string? toSet)
        {
            if (toSet == "-")
            {
                await ctx.Channel.SendConfirmAsync(Strings.SuggestButtonDefault(ctx.Guild.Id))
                    .ConfigureAwait(false);
                await Service.SetSuggestButtonMessage(ctx.Guild, "-").ConfigureAwait(false);
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, toSet).ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(Strings.SuggestButtonCustom(ctx.Guild.Id))
                    .ConfigureAwait(false);
                await Service.SetSuggestButtonMessage(ctx.Guild, toSet).ConfigureAwait(false);
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, toSet).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Sets the label for the suggest button.
        /// </summary>
        /// <param name="toSet">The label text for the suggest button. Use "-" to reset to default.</param>
        /// <remarks>
        ///     Customizes the text label of the suggest button. Providing "-" will reset the label to its default text.
        ///     Requires Administrator permissions. Max length is 80 characters.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SuggestButtonLabel([Remainder] string toSet)
        {
            if (toSet.Length > 80)
            {
                await ctx.Channel.SendErrorAsync(Strings.LabelMaxLength(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (toSet is "-" or "disabled")
            {
                await ctx.Channel.SendConfirmAsync(Strings.SuggestButtonLabelDefault(ctx.Guild.Id))
                    .ConfigureAwait(false);
                await Service.SetSuggestButtonLabel(ctx.Guild, "-").ConfigureAwait(false);
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild))
                    .ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(Strings.SuccessfullySetSuggestButtonLabel(ctx.Guild.Id, toSet))
                    .ConfigureAwait(false);
                await Service.SetSuggestButtonLabel(ctx.Guild, toSet).ConfigureAwait(false);
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Sets the emote for the suggest button.
        /// </summary>
        /// <param name="emote">The emote to use for the suggest button. If null, resets to default.</param>
        /// <remarks>
        ///     Customizes the emote displayed on the suggest button. Providing null will reset the emote to its default.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SuggestButtonEmote(IEmote? emote = null)
        {
            if (emote is null)
            {
                await ctx.Channel.SendConfirmAsync(Strings.SuggestButtonEmoteDefault(ctx.Guild.Id))
                    .ConfigureAwait(false);
                await Service.SetSuggestButtonEmote(ctx.Guild, "-").ConfigureAwait(false);
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild))
                    .ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel
                    .SendConfirmAsync(Strings.SuccessfullySetSuggestButtonLabel(ctx.Guild.Id, emote.ToString()))
                    .ConfigureAwait(false);
                await Service.SetSuggestButtonEmote(ctx.Guild, emote.ToString()).ConfigureAwait(false);
                await Service.UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles the archive setting for suggestions upon denial.
        /// </summary>
        /// <remarks>
        ///     Enables or disables automatic archiving of suggestion threads when a suggestion is denied.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ArchiveOnDeny()
        {
            var current = await Service.GetArchiveOnDeny(ctx.Guild);
            await Service.SetArchiveOnDeny(ctx.Guild, !current).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.ArchiveOnDenySet(ctx.Guild.Id, !current)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the archive setting for suggestions upon acceptance.
        /// </summary>
        /// <remarks>
        ///     Enables or disables automatic archiving of suggestion threads when a suggestion is accepted.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ArchiveOnAccept()
        {
            var current = await Service.GetArchiveOnAccept(ctx.Guild);
            await Service.SetArchiveOnAccept(ctx.Guild, !current).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.ArchiveOnAcceptSet(ctx.Guild.Id, !current))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the archive setting for suggestions upon consideration.
        /// </summary>
        /// <remarks>
        ///     Enables or disables automatic archiving of suggestion threads when a suggestion is considered.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ArchiveOnConsider()
        {
            var current = await Service.GetArchiveOnConsider(ctx.Guild);
            await Service.SetArchiveOnConsider(ctx.Guild, !current).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.ArchiveOnConsiderSet(ctx.Guild.Id, !current))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the archive setting for suggestions upon implementation.
        /// </summary>
        /// <remarks>
        ///     Enables or disables automatic archiving of suggestion threads when a suggestion is implemented.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ArchiveOnImplement()
        {
            var current = await Service.GetArchiveOnImplement(ctx.Guild);
            await Service.SetArchiveOnImplement(ctx.Guild, !current).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.ArchiveOnImplementSet(ctx.Guild.Id, !current))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets or updates the deny message for suggestions.
        /// </summary>
        /// <param name="embed">The message or embed code. Use "-" to reset to the default message.</param>
        /// <remarks>
        ///     Customizes the message displayed when a suggestion is denied. Providing "-" will reset the message to its default
        ///     appearance.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task DenyMessage([Remainder] string embed)
        {
            if (embed == "-")
            {
                await Service.SetDenyMessage(ctx.Guild, embed).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(Strings.DeniedSuggestionsDefaultLook(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetDenyMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.DeniedMessageUpdated(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets or updates the consider message for suggestions.
        /// </summary>
        /// <param name="embed">The message or embed code. Use "-" to reset to the default message.</param>
        /// <remarks>
        ///     Customizes the message displayed when a suggestion is considered. Providing "-" will reset the message to its
        ///     default appearance.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ConsiderMessage([Remainder] string embed)
        {
            if (embed == "-")
            {
                await Service.SetConsiderMessage(ctx.Guild, embed).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(Strings.ConsideredSuggestionsDefaultLook(ctx.Guild.Id));
                return;
            }

            await Service.SetConsiderMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.ConsideredMessageUpdated(ctx.Guild.Id));
        }

        /// <summary>
        ///     Configures the custom emotes used for suggestions.
        /// </summary>
        /// <param name="_">A comma-separated list of emote strings, or "disable" to use default reactions.</param>
        /// <remarks>
        ///     Allows the server to specify custom emotes for reacting to suggestions. Set to "disable" to revert to default
        ///     thumbs up/down reactions.
        ///     Requires Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SuggestMotes([Remainder] string? _ = null)
        {
            if (_ == null)
            {
                await ctx.Channel.SendErrorAsync(Strings.EmotesRequired(ctx.Guild.Id), Config);
                return;
            }

            if (_ != null && _.Contains("disable"))
            {
                await Service.SetSuggestionEmotes(ctx.Guild, "disable").ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(Strings.CustomEmotesDisabled(ctx.Guild.Id));
                return;
            }

            if (_ != null && !_.Contains("disable") &&
                ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value).Count() > 5)
            {
                await ctx.Channel.SendErrorAsync(Strings.MaxEmotesLimit(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (!_.Contains("disable") &&
                !ctx.Message.Tags.Any(t => t.Type == TagType.Emoji))
            {
                await ctx.Channel.SendErrorAsync(Strings.EmotesRequiredSpecific(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var emotes = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (IEmote)x.Value);
            foreach (var emoji in emotes)
            {
                try
                {
                    await ctx.Message.AddReactionAsync(emoji).ConfigureAwait(false);
                }
                catch
                {
                    await ctx.Channel
                        .SendErrorAsync(
                            Strings.UnableAccessEmote(ctx.Guild.Id, emoji.Name),
                            Config)
                        .ConfigureAwait(false);
                    return;
                }
            }

            var list = emotes.Select(emote => emote.ToString()).ToList();
            await Service.SetSuggestionEmotes(ctx.Guild, string.Join(",", list)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.SuggestionEmotesSet(ctx.Guild.Id, string.Join(",", list)))
                .ConfigureAwait(false);
        }
    }
}