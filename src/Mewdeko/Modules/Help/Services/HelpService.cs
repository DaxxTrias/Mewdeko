﻿using System.Reflection;
using System.Text;
using CommandLine;
using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Mewdeko.Services.Strings;
using MoreLinq;
using ModuleInfo = Discord.Commands.ModuleInfo;

namespace Mewdeko.Modules.Help.Services;

/// <summary>
///     A service for handling help commands.
/// </summary>
public class HelpService : INService
{
    private readonly BlacklistService blacklistService;
    private readonly Mewdeko bot;
    private readonly BotConfigService bss;
    private readonly DiscordShardedClient client;
    private readonly CommandService cmds;
    private readonly DiscordPermOverrideService dpos;
    private readonly GeneratedBotStrings genStrings;
    private readonly GuildSettingsService guildSettings;
    private readonly InteractionService interactionService;
    private readonly PermissionService nPerms;
    private readonly GlobalPermissionService perms;
    private readonly IBotStrings strings;


    /// <summary>
    ///     Initializes a new instance of <see cref="HelpService" />.
    /// </summary>
    /// <param name="strings">Bot localization strings</param>
    /// <param name="dpos">Permission override service for commands</param>
    /// <param name="bss">Settings service for yml based configs</param>
    /// <param name="client">The discord client</param>
    /// <param name="bot">The bot itself</param>
    /// <param name="blacklistService">The user/server blacklist service</param>
    /// <param name="cmds">The command service</param>
    /// <param name="perms">The global permissions service</param>
    /// <param name="nPerms">The per server permission service</param>
    /// <param name="interactionService">The discord interaction service</param>
    /// <param name="guildSettings">Service to get guild configs</param>
    /// <param name="eventHandler">The event handler Sylveon made because the events in dnet were single threaded.</param>
    /// <param name="genStrings">The class that holds generated locale strings.</param>
    public HelpService(
        IBotStrings strings,
        DiscordPermOverrideService dpos,
        BotConfigService bss,
        DiscordShardedClient client,
        Mewdeko bot,
        BlacklistService blacklistService,
        CommandService cmds,
        GlobalPermissionService perms,
        PermissionService nPerms,
        InteractionService interactionService,
        GuildSettingsService guildSettings, EventHandler eventHandler, GeneratedBotStrings genStrings)
    {
        this.dpos = dpos;
        this.strings = strings;
        this.client = client;
        this.bot = bot;
        this.blacklistService = blacklistService;
        this.cmds = cmds;
        this.bss = bss;
        eventHandler.Subscribe("MessageReceived", "HelpService", HandlePing);
        eventHandler.Subscribe("JoinedGuild", "HelpService", HandleJoin);
        this.perms = perms;
        this.nPerms = nPerms;
        this.interactionService = interactionService;
        this.guildSettings = guildSettings;
        this.genStrings = genStrings;
    }

    /// <summary>
    ///     Executes the help text when someone attempts to dm the bot with a bad command
    /// </summary>
    /// <param name="DiscordShardedClient">The client</param>
    /// <param name="guild">The guild (hopefully null otherwise this method is useless)</param>
    /// <param name="msg">The message of the user</param>
    /// <returns></returns>
    public async Task BadCommand(DiscordShardedClient DiscordShardedClient, IGuild? guild, IUserMessage msg)
    {
        var settings = bss.Data;
        if (guild != null) return;
        if (string.IsNullOrWhiteSpace(settings.DmHelpText) || settings.DmHelpText == "-")
            return;
        var replacer = new ReplacementBuilder()
            .WithDefault(msg.Author, msg.Channel, null, DiscordShardedClient).Build();
        if (SmartEmbed.TryParse(replacer.Replace(settings.DmHelpText), null, out var embed, out var plainText,
                out var components))
            await msg.Channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
        else
            await msg.Channel.SendMessageAsync(settings.DmHelpText);
    }

    /// <summary>
    ///     Builds the select menus for the modules
    /// </summary>
    /// <param name="guild">The guild the help menu was executed in, may be null if in dm</param>
    /// <param name="user">The user that executed the help menu</param>
    /// <param name="descriptions">Whether descriptions are on or off</param>
    /// <returns>A <see cref="ComponentBuilder" /> instance with the bots modules in it</returns>
    public ComponentBuilder GetHelpComponents(IGuild? guild, IUser user, bool descriptions = true)
    {
        var modules = cmds.Commands.Select(x => x.Module).Where(x => !x.IsSubmodule).Distinct();
        var compBuilder = new ComponentBuilder();
        var menuCount = (modules.Count() - 1) / 25 + 1;

        for (var j = 0; j < menuCount; j++)
        {
            var selMenu = new SelectMenuBuilder().WithCustomId($"helpselect:{j}");
            foreach (var i in modules.Skip(j * 25).Take(25)
                         .Where(x => !x.Attributes.Any(attribute => attribute is HelpDisabled)))
            {
                selMenu.Options.Add(new SelectMenuOptionBuilder()
                    .WithLabel(i.Name).WithDescription(GetModuleDescription(i.Name, guild))
                    .WithValue(i.Name.ToLower()));
            }

            compBuilder.WithSelectMenu(selMenu); // add the select menu to the component builder
        }

        compBuilder.WithButton(genStrings.ToggleDescriptions(guild?.Id ?? 0),
            $"toggle-descriptions:{descriptions},{user.Id}");
        compBuilder.WithButton(genStrings.InviteMe(guild?.Id ?? 0), style: ButtonStyle.Link,
            url:
            "https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303&scope=bot%20applications.commands");
        compBuilder.WithButton(genStrings.Donatetext(guild?.Id ?? 0), style: ButtonStyle.Link,
            url: "https://ko-fi.com/mewdeko");
        return compBuilder;
    }


    /// <summary>
    ///     Builds the help embed for the help menu
    /// </summary>
    /// <param name="description">Whether descriptions for each module are on or off</param>
    /// <param name="guild">The guild where the help menu was executed</param>
    /// <param name="channel">The channel where the help menu was executed</param>
    /// <param name="user">The user who executed the help menu</param>
    /// <returns></returns>
    public async Task<EmbedBuilder> GetHelpEmbed(bool description, IGuild? guild, IMessageChannel channel, IUser user)
    {
        var prefix = await guildSettings.GetPrefix(guild);
        EmbedBuilder embed = new();
        embed.WithAuthor(new EmbedAuthorBuilder()
            .WithName(genStrings.HelpmenuHelptext(guild?.Id ?? 0, client.CurrentUser))
            .WithIconUrl(client.CurrentUser.RealAvatarUrl().AbsoluteUri));
        embed.WithOkColor();
        embed.WithDescription(
            genStrings.CommandHelpDescription(guild?.Id ?? 0, prefix) +
            $"\n{genStrings.ModuleHelpDescription(guild?.Id ?? 0, prefix)}" +
            "\n\n**Youtube Tutorials**\nhttps://www.youtube.com/channel/UCKJEaaZMJQq6lH33L3b_sTg\n\n**Links**\n" +
            $"[Documentation](https://mewdeko.tech) | [Support Server]({bss.Data.SupportServer}) | [Invite Me](https://discord.com/oauth2/authorize?client_id={bot.Client.CurrentUser.Id}&scope=bot&permissions=66186303&scope=bot%20applications.commands) | [Top.gg Listing](https://top.gg/bot/752236274261426212) | [Donate!](https://ko-fi.com/mewdeko)");
        var modules = cmds.Commands.Select(x => x.Module)
            .Where(x => !x.IsSubmodule && !x.Attributes.Any(attribute => attribute is HelpDisabled)).Distinct();
        var count = 0;
        if (description)
        {
            foreach (var mod in modules)
            {
                embed.AddField($"{await CheckEnabled(guild?.Id, channel, user, mod.Name)} {mod.Name}",
                    $">>> {GetModuleDescription(mod.Name, guild)}", true);
            }
        }
        else
        {
            foreach (var i in modules.Batch(modules.Count() / 2))
            {
                var categoryStrings = await Task.WhenAll(i.Select(x =>
                    CheckEnabled(guild?.Id, channel, user, x.Name)
                        .ContinueWith(task => $"> {task.Result} {Format.Bold(x.Name)}")
                ));

                embed.AddField(
                    count == 0 ? "Categories" : "_ _",
                    string.Join("\n", categoryStrings),
                    true
                );
                count++;
            }
        }

        return embed;
    }

    private async Task<string> CheckEnabled(ulong? guildId, IMessageChannel channel, IUser user, string moduleName)
    {
        if (!guildId.HasValue)
            return "✅";
        var pc = await nPerms.GetCacheFor(guildId.Value);
        if (perms.BlockedModules.Contains(moduleName.ToLower())) return "🌐❌";
        return !pc.Permissions.CheckSlashPermissions(moduleName, "none", user, channel, out _) ? "❌" : "✅";
    }

    private string? GetModuleDescription(string module, IGuild? guild)
    {
        return module.ToLower() switch
        {
            "administration" => genStrings.ModuleDescriptionAdministration(guild?.Id ?? 0),
            "afk" => genStrings.ModuleDescriptionAfk(guild?.Id ?? 0),
            "chattriggers" => genStrings.ModuleDescriptionChattriggers(guild?.Id ?? 0),
            "confessions" => genStrings.ModuleDescriptionConfessions(guild?.Id ?? 0),
            "currency" => genStrings.ModuleDescriptionCurrency(guild?.Id ?? 0),
            "gambling" => genStrings.ModuleDescriptionGambling(guild?.Id ?? 0),
            "games" => genStrings.ModuleDescriptionGames(guild?.Id ?? 0),
            "giveaways" => genStrings.ModuleDescriptionGiveaways(guild?.Id ?? 0),
            "help" => genStrings.ModuleDescriptionHelp(guild?.Id ?? 0),
            "highlights" => genStrings.ModuleDescriptionHighlights(guild?.Id ?? 0),
            "multigreets" => genStrings.ModuleDescriptionMultigreets(guild?.Id ?? 0),
            "music" => genStrings.ModuleDescriptionMusic(guild?.Id ?? 0),
            "nsfw" => genStrings.ModuleDescriptionNsfw(guild?.Id ?? 0),
            "owneronly" => genStrings.ModuleDescriptionOwneronly(guild?.Id ?? 0),
            "permissions" => genStrings.ModuleDescriptionPermissions(guild?.Id ?? 0),
            "rolegreets" => genStrings.ModuleDescriptionRolegreets(guild?.Id ?? 0),
            "rolestates" => genStrings.ModuleDescriptionRolestates(guild?.Id ?? 0),
            "searches" => genStrings.ModuleDescriptionSearches(guild?.Id ?? 0),
            "servermanagement" => genStrings.ModuleDescriptionServermanagement(guild?.Id ?? 0),
            "starboard" => genStrings.ModuleDescriptionStarboard(guild?.Id ?? 0),
            "statusroles" => genStrings.ModuleDescriptionStatusroles(guild?.Id ?? 0),
            "suggestions" => genStrings.ModuleDescriptionSuggestions(guild?.Id ?? 0),
            "userprofile" => genStrings.ModuleDescriptionUserprofile(guild?.Id ?? 0),
            "utility" => genStrings.ModuleDescriptionUtility(guild?.Id ?? 0),
            "vote" => genStrings.ModuleDescriptionVote(guild?.Id ?? 0),
            "xp" => genStrings.ModuleDescriptionXp(guild?.Id ?? 0),
            _ => null
        };
    }

    private async Task HandlePing(SocketMessage msg)
    {
        if (msg.Content == $"<@{client.CurrentUser.Id}>" || msg.Content == $"<@!{client.CurrentUser.Id}>")
        {
            if (msg.Channel is ITextChannel chan)
            {
                var cb = new ComponentBuilder();
                var prefix = await guildSettings.GetPrefix(chan.Guild);
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(
                    $"Hi there! To see my command categories do `{prefix}cmds`\nMy current Prefix is `{prefix}`\nIf you need help using the bot feel free to join the [Support Server]({bss.Data.SupportServer})!\n**Please support me! While this bot is free it's not free to run! https://ko-fi.com/mewdeko**\n\n I hope you have a great day!");
                eb.WithThumbnailUrl("https://cdn.discordapp.com/emojis/914307922287276052.gif");
                eb.WithFooter(new EmbedFooterBuilder().WithText(client.CurrentUser.Username)
                    .WithIconUrl(client.CurrentUser.RealAvatarUrl().ToString()));

                if (bss.Data.ShowInviteButton)
                    cb.WithButton("Invite Me!", style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands")
                        .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko");

                await chan.SendMessageAsync(embed: eb.Build(),
                    components: bss.Data.ShowInviteButton ? cb.Build() : null).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleJoin(IGuild guild)
    {
        if (blacklistService.BlacklistEntries.Select(x => x.ItemId).Contains(guild.Id))
            return;

        var cb = new ComponentBuilder();
        var e = await guild.GetDefaultChannelAsync();
        var px = await guildSettings.GetPrefix(guild);
        var eb = new EmbedBuilder
        {
            Description =
                $"Hi, thanks for inviting Mewdeko! I hope you like the bot, and discover all its features! The default prefix is `{px}.` This can be changed with the prefix command."
        };
        eb.AddField("How to look for commands",
            $"1) Use the {px}cmds command to see all the categories\n2) use {px}cmds with the category name to glance at what commands it has. ex: `{px}cmds mod`\n3) Use {px}h with a command name to view its help. ex: `{px}h purge`");
        eb.AddField("Have any questions, or need my invite link?",
            "Support Server: https://discord.gg/mewdeko \nInvite Link: https://mewdeko.tech/invite");
        eb.AddField("Youtube Channel", "https://youtube.com/channel/UCKJEaaZMJQq6lH33L3b_sTg");
        eb.WithThumbnailUrl(
            "https://cdn.discordapp.com/emojis/968564817784877066.gif");
        eb.WithOkColor();
        if (bss.Data.ShowInviteButton)
            cb.WithButton("Invite Me!", style: ButtonStyle.Link,
                    url:
                    "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands")
                .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko");
        await e.SendMessageAsync(embed: eb.Build(), components: bss.Data.ShowInviteButton ? cb.Build() : null)
            .ConfigureAwait(false);
    }


    /// <summary>
    ///     Gets the help for a command
    /// </summary>
    /// <param name="com">The command in question</param>
    /// <param name="guild">The guild where this was executed</param>
    /// <param name="user">The user who executed the command</param>
    /// <returns>A tuple containing a <see cref="EmbedBuilder" /> and <see cref="ComponentBuilder" /></returns>
    public async Task<(EmbedBuilder, ComponentBuilder)> GetCommandHelp(CommandInfo com, IGuild? guild, IGuildUser user)
    {
        var actualUrl = GenerateDocumentationUrl(com);
        if (com.Attributes.Any(x => x is HelpDisabled))
            return (new EmbedBuilder().WithDescription(genStrings.HelpDisabled(guild?.Id ?? 0)),
                new ComponentBuilder());

        var prefix = await guildSettings.GetPrefix(guild);
        var potentialCommand = interactionService.SlashCommands.FirstOrDefault(x =>
            string.Equals(x.MethodName, com.MethodName(), StringComparison.CurrentCultureIgnoreCase));

        var str = $"**{prefix + com.Aliases[0]}**";
        var alias = com.Aliases.Skip(1).FirstOrDefault();
        if (alias != null)
            str += $" **| {prefix + alias}**";

        var em = new EmbedBuilder().AddField(fb =>
            fb.WithName(str).WithValue($"{com.RealSummary(strings, guild?.Id, prefix)}").WithIsInline(true));

        var tryGetOverrides = dpos.TryGetOverrides(guild.Id, com.Name, out var overrides);
        var reqs = GetCommandRequirements(com, tryGetOverrides ? overrides : null);
        var botReqs = GetCommandBotRequirements(com);
        var attribute = (RatelimitAttribute)com.Preconditions.FirstOrDefault(x => x is RatelimitAttribute);

        if (reqs.Length > 0)
            em.AddField("User Permissions", string.Join("\n", reqs));
        if (botReqs.Length > 0)
            em.AddField("Bot Permissions", string.Join("\n", botReqs));
        if (actualUrl != null)
            em.AddField("Documentation", $"[Click here]({actualUrl})");
        if (attribute?.Seconds > 0)
            em.AddField("Cooldown", $"{attribute.Seconds} seconds");

        var cb = new ComponentBuilder()
            .WithButton(genStrings.HelpRunCmd(guild?.Id ?? 0), $"runcmd.{com.Aliases[0]}", ButtonStyle.Success);

        if (user.GuildPermissions.Administrator)
            cb.WithButton(genStrings.HelpPermenuLink(guild.Id), $"permenu_update.{com.Aliases[0]}", ButtonStyle
                .Primary, Emote.Parse("<:IconPrivacySettings:845090111976636446>"));

        if (potentialCommand is not null)
        {
            var globalCommands = await client.Rest.GetGlobalApplicationCommands();
            var guildCommands = await client.Rest.GetGuildApplicationCommands(guild.Id);
            var globalCommand = globalCommands.FirstOrDefault(x => x.Name == potentialCommand.Module.SlashGroupName);
            var guildCommand = guildCommands.FirstOrDefault(x => x.Name == potentialCommand.Module.SlashGroupName);
            if (globalCommand is not null)
                em.AddField("Slash Command",
                    potentialCommand == null
                        ? "`None`"
                        : $"</{potentialCommand.Module.SlashGroupName} {potentialCommand.Name}:{globalCommand.Id}>");
            else if (guildCommand is not null)
                em.AddField("Slash Command",
                    potentialCommand == null
                        ? "`None`"
                        : $"</{potentialCommand.Module.SlashGroupName} {potentialCommand.Name}:{guildCommand.Id}>");
        }

        // Get command strings from YAML documentation
        var commandStrings = strings.GetCommandStrings(com.Name, guild?.Id);

        // Add parameter descriptions if available
        if (commandStrings.Parameters?.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var param in commandStrings.Parameters)
            {
                var optionalText = param.IsOptional
                    ? $" (Optional{(string.IsNullOrEmpty(param.DefaultValue) ? "" : $", default: {param.DefaultValue}")})"
                    : "";

                sb.AppendLine($"• `{param.Name}`{optionalText}: {param.Description}");
            }

            em.AddField(genStrings.Parameters(guild?.Id ?? 0), sb.ToString());
        }

        // Add overload information if available
        if (commandStrings.Overloads?.Count > 0)
        {
            var sb = new StringBuilder();

            // Show the main command format first
            var mainParams = string.Join(" ", com.Parameters.Select(p =>
                p.IsOptional ? $"[{p.Name}]" : p.Name));
            sb.AppendLine($"**{prefix}{com.Name} {mainParams}**");

            // Show overloads
            sb.AppendLine("\n**Other versions:**");

            foreach (var overload in commandStrings.Overloads)
            {
                var overloadParams = string.Join(" ", overload.Parameters.Select(p =>
                    p.IsOptional ? $"[{p.Name}]" : p.Name));

                sb.AppendLine($"• **{prefix}{com.Name} {overloadParams}**");

                // Add detailed parameter descriptions for this overload if needed
                if (overload.Parameters?.Count > 0)
                {
                    foreach (var param in overload.Parameters)
                    {
                        var optionalText = param.IsOptional
                            ? $" (Optional{(string.IsNullOrEmpty(param.DefaultValue) ? "" : $", default: {param.DefaultValue}")})"
                            : "";

                        sb.AppendLine($"  → `{param.Name}`{optionalText}: {param.Description}");
                    }
                }
            }

            em.AddField(genStrings.Overloads(guild?.Id ?? 0), sb.ToString());
        }

        em.AddField(fb => fb.WithName(genStrings.Usage(guild.Id)).WithValue(string.Join("\n",
                    Array.ConvertAll(com.RealRemarksArr(strings, guild?.Id, prefix),
                        arg => Format.Code(arg))))
                .WithIsInline(false))
            .WithFooter(
                $"Module: {com.Module.GetTopLevelModule().Name} || Submodule: {com.Module.Name.Replace("Commands", "")} || Method Name: {com.MethodName()}")
            .WithColor(Mewdeko.OkColor);

        var opt = ((MewdekoOptionsAttribute)com.Attributes.FirstOrDefault(x => x is MewdekoOptionsAttribute))
            ?.OptionType;
        if (opt == null) return (em, cb);
        var hs = GetCommandOptionHelp(opt);
        if (!string.IsNullOrWhiteSpace(hs))
            em.AddField(genStrings.Options(guild.Id), hs);

        if (bss.Data.ShowInviteButton)
            cb.WithButton(style: ButtonStyle.Link,
                    url:
                    "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                    label: "Invite Me!",
                    emote: "<a:HaneMeow:968564817784877066>".ToIEmote())
                .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/Mewdeko");

        return (em, cb);
    }

    private static string GetCommandOptionHelp(Type opt)
    {
        var strs = GetCommandOptionHelpList(opt);

        return string.Join("\n", strs);
    }

    private static List<string> GetCommandOptionHelpList(Type opt)
    {
        return opt.GetProperties()
            .Select(x => Array.Find(x.GetCustomAttributes(true), a => a is OptionAttribute))
            .Where(x => x != null).Cast<OptionAttribute>().Select(x =>
            {
                var toReturn = $"`--{x.LongName}`";

                if (!string.IsNullOrWhiteSpace(x.ShortName))
                    toReturn += $" (`-{x.ShortName}`)";

                toReturn += $"   {x.HelpText}  ";
                return toReturn;
            }).ToList();
    }

    private static string[] GetCommandRequirements(CommandInfo cmd, GuildPermission? overrides = null)
    {
        var toReturn = new List<string>();

        if (cmd.Preconditions.Any(x => x is OwnerOnlyAttribute))
            toReturn.Add("Bot Owner Only");

        var userPerm = (UserPermAttribute)cmd.Preconditions.FirstOrDefault(ca => ca is UserPermAttribute);

        var userPermString = string.Empty;
        if (userPerm is not null)
        {
            if (userPerm.UserPermissionAttribute.ChannelPermission is { } cPerm)
                userPermString = GetPreconditionString(cPerm);
            if (userPerm.UserPermissionAttribute.GuildPermission is { } gPerm)
                userPermString = GetPreconditionString(gPerm);
        }

        if (overrides is null)
        {
            if (!string.IsNullOrWhiteSpace(userPermString))
                toReturn.Add(userPermString);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(userPermString))
                toReturn.Add(Format.Strikethrough(userPermString));

            toReturn.Add(GetPreconditionString(overrides.Value));
        }

        return toReturn.ToArray();
    }

    /// <summary>
    /// </summary>
    /// <param name="commandName"></param>
    /// <param name="overloads"></param>
    /// <param name="prefix"></param>
    /// <returns></returns>
    public string FormatCommandHelp(string commandName, List<OwnerOnlyService.CommandInfo> overloads, string prefix)
    {
        var sb = new StringBuilder();

        // If there's only one version, format it normally
        if (overloads.Count == 1 && !overloads[0].IsOverload)
        {
            var cmd = overloads[0];
            sb.AppendLine($"**{prefix}{commandName}**");
            sb.AppendLine(cmd.Desc);

            // Add usage examples
            if (cmd.Args.Count > 0)
            {
                sb.AppendLine("\n**Usage:**");
                foreach (var usage in cmd.Args)
                {
                    sb.AppendLine($"`{prefix}{commandName} {usage}`");
                }
            }

            // Add parameter descriptions if available
            if (cmd.Parameters.Count > 0)
            {
                sb.AppendLine("\n**Parameters:**");
                foreach (var param in cmd.Parameters)
                {
                    var optional = param.IsOptional
                        ? " (Optional" + (param.DefaultValue != null ? $", default: {param.DefaultValue}" : "") + ")"
                        : "";
                    var paramDesc = !string.IsNullOrEmpty(param.Description) ? $" - {param.Description}" : "";
                    sb.AppendLine($"• `{param.Name}`: {param.Type}{optional}{paramDesc}");
                }
            }
        }
        else
        {
            // Multiple overloads
            sb.AppendLine($"**{prefix}{commandName}** (Multiple Versions)");

            // Add the first description (they should be similar)
            sb.AppendLine(overloads[0].Desc);

            // Show each overload
            sb.AppendLine("\n**Overloads:**");

            for (var i = 0; i < overloads.Count; i++)
            {
                var cmd = overloads[i];

                // Format parameters for this overload
                var paramList = string.Join(", ", cmd.Parameters.Select(p =>
                {
                    var paramString = $"{p.Name}: {p.Type}";
                    if (p.IsOptional) paramString = $"[{paramString}]";
                    return paramString;
                }));

                sb.AppendLine($"\n**Version {i + 1}:** `{prefix}{commandName} {paramList}`");

                // Add parameter descriptions
                if (cmd.Parameters.Count > 0)
                {
                    sb.AppendLine("Parameters:");
                    foreach (var param in cmd.Parameters)
                    {
                        var optional = param.IsOptional
                            ? " (Optional" + (param.DefaultValue != null ? $", default: {param.DefaultValue}" : "") +
                              ")"
                            : "";
                        var paramDesc = !string.IsNullOrEmpty(param.Description) ? $" - {param.Description}" : "";
                        sb.AppendLine($"• `{param.Name}`: {param.Type}{optional}{paramDesc}");
                    }
                }

                // Add usage examples for this overload
                if (cmd.Args.Count > 0)
                {
                    sb.AppendLine("Examples:");
                    foreach (var usage in cmd.Args)
                    {
                        sb.AppendLine($"`{prefix}{commandName} {usage}`");
                    }
                }
            }
        }

        return sb.ToString();
    }

    private static string[] GetCommandBotRequirements(CommandInfo cmd)
    {
        var toReturn = new List<string>();

        if (cmd.Preconditions.Any(x => x is OwnerOnlyAttribute))
            toReturn.Add("Bot Owner Only");

        var botPerm = (BotPermAttribute)cmd.Preconditions.FirstOrDefault(ca => ca is BotPermAttribute);

        var botPermString = string.Empty;
        if (botPerm is not null)
        {
            if (botPerm.ChannelPermission is { } cPerm)
                botPermString = GetPreconditionString(cPerm);
            if (botPerm.GuildPermission is { } gPerm)
                botPermString = GetPreconditionString(gPerm);
        }

        if (!string.IsNullOrWhiteSpace(botPermString))
            toReturn.Add(botPermString);

        return toReturn.ToArray();
    }

    private static string? GenerateDocumentationUrl(CommandInfo com)
    {
        const string baseUrl = "https://docs.mewdeko.tech/api/";

        // Get the module's type
        Type moduleType = null;
        if (com.Module is ModuleInfo moduleInfo)
        {
            var assembly = typeof(Mewdeko).Assembly;
            var possibleTypes = assembly.GetTypes()
                .Where(t =>
                    t.IsSubclassOf(typeof(MewdekoSubmodule)) || t.IsSubclassOf(typeof(MewdekoModule)))
                .Where(t => t.Name.Equals(moduleInfo.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            moduleType = possibleTypes.Count switch
            {
                1 => possibleTypes[0],
                > 1 => possibleTypes.FirstOrDefault(t => !t.IsNested) ?? possibleTypes[0],
                _ => null
            };

            if (moduleType == null)
            {
                // If we still can't find the type, we can't generate the URL
                return null;
            }
        }
        else
        {
            // Fallback to the type of Module if it's not ModuleInfo
            moduleType = com.Module.GetType();
        }

        // Get the method name
        var methodName = com.Name;

        // Get the parameter types
        var parameterTypes = com.Parameters.Select(p => p.Type).ToArray();

        // Find the MethodInfo
        var methodInfo = moduleType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            parameterTypes,
            null);

        if (methodInfo == null)
        {
            // Handle method overloads
            var methods = moduleType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name.Equals(com.MethodName(), StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var method in methods)
            {
                var methodParams = method.GetParameters();
                if (methodParams.Length != parameterTypes.Length) continue;
                var parametersMatch =
                    !parameterTypes.Select((t, i) => new
                        {
                            Type = t, Index = i
                        })
                        .Any(x => !x.Type.IsAssignableFrom(methodParams[x.Index].ParameterType));
                if (!parametersMatch) continue;
                methodInfo = method;
                break;
            }
        }

        if (methodInfo == null)
        {
            // Can't find method info
            return null;
        }

        // Adjust the class full name for the URL
        var classFullNameForUrl = moduleType.FullName.Replace('+', '.');

        // Construct the class URL
        var classUrl = baseUrl + classFullNameForUrl + ".html";

        // Generate the anchor
        var typeAnchor = moduleType.FullName.Replace('+', '_').Replace('.', '_');
        var methodAnchorName = methodInfo.Name;
        var anchor = $"{typeAnchor}_{methodAnchorName}";

        // Get parameter types for anchor
        var methodParameterTypes = methodInfo.GetParameters().Select(p => p.ParameterType);

        if (methodParameterTypes.Any())
        {
            // Append parameter types to the anchor
            var parameterAnchor = string.Join("_", methodParameterTypes.Select(FormatParameterType));
            anchor += $"_{parameterAnchor}_"; // Note the extra underscore at the end
        }

        // Construct the full URL
        var actualUrl = $"{classUrl}#{anchor}";
        return actualUrl;
    }

    private static string FormatParameterType(Type type)
    {
        // Handle arrays
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            var formattedElementType = FormatParameterType(elementType);
            // Use triple underscores for arrays
            var underscores = new string('_', type.GetArrayRank() * 3);
            return $"{formattedElementType}{underscores}";
        }

        // Handle generic types
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericTypeName = genericTypeDef.FullName.Split('`')[0].Replace('+', '.').Replace('.', '_');
            var genericArgs = string.Join("_", type.GetGenericArguments().Select(FormatParameterType));
            return $"{genericTypeName}_{genericArgs}";
        }

        // Handle nested types and replace '+' with '.'
        var fullName = type.FullName.Replace('+', '.').Replace('.', '_');
        return fullName;
    }


    private static string GetPreconditionString(ChannelPermission perm)
    {
        return (perm + " Channel Permission").Replace("Guild", "Server", StringComparison.InvariantCulture);
    }

    private static string GetPreconditionString(GuildPermission perm)
    {
        return (perm + " Server Permission").Replace("Guild", "Server", StringComparison.InvariantCulture);
    }

    private string? GetText(string? text, IGuild? guild, params object?[] replacements)
    {
        return strings.GetText(text, guild?.Id, replacements);
    }
}