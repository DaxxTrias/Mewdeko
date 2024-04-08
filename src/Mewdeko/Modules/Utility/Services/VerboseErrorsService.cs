﻿using Discord.Commands;
using Mewdeko.Common.Collections;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Service for managing verbose error responses for commands.
/// </summary>
public class VerboseErrorsService : INService, IUnloadableService
{
    private readonly CommandHandler ch;
    private readonly DbService db;
    private readonly IBotStrings strings;
    private readonly ConcurrentHashSet<ulong> guildsEnabled;
    private readonly GuildSettingsService guildSettings;
    private readonly IServiceProvider services;
    private readonly BotConfigService botConfigService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerboseErrorsService"/> class.
    /// </summary>
    /// <param name="db">The database service.</param>
    /// <param name="ch">The command handler.</param>
    /// <param name="strings">The bot strings service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="botConfigService">The bot configuration service.</param>
    /// <param name="bot">The bot instance.</param>
    public VerboseErrorsService(DbService db, CommandHandler ch,
        IBotStrings strings,
        GuildSettingsService guildSettings,
        IServiceProvider services, BotConfigService botConfigService, Mewdeko bot)
    {
        this.strings = strings;
        this.guildSettings = guildSettings;
        this.services = services;
        this.botConfigService = botConfigService;
        this.db = db;
        this.ch = ch;
        var allgc = bot.AllGuildConfigs;
        this.ch.CommandErrored += LogVerboseError;

        guildsEnabled = new ConcurrentHashSet<ulong>(allgc
            .Where(x => x.VerboseErrors != 0)
            .Select(x => x.GuildId));
    }

    /// <summary>
    /// Unloads the service, detaching event handlers.
    /// </summary>
    public Task Unload()
    {
        ch.CommandErrored -= LogVerboseError;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs a detailed error when a command execution fails, providing additional context to the user.
    /// </summary>
    private async Task LogVerboseError(CommandInfo cmd, ITextChannel? channel, string reason, IUser user)
    {
        if (channel == null || !guildsEnabled.Contains(channel.GuildId))
            return;
        var perms = services.GetService<PermissionService>();
        var pc = await perms.GetCacheFor(channel.GuildId);
        foreach (var i in cmd.Aliases)
        {
            if (!(pc.Permissions != null
                  && pc.Permissions.CheckPermissions(new MewdekoUserMessage
                      {
                          Author = user, Channel = channel
                      },
                      i,
                      cmd.MethodName(), out _)))
                return;
        }

        try
        {
            var embed = new EmbedBuilder()
                .WithTitle("Command Error")
                .WithDescription(reason)
                .AddField("Usages",
                    string.Join("\n",
                        cmd.RealRemarksArr(strings, channel.Guild.Id, await guildSettings.GetPrefix(channel.Guild))))
                .WithFooter($"Run {await guildSettings.GetPrefix(channel.Guild.Id)}ve to disable these prompts.")
                .WithErrorColor();

            if (!botConfigService.Data.ShowInviteButton)
                await channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
            else
                await channel.SendMessageAsync(embed: embed.Build(), components: new ComponentBuilder()
                    .WithButton(label: "Support Server", style: ButtonStyle.Link,
                        url: botConfigService.Data.SupportServer).Build()).ConfigureAwait(false);
        }
        catch
        {
            //ignore
        }
    }

    /// <summary>
    /// Toggles the verbose error functionality for a guild, allowing users to enable or disable detailed command errors.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle verbose errors for.</param>
    /// <param name="enabled">Optionally specifies whether to enable or disable verbose errors. If null, toggles the current state.</param>
    /// <returns>True if verbose errors are enabled after the operation; otherwise, false.</returns>
    public async Task<bool> ToggleVerboseErrors(ulong guildId, bool? enabled = null)
    {
        await using (var uow = db.GetDbContext())
        {
            var gc = await uow.ForGuildId(guildId, set => set);

            long? longEnabled = enabled.HasValue ? (enabled.Value ? 1L : 0L) : null;

            if (longEnabled == null)
                longEnabled =
                    gc.VerboseErrors = gc.VerboseErrors == 0L ? 1L : 0L; // Old behaviour, now behind a condition
            else gc.VerboseErrors = (long)longEnabled; // New behaviour, just set it.

            await uow.SaveChangesAsync();
        }

        if (enabled != null && (bool)enabled) // This doesn't need to be duplicated inside the using block
            guildsEnabled.Add(guildId);
        else
            guildsEnabled.TryRemove(guildId);

        return (bool)enabled;
    }
}