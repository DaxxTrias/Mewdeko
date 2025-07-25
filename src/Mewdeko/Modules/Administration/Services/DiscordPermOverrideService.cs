﻿using DataModel;
using Discord.Commands;
using Discord.Interactions;
using LinqToDB;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using CommandInfo = Discord.Commands.CommandInfo;
using DiscordShardedClient = Discord.WebSocket.DiscordShardedClient;
using PreconditionResult = Discord.Commands.PreconditionResult;
using RequireUserPermissionAttribute = Discord.Commands.RequireUserPermissionAttribute;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service for managing Discord permission overrides.
/// </summary>
public class DiscordPermOverrideService : INService, ILateBlocker
{
    private readonly BotConfig botConfig;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IServiceProvider services;
    private ConcurrentDictionary<(ulong, string), DiscordPermOverride> overrides;

    /// <summary>
    ///     Constructs a new instance of the DiscordPermOverrideService.
    /// </summary>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="config">The bot config service.</param>
    public DiscordPermOverrideService(IDataConnectionFactory dbFactory, IServiceProvider services, BotConfig config)
    {
        this.botConfig = config;
        this.dbFactory = dbFactory;
        this.services = services;
        this.overrides = new ConcurrentDictionary<(ulong, string), DiscordPermOverride>();
        _ = StartService();
    }

    /// <summary>
    ///     The priority of the service.
    /// </summary>
    public int Priority { get; } = int.MaxValue;

    /// <summary>
    ///     Tries to block a command based on the permission overrides.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="context">The command context.</param>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="command">The command to block.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean indicating whether the
    ///     command was blocked.
    /// </returns>
    public async Task<bool> TryBlockLate(DiscordShardedClient client, ICommandContext context, string moduleName,
        CommandInfo command)
    {
        if (!TryGetOverrides(context.Guild?.Id ?? 0, command.MethodName(), out var perm)) return false;
        var result = await new RequireUserPermissionAttribute(perm)
            .CheckPermissionsAsync(context, command, services).ConfigureAwait(false);
        return !result.IsSuccess;
    }

    /// <summary>
    ///     Tries to block a command based on the permission overrides for interaction context.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="context">The interaction context.</param>
    /// <param name="command">The command to block.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean indicating whether the
    ///     command was blocked.
    /// </returns>
    public async Task<bool> TryBlockLate(DiscordShardedClient client, IInteractionContext context,
        ICommandInfo command)
    {
        if (!TryGetOverrides(context.Guild?.Id ?? 0, command.MethodName, out var perm)) return false;
        var result = await new Discord.Interactions.RequireUserPermissionAttribute(perm)
            .CheckRequirementsAsync(context, command, services).ConfigureAwait(false);
        if (!result.IsSuccess)
            await context.Interaction.SendEphemeralErrorAsync($"You need `{perm}` to use this command.", botConfig)
                .ConfigureAwait(false);
        return !result.IsSuccess;
    }

    private async Task StartService()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var allOverrides = await db.DiscordPermOverrides.ToListAsync().ConfigureAwait(false);
            var dict = allOverrides.ToDictionary(o => (o.GuildId ?? 0, o.Command), o => o);
            overrides = new ConcurrentDictionary<(ulong, string), DiscordPermOverride>(dict);
        }
        catch
        {
            overrides ??= new ConcurrentDictionary<(ulong, string), DiscordPermOverride>();
        }
    }

    /// <summary>
    ///     Tries to get the permission overrides for a specific command in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="commandName">The name of the command.</param>
    /// <param name="perm">The permission override for the command, if it exists.</param>
    /// <returns>A boolean indicating whether a permission override was found for the command.</returns>
    public bool TryGetOverrides(ulong guildId, string commandName, out GuildPermission perm)
    {
        commandName = commandName.ToLowerInvariant();
        if (overrides.TryGetValue((guildId, commandName), out var dpo))
        {
            perm = (GuildPermission)dpo.Perm; // Cast ulong from model to GuildPermission
            return true;
        }

        perm = default;
        return false;
    }

    /// <summary>
    ///     Executes the permission overrides for a specific command in a command context.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <param name="command">The command to execute the overrides for.</param>
    /// <param name="perms">The permissions to check.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the result of the permission
    ///     check.
    /// </returns>
    public static Task<PreconditionResult> ExecuteOverrides(ICommandContext ctx, CommandInfo command,
        GuildPermission perms, IServiceProvider services)
    {
        var rupa = new RequireUserPermissionAttribute(perms);
        return rupa.CheckPermissionsAsync(ctx, command, services);
    }

    /// <summary>
    ///     Executes the permission overrides for a specific command in an interaction context.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="command">The command to execute the overrides for.</param>
    /// <param name="perms">The permissions to check.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the result of the permission
    ///     check.
    /// </returns>
    public static Task<Discord.Interactions.PreconditionResult> ExecuteOverrides(IInteractionContext ctx,
        ICommandInfo command,
        GuildPermission perms, IServiceProvider services)
    {
        var rupa = new Discord.Interactions.RequireUserPermissionAttribute(perms);
        return rupa.CheckRequirementsAsync(ctx, command, services);
    }

    /// <summary>
    ///     Adds a permission override for a specific command in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="commandName">The name of the command.</param>
    /// <param name="perm">The permission to override with.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<DiscordPermOverride> AddOverride(ulong guildId, string commandName, GuildPermission perm)
    {
        commandName = commandName.ToLowerInvariant();
        await using var db = await dbFactory.CreateConnectionAsync();
        var over = await db.DiscordPermOverrides
            .FirstOrDefaultAsync(x => x.GuildId == guildId && commandName == x.Command).ConfigureAwait(false);

        if (over is null)
        {
            over = new DiscordPermOverride
            {
                Command = commandName, Perm = (ulong)perm, GuildId = guildId
            };
            await db.InsertAsync(over).ConfigureAwait(false);
        }
        else
        {
            over.Perm = (ulong)perm;
            await db.UpdateAsync(over).ConfigureAwait(false);
        }

        overrides[(guildId, commandName)] = over;

        return over;
    }

    /// <summary>
    ///     Clears all permission overrides for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ClearAllOverrides(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var commandsToRemove = await db.DiscordPermOverrides
            .Where(x => x.GuildId == guildId)
            .Select(x => x.Command)
            .ToListAsync().ConfigureAwait(false);

        await db.DiscordPermOverrides
            .Where(x => x.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);

        foreach (var cmd in commandsToRemove)
        {
            overrides.TryRemove((guildId, cmd), out _);
        }
    }

    /// <summary>
    ///     Removes a permission override for a specific command in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="commandName">The name of the command.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RemoveOverride(ulong guildId, string commandName)
    {
        commandName = commandName.ToLowerInvariant();
        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.DiscordPermOverrides
            .Where(x => x.GuildId == guildId && x.Command == commandName)
            .DeleteAsync().ConfigureAwait(false);

        if (deletedCount > 0)
        {
            overrides.TryRemove((guildId, commandName), out _);
        }
    }

    /// <summary>
    ///     Retrieves all permission overrides for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>An enumerable of DiscordPermOverride objects representing all permission overrides for the guild.</returns>
    public async Task<IEnumerable<DiscordPermOverride>> GetAllOverrides(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var results = await db.DiscordPermOverrides
            .Where(x => x.GuildId == guildId)
            .ToListAsync().ConfigureAwait(false);
        return results;
    }
}