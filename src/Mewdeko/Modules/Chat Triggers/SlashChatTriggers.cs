using System.Net.Http;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Configs;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Chat_Triggers.Common;
using Mewdeko.Modules.Chat_Triggers.Services;

namespace Mewdeko.Modules.Chat_Triggers;

/// <summary>
///     Slash Module for managing chat triggers.
/// </summary>
[Group("triggers", "Manage chat triggers.")]
// [RequireUserPermission(GuildPermission.Administrator)] coming soon???
public class SlashChatTriggers(IHttpClientFactory clientFactory, InteractiveService interactivity, BotConfig config)
    : MewdekoSlashModuleBase<ChatTriggersService>
{
    /// <summary>
    ///     Handles triggering run-in interactions.
    /// </summary>
    /// <param name="triggerId">The ID of the trigger.</param>
    /// <param name="guildId">The ID of the guild where the trigger is executed.</param>
    /// <param name="_">Placeholder parameter.</param>
    [ComponentInteraction("trigger.*.runin.*$*", true)]
    [CheckPermissions]
    public async Task TriggerRunInHandler(int triggerId, ulong? guildId, string _)
    {
        guildId ??= 0;
        var ct = await Service.GetChatTriggers(guildId, triggerId);
        await Service.RunInteractionTrigger(ctx.Interaction as SocketInteraction, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Exports Chat Triggers into a .yml file.
    /// </summary>
    /// <example>/triggers export</example>
    [SlashCommand("export", "Exports Chat Triggers into a .yml file.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task CtsExport()
    {
        await DeferAsync().ConfigureAwait(false);

        var serialized = await Service.ExportCrs(ctx.Guild?.Id);
        var stream = await serialized.ToStream().ConfigureAwait(false);
        await using var _ = stream.ConfigureAwait(false);
        await FollowupWithFileAsync(stream, "cts-export.yml").ConfigureAwait(false);

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Imports Chat Triggers from a .yml file.
    /// </summary>
    /// <param name="file">The yml file to import.</param>
    /// <example>/triggers import</example>
    [SlashCommand("import", "Imports Chat Triggers from a .yml file.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task CtsImport(IAttachment file)
    {
        await DeferAsync().ConfigureAwait(false);

        using var client = clientFactory.CreateClient();
        var content = await client.GetStringAsync(file.Url).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(content))
        {
            await FollowupAsync(Strings.ExprImportNoInput(ctx.Guild.Id)).ConfigureAwait(false);
            await FollowupWithTriggerStatus().ConfigureAwait(false);
            return;
        }

        var succ = await Service.ImportCrsAsync(ctx.User as IGuildUser, content).ConfigureAwait(false);
        if (!succ)
        {
            await FollowupAsync(Strings.ExprImportInvalidData(ctx.Guild.Id)).ConfigureAwait(false);
            await FollowupWithTriggerStatus().ConfigureAwait(false);
            return;
        }

        await FollowupAsync(Strings.ExprImportSuccess(ctx.Guild.Id)).ConfigureAwait(false);
        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }


    /// <summary>
    ///     Adds a new chat trigger.
    /// </summary>
    /// <param name="regex">Indicates whether the trigger should use regex.</param>
    /// <example>/triggers add</example>
    [SlashCommand("add", "Add new chat trigger.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public Task AddChatTrigger([Summary("regex", "Should the trigger use regex.")] bool regex = false)
    {
        return RespondWithModalAsync<ChatTriggerModal>($"chat_trigger_add:{regex}");
    }

    /// <summary>
    ///     Handles the modal interaction for adding a chat trigger.
    /// </summary>
    /// <param name="sRgx">The string representation of the regex.</param>
    /// <param name="modal">The modal containing trigger details.</param>
    [ModalInteraction("chat_trigger_add:*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task AddChatTriggerModal(bool sRgx, ChatTriggerModal modal)
    {
        if (string.IsNullOrWhiteSpace(modal.Trigger) || string.IsNullOrWhiteSpace(modal.Message))
        {
            await RespondAsync(Strings.TriggerAddInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            await FollowupWithTriggerStatus().ConfigureAwait(false);
            return;
        }

        var ct = await Service.AddAsync(ctx.Guild?.Id, modal.Trigger, modal.Message, sRgx).ConfigureAwait(false);

        await RespondAsync(embed: Service.GetEmbed(ct, ctx.Guild?.Id).Build()).ConfigureAwait(false);
        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Edits a chat trigger.
    /// </summary>
    /// <param name="id">The ID of the chat trigger to edit.</param>
    /// <param name="regex">Indicates whether the trigger should use regex.</param>
    /// <example>/triggers edit 9987 true/false</example>
    [SlashCommand("edit", "Edit a chat trigger.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task EditChatTrigger
    (
        [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
        int id,
        [Summary("regex", "Should the trigger use regex.")]
        bool regex = false
    )
    {
        var trigger = await Service.GetChatTriggers(ctx.Guild?.Id, id);
        await ctx.Interaction.RespondWithModalAsync<ChatTriggerModal>($"chat_trigger_edit:{id},{regex}", null,
                x => x
                    .WithTitle(Strings.ChatTriggerEdit(ctx.Guild.Id))
                    .UpdateTextInput("key", textInputBuilder => textInputBuilder.Value = trigger.Trigger)
                    .UpdateTextInput("message", textInputBuilder => textInputBuilder.Value = trigger.Response))
            .ConfigureAwait(false);

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }


    /// <summary>
    ///     Handles the modal interaction for editing a chat trigger.
    /// </summary>
    /// <param name="sId">The string representation of the trigger ID.</param>
    /// <param name="sRgx">The string representation of the regex.</param>
    /// <param name="modal">The modal containing trigger details.</param>
    [ModalInteraction("chat_trigger_edit:*,*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task EditChatTriggerModal(string sId, bool sRgx, ChatTriggerModal modal)
    {
        var id = int.Parse(sId);
        if (string.IsNullOrWhiteSpace(modal.Message) || id < 0)
            return;

        var cr = await Service.EditAsync(ctx.Guild?.Id, id, modal.Message, sRgx, modal.Trigger).ConfigureAwait(false);
        if (cr != null)
        {
            await RespondAsync(embed: new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.EditedChatTrig(ctx.Guild.Id))
                .WithDescription(Strings.ChatTriggerId(ctx.Guild.Id, id))
                .AddField(efb => efb.WithName(Strings.Trigger(ctx.Guild.Id)).WithValue(cr.Trigger))
                .AddField(efb =>
                    efb.WithName(Strings.Response(ctx.Guild.Id))
                        .WithValue(modal.Message.Length > 1024 ? Strings.RedactedTooLong(ctx.Guild.Id) : modal.Message))
                .Build()).ConfigureAwait(false);
        }
        else
        {
            await RespondAsync(Strings.EditFail(ctx.Guild.Id)).ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the type of prefix this chat trigger will use.
    /// </summary>
    /// <param name="id">The chat trigger's id.</param>
    /// <param name="type">The type of prefix to use. <see cref="RequirePrefixType" /></param>
    /// <example>/triggers prefix-type 9987 Guild</example>
    [SlashCommand("prefix-type", "Sets the type of prefix this chat trigger will use")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task CtPrefixType
    (
        [Summary("id", "The chat trigger's id.")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
        int id,
        [Summary("type", "The type of prefix to use.")]
        RequirePrefixType type
    )
    {
        var res = await Service.SetPrefixType(ctx.Guild?.Id, id, type).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Sets the prefix for a chat trigger when the prefix type is custom.
    /// </summary>
    /// <param name="id">The chat trigger's id.</param>
    /// <param name="prefix">The prefix to use when the prefix type is custom.</param>
    /// <example>/triggers prefix 9987 !</example>
    [SlashCommand("prefix", "Sets prefix this chat trigger when prefix type is custom")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task CtPrefix
    (
        [Summary("id", "The chat trigger's id.")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
        int id,
        [Summary("prefix", "The prefix to use when prefix type is custom")]
        string prefix)
    {
        var res = await Service.SetPrefix(ctx.Guild?.Id, id, prefix).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Lists chat triggers.
    /// </summary>
    /// <example>/triggers list</example>
    [SlashCommand("list", "List chat triggers.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task ListChatTriggers()
    {
        var chatTriggers = await Service.GetChatTriggersFor(ctx.Guild?.Id);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(chatTriggers.Length / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity
            .SendPaginatorAsync(paginator, ctx.Interaction as SocketInteraction, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithColor(Mewdeko.OkColor).WithTitle(Strings.ChatTriggers(ctx.Guild.Id))
                .WithDescription(string.Join("\n",
                    chatTriggers.OrderBy(cr => cr.Trigger).Skip(page * 20)
                        .Take(20).Select(cr =>
                        {
                            var str = $"`#{cr.Id}` {cr.Trigger}";
                            if (cr.AutoDeleteTrigger)
                                str = $"🗑{str}";
                            if (cr.DmResponse)
                                str = $"📪{str}";
                            var reactions = cr.GetReactions();
                            if (reactions.Length > 0)
                            {
                                str =
                                    $"{str} // {string.Join(" ", reactions)}";
                            }

                            return str;
                        })));
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }


    /// <summary>
    ///     Lists chat triggers grouped by trigger.
    /// </summary>
    /// <example>/triggers list-group</example>
    [SlashCommand("list-group", "List chat triggers..")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task ListChatTriggersGroup()
    {
        var chatTriggers = await Service.GetChatTriggersFor(ctx.Guild?.Id);

        if (!chatTriggers.Any())
        {
            await ctx.Interaction.SendErrorAsync(Strings.NoFound(ctx.Guild.Id), config).ConfigureAwait(false);
        }
        else
        {
            var ordered = chatTriggers
                .GroupBy(ct => ct.Trigger)
                .OrderBy(ct => ct.Key)
                .ToList();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(chatTriggers.Length / 20)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity
                .SendPaginatorAsync(paginator, Context.Interaction as SocketInteraction, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithColor(Mewdeko.OkColor).WithTitle(Strings.Name(ctx.Guild.Id))
                    .WithDescription(string.Join("\r\n",
                        ordered.Skip(page * 20).Take(20).Select(ct =>
                            $"**{ct.Key.Trim().ToLowerInvariant()}** `x{ct.Count()}`")));
            }
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }


    /// <summary>
    ///     Shows the response of a chat trigger.
    /// </summary>
    /// <param name="id">The chat trigger's id.</param>
    /// <example>/triggers show 9987</example>
    [SlashCommand("show", "Shows the response of a chat trigger.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task ShowChatTrigger(
        [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
        int id)
    {
        var found = await Service.GetChatTriggers(ctx.Guild?.Id, id);

        if (found == null)
            await ctx.Interaction.SendErrorAsync(Strings.NoFoundId(ctx.Guild.Id), config).ConfigureAwait(false);
        else
            await ctx.Interaction.RespondAsync(embed: Service.GetEmbed(found, ctx.Guild?.Id).Build())
                .ConfigureAwait(false);

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a chat trigger.
    /// </summary>
    /// <param name="id">The chat trigger's id.</param>
    /// <example>/triggers delete 9987</example>
    [SlashCommand("delete", "delete a chat trigger.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task DeleteChatTrigger(
        [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
        int id)
    {
        var ct = await Service.DeleteAsync(ctx.Guild?.Id, id).ConfigureAwait(false);

        if (ct != null)
        {
            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.Deleted(ctx.Guild.Id))
                .WithDescription(Strings.ChatTriggerId(ctx.Guild.Id, ct.Id))
                .AddField(efb => efb.WithName(Strings.Trigger(ctx.Guild.Id)).WithValue(ct.Trigger.TrimTo(1024)))
                .AddField(efb => efb.WithName(Strings.Response(ctx.Guild.Id)).WithValue(ct.Response.TrimTo(1024)))
                .Build()).ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction.SendErrorAsync(Strings.NoFoundId(ctx.Guild.Id), config).ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }


    /// <summary>
    ///     Adds reaction to a chat trigger.
    /// </summary>
    /// <param name="id">The chat trigger's id.</param>
    /// <param name="emoji">A space-separated list of emojis to react with.</param>
    /// <example>/triggers react 9987 :sylvsad:</example>
    [SlashCommand("react", "add a reaction chat trigger..")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task CtReact
    (
        [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
        int id,
        [Summary("emoji", "A space-seperated list of emojis to react with")]
        string emoji
    )
    {
        var ct = await Service.GetChatTriggers(Context.Guild?.Id, id);
        if (ct is null)
        {
            await ctx.Interaction.SendErrorAsync(Strings.NoFound(ctx.Guild.Id), config).ConfigureAwait(false);
            return;
        }

        var emojiStrs = emoji.Split(' ');

        if (emojiStrs.Length == 0)
        {
            await Service.ResetCrReactions(ctx.Guild?.Id, id).ConfigureAwait(false);
            await ctx.Interaction.SendErrorAsync(Strings.CtrReset(ctx.Guild.Id, id), config).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.SendConfirmAsync(Strings.CtrTestingEmotes(ctx.Guild.Id)).ConfigureAwait(false);
        var message = await ctx.Interaction.GetOriginalResponseAsync().ConfigureAwait(false);
        var succ = new List<string>();
        foreach (var emojiStr in emojiStrs)
        {
            var emote = emojiStr.ToIEmote();

            // i should try adding these emojis right away to the message, to make sure the bot can react with these emojis. If it fails, skip that emoji
            try
            {
                await message.AddReactionAsync(emote).ConfigureAwait(false);
                await Task.Delay(100).ConfigureAwait(false);
                succ.Add(emojiStr);

                if (succ.Count >= 6)
                    break;
            }
            catch
            {
                // ignored
            }
        }

        if (succ.Count == 0)
        {
            await message.ModifyAsync(x => x.Embed = new EmbedBuilder().WithErrorColor()
                .WithDescription(Strings.InvalidEmojis(ctx.Guild.Id))
                .Build()).ConfigureAwait(false);
            return;
        }

        await Service.SetCrReactions(ctx.Guild?.Id, id, succ).ConfigureAwait(false);

        var text = Strings.CtrSet(ctx.Guild.Id, Format.Bold(id.ToString()),
            string.Join(',', succ.Select(x => x.ToString())));
        await message.ModifyAsync(x => x.Embed = new EmbedBuilder().WithOkColor().WithDescription(text).Build())
            .ConfigureAwait(false);

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }


    /// <summary>
    ///     Edits chat trigger options.
    /// </summary>
    /// <param name="id">The chat trigger's id.</param>
    /// <param name="option">The option to toggle.</param>
    /// <example>/triggers toggle-option 9987 DmResponse</example>
    [SlashCommand("toggle-option", "Edit chat trigger options.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task InternalCtEdit
    (
        [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
        int id,
        [Summary("option", "The option to toggle")]
        ChatTriggersService.CtField option
    )
    {
        var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);
        if (ct is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (success, newVal) = await Service.ToggleCrOptionAsync(ct, option).ConfigureAwait(false);

        if (!success)
        {
            await ctx.Interaction.SendConfirmAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (newVal)
        {
            await ctx.Interaction.SendConfirmAsync(Strings.OptionEnabled(ctx.Guild.Id, Format.Code(option.ToString()),
                Format.Code(id.ToString()))).ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction.SendConfirmAsync(Strings.OptionDisabled(ctx.Guild.Id, Format.Code(option.ToString()),
                Format.Code(id.ToString()))).ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Changes the valid types of the trigger.
    /// </summary>
    /// <param name="id">The chat trigger's id.</param>
    /// <param name="type">The type to enable/disable. <see cref="ChatTriggerType" /></param>
    /// <param name="enabled">Should the type be enabled?</param>
    /// <example>/triggers valid-types 9987 Message true</example>
    [SlashCommand("valid-types", "Change the valid types of the trigger")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task ChatTriggerValidType(
        [Summary("trigger", "The chat trigger to edit.")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
        int id,
        [Summary("type", "The type to enable/disable.")]
        ChatTriggerType type,
        [Summary("enabled", "Should the type be enabled?")]
        bool enabled)
    {
        var res = await Service.SetValidTriggerType(ctx.Guild?.Id, id, type, enabled).ConfigureAwait(false);

        if (res is null)
        {
            await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }


    /// <summary>
    ///     Clears all chat triggers.
    /// </summary>
    /// <example>/triggers clear</example>
    [SlashCommand("clear", "Clear all chat triggers.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task CtsClear()
    {
        await DeferAsync().ConfigureAwait(false);
        if (await PromptUserConfirmAsync(new EmbedBuilder()
                .WithTitle(Strings.CtClear(ctx.Guild.Id))
                .WithDescription(Strings.CtClearDone(ctx.Guild.Id)), ctx.User.Id).ConfigureAwait(false))
        {
            var count = Service.DeleteAllChatTriggers(ctx.Guild.Id);
            await ConfirmAsync(Strings.Cleared(ctx.Guild.Id, count)).ConfigureAwait(false);
        }

        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Follows up with trigger status by checking for errors.
    /// </summary>
    private async Task FollowupWithTriggerStatus()
    {
        var errors = await Service.GetAcctErrors(ctx.Guild?.Id);
        if (!(errors?.Any() ?? false))
            return;
        var embed = new EmbedBuilder()
            .WithTitle(Strings.CtInteractionErrorsTitle(ctx.Guild.Id))
            .WithDescription(Strings.CtInteractionErrorsDesc(ctx.Guild.Id))
            .WithErrorColor();
        await ctx.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles component interactions related to multitriggers.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="_">The interaction string.</param>
    [ComponentInteraction("multitrigger.runin.*$*", true)]
    public async Task HandleMultitriggers(ulong? guildId, string _)
    {
        var values = (Context.Interaction as SocketMessageComponent).Data.Values;
        var i = -1;

        foreach (var n in values)
        {
            var id = n.Split('.').Length >= 2 && int.TryParse(n.Split('.')[1], out var parsedId)
                ? parsedId
                : int.TryParse(n, out parsedId)
                    ? parsedId
                    : (int?)null;

            if (!id.HasValue) continue;
            var chatTrigger = await Service.GetChatTriggers(guildId, id.Value);
            await Service.RunInteractionTrigger(ctx.Interaction as SocketInteraction, chatTrigger, ++i >= 1);
        }
    }

    /// <summary>
    ///     Group for crossposting related commands.
    /// </summary>
    [Group("crossposting", "crossposting")]
    public class Crossposting : MewdekoSlashModuleBase<ChatTriggersService>
    {
        /// <summary>
        ///     Sets the webhook URL for crossposting triggers.
        /// </summary>
        /// <param name="id">The chat trigger's id.</param>
        /// <param name="webhookUrl">The webhook URL to crosspost messages with.</param>
        /// <example>/triggers crossposting webhook 9987 https://discord.com/api/webhooks/1234567890/abcdefg</example>
        [SlashCommand("webhook", "crosspost triggers using a webhook")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtCpSetWebhook
        (
            [Summary("trigger", "The chat trigger to edit.")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("webhook-url", "What webhook do you want to crosspost messages with?")]
            string webhookUrl
        )
        {
            var res = await Service.SetCrosspostingWebhookUrl(ctx.Guild?.Id, id, webhookUrl).ConfigureAwait(false);
            if (!res.Valid)
            {
                await ReplyErrorAsync(Strings.CtWebhookInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (res.Trigger is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(embed: Service.GetEmbed(res.Trigger, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id))
                    .Build())
                .ConfigureAwait(false);

            await FollowupWithTriggerStatus().ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel for crossposting triggers.
        /// </summary>
        /// <param name="id">The chat trigger's id.</param>
        /// <param name="channel">The channel to crosspost messages to.</param>
        /// <example>/triggers crossposting channel 9987 #crosspost-channel</example>
        [SlashCommand("channel", "crosspost triggers to a channel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        [RequireContext(ContextType.Guild)]
        public async Task CtCpSetChannel
        (
            [Summary("trigger", "The chat trigger to edit.")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("channel", "What channels do you want to crosspost messages to?")]
            ITextChannel channel
        )
        {
            var res = await Service.SetCrosspostingChannelId(ctx.Guild?.Id, id, channel.Id).ConfigureAwait(false);
            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);

            await FollowupWithTriggerStatus().ConfigureAwait(false);
        }

        /// <summary>
        ///     Follows up with the status of triggers.
        /// </summary>
        private async Task FollowupWithTriggerStatus()
        {
            var errors = await Service.GetAcctErrors(ctx.Guild?.Id);
            if (!(errors?.Any() ?? false))
                return;
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CtInteractionErrorsTitle(ctx.Guild.Id))
                .WithDescription(Strings.CtInteractionErrorsDesc(ctx.Guild.Id))
                .WithErrorColor();
            await ctx.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Group for role-related commands.
    /// </summary>
    [Group("roles", "roles")]
    public class Roles : MewdekoSlashModuleBase<ChatTriggersService>
    {
        /// <summary>
        ///     Toggles whether running this command will add the role to the user.
        /// </summary>
        /// <param name="id">The trigger to add roles to.</param>
        /// <param name="role">The role to toggle.</param>
        /// <example>/triggers roles add 9987 @role</example>
        [SlashCommand("add", "Toggle whether running this command will add the role to the user.")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtrGrantToggle
        (
            [Autocomplete(typeof(ChatTriggerAutocompleter))] [Summary("trigger", "The trigger to add roles to.")]
            int id,
            [Summary("role", "The roll to toggle.")]
            IRole role
        )
        {
            var gUsr = ctx.User as IGuildUser;

            if (!role.CanManageRole(gUsr))
            {
                await ReplyErrorAsync(Strings.CantManageRole(ctx.Guild.Id)).ConfigureAwait(false);
                await FollowupWithTriggerStatus().ConfigureAwait(false);
                return;
            }

            var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);

            if (ct is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                await FollowupWithTriggerStatus().ConfigureAwait(false);
                return;
            }

            var toggleDisabled = ct.IsToggled(role.Id);

            await Service.ToggleGrantedRole(ct, role.Id).ConfigureAwait(false);

            var text = toggleDisabled
                ? Strings.CtRoleToggleDisabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
                : ct.IsToggled(role.Id)
                    ? Strings.CtRoleToggleEnabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
                    : ct.IsGranted(role.Id)
                        ? Strings.CtRoleAddEnabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
                        : Strings.CtRoleAddDisabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()));

            await ReplyConfirmAsync(text).ConfigureAwait(false);
            await FollowupWithTriggerStatus().ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles whether running this command will remove the role from the user.
        /// </summary>
        /// <param name="id">The trigger to remove roles from.</param>
        /// <param name="role">The role to toggle.</param>
        /// ///
        /// <example>/triggers roles toggle-remove 9987 @role</example>
        [SlashCommand("toggle-remove", "Toggle whether running this command will remove the role to the user.")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtrRemoveToggle
        (
            [Autocomplete(typeof(ChatTriggerAutocompleter))] [Summary("trigger", "The trigger to remove roles from.")]
            int id,
            [Summary("role", "The roll to toggle.")]
            IRole role
        )
        {
            var gUsr = ctx.User as IGuildUser;

            if (!role.CanManageRole(gUsr))
            {
                await ReplyErrorAsync(Strings.CantManageRole(ctx.Guild.Id)).ConfigureAwait(false);
                await FollowupWithTriggerStatus().ConfigureAwait(false);
                return;
            }

            var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);
            if (ct is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                await FollowupWithTriggerStatus().ConfigureAwait(false);
                return;
            }

            var toggleDisabled = ct.IsToggled(role.Id);

            await Service.ToggleRemovedRole(ct, role.Id).ConfigureAwait(false);

            var text = toggleDisabled
                ? Strings.CtRoleToggleDisabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
                : ct.IsToggled(role.Id)
                    ? Strings.CtRoleToggleEnabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
                    : ct.IsRemoved(role.Id)
                        ? Strings.CtRoleRemoveEnabled(ctx.Guild.Id, Format.Bold(role.Name), Format.Code(id.ToString()))
                        : Strings.CtRoleRemoveDisabled(ctx.Guild.Id, Format.Bold(role.Name),
                            Format.Code(id.ToString()));

            await ReplyConfirmAsync(text).ConfigureAwait(false);
            await FollowupWithTriggerStatus().ConfigureAwait(false);
        }


        /// <summary>
        ///     Changes the way roles are added to chat triggers.
        /// </summary>
        /// <param name="id">The trigger to remove roles from.</param>
        /// <param name="type">How should roles be added when the trigger is used.</param>
        /// <example>/triggers roles mode 9987 Sender</example>
        [SlashCommand("mode", "Changes the way roles are added to chat triggers.")]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ChatTriggerRoleGrantType(
            [Autocomplete(typeof(ChatTriggerAutocompleter))] [Summary("trigger", "The trigger to remove roles from.")]
            int id,
            [Summary("mode", "How should roles be added when the trigger is used.")]
            CtRoleGrantType type)
        {
            var res = await Service.SetRoleGrantType(ctx.Guild?.Id, id, type).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id))
                        .Build())
                    .ConfigureAwait(false);
            }

            await FollowupWithTriggerStatus().ConfigureAwait(false);
        }

        /// <summary>
        ///     Follows up with the status of triggers.
        /// </summary>
        private async Task FollowupWithTriggerStatus()
        {
            var errors = await Service.GetAcctErrors(ctx.Guild?.Id);
            if (!(errors?.Any() ?? false))
                return;
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CtInteractionErrorsTitle(ctx.Guild.Id))
                .WithDescription(Strings.CtInteractionErrorsDesc(ctx.Guild.Id))
                .WithErrorColor();
            await ctx.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Group for interaction-related commands.
    /// </summary>
    [Group("interactions", "interactions")]
    public class Interactions : MewdekoSlashModuleBase<ChatTriggersService>
    {
        /// <summary>
        ///     Sets the type of interaction support (user, message, or slash).
        /// </summary>
        /// <param name="id">The trigger to update.</param>
        /// <param name="type">The type of command, use 'none' to disable commands in their entirety.</param>
        /// <example>/triggers interactions type 9987 user</example>
        [SlashCommand("type", "Sets the type of interaction support (user, message, or slash).")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task SetCtInterType(
            [Autocomplete(typeof(ChatTriggerAutocompleter))] [Summary("trigger", "The trigger to update.")]
            int id,
            [Summary("type", "The type of command, use 'none' to disable commands in their entirety.")]
            CtApplicationCommandType type)
        {
            var ct = await Service.GetChatTriggers(ctx.Guild?.Id, id);
            if (ct is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            // validate the name based on type
            if (type != CtApplicationCommandType.None && !ChatTriggersService.IsValidName(type,
                    string.IsNullOrWhiteSpace(ct.ApplicationCommandName) ? ct.Trigger : ct.ApplicationCommandName))
            {
                await ReplyErrorAsync(Strings.CtInteractionNameInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var res = await Service.SetInteractionType(ctx.Guild?.Id, id, type).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id))
                        .Build())
                    .ConfigureAwait(false);
            }

            await FollowupWithTriggerStatus().ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the name of the interaction.
        /// </summary>
        /// <param name="id">The trigger to update.</param>
        /// <param name="name">The name of the interaction.</param>
        /// <example>/triggers interactions name 9987 my-interaction</example>
        [SlashCommand("name", "Sets the name of the interaction.")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task SetCtInterName(
            [Autocomplete(typeof(ChatTriggerAutocompleter))] [Summary("trigger", "The trigger to update.")]
            int id,
            [Summary("name", "The name of the interaction.")]
            string name)
        {
            var res = await Service.SetInteractionName(ctx.Guild?.Id, id, name).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id))
                        .Build())
                    .ConfigureAwait(false);
            }

            await FollowupWithTriggerStatus().ConfigureAwait(false);
        }


        /// <summary>
        ///     Sets the description of the interaction.
        /// </summary>
        /// <param name="id">The trigger to update.</param>
        /// <param name="description">The description of the interaction.</param>
        /// <example>/triggers interactions description 9987 my-interaction</example>
        [SlashCommand("description", "Sets the description of the interaction.")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task SetCtInterDesc(
            [Autocomplete(typeof(ChatTriggerAutocompleter))] [Summary("trigger", "The trigger to update.")]
            int id,
            [Summary("description", "The description of the interaction.")]
            string description)
        {
            var res = await Service.SetInteractionDescription(ctx.Guild?.Id, id, description).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id))
                        .Build())
                    .ConfigureAwait(false);
            }

            await FollowupWithTriggerStatus().ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables/Disables ephemeral mode.
        /// </summary>
        /// <param name="id">The trigger to update.</param>
        /// <param name="ephemeral">Should the trigger be ephemeral?</param>
        /// <example>/triggers interactions ephemeral 9987 true</example>
        [SlashCommand("ephemeral", "Enables/Disables ephemeral mode.")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtInterEphemeral(
            [Autocomplete(typeof(ChatTriggerAutocompleter))] [Summary("trigger", "The trigger to update.")]
            int id,
            [Summary("ephemeral", "Should the trigger be ephemeral?")]
            bool ephemeral)
        {
            var res = await Service.SetInteractionEphemeral(ctx.Guild?.Id, id, ephemeral).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await RespondAsync(embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id))
                        .Build())
                    .ConfigureAwait(false);
            }

            await FollowupWithTriggerStatus().ConfigureAwait(false);
        }


        private async Task FollowupWithTriggerStatus()
        {
            var errors = await Service.GetAcctErrors(ctx.Guild?.Id);
            if (!(errors?.Any() ?? false))
                return;
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CtInteractionErrorsTitle(ctx.Guild.Id))
                .WithDescription(Strings.CtInteractionErrorsDesc(ctx.Guild.Id))
                .WithErrorColor();
            await ctx.Interaction.FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
        }

        /// <summary>
        ///     Checks for errors in interaction chat triggers.
        /// </summary>
        /// <example>/triggers interactions errors</example>
        [SlashCommand("errors", "Check for errors in your interaction chat triggers.")]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
// ReSharper disable once UnusedMember.Local
        public async Task CtInterErrors()
        {
            var errors = await Service.GetAcctErrors(ctx.Guild?.Id);
            var eb = new EmbedBuilder();
            var cb = new ComponentBuilder().WithButton("Support Server", style: ButtonStyle.Link,
                url: "https://discord.gg/Mewdeko",
                emote: Emote.Parse("<:IconInvite:778931752835088426>"));

            if (errors?.Any() ?? false)
            {
                eb.WithFields(errors.Select(x =>
                    {
                        var title = x.ErrorKey switch
                        {
                            "duplicate" => Strings.CtInterrDuplicate(ctx.Guild.Id),
                            "invalid_name" => Strings.CtInterrInvalidName(ctx.Guild.Id),
                            "subcommand_match_parent" => Strings.CtInterrSubcommandMatchParent(ctx.Guild.Id),
                            "too_many_children" => Strings.CtInterrTooManyChildren(ctx.Guild.Id),
                            _ => x.ErrorKey
                        };

                        var body = x.ErrorKey switch
                        {
                            "duplicate" => Strings.CtInterrDuplicateBody(ctx.Guild.Id,
                                x.CtRealNames.Select(s => $" - {s}").Join('\n')),
                            "invalid_name" => Strings.CtInterrInvalidNameBody(ctx.Guild.Id,
                                x.CtRealNames.Select(s => $" - {s}").Join('\n')),
                            "subcommand_match_parent" => Strings.CtInterrSubcommandMatchParentBody(ctx.Guild.Id,
                                x.CtRealNames.Select(s => $" - {s}").Join('\n')),
                            "too_many_children" => Strings.CtInterrTooManyChildrenBody(ctx.Guild.Id,
                                x.CtRealNames.Select(s => $" - {s}").Join('\n')),
                            _ => x.ErrorKey
                        };

                        return new EmbedFieldBuilder().WithName(title).WithValue(body);
                    }))
                    .WithTitle(Strings.CtInteractionErrorsInfoTitle(ctx.Guild.Id, errors.Count))
                    .WithDescription(Strings.CtInteractionErrorsInfoDesc(ctx.Guild.Id))
                    .WithErrorColor();
            }
            else
            {
                eb.WithOkColor()
                    .WithTitle(Strings.CtInteractionErrorsNone(ctx.Guild.Id))
                    .WithDescription(Strings.CtInteractionErrorsNoneDesc(ctx.Guild.Id));
            }

            await RespondAsync(embed: eb.Build(), components: cb.Build());
        }
    }
}