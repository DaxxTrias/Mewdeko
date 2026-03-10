using System.IO;
using System.Text;
using System.Text.Json;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Provides sniping functionality to retrieve and display previously deleted or edited messages.
    /// </summary>
    [Group("snipe", "Snipe edited or delete messages!")]
    public class SlashSnipes(
        DiscordShardedClient client,
        InteractiveService interactiveService,
        GuildSettingsService guildSettings,
        BotConfigService config)
        : MewdekoSlashModuleBase<UtilityService>
    {
        /// <summary>
        ///     Snipes deleted messages for the current or mentioned channel. This command requires guild context.
        /// </summary>
        /// <param name="channel">The channel to snipe messages from. If null, defaults to the current channel.</param>
        /// <param name="user">The user to filter sniped messages by. If null, messages by all users are considered.</param>
        /// <returns>A task that represents the asynchronous operation of sniping a deleted message.</returns>
        [SlashCommand("deleted", "Snipes deleted messages for the current or mentioned channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Snipe(IMessageChannel? channel = null, IUser? user = null)
        {
            channel ??= ctx.Channel;
            if (!await Service.GetSnipeSet(ctx.Guild.Id))
            {
                await ReplyErrorAsync(Strings.SnipeSlashNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x => !x.Edited)
                .LastOrDefault(x => x.ChannelId == channel.Id);

            if (user is not null)
            {
                msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x => !x.Edited)
                    .LastOrDefault(x => x.ChannelId == channel.Id && x.UserId == user.Id);
            }

            if (msg is null)
            {
                await ReplyErrorAsync(Strings.NoSnipes(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ??
                   await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);
            var attachments = GetAttachments(msg);

            var em = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = user.GetAvatarUrl(), Name = $"{user} said:"
                },
                Description = GetDisplayMessage(msg, attachments),
                Footer = new EmbedFooterBuilder
                {
                    IconUrl = ctx.User.GetAvatarUrl(),
                    Text =
                        Strings.SnipeRequest(ctx.Guild.Id, ctx.User.ToString(),
                            (DateTime.UtcNow - msg.DateAdded).Humanize())
                },
                Color = Mewdeko.OkColor,
                Timestamp = msg.MessageTimestamp
            };

            if (msg.ReferenceMessage is not null)
                em.AddField("Replied To", msg.ReferenceMessage);

            em.AddField("Sent", TimestampTag.FromDateTimeOffset(msg.MessageTimestamp, TimestampTagStyles.ShortDateTime).ToString(), true);
            AddAttachmentsField(em, attachments);

            if (!string.IsNullOrEmpty(msg.JsonData))
            {
                await using var ms = new MemoryStream();
                await using var writer = new StreamWriter(ms);
                await writer.WriteAsync(msg.JsonData);
                await writer.FlushAsync();
                ms.Position = 0;

                await ctx.Interaction.RespondWithFileAsync(ms, $"snipe_{msg.MessageId}.json", embed: em.Build(),
                    components: config.Data.ShowInviteButton
                        ? new ComponentBuilder()
                            .WithButton(style: ButtonStyle.Link,
                                url: "",
                                label: "",
                                emote: "".ToIEmote()).Build()
                        : null).ConfigureAwait(false);
            }
            else
            {
                await ctx.Interaction.RespondAsync(embed: em.Build(),
                    components: config.Data.ShowInviteButton
                        ? new ComponentBuilder()
                            .WithButton(style: ButtonStyle.Link,
                                url: "",
                                label: "",
                                emote: "".ToIEmote()).Build()
                        : null).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Snipes edited messages for the current or mentioned channel. This command requires guild context.
        /// </summary>
        /// <param name="channel">The channel to snipe messages from. If null, defaults to the current channel.</param>
        /// <param name="user">The user to filter sniped messages by. If null, messages by all users are considered.</param>
        /// <returns>A task that represents the asynchronous operation of sniping an edited message.</returns>
        [SlashCommand("edited", "Snipes edited messages for the current or mentioned channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task EditSnipe(IMessageChannel? channel = null, IUser? user = null)
        {
            channel ??= ctx.Channel;
            if (!await Service.GetSnipeSet(ctx.Guild.Id))
            {
                await ReplyErrorAsync(Strings.SnipeSlashNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x => x.Edited)
                .LastOrDefault(x => x.ChannelId == channel.Id);

            if (user is not null)
            {
                msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x => x.Edited)
                    .LastOrDefault(x => x.ChannelId == channel.Id && x.UserId == user.Id);
            }

            if (msg is null)
            {
                await ReplyErrorAsync(Strings.NoSnipes(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ??
                   await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);
            var attachments = GetAttachments(msg);

            var em = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = user.GetAvatarUrl(), Name = $"{user} originally said:"
                },
                Description = GetDisplayMessage(msg, attachments),
                Footer = new EmbedFooterBuilder
                {
                    IconUrl = ctx.User.GetAvatarUrl(),
                    Text =
                        Strings.SnipeRequest(ctx.Guild.Id, ctx.User.ToString(),
                            (DateTime.UtcNow - msg.DateAdded).Humanize())
                },
                Color = Mewdeko.OkColor,
                Timestamp = msg.MessageTimestamp
            };

            if (msg.ReferenceMessage is not null)
                em.AddField("Replied To", msg.ReferenceMessage);

            em.AddField("Sent", TimestampTag.FromDateTimeOffset(msg.MessageTimestamp, TimestampTagStyles.ShortDateTime).ToString(), true);
            AddAttachmentsField(em, attachments);

            if (!string.IsNullOrEmpty(msg.JsonData))
            {
                await using var ms = new MemoryStream();
                await using var writer = new StreamWriter(ms);
                await writer.WriteAsync(msg.JsonData);
                await writer.FlushAsync();
                ms.Position = 0;

                await ctx.Interaction.RespondWithFileAsync(ms, $"snipe_{msg.MessageId}.json", embed: em.Build(),
                    components: config.Data.ShowInviteButton
                        ? new ComponentBuilder()
                            .WithButton(style: ButtonStyle.Link,
                                url: "",
                                label: "",
                                emote: "".ToIEmote()).Build()
                        : null).ConfigureAwait(false);
            }
            else
            {
                await ctx.Interaction.RespondAsync(embed: em.Build(),
                    components: config.Data.ShowInviteButton
                        ? new ComponentBuilder()
                            .WithButton(style: ButtonStyle.Link,
                                url: "",
                                label: "",
                                emote: "".ToIEmote()).Build()
                        : null).ConfigureAwait(false);
            }
        }

        private async Task SnipeListBase(bool edited, int amount = 5, ITextChannel channel = null, IUser user = null)
        {
            var channelToList = channel ?? ctx.Channel;
            if (!await Service.GetSnipeSet(ctx.Guild.Id))
            {
                await ctx.Channel.SendErrorAsync(
                        $"Sniping is not enabled in this server! Use `{await guildSettings.GetPrefix(ctx.Guild)}snipeset enable` to enable it!",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false))
                .Where(x => x.ChannelId == channelToList.Id && x.Edited == edited);
            if (user is not null)
            {
                msgs = msgs.Where(x => x.UserId == user.Id);
            }

            var snipeStores = msgs as SnipeStore[] ?? msgs.ToArray();
            if (snipeStores.Length == 0)
            {
                await ctx.Interaction.SendErrorAsync(Strings.NothingToSnipe(ctx.Guild.Id), Config);
                return;
            }

            var msg = snipeStores.OrderByDescending(d => d.DateAdded).Where(x => x.Edited == edited).Take(amount);
            var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                .WithFooter(
                    PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(msg.Count() - 1).WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactiveService
                .SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                var msg1 = msg.Skip(page).FirstOrDefault();
                var user = await ctx.Channel.GetUserAsync(msg1.UserId).ConfigureAwait(false)
                           ?? await client.Rest.GetUserAsync(msg1.UserId).ConfigureAwait(false);
                var attachments = GetAttachments(msg1);

                var builder = new PageBuilder().WithOkColor()
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithIconUrl(user.RealAvatarUrl().AbsoluteUri)
                        .WithName($"{user} {(edited ? "originally said" : "said")}:"))
                    .WithDescription(GetDisplayMessage(msg1, attachments))
                    .WithFooter($"Message {(edited ? "edited" : "deleted")} {(DateTime.UtcNow - msg1.DateAdded).Humanize()} ago")
                    .WithTimestamp(msg1.MessageTimestamp);

                if (msg1.ReferenceMessage is not null)
                    builder.AddField("Replied To", msg1.ReferenceMessage);

                builder.AddField("Sent", TimestampTag.FromDateTimeOffset(msg1.MessageTimestamp, TimestampTagStyles.ShortDateTime).ToString(), true);
                AddAttachmentsField(builder, attachments);

                return builder;
            }
        }

        /// <summary>
        ///     Lists the last 5 deleted snipes for the current or mentioned channel, unless specified otherwise. This command
        ///     requires guild context and the appropriate permissions to execute.
        /// </summary>
        /// <param name="amount">The number of deleted messages to retrieve, defaults to 5.</param>
        /// <param name="channel">The specific channel to check for deleted messages. If null, checks the current channel.</param>
        /// <param name="user">Filters the snipes by the specified user. If null, retrieves messages deleted by any user.</param>
        /// <returns>A task that represents the asynchronous operation of listing deleted snipes.</returns>
        [SlashCommand("deletedlist", "Lists the last 5 delete snipes unless specified otherwise.")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task SnipeList(int amount = 5, ITextChannel channel = null, IUser user = null)
        {
            return SnipeListBase(false, amount, channel, user);
        }

        /// <summary>
        ///     Lists the last 5 edited snipes for the current or mentioned channel, unless specified otherwise. This command
        ///     requires guild context and the appropriate permissions to execute.
        /// </summary>
        /// <param name="amount">The number of edited messages to retrieve, defaults to 5.</param>
        /// <param name="channel">The specific channel to check for edited messages. If null, checks the current channel.</param>
        /// <param name="user">Filters the snipes by the specified user. If null, retrieves messages edited by any user.</param>
        /// <returns>A task that represents the asynchronous operation of listing edited snipes.</returns>
        [SlashCommand("editedlist", "Lists the last 5 edit snipes unless specified otherwise.")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task EditSnipeList(int amount = 5, ITextChannel channel = null, IUser user = null)
        {
            return SnipeListBase(true, amount, channel, user);
        }


        /// <summary>
        ///     Enables or disables the sniping functionality for the server. This command requires administrator permissions.
        /// </summary>
        /// <param name="enabled">True to enable sniping, false to disable.</param>
        /// <returns>A task that represents the asynchronous operation of setting the snipe functionality state.</returns>
        [SlashCommand("set", "Enable or Disable sniping")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task SnipeSet(bool enabled)
        {
            await Service.SnipeSet(ctx.Guild, enabled).ConfigureAwait(false);
            var t = await Service.GetSnipeSet(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.SnipeSet(ctx.Guild.Id, t ? "Enabled" : "Disabled")).ConfigureAwait(false);
        }

        private static string GetDisplayMessage(SnipeStore message, IReadOnlyList<SnipeAttachmentStore> attachments)
        {
            if (!string.IsNullOrWhiteSpace(message.Message))
                return message.Message;

            return attachments.Count > 0
                ? "[Attachment-only message]"
                : "[No text content]";
        }

        private static IReadOnlyList<SnipeAttachmentStore> GetAttachments(SnipeStore message)
        {
            if (message.Attachments is { Count: > 0 })
                return message.Attachments;

            if (string.IsNullOrWhiteSpace(message.JsonData))
                return [];

            try
            {
                var messageData = JsonSerializer.Deserialize<SnipeMessageData>(message.JsonData);
                if (messageData?.Attachments is not { Count: > 0 })
                    return [];

                return messageData.Attachments
                    .Select(x => new SnipeAttachmentStore
                    {
                        Filename = x.Filename,
                        Url = x.Url,
                        ProxyUrl = x.ProxyUrl
                    }).ToList();
            }
            catch
            {
                return [];
            }
        }

        private static void AddAttachmentsField(EmbedBuilder embed, IReadOnlyList<SnipeAttachmentStore> attachments)
        {
            var value = BuildAttachmentsFieldValue(attachments);
            if (!string.IsNullOrWhiteSpace(value))
                embed.AddField("Attachments", value);
        }

        private static void AddAttachmentsField(PageBuilder embed, IReadOnlyList<SnipeAttachmentStore> attachments)
        {
            var value = BuildAttachmentsFieldValue(attachments);
            if (!string.IsNullOrWhiteSpace(value))
                embed.AddField("Attachments", value);
        }

        private static string? BuildAttachmentsFieldValue(IReadOnlyList<SnipeAttachmentStore> attachments)
        {
            if (attachments.Count == 0)
                return null;

            var sb = new StringBuilder();
            var added = 0;
            for (var i = 0; i < attachments.Count; i++)
            {
                var attachment = attachments[i];
                var url = attachment.Url ?? attachment.ProxyUrl;
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                var fileName = string.IsNullOrWhiteSpace(attachment.Filename)
                    ? $"attachment-{i + 1}"
                    : attachment.Filename;
                var line = $"[{fileName}]({url})";

                if (sb.Length > 0)
                    line = $"\n{line}";

                if (sb.Length + line.Length > 1024)
                    break;

                sb.Append(line);
                added++;
            }

            if (added == 0)
                return "Attachment metadata was captured, but no URL is available.";

            if (attachments.Count > added)
            {
                var suffix = $"\n...and {attachments.Count - added} more.";
                if (sb.Length + suffix.Length <= 1024)
                    sb.Append(suffix);
            }

            return sb.ToString();
        }
    }
}