using System.Net.Http;
using System.Text.RegularExpressions;
using Discord.Interactions;
using Discord.Net;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Services.Settings;
using Serilog;
using Image = Discord.Image;

namespace Mewdeko.Modules.Server_Management;

/// <summary>
///     A module for stealing emotes and stickers from messages and adding them to the server.
/// </summary>
public class EmoteStealer(IHttpClientFactory httpFactory, BotConfigService config, ILogger<EmoteStealer> logger)
    : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Steals emotes from a message and adds them to the server's emote collection.
    /// </summary>
    /// <param name="message">The message containing emotes to be stolen.</param>
    /// <remarks>
    ///     This command requires the "Manage Emojis and Stickers" permission.
    ///     It goes through all the emotes in the specified message, downloads them, and attempts to add them to the guild.
    ///     Errors are logged, and a summary of successful and failed additions is provided.
    /// </remarks>
    [MessageCommand("Steal Emotes")]
    [RequireBotPermission(GuildPermission.ManageEmojisAndStickers)]
    [SlashUserPerm(GuildPermission.ManageEmojisAndStickers)]
    [CheckPermissions]
    public async Task Steal(IMessage message)
    {
        await ctx.Interaction.DeferAsync(true).ConfigureAwait(false);
        await ctx.Interaction.FollowupAsync(Strings.EmoteUploadLimitWarning(ctx.Guild.Id));
        var eb = new EmbedBuilder
        {
            Description = Strings.AddingEmotes(ctx.Guild.Id, config.Data.LoadingEmote),
            Color = Mewdeko.OkColor
        };
        var tags = message.Tags.Where(x => x.Type == TagType.Emoji).Select(x => (Emote)x.Value).Distinct();
        if (!tags.Any())
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.NoEmotesInMessage(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        var errored = new List<string>();
        var emotes = new List<string>();
        var msg = await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
        foreach (var i in tags)
        {
            var emoteName = i.Name; // Default to the emote name

            var pattern = $"<a?:{i.Name}:[0-9]+>";
            var match = Regex.Match(message.Content, pattern);

            if (match.Success && tags.Count() == 1)
            {
                // Find the index immediately after the emote match
                var indexAfterEmote = match.Index + match.Length;

                // Get the substring from the message that comes after the emote
                var potentialNamePart = message.Content.Substring(indexAfterEmote).Trim();

                // Split the remaining message by spaces and take the first word if any
                var parts = potentialNamePart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // Use the provided name only if there is exactly one emote and one potential name
                if (parts.Length > 0)
                {

                    // newer newer code
                    var candidateName = parts[0];
                    // Validate Discord emote name: 2-32 chars, alphanumeric/underscore only
                    if (Regex.IsMatch(candidateName, @"^[a-zA-Z0-9_]{2,32}$"))
                    {
                        emoteName = candidateName;
                    }
                }
            }

            using var http = httpFactory.CreateClient();
            using var sr = await http.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            {
                try
                {
                    var emote = await ctx.Guild.CreateEmoteAsync(emoteName, new Image(imgStream)).ConfigureAwait(false);
                    emotes.Add($"{emote} {Format.Code(emote.Name)}");
                }
                catch (HttpException httpEx) when (httpEx.HttpCode == System.Net.HttpStatusCode.BadRequest)
                {
                    if (httpEx.DiscordCode.HasValue && httpEx.DiscordCode.Value == (DiscordErrorCode)30008)
                    {
                        // check if the error is 30008
                        errored.Add($"Unable to add '{i.Name}'. Discord server reports no free emoji slots.");
                    }
                    // check if the error is 50138
                    else if (httpEx.DiscordCode.HasValue && httpEx.DiscordCode.Value == (DiscordErrorCode)50138)
                    {
                        errored.Add($"Unable to add '{i.Name}'. Discord server reports emoji file size is too large.");
                    }
                    else
                    {
                        // other HttpExceptions
                        Log.Information($"Failed to add emotes. Message: {httpEx.Message}");
                        errored.Add($"{i.Name}\n{i.Url}");
                    }
                }
                catch (Exception ex)
                {
                    // handle non-HTTP exceptions
                    Log.Information($"Failed to add emotes. Message: {ex.Message}");
                    errored.Add($"{emoteName}\n{i.Url}");
                }
            }
        }

        var b = new EmbedBuilder
        {
            Color = Mewdeko.OkColor
        };
        if (emotes.Count > 0)
            b.WithDescription(Strings.EmotesAdded(ctx.Guild.Id, string.Join("\n", emotes)));
        if (errored.Count > 0)
            b.AddField("Errored Emotes", string.Join("\n\n", errored));
        await msg.ModifyAsync(x => x.Embed = b.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Steals stickers from a message and adds them to the server's sticker collection.
    /// </summary>
    /// <param name="message">The message containing stickers to be stolen.</param>
    /// <remarks>
    ///     Similar to the emote stealing function, this command requires "Manage Emojis and Stickers" permission.
    ///     It processes all the stickers in the provided message, attempting to add each to the server.
    ///     Successes and failures are reported, with errors logged for troubleshooting.
    /// </remarks>
    [MessageCommand("Steal Sticker")]
    [RequireBotPermission(GuildPermission.ManageEmojisAndStickers)]
    [SlashUserPerm(GuildPermission.ManageEmojisAndStickers)]
    [CheckPermissions]
    public async Task StealSticker(IMessage message)
    {
        await ctx.Interaction.DeferAsync(true).ConfigureAwait(false);
        await ctx.Interaction.FollowupAsync(
            "If the message below loads infinitely, discord has limited the servers stickers upload limit. And no, this cant be circumvented with other bots (to my knowledge).");
        var eb = new EmbedBuilder
        {
            Description = Strings.AddingStickers(ctx.Guild.Id, config.Data.LoadingEmote),
            Color = Mewdeko.OkColor
        };
        var tags = message.Stickers.Select(x => x as SocketUnknownSticker).Distinct();
        if (!tags.Any())
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync(Strings.NoStickersInMessage(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        var errored = new List<string>();
        var emotes = new List<string>();
        await ctx.Interaction.FollowupAsync(embed: eb.Build(), ephemeral: true).ConfigureAwait(false);
        foreach (var i in tags)
        {
            using var http = httpFactory.CreateClient();
            using var sr = await http.GetAsync(i.GetStickerUrl(), HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            {
                try
                {
                    var emote = await ctx.Guild.CreateStickerAsync(i.Name, new Image(imgStream), [
                            "Mewdeko"
                        ], i.Description)
                        .ConfigureAwait(false);
                    emotes.Add($"{emote.Name} [Url]({emote.GetStickerUrl()})");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                    errored.Add($"{i.Name} | [Url]({i.GetStickerUrl()})");
                }
            }
        }

        var b = new EmbedBuilder
        {
            Color = Mewdeko.OkColor
        };
        if (emotes.Count > 0)
            b.WithDescription(Strings.AddedStickers(ctx.Guild.Id, string.Join("\n", emotes)));
        if (errored.Count > 0)
            b.AddField("Errored Stickers", string.Join("\n\n", errored));
        await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = b.Build()).ConfigureAwait(false);
    }
}