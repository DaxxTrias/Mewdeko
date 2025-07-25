﻿namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service for managing game voice channels.
/// </summary>
public class GameVoiceChannelService : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<GameVoiceChannelService> logger;


    /// <summary>
    ///     Constructs a new instance of the GameVoiceChannelService.
    /// </summary>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="eventHandler">The event handler.</param>
    public GameVoiceChannelService(IDataConnectionFactory dbFactory,
        GuildSettingsService guildSettings, EventHandler eventHandler, ILogger<GameVoiceChannelService> logger)
    {
        this.dbFactory = dbFactory;
        this.guildSettings = guildSettings;
        this.logger = logger;

        eventHandler.Subscribe("UserVoiceStateUpdated", "GameVoiceChannelService", Client_UserVoiceStateUpdated);
        eventHandler.Subscribe("GuildMemberUpdated", "GameVoiceChannelService", _client_GuildMemberUpdated);
    }

    /// <summary>
    ///     Handles the GuildMemberUpdated event.
    /// </summary>
    /// <param name="cacheable">The cacheable guild user.</param>
    /// <param name="after">The guild user after the update.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task _client_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser? after)
    {
        try
        {
            if (after is null)
                return;

            var gConfig = await guildSettings.GetGuildConfig(after.Guild.Id);
            if (gConfig.GameVoiceChannel != after.VoiceChannel?.Id)
                return;

            if (!cacheable.HasValue)
                return;

            var oldActivities = cacheable.Value.Activities;
            var newActivities = after.Activities;
            var firstNewActivity = newActivities?.FirstOrDefault();

            if (!Equals(oldActivities, newActivities)
                && firstNewActivity?.Type == ActivityType.Playing)
            {
                await TriggerGvc(after, firstNewActivity.Name).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error running GuildMemberUpdated in gvc");
        }
    }

    /// <summary>
    ///     Toggles the game voice channel for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="vchId">The ID of the voice channel.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the ID of the toggled game voice
    ///     channel.
    /// </returns>
    public async Task<ulong?> ToggleGameVoiceChannel(ulong guildId, ulong vchId)
    {
        ulong? id;
        var gc = await guildSettings.GetGuildConfig(guildId);

        if (gc == null)
        {
            logger.LogWarning("GuildConfig is null for GuildId {GuildId} in ToggleGameVoiceChannel", guildId);
            return null;
        }

        if (gc.GameVoiceChannel == vchId)
        {
            id = gc.GameVoiceChannel = 0;
            await guildSettings.UpdateGuildConfig(guildId, gc);
        }
        else
        {
            id = gc.GameVoiceChannel = vchId;
            await guildSettings.UpdateGuildConfig(guildId, gc);
        }

        return id;
    }

    /// <summary>
    ///     Handles the UserVoiceStateUpdated event.
    /// </summary>
    /// <param name="usr">The user whose voice state was updated.</param>
    /// <param name="oldState">The old voice state.</param>
    /// <param name="newState">The new voice state.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task Client_UserVoiceStateUpdated(SocketUser usr, SocketVoiceState oldState,
        SocketVoiceState newState)
    {
        try
        {
            if (usr is not SocketGuildUser gUser)
                return;

            var game = gUser.Activities.FirstOrDefault()?.Name;

            if (oldState.VoiceChannel == newState.VoiceChannel ||
                newState.VoiceChannel == null)
            {
                return;
            }

            var gConfig = await guildSettings.GetGuildConfig(gUser.Guild.Id);
            if (gConfig.GameVoiceChannel != newState.VoiceChannel.Id ||
                string.IsNullOrWhiteSpace(game))
            {
                return;
            }

            await TriggerGvc(gUser, game).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error running VoiceStateUpdate in gvc");
        }
    }


    /// <summary>
    ///     Triggers the game voice channel for a guild user.
    /// </summary>
    /// <param name="gUser">The guild user.</param>
    /// <param name="game">The game.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task TriggerGvc(SocketGuildUser gUser, string game)
    {
        if (string.IsNullOrWhiteSpace(game))
            return;

        game = game.TrimTo(50).ToLowerInvariant();
        var vch = gUser.Guild.VoiceChannels
            .FirstOrDefault(x => x.Name.ToLowerInvariant() == game);

        if (vch == null)
            return;

        await Task.Delay(1000).ConfigureAwait(false);
        await gUser.ModifyAsync(gu => gu.Channel = vch).ConfigureAwait(false);
    }
}