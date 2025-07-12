using System.Net.Http;
using System.Text.RegularExpressions;
using Discord.Commands;
using Discord.Net;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Services.Settings;
using Serilog;
using Image = Discord.Image;

namespace Mewdeko.Modules.Server_Management;

/// <summary>
///     Contains commands related to server management.
/// </summary>
public partial class ServerManagement(IHttpClientFactory factory, BotConfigService config)
    : MewdekoModule
{
    /// <summary>
    ///     Displays the list of allowed permissions for the invoking user.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task PermView()
    {
        var perms = ((IGuildUser)ctx.User).GuildPermissions;
        var eb = new EmbedBuilder();
        eb.WithTitle(Strings.ListAllowedPerms(ctx.Guild.Id));
        eb.WithOkColor();
        var allowed = perms.ToList().Select(i => $"**{i}**").ToList();

        eb.WithDescription(string.Join("\n", allowed));
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays the list of allowed permissions for the specified user.
    /// </summary>
    /// <param name="user">The user whose permissions will be displayed.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [Priority(0)]
    public async Task PermView(IGuildUser user)
    {
        var perms = user.GuildPermissions;
        var eb = new EmbedBuilder();
        eb.WithTitle($"{Strings.ListAllowedPerms(ctx.Guild.Id)} for {user}");
        eb.WithOkColor();
        var allowed = perms.ToList().Select(i => $"**{i}**").ToList();

        eb.WithDescription(string.Join("\n", allowed));
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays the list of allowed permissions for the specified role.
    /// </summary>
    /// <param name="user">The role whose permissions will be displayed.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [Priority(1)]
    public async Task PermView(IRole user)
    {
        var perms = user.Permissions;
        var eb = new EmbedBuilder();
        eb.WithTitle($"{Strings.ListAllowedPerms(ctx.Guild.Id)} for {user}");
        eb.WithOkColor();
        var allowed = perms.ToList().Select(i => $"**{i}**").ToList();

        eb.WithDescription(string.Join("\n", allowed));
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the splash image of the server.
    /// </summary>
    /// <param name="img">The URL of the new splash image.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetSplash(string img)
    {
        var guild = ctx.Guild;
        var uri = new Uri(img);
        using var http = factory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await guild.ModifyAsync(x => x.Splash = new Image(imgStream)).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync(Strings.SplashImageSet(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the icon of the server.
    /// </summary>
    /// <param name="img">The URL of the new server icon.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetIcon(string img)
    {
        var guild = ctx.Guild;
        var uri = new Uri(img);
        using var http = factory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await guild.ModifyAsync(x => x.Icon = new Image(imgStream)).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync(Strings.ServerIconSet(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the banner of the server.
    /// </summary>
    /// <param name="img">The URL of the new server banner.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetBanner(string img)
    {
        var guild = ctx.Guild;
        var uri = new Uri(img);
        using var http = factory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await guild.ModifyAsync(x => x.Banner = new Image(imgStream)).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync(Strings.ServerBannerSet(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the name of the server.
    /// </summary>
    /// <param name="name">The new name for the server.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetServerName([Remainder] string name)
    {
        var guild = ctx.Guild;
        await guild.ModifyAsync(x => x.Name = name).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync(Strings.ServerNameSet(ctx.Guild.Id, name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a new emote to the server.
    /// </summary>
    /// <param name="name">The name of the emote.</param>
    /// <param name="url">The URL of the emote image. If not provided, the image will be taken from the message attachments.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageEmojisAndStickers)]
    [BotPerm(GuildPermission.ManageEmojisAndStickers)]
    [Priority(0)]
    public async Task AddEmote(string name, string? url = null)
    {
        string acturl;
        if (string.IsNullOrWhiteSpace(url))
        {
            var tags = ctx.Message.Attachments.FirstOrDefault();
            acturl = tags.Url;
        }
        else if (url.StartsWith("<"))
        {
            var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value);
            var result = tags.Select(m => m.Url);
            acturl = string.Join("", result);
        }
        else
        {
            acturl = url;
        }

        var uri = new Uri(acturl);
        using var http = factory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        try
        {
            var emote = await ctx.Guild.CreateEmoteAsync(name, new Image(imgStream)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"{emote} with the name {Format.Code(name)} created!")
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ctx.Channel.SendErrorAsync(
                    "The emote could not be added because it is either: Too Big(Over 256kb), is not a direct link, Or exceeds server emoji limit.",
                    Config)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes an emote from the server.
    /// </summary>
    /// <param name="_">Placeholder parameter to satisfy command signature requirements.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageEmojisAndStickers)]
    [BotPerm(GuildPermission.ManageEmojisAndStickers)]
    [RequireContext(ContextType.Guild)]
    public async Task RemoveEmote(string _)
    {
        var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value)
            .FirstOrDefault();
        try
        {
            var emote1 = await ctx.Guild.GetEmoteAsync(tags.Id).ConfigureAwait(false);
            await ctx.Guild.DeleteEmoteAsync(emote1).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.EmoteDeleted(ctx.Guild.Id, emote1)).ConfigureAwait(false);
        }
        catch (HttpException)
        {
            await ctx.Channel.SendErrorAsync(Strings.EmoteNotFromGuild(ctx.Guild.Id), Config).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Renames an existing emote on the server.
    /// </summary>
    /// <param name="emote">The existing emote to rename.</param>
    /// <param name="name">The new name for the emote.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageEmojisAndStickers)]
    [BotPerm(GuildPermission.ManageEmojisAndStickers)]
    [RequireContext(ContextType.Guild)]
    public async Task RenameEmote(string emote, string name)
    {
        if (name.StartsWith("<"))
        {
            await ctx.Channel.SendErrorAsync(Strings.EmoteInvalidName(ctx.Guild.Id), Config).ConfigureAwait(false);
            return;
        }

        var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value)
            .FirstOrDefault();
        try
        {
            var emote1 = await ctx.Guild.GetEmoteAsync(tags.Id).ConfigureAwait(false);
            var ogname = emote1.Name;
            await ctx.Guild.ModifyEmoteAsync(emote1, x => x.Name = name).ConfigureAwait(false);
            var emote2 = await ctx.Guild.GetEmoteAsync(tags.Id).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                    $"{emote1} has been renamed from {Format.Code(ogname)} to {Format.Code(emote2.Name)}")
                .ConfigureAwait(false);
        }
        catch (HttpException)
        {
            await ctx.Channel.SendErrorAsync(Strings.EmoteWrongGuild(ctx.Guild.Id), Config).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Steals emotes from a message and adds them to the server.
    /// </summary>
    /// <param name="e">The message containing the emotes to steal.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageEmojisAndStickers)]
    [BotPerm(GuildPermission.ManageEmojisAndStickers)]
    [Priority(1)]
    public async Task StealEmotes([Remainder] string e)
    {
        var eb = new EmbedBuilder   
        {
            Description = $"{config.Data.LoadingEmote} Adding Emotes...",
            Color = Mewdeko.OkColor
        };
        var errored = new List<string>();
        var emotes = new List<string>();
        var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value).Distinct();
        if (!tags.Any())
            return;
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);

        foreach (var i in tags)
        {
            var emoteName = i.Name; // Default to the emote name

            // Define a pattern to find the emote in the message
            var pattern = $"<a?:{i.Name}:[0-9]+>";
            var match = Regex.Match(ctx.Message.Content, pattern);

            if (match.Success && tags.Count() == 1)
            {
                // Find the index immediately after the emote match
                var indexAfterEmote = match.Index + match.Length;

                // Get the substring from the message that comes after the emote
                var potentialNamePart = ctx.Message.Content.Substring(indexAfterEmote).Trim();

                // Split the remaining message by spaces and take the first word if any
                var parts = potentialNamePart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // Use the provided name only if there is exactly one emote and one potential name
                if (parts.Length > 0)
                {
                    var candidateName = parts[0];
                    // Validate Discord emote name: 2-32 chars, alphanumeric/underscore only
                    if (Regex.IsMatch(candidateName, @"^[a-zA-Z0-9_]{2,32}$"))
                    {
                        emoteName = candidateName;
                    }
                }
            }

            using var http = factory.CreateClient();
            using var sr = await http.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();

            try
            {
                imgStream = ImageCompressor.EnsureImageUnder256Kb(imgData);
            }
            catch (InvalidOperationException)
            {
                errored.Add($"Unable to add '{i.Name}'. Image could not be compressed under 256kb.");
                continue;
            }

            await using var _ = imgStream.ConfigureAwait(false);
            {
                try
                {
                    var emote = await ctx.Guild.CreateEmoteAsync(emoteName, new Image(imgStream)).ConfigureAwait(false);
                    emotes.Add($"{emote} {Format.Code(emote.Name)}");
                }
                catch (HttpException httpEx) when (httpEx.HttpCode == System.Net.HttpStatusCode.BadRequest)
                {
                    // check if the error is 30008
                    if (httpEx.DiscordCode.HasValue && httpEx.DiscordCode.Value == (DiscordErrorCode)30008)
                    {
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
                    Log.Information($"Failed to add emotes. Message: {ex.Message}");
                    errored.Add($"{i.Name}\n{i.Url}");
                }
            }
        }

        var b = new EmbedBuilder
        {
            Color = Mewdeko.OkColor
        };
        if (emotes.Count > 0)
            b.WithDescription($"**Added Emotes**\n{string.Join("\n", emotes)}");
        if (errored.Count > 0)
            b.AddField("Errored Emotes", string.Join("\n\n", errored));
        await msg.ModifyAsync(x => x.Embed = b.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Steals emotes from a message and locks them to a specified role.
    /// </summary>
    /// <param name="role">The role to add the emotes to.</param>
    /// <param name="e">The message containing the emotes to steal.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageEmojisAndStickers)]
    [BotPerm(GuildPermission.ManageEmojisAndStickers)]
    [Priority(0)]
    public async Task StealForRole(IRole role, [Remainder] string e)
    {
        var eb = new EmbedBuilder
        {
            Description = $"{config.Data.LoadingEmote} Adding Emotes to {role.Mention}...", Color = Mewdeko.OkColor
        };
        var list = new Optional<IEnumerable<IRole>>([
            role
        ]);
        var errored = new List<string>();
        var emotes = new List<string>();
        var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value).Distinct();
        if (!tags.Any()) return;
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);

        foreach (var i in tags)
        {
            using var http = factory.CreateClient();
            using var sr = await http.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            {
                try
                {
                    var emote = await ctx.Guild.CreateEmoteAsync(i.Name, new Image(imgStream), list)
                        .ConfigureAwait(false);
                    emotes.Add($"{emote} {Format.Code(emote.Name)}");
                }
                catch (Exception)
                {
                    errored.Add($"{i.Name}\n{i.Url}");
                }
            }
        }

        var b = new EmbedBuilder
        {
            Color = Mewdeko.OkColor
        };
        if (emotes.Count > 0)
            b.WithDescription(Strings.AddedEmotesToRole(ctx.Guild.Id, emotes.Count, role.Mention,
                string.Join("\n", emotes)));
        if (errored.Count > 0) b.AddField($"{errored.Count} Errored Emotes", string.Join("\n\n", errored));
        await msg.ModifyAsync(x => x.Embed = b.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Steals stickers from a message and adds them to the server.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageEmojisAndStickers)]
    [BotPerm(GuildPermission.ManageEmojisAndStickers)]
    public async Task StealSticker([Remainder] string? _ = null)
    {
        var stickers = ctx.Message.Stickers;
        if (!stickers.Any())
        {
            await ctx.Channel.SendErrorAsync("Message contains no stickers.", Config).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder
        {
            Description = $"{config.Data.LoadingEmote} Adding Stickers...",
            Color = Mewdeko.OkColor
        };
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);

        var errored = new List<string>();
        var added = new List<string>();

        foreach (var sticker in stickers)
        {
            try
            {
                using var http = factory.CreateClient();
                await using var stream = await http.GetStreamAsync(sticker.GetStickerUrl());
                var ms = new System.IO.MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Position = 0;

                var createdSticker = await ctx.Guild.CreateStickerAsync(sticker.Name, new Image(ms), new[] { "Mewdeko" }, sticker.Name).ConfigureAwait(false);
                added.Add($"{createdSticker.Name} - [View]({createdSticker.GetStickerUrl()})");
            }
            catch (HttpException httpEx) when (httpEx.DiscordCode == (DiscordErrorCode)50138)
            {
                errored.Add($"Unable to add '{sticker.Name}'. Sticker file size is too large.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to add sticker {StickerName}", sticker.Name);
                errored.Add($"Unable to add '{sticker.Name}'. An unknown error occurred.");
            }
        }

        var b = new EmbedBuilder
        {
            Color = Mewdeko.OkColor
        };

        if (added.Any())
            b.WithDescription($"**Added Stickers**\n{string.Join("\n", added)}");
        else
            b.WithDescription("No stickers were added.");

        if (errored.Any())
            b.AddField("Errored Stickers", string.Join("\n", errored));

        await msg.ModifyAsync(x => x.Embed = b.Build()).ConfigureAwait(false);
    }
}
