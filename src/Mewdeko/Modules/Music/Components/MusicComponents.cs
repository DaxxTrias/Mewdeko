using System.Threading;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Mewdeko.Modules.Music.CustomPlayer;

namespace Mewdeko.Modules.Music.Components;

/// <summary>
///     Handles interaction components for the music system.
///     Processes select menus and buttons for track selection and queue management.
/// </summary>
public class MusicComponents(IAudioService service, IDataCache cache, GuildSettingsService guildSettingsService)
    : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Handles the select menu for track selection in the music queue.
    /// </summary>
    /// <param name="values">The selected values from the select menu.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [ComponentInteraction("music_track_select")]
    public async Task HandleTrackSelect(string[] values)
    {
        if (values.Length == 0 || string.IsNullOrEmpty(values[0]))
        {
            await Context.Interaction.RespondAsync("No track selected.", ephemeral: true);
            return;
        }

        // Check if user is in voice channel
        var voiceState = Context.User as IVoiceState;
        if (voiceState?.VoiceChannel == null)
        {
            await Context.Interaction.RespondAsync("You must be in a voice channel to interact with the music queue.",
                ephemeral: true);
            return;
        }

        // Get the player and check if user is in the same voice channel
        var (player, _) = await GetPlayerAsync();
        if (player == null)
        {
            await Context.Interaction.RespondAsync("There is no active music player in this server.", ephemeral: true);
            return;
        }

        if (voiceState.VoiceChannel.Id != player.VoiceChannelId)
        {
            await Context.Interaction.RespondAsync(
                "You must be in the same voice channel as the bot to interact with the music queue.", ephemeral: true);
            return;
        }

        // Extract track index from the value format "music_track_info:trackIndex"
        var selectedValue = values[0];
        if (!selectedValue.StartsWith("music_track_info:"))
        {
            await Context.Interaction.RespondAsync("Invalid selection.", ephemeral: true);
            return;
        }

        var trackIndexStr = selectedValue["music_track_info:".Length..];
        if (!int.TryParse(trackIndexStr, out var trackIndex))
        {
            await Context.Interaction.RespondAsync("Invalid track index.", ephemeral: true);
            return;
        }

        await HandleTrackInfo(trackIndex);
    }

    /// <summary>
    ///     Handles displaying track information and options.
    /// </summary>
    /// <param name="trackIndex">The track index to display information for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleTrackInfo(int trackIndex)
    {
        await Context.Interaction.DeferAsync(true);

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
        var track = queue.FirstOrDefault(t => t.Index == trackIndex);

        if (track == null)
        {
            await Context.Interaction.FollowupAsync("That track no longer exists in the queue.", ephemeral: true);
            return;
        }

        var currentTrack = await cache.GetCurrentTrack(Context.Guild.Id);
        var isCurrentTrack = currentTrack?.Index == trackIndex;

        // Create track info display
        var containerComponents = new List<IMessageComponentBuilder>();

        containerComponents.Add(new TextDisplayBuilder()
            .WithContent($"# Track {trackIndex} Info"));

        containerComponents.Add(new SeparatorBuilder());

        // Track details section with artwork
        var trackSection = new SectionBuilder()
            .WithComponents([
                new TextDisplayBuilder($"### [{track.Track.Title}]({track.Track.Uri})\n" +
                                       $"**Artist:** {track.Track.Author}\n" +
                                       $"**Duration:** {track.Track.Duration}\n" +
                                       $"**Source:** {track.Track.Provider}\n" +
                                       $"**Requested by:** {track.Requester.Username}\n" +
                                       $"**Status:** {(isCurrentTrack ? "Currently Playing" : "In Queue")}")
            ]);

        if (track.Track.ArtworkUri != null)
        {
            var thumbnailBuilder = new ThumbnailBuilder()
                .WithMedia(new UnfurledMediaItemProperties
                {
                    Url = track.Track.ArtworkUri.ToString()
                });
            trackSection.WithAccessory(thumbnailBuilder);
        }

        containerComponents.Add(trackSection);

        // Add action buttons if not currently playing
        if (!isCurrentTrack)
        {
            containerComponents.Add(new SeparatorBuilder());

            var actionRow = new ActionRowBuilder()
                .WithButton("Skip to Track", $"music_skip_to:{trackIndex}", ButtonStyle.Primary, new Emoji("⏭️"))
                .WithButton("Remove Track", $"music_remove:{trackIndex}", ButtonStyle.Danger, new Emoji("🗑️"));

            containerComponents.Add(actionRow);
        }

        // Create the main container
        var mainContainer = new ContainerBuilder()
            .WithComponents(containerComponents)
            .WithAccentColor(isCurrentTrack ? Mewdeko.OkColor : Color.LightGrey);

        var componentsV2 = new ComponentBuilderV2()
            .AddComponent(mainContainer);

        await Context.Interaction.FollowupAsync(components: componentsV2.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Handles skipping to a specific track in the queue.
    /// </summary>
    /// <param name="trackIndex">The track index to skip to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [ComponentInteraction("music_skip_to:*")]
    public async Task HandleSkipToTrack(int trackIndex)
    {
        // Check voice channel permissions first
        var voiceState = Context.User as IVoiceState;
        if (voiceState?.VoiceChannel == null)
        {
            await Context.Interaction.RespondAsync("You must be in a voice channel to skip tracks.", ephemeral: true);
            return;
        }

        var (player, _) = await GetPlayerAsync();
        if (player == null)
        {
            await Context.Interaction.RespondAsync("There is no active music player in this server.", ephemeral: true);
            return;
        }

        if (voiceState.VoiceChannel.Id != player.VoiceChannelId)
        {
            await Context.Interaction.RespondAsync("You must be in the same voice channel as the bot to skip tracks.",
                ephemeral: true);
            return;
        }

        await Context.Interaction.DeferAsync();

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
        var track = queue.FirstOrDefault(t => t.Index == trackIndex);

        if (track == null)
        {
            await Context.Interaction.FollowupAsync("That track no longer exists in the queue.", ephemeral: true);
            return;
        }

        // Update current track and play the selected track
        await cache.SetCurrentTrack(Context.Guild.Id, track);
        await player.PlayAsync(track.Track);

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# ⏭️ Skipped to Track {trackIndex}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(
                $"**{track.Track.Title}**\n`{track.Track.Duration} | {track.Requester.Username}`"));

        await Context.Interaction.FollowupAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
            allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    ///     Handles removing a specific track from the queue.
    /// </summary>
    /// <param name="trackIndex">The track index to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [ComponentInteraction("music_remove:*")]
    public async Task HandleRemoveTrack(int trackIndex)
    {
        // Check voice channel permissions first
        var voiceState = Context.User as IVoiceState;
        if (voiceState?.VoiceChannel == null)
        {
            await Context.Interaction.RespondAsync("You must be in a voice channel to remove tracks.", ephemeral: true);
            return;
        }

        var (player, _) = await GetPlayerAsync();
        if (player == null)
        {
            await Context.Interaction.RespondAsync("There is no active music player in this server.", ephemeral: true);
            return;
        }

        if (voiceState.VoiceChannel.Id != player.VoiceChannelId)
        {
            await Context.Interaction.RespondAsync("You must be in the same voice channel as the bot to remove tracks.",
                ephemeral: true);
            return;
        }

        await Context.Interaction.DeferAsync();

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
        var track = queue.FirstOrDefault(t => t.Index == trackIndex);

        if (track == null)
        {
            await Context.Interaction.FollowupAsync("That track no longer exists in the queue.", ephemeral: true);
            return;
        }

        var currentTrack = await cache.GetCurrentTrack(Context.Guild.Id);
        if (currentTrack?.Index == trackIndex)
        {
            await Context.Interaction.FollowupAsync("Cannot remove the currently playing track. Use skip instead.",
                ephemeral: true);
            return;
        }

        // Remove the track and reindex
        queue.Remove(track);
        for (var i = 0; i < queue.Count; i++)
        {
            queue[i].Index = i + 1;
        }

        await cache.SetMusicQueue(Context.Guild.Id, queue);

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder("# 🗑️ Track Removed")
            ], Mewdeko.ErrorColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(
                $"**{track.Track.Title}** has been removed from the queue.\n`{track.Track.Duration} | {track.Requester.Username}`"));

        await Context.Interaction.FollowupAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
            allowedMentions: AllowedMentions.None);
    }

    private async ValueTask<(MewdekoPlayer, string?)> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        try
        {
            var channelBehavior = connectToVoiceChannel
                ? PlayerChannelBehavior.Join
                : PlayerChannelBehavior.None;

            var retrieveOptions = new PlayerRetrieveOptions(channelBehavior);

            var options = new MewdekoPlayerOptions
            {
                Channel = Context.Channel as ITextChannel
            };

            var result = await service.Players
                .RetrieveAsync<MewdekoPlayer, MewdekoPlayerOptions>(Context, CreatePlayerAsync, options,
                    retrieveOptions)
                .ConfigureAwait(false);

            await result.Player.SetVolumeAsync(await result.Player.GetVolume() / 100f).ConfigureAwait(false);

            if (result.IsSuccess) return (result.Player, null);
            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => Strings.MusicNotInChannel(ctx.Guild.Id),
                PlayerRetrieveStatus.BotNotConnected => Strings.MusicBotNotConnect(ctx.Guild.Id,
                    await guildSettingsService.GetPrefix(ctx.Guild)),
                PlayerRetrieveStatus.VoiceChannelMismatch => Strings.MusicVoiceChannelMismatch(ctx.Guild.Id),
                PlayerRetrieveStatus.Success => null,
                PlayerRetrieveStatus.UserInSameVoiceChannel => null,
                PlayerRetrieveStatus.PreconditionFailed => null,
                _ => throw new ArgumentOutOfRangeException()
            };
            return (null, errorMessage);
        }
        catch (TimeoutException)
        {
            return (null, Strings.MusicLavalinkDisconnected(ctx.Guild.Id));
        }
    }

    private static ValueTask<MewdekoPlayer> CreatePlayerAsync(
        IPlayerProperties<MewdekoPlayer, MewdekoPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        return ValueTask.FromResult(new MewdekoPlayer(properties));
    }
}