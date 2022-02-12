﻿#nullable enable
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Modules.Music.Extensions;
using Mewdeko.Modules.Music.Services;
using System.Collections.Generic;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

namespace Mewdeko.Modules.Music;
public class Music : MewdekoModuleBase<MusicService>
{
    private readonly InteractiveService _interactivity;
    private readonly LavaNode _lavaNode;
    private readonly DbService _db;

    public Music(LavaNode lava, InteractiveService interactive, DbService dbService)
    {
        _db = dbService;
        _interactivity = interactive;
        _lavaNode = lava;
    }

    public enum PlaylistAction
    {
        Show,
        Delete,
        Create,
        Remove,
        Add,
        Load,
        Save,
        Default
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task AutoDisconnect(AutoDisconnect disconnect)
    {
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
            (settings, _) => settings.AutoDisconnect = disconnect, disconnect);
        await ctx.Channel.SendConfirmAsync(
            $"Successfully set AutoDisconnect to {Format.Code(disconnect.ToString())}");
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Playlists()
    {
        var plists = Service.GetPlaylists(ctx.User);
        if (!plists.Any())
        {
            await ctx.Channel.SendErrorAsync("You dont have any saved playlists!");
            return;
        }
        var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(plists.Count() / 15)
                        .WithDefaultCanceledPage()
                        .WithDefaultEmotes()
                        .Build();
        await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

        Task<PageBuilder> PageFactory(int page)
        {
            var e = 1;
            return Task.FromResult(new PageBuilder().WithOkColor()
                                                    .WithDescription(string.Join("\n",
                                                        plists.Skip(page).Take(15).Select(x =>
                                                            $"{e++}. {x.Name} - {x.Songs.Count()} songs"))));
        }
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Playlist(PlaylistAction action, [Remainder] string? playlistOrSongName = null)
    {
        var plists = Service.GetPlaylists(ctx.User);
        switch (action)
        {

            case PlaylistAction.Show:
                var plist = new MusicPlaylist();
                if (playlistOrSongName is null)
                {
                    if (Service.GetDefaultPlaylist(ctx.User) is not null)
                        plist = Service.GetDefaultPlaylist(ctx.User);
                    else
                    {
                        await ctx.Channel.SendErrorAsync(
                            "You have not specified a playlist name and do not have a default playlist set, there's nothing to show!");
                        return;
                    }
                }
                else
                {
                    plist = Service.GetPlaylists(ctx.User)
                                   .FirstOrDefault(x => x.Name.ToLower() == playlistOrSongName.ToLower())!;
                }

                var songcount = 1;
                if (plist is null)
                {
                    await ctx.Channel.SendErrorAsync("This is not a valid playlist!");
                    return;
                }
                if (!plist.Songs.Any())
                {
                    await ctx.Channel.SendErrorAsync("This playlist has no songs!");
                    return;
                }

                var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                                                          .WithFooter(
                                                              PaginatorFooter.PageNumber | PaginatorFooter.Users)
                                                          .WithMaxPageIndex(plist.Songs.Count() / 15)
                                                          .WithDefaultCanceledPage().WithDefaultEmotes().Build();
                await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

                Task<PageBuilder> PageFactory(int page) => Task.FromResult(new PageBuilder().WithOkColor().WithDescription(string.Join("\n",
                        plist.Songs.Select(x =>
                            $"`{songcount++}.` [{x.Title.TrimTo(45)}]({x.Query}) `{x.Provider}`"))));

                break;
            case PlaylistAction.Delete:
                var plist1 = plists.FirstOrDefault(x => x.Name.ToLower() == playlistOrSongName?.ToLower());
                if (plist1 == null)
                {
                    await ctx.Channel.SendErrorAsync("Playlist with that name could not be found!");
                    return;
                }

                if (await PromptUserConfirmAsync("Are you sure you want to delete this playlist", ctx.User.Id))
                {
                    await using var uow = _db.GetDbContext();
                    uow.MusicPlaylists.Remove(plist1);
                    await uow.SaveChangesAsync();
                    await ctx.Channel.SendConfirmAsync("Playlist deleted.");
                }

                break;

            case PlaylistAction.Create:
                if (playlistOrSongName is null)
                {
                    await ctx.Channel.SendErrorAsync("You need to specify a playlist name!");
                }

                if (Service.GetPlaylists(ctx.User).Select(x => x.Name.ToLower()).Contains(playlistOrSongName?.ToLower()))
                {
                    await ctx.Channel.SendErrorAsync("You already have a playlist with this name!");
                }
                else
                {
                    var toadd = new MusicPlaylist()
                    {
                        Author = ctx.User.ToString(),
                        AuthorId = ctx.User.Id,
                        Name = playlistOrSongName,
                        Songs = new List<PlaylistSong>()
                    };
                    await using var uow = _db.GetDbContext();
                    uow.MusicPlaylists.Add(toadd);
                    await uow.SaveChangesAsync();
                    await ctx.Channel.SendConfirmAsync(
                        $"Successfully created playlist with name `{playlistOrSongName}`!");
                }

                break;
            case PlaylistAction.Load:
                if (!string.IsNullOrEmpty(playlistOrSongName))
                {
                    var vstate = ctx.User as IVoiceState;
                    if (vstate?.VoiceChannel is null)
                    {
                        await ctx.Channel.SendErrorAsync("You must be in a channel to use this!");
                        return;
                    }

                    if (!_lavaNode.HasPlayer(ctx.Guild))
                    {
                        try
                        {
                            await _lavaNode.JoinAsync(vstate.VoiceChannel);
                            if (vstate.VoiceChannel is IStageChannel chan)
                            {
                                await chan.BecomeSpeakerAsync();
                            }
                        }
                        catch (Exception)
                        {
                            await ctx.Channel.SendErrorAsync("Seems I may not have permission to join...");
                            return;
                        }
                    }

                    var plist3 = Service.GetPlaylists(ctx.User).Where(x => x.Name.ToLower() == playlistOrSongName);
                    var musicPlaylists = plist3 as MusicPlaylist[] ?? plist3.ToArray();
                    if (!musicPlaylists.Any())
                    {
                        await ctx.Channel.SendErrorAsync("A playlist with that name wasnt found!");
                        return;
                    }

                    var songs3 = musicPlaylists.Select(x => x.Songs).FirstOrDefault();
                    var msg = await ctx.Channel.SendConfirmAsync(
                        $"Queueing {songs3!.Count()} songs from {musicPlaylists.FirstOrDefault()?.Name}...");
                    foreach (var i in songs3!)
                    {
                        var search = await _lavaNode.SearchAsync(SearchType.Direct, i.Query);
                        var platform = AdvancedLavaTrack.Platform.Youtube;
                        if (search.Status != SearchStatus.NoMatches)
                        {
                            platform = i.Provider switch
                            {
                                "Spotify" => AdvancedLavaTrack.Platform.Spotify,
                                "Soundcloud" => AdvancedLavaTrack.Platform.Soundcloud,
                                "Direct Url / File" => AdvancedLavaTrack.Platform.Url,
                                "Youtube" => AdvancedLavaTrack.Platform.Youtube,
                                _ => platform
                            };

                            await Service.Enqueue(ctx.Guild.Id, ctx.User, search.Tracks.FirstOrDefault(), platform);
                        }

                        var player = _lavaNode.GetPlayer(ctx.Guild);
                        if (player.PlayerState == PlayerState.Playing) continue;
                        await player.PlayAsync(search.Tracks.FirstOrDefault());
                        await player.UpdateVolumeAsync((ushort)Service.GetVolume(ctx.Guild.Id));
                    }

                    await msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                                                         .WithOkColor()
                                                         .WithDescription(
                                                             $"Successfully loaded {songs3.Count()} songs from {musicPlaylists.FirstOrDefault()?.Name}!")
                                                         .Build());
                    return;
                }

                if (Service.GetDefaultPlaylist(ctx.User) is not null && !string.IsNullOrEmpty(playlistOrSongName))
                {
                    var vstate = ctx.User as IVoiceState;
                    if (vstate?.VoiceChannel is null)
                    {
                        await ctx.Channel.SendErrorAsync("You must be in a channel to use this!");
                        return;
                    }

                    await using var uow = _db.GetDbContext();
                    var plist2 = uow.MusicPlaylists.GetDefaultPlaylist(ctx.User.Id);
                    var songs2 = plist2.Songs;
                    if (!songs2.Any())
                    {
                        await ctx.Channel.SendErrorAsync(
                            "Your default playlist has no songs! Please add songs and try again.");
                        return;
                    }

                    if (!_lavaNode.HasPlayer(ctx.Guild))
                    {
                        try
                        {
                            await _lavaNode.JoinAsync(vstate.VoiceChannel);
                            if (vstate.VoiceChannel is IStageChannel chan)
                            {
                                await chan.BecomeSpeakerAsync();
                            }
                        }
                        catch (Exception)
                        {
                            await ctx.Channel.SendErrorAsync("Seems I may not have permission to join...");
                            return;
                        }
                    }

                    var msg = await ctx.Channel.SendConfirmAsync(
                        $"Queueing {songs2.Count()} songs from {plist2.Name}...");
                    foreach (var i in songs2)
                    {
                        var search = await _lavaNode.SearchAsync(SearchType.Direct, i.Query);
                        if (search.Status != SearchStatus.NoMatches)
                            await Service.Enqueue(ctx.Guild.Id, ctx.User, search.Tracks.FirstOrDefault());
                        var player = _lavaNode.GetPlayer(ctx.Guild);
                        if (player.PlayerState == PlayerState.Playing) continue;
                        await player.PlayAsync(search.Tracks.FirstOrDefault());
                        await player.UpdateVolumeAsync((ushort)Service.GetVolume(ctx.Guild.Id));
                    }

                    await msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                                                         .WithOkColor()
                                                         .WithDescription(
                                                             $"Successfully loaded {songs2.Count()} songs from {plist2.Name}!")
                                                         .Build());
                    return;
                }

                if (Service.GetDefaultPlaylist(ctx.User) is null && string.IsNullOrEmpty(playlistOrSongName))
                {
                    await ctx.Channel.SendErrorAsync(
                        "You don't have a default playlist set and have not specified a playlist name!");
                }

                break;
            case PlaylistAction.Save:
                var queue = Service.GetQueue(ctx.Guild.Id);
                var plists5 = Service.GetPlaylists(ctx.User);
                if (!plists5.Any())
                {
                    await ctx.Channel.SendErrorAsync("You do not have any playlists!");
                    return;
                }


                var trysearch = queue.Where(x => x.Title.ToLower().Contains(playlistOrSongName?.ToLower() ?? "")).Take(20);
                var advancedLavaTracks = trysearch as AdvancedLavaTrack[] ?? trysearch.ToArray();
                if (!advancedLavaTracks.Any())
                {
                    var search = await _lavaNode.SearchAsync(SearchType.YouTube, playlistOrSongName)
                                         .ConfigureAwait(false);
                    trysearch = search.Tracks.Select(x => new AdvancedLavaTrack(x, queue.Count+1, ctx.User));
                }

                if (advancedLavaTracks.Length == 1)
                {
                    var msg = await ctx.Channel.SendConfirmAsync(
                        "Please type the name of the playlist you wanna save this to!");
                    var nmsg = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                    var plists6 = plists5.FirstOrDefault(x => x.Name.ToLower() == nmsg.ToLower());
                    if (plists6 is not null)
                    {
                        var toadd = new PlaylistSong
                        {
                            Title = advancedLavaTracks.FirstOrDefault()?.Title,
                            ProviderType = advancedLavaTracks.FirstOrDefault()!.QueuedPlatform,
                            Provider = advancedLavaTracks.FirstOrDefault()!.QueuedPlatform.ToString(),
                            Query = advancedLavaTracks.FirstOrDefault()!.Url
                        };
                        var newsongs = plists6.Songs.ToList();
                        newsongs.Add(toadd);
                        var toupdate = new MusicPlaylist
                        {
                            Id = plists6.Id,
                            AuthorId = plists6.AuthorId,
                            Author = plists6.Author,
                            DateAdded = plists6.DateAdded,
                            IsDefault = plists6.IsDefault,
                            Name = plists6.Name,
                            Songs = newsongs
                        };
                        await using var uow = _db.GetDbContext();
                        uow.MusicPlaylists.Update(toupdate);
                        await uow.SaveChangesAsync();
                        await msg.DeleteAsync();
                        await ctx.Channel.SendConfirmAsync(
                            $"Added {advancedLavaTracks.FirstOrDefault()?.Title} to {plists6.Name}.");
                    }
                    else
                    {
                        await ctx.Channel.SendErrorAsync("Please make sure you put in the right playlist name.");
                    }
                }
                else
                {
                    var components = new ComponentBuilder().WithButton("Save All", "all")
                                                           .WithButton("Choose", "choose");
                    var msg = await ctx.Channel.SendConfirmAsync(
                        "I found more than one result for that name. Would you like me to save all or choose from 10?",
                        components);
                    switch (await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id))
                    {
                        case "all":
                            msg = await ctx.Channel.SendConfirmAsync(
                                "Please type the name of the playlist you wanna save this to!");
                            var nmsg1 = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                            var plists7 = plists5.FirstOrDefault(x => x.Name.ToLower() == nmsg1.ToLower());
                            if (plists7 is not null)
                            {
                                var toadd = advancedLavaTracks.Select(x => new PlaylistSong
                                {
                                    Title = x.Title,
                                    ProviderType = x.QueuedPlatform,
                                    Provider = x.QueuedPlatform.ToString(),
                                    Query = x.Url
                                });
                                var newsongs = plists7.Songs.ToList();
                                newsongs.AddRange(toadd);
                                var toupdate = new MusicPlaylist
                                {
                                    Id = plists7.Id,
                                    AuthorId = plists7.AuthorId,
                                    Author = plists7.Author,
                                    DateAdded = plists7.DateAdded,
                                    IsDefault = plists7.IsDefault,
                                    Name = plists7.Name,
                                    Songs = newsongs
                                };
                                await using var uow = _db.GetDbContext();
                                uow.MusicPlaylists.Update(toupdate);
                                await uow.SaveChangesAsync();
                                await msg.DeleteAsync();
                                await ctx.Channel.SendConfirmAsync($"Added {toadd.Count()} tracks to {plists7.Name}.");
                            }
                            else
                            {
                                await ctx.Channel.SendErrorAsync(
                                    "Please make sure you put in the right playlist name.");
                            }

                            break;

                        case "choose":
                            var components1 = new ComponentBuilder();
                            var count1 = 1;
                            var count = 1;
                            foreach (var i in advancedLavaTracks)
                            {
                                if (count1 >= 6)
                                    components1.WithButton(count1++.ToString(), count1.ToString(), ButtonStyle.Primary,
                                        row: 1);
                                else
                                    components1.WithButton(count1++.ToString(), count1.ToString());
                            }


                            await msg.DeleteAsync();
                            var msg2 = await ctx.Channel.SendConfirmAsync(
                                string.Join("\n",
                                    advancedLavaTracks.Select(x => $"{count++}. {x.Title.TrimTo(140)} - {x.Author}")),
                                components1);
                            var response = await GetButtonInputAsync(ctx.Channel.Id, msg2.Id, ctx.User.Id);
                            var track = advancedLavaTracks.ElementAt(int.Parse(response) - 2);
                            msg = await ctx.Channel.SendConfirmAsync(
                                "Please type the name of the playlist you wanna save this to!");
                            var nmsg = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                            var plists6 = plists5.FirstOrDefault(x => x.Name.ToLower() == nmsg.ToLower());
                            if (plists6 is not null)
                            {
                                var toadd = new PlaylistSong
                                {
                                    Title = track.Title,
                                    ProviderType = track.QueuedPlatform,
                                    Provider = track.QueuedPlatform.ToString(),
                                    Query = track.Url
                                };
                                var newsongs = plists6.Songs.ToList();
                                newsongs.Add(toadd);
                                var toupdate = new MusicPlaylist
                                {
                                    Id = plists6.Id,
                                    AuthorId = plists6.AuthorId,
                                    Author = plists6.Author,
                                    DateAdded = plists6.DateAdded,
                                    IsDefault = plists6.IsDefault,
                                    Name = plists6.Name,
                                    Songs = newsongs
                                };
                                await using var uow = _db.GetDbContext();
                                uow.MusicPlaylists.Update(toupdate);
                                await uow.SaveChangesAsync();
                                await msg.DeleteAsync();
                                await ctx.Channel.SendConfirmAsync($"Added {track.Title} to {plists6.Name}.");
                            }
                            else
                            {
                                await ctx.Channel.SendErrorAsync(
                                    "Please make sure you put in the right playlist name.");
                            }

                            break;
                    }
                }

                break;
            case PlaylistAction.Default:
                var defaultplaylist = Service.GetDefaultPlaylist(ctx.User);
                if (string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is not null)
                {
                    await ctx.Channel.SendConfirmAsync($"Your current default playlist is {defaultplaylist.Name}");
                    return;
                }

                if (string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is null)
                {
                    await ctx.Channel.SendErrorAsync("You do not have a default playlist set.");
                    return;
                }

                if (!string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is not null)
                {
                    var plist4 = Service.GetPlaylists(ctx.User)
                                        .FirstOrDefault(x => x.Name.ToLower() == playlistOrSongName.ToLower());
                    if (plist4 is null)
                    {
                        await ctx.Channel.SendErrorAsync(
                            "Playlist by that name wasn't found. Please try another name!");
                        return;
                    }

                    if (plist4.Name == defaultplaylist.Name)
                    {
                        await ctx.Channel.SendErrorAsync("This is already your default playlist!");
                        return;
                    }

                    if (await PromptUserConfirmAsync("Are you sure you want to switch default playlists?", ctx.User.Id))
                    {
                        await Service.UpdateDefaultPlaylist(ctx.User, plist4);
                        await ctx.Channel.SendConfirmAsync("Default Playlist Updated.");
                    }
                }

                if (!string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is null)
                {
                    var plist4 = Service.GetPlaylists(ctx.User)
                                        .FirstOrDefault(x => x.Name.ToLower() == playlistOrSongName.ToLower());
                    if (plist4 is null)
                    {
                        await ctx.Channel.SendErrorAsync(
                            "Playlist by that name wasn't found. Please try another name!");
                        return;
                    }

                    await Service.UpdateDefaultPlaylist(ctx.User, plist4);
                    await ctx.Channel.SendConfirmAsync("Default Playlist Set.");
                }

                break;
        }
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Join()
    {
        if (_lavaNode.HasPlayer(Context.Guild))
        {
            await ctx.Channel.SendErrorAsync("I'm already connected to a voice channel!");
            return;
        }

        var voiceState = Context.User as IVoiceState;
        if (voiceState?.VoiceChannel == null)
        {
            await ctx.Channel.SendErrorAsync("You must be connected to a voice channel!");
            return;
        }


        await _lavaNode.JoinAsync(voiceState.VoiceChannel);
        if (voiceState.VoiceChannel is IStageChannel chan)
        {
            try
            {
                await chan.BecomeSpeakerAsync();
            }
            catch
            {//
            }
        }
        await ctx.Channel.SendConfirmAsync($"Joined {voiceState.VoiceChannel.Name}!");
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Leave()
    {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
        {
            await ctx.Channel.SendErrorAsync("I'm not connected to any voice channels!");
            return;
        }

        var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel ?? player.VoiceChannel;
        if (voiceChannel == null)
        {
            await ctx.Channel.SendErrorAsync("Not sure which voice channel to disconnect from.");
            return;
        }

        await _lavaNode.LeaveAsync(voiceChannel);
        await ctx.Channel.SendConfirmAsync($"I've left {voiceChannel.Name}!");
        await Service.QueueClear(ctx.Guild.Id);
    }


    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Play(int number)
    {
        var queue = Service.GetQueue(ctx.Guild.Id);
        if (!_lavaNode.TryGetPlayer(ctx.Guild, out var player))
        {
            var vc = ctx.User as IVoiceState;
            if (vc?.VoiceChannel is null)
            {
                await ctx.Channel.SendErrorAsync("Looks like both you and the bot are not in a voice channel.");
                return;
            }
        }

        if (queue.Any())
        {
            var track = queue.FirstOrDefault(x => x.Index == number);
            if (track is null)
            {
                await Play($"{number}");
                return;
            }

            await player.PlayAsync(track);
            var e = await track.FetchArtworkAsync();
            var eb = new EmbedBuilder()
                .WithDescription($"Playing {track.Title}")
                .WithFooter($"Track {track.Index} | {track.Duration:hh\\:mm\\:ss} | {track.QueueUser}")
                .WithThumbnailUrl(e)
                .WithOkColor();
            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    // ReSharper disable once MemberCanBePrivate.Global
    public async Task Play([Remainder] string? searchQuery = null)
    {
        var count = 0;
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            var firstattach = ctx.Message.Attachments;
            if (!firstattach.Any())
            {
                await ctx.Channel.SendErrorAsync("Please provide a url or file to play.");
                return;
            }

            searchQuery = firstattach.FirstOrDefault()?.Url;
        }

        if (!_lavaNode.HasPlayer(Context.Guild))
        {
            var vc = ctx.User as IVoiceState;
            if (vc?.VoiceChannel is null)
            {
                await ctx.Channel.SendErrorAsync("Looks like both you and the bot are not in a voice channel.");
                return;
            }

            try
            {
                await _lavaNode.JoinAsync(vc.VoiceChannel);
                if (vc.VoiceChannel is SocketStageChannel chan)
                    try
                    {
                        await chan.BecomeSpeakerAsync();
                    }
                    catch
                    {
                        await ctx.Channel.SendErrorAsync(
                            "I tried to join as a speaker but I'm unable to! Please drag me to the channel manually.");
                    }
            }
            catch
            {
                await ctx.Channel.SendErrorAsync("Seems I'm unable to join the channel! Check permissions!");
                return;
            }
        }

        await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
            (settings, _) => settings.MusicChannelId = ctx.Channel.Id, ctx.Channel.Id);
        var player = _lavaNode.GetPlayer(ctx.Guild);
        SearchResponse searchResponse;
        if (Uri.IsWellFormedUriString(searchQuery, UriKind.RelativeOrAbsolute))
            if (searchQuery.Contains("youtube.com") || searchQuery.Contains("youtu.be") ||
                searchQuery.Contains("soundcloud.com") || searchQuery.CheckIfMusicUrl() || ctx.Message.Attachments.IsValidAttachment())
            {
                if (ctx.Message.Attachments.IsValidAttachment())
                    searchQuery = ctx.Message.Attachments.FirstOrDefault()?.Url;
                searchResponse = await _lavaNode.SearchAsync(SearchType.Direct, searchQuery);
                var track1 = searchResponse.Tracks.FirstOrDefault();
                var platform = AdvancedLavaTrack.Platform.Youtube;
                if (searchQuery!.Contains("soundcloud.com"))
                    platform = AdvancedLavaTrack.Platform.Soundcloud;
                if (searchQuery.CheckIfMusicUrl())
                    platform = AdvancedLavaTrack.Platform.Url;
                await Service.Enqueue(ctx.Guild.Id, ctx.User, searchResponse.Tracks.ToArray(), platform);
                count = Service.GetQueue(ctx.Guild.Id).Count;
                if (searchResponse.Playlist.Name is not null)
                {
                    var eb = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(
                            $"Queued {searchResponse.Tracks.Count} tracks from {searchResponse.Playlist.Name}")
                        .WithFooter($"{count} songs now in the queue");
                    await ctx.Channel.SendMessageAsync(embed: eb.Build());
                    if (player.PlayerState != PlayerState.Playing)
                        await player.PlayAsync(track1);
                    await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                    return;
                }
                else
                {
                    var eb = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(
                            $"Queued {searchResponse.Tracks.Count} tracks from {searchResponse.Playlist.Name} and bound the queue info to {ctx.Channel.Name}!");
                    await ctx.Channel.SendMessageAsync(embed: eb.Build());
                    if (player.PlayerState != PlayerState.Playing)
                        await player.PlayAsync(x => x.Track = track1);
                    await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                    return;
                }
            }

        if (searchQuery!.Contains("spotify"))
        {
            await Service.SpotifyQueue(ctx.Guild, ctx.User, ctx.Channel as ITextChannel, player, searchQuery);
            return;
        }

        searchResponse = await _lavaNode.SearchAsync(SearchType.YouTube, searchQuery);
        if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
        {
            await ctx.Channel.SendErrorAsync("Seems like I can't find that video, please try again.");
            return;
        }

        var components = new ComponentBuilder().WithButton("Play All", "all").WithButton("Select", "select")
            .WithButton("Play First", "pf").WithButton("Cancel", "cancel", ButtonStyle.Danger);
        var eb12 = new EmbedBuilder()
            .WithOkColor()
            .WithTitle("Would you like me to:")
            .WithDescription("Play all that I found\n" +
                             "Let you select from the top 5\n" +
                             "Just play the first thing I found");
        var msg = await ctx.Channel.SendMessageAsync(embed: eb12.Build(), components: components.Build());
        var button = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
        switch (button)
        {
            case "all":
                await Service.Enqueue(ctx.Guild.Id, ctx.User, searchResponse.Tracks.ToArray());
                count = Service.GetQueue(ctx.Guild.Id).Count;
                var track = searchResponse.Tracks.FirstOrDefault();
                var eb1 = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription($"Added {track?.Title} along with {searchResponse.Tracks.Count} other tracks.")
                    .WithThumbnailUrl(await track.FetchArtworkAsync())
                    .WithFooter($"{count} songs in queue");
                if (player.PlayerState != PlayerState.Playing)
                {
                    await player.PlayAsync(x => x.Track = track);
                    await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                }

                await msg.ModifyAsync(x =>
                {
                    x.Components = null;
                    x.Embed = eb1.Build();
                });
                break;
            case "select":
                var tracks = searchResponse.Tracks.Take(5).ToArray();
                var count1 = 1;
                var eb = new EmbedBuilder()
                    .WithDescription(string.Join("\n", tracks.Select(x => $"{count1++}. {x.Title} by {x.Author}")))
                    .WithOkColor()
                    .WithTitle("Pick which one!");
                count1 = 0;
                var components1 = new ComponentBuilder();
                foreach (var i in tracks)
                {
                    var component =
                        new ButtonBuilder(customId: (count1 + 1).ToString(), label: (count1 + 1).ToString());
                    count1++;
                    components1.WithButton(component);
                }

                await msg.ModifyAsync(x =>
                {
                    x.Components = components1.Build();
                    x.Embed = eb.Build();
                });
                var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
                var chosen = tracks[int.Parse(input) - 1];
                await Service.Enqueue(ctx.Guild.Id, ctx.User, chosen);
                count = Service.GetQueue(ctx.Guild.Id).Count;
                eb1 = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription($"Added {chosen.Title} by {chosen.Author} to the queue.")
                    .WithThumbnailUrl(await chosen.FetchArtworkAsync())
                    .WithFooter($"{count} songs in queue");
                if (player.PlayerState != PlayerState.Playing)
                {
                    await player.PlayAsync(x => x.Track = chosen);
                    await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                }

                await msg.ModifyAsync(x =>
                {
                    x.Components = null;
                    x.Embed = eb1.Build();
                });
                break;
            case "pf":
                track = searchResponse.Tracks.FirstOrDefault();
                await Service.Enqueue(ctx.Guild.Id, ctx.User, track);
                count = Service.GetQueue(ctx.Guild.Id).Count;
                eb1 = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription($"Added {track?.Title} by {track?.Author} to the queue.")
                    .WithThumbnailUrl(await track.FetchArtworkAsync())
                    .WithFooter($"{count} songs in queue");
                await msg.ModifyAsync(x =>
                {
                    x.Embed = eb1.Build();
                    x.Components = null;
                });
                if (player.PlayerState != PlayerState.Playing)
                {
                    await player.PlayAsync(x => x.Track = track);
                    await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                }

                break;
            case "cancel":
                var eb13 = new EmbedBuilder()
                    .WithDescription("Cancelled.")
                    .WithErrorColor();
                await msg.ModifyAsync(x =>
                {
                    x.Embed = eb13.Build();
                    x.Components = null;
                });
                break;
        }
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Pause()
    {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
        {
            await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState != PlayerState.Playing)
        {
            await player.ResumeAsync();
            await ctx.Channel.SendConfirmAsync("Resumed player.");
            return;
        }

        await player.PauseAsync();
        await ctx.Channel.SendConfirmAsync($"Paused player. Do {Prefix}pause again to resume.");
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Shuffle()
    {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out _))
        {
            await ctx.Channel.SendErrorAsync("I'm not even playing anything.");
            return;
        }

        if (!Service.GetQueue(ctx.Guild.Id).Any())
        {
            await ctx.Channel.SendErrorAsync("There's nothing in queue.");
            return;
        }

        if (Service.GetQueue(ctx.Guild.Id).Count == 1)
        {
            await ctx.Channel.SendErrorAsync("... There's literally only one thing in queue.");
            return;
        }

        Service.Shuffle(ctx.Guild);
        await ctx.Channel.SendConfirmAsync("Successfully shuffled the queue!");
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Stop()
    {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
        {
            await ctx.Channel.SendErrorAsync("I'm not connected to a channel!");
            return;
        }

        await player.StopAsync();
        await Service.QueueClear(ctx.Guild.Id);
        await ctx.Channel.SendConfirmAsync("Stopped the player and cleared the queue!");
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Skip()
    {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
        {
            await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
            return;
        }

        await Service.Skip(ctx.Guild, ctx.Channel as ITextChannel, player);
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Seek(TimeSpan timeSpan)
    {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
        {
            await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState != PlayerState.Playing)
        {
            await ctx.Channel.SendErrorAsync("Woaaah there, I can't seek when nothing is playing.");
            return;
        }

        if (timeSpan > player.Track.Duration)
            await ctx.Channel.SendErrorAsync("That's longer than the song lol, try again.");
        await player.SeekAsync(timeSpan);
        await ctx.Channel.SendConfirmAsync($"I've seeked `{player.Track.Title}` to {timeSpan}.");
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task ClearQueue()
    {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
        {
            await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
            return;
        }

        await player.StopAsync();
        await Service.QueueClear(ctx.Guild.Id);
        await ctx.Channel.SendConfirmAsync("Cleared the queue!");
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Loop(PlayerRepeatType reptype = PlayerRepeatType.None)
    {
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => settings.PlayerRepeat = reptype,
            reptype);
        await ctx.Channel.SendConfirmAsync($"Loop has now been set to {reptype}");
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Volume(ushort volume)
    {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
        {
            await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
            return;
        }

        if (volume > 100)
        {
            await ctx.Channel.SendErrorAsync("Max is 100 m8");
            return;
        }

        await player.UpdateVolumeAsync(volume);
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => settings.Volume = volume, volume);
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task NowPlaying()
    {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
        {
            await ReplyAsync("I'm not connected to a voice channel.");
            return;
        }

        if (player.PlayerState != PlayerState.Playing)
        {
            await ReplyAsync("Woaaah there, I'm not playing any tracks.");
            return;
        }

        var qcount = Service.GetQueue(ctx.Guild.Id).Count;
        var track = Service.GetCurrentTrack(player, ctx.Guild);
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle($"Track #{track.Index}")
            .WithDescription($"Now Playing {track.Title} by {track.Author}")
            .WithThumbnailUrl(await track.FetchArtworkAsync())
            .WithFooter(
                $"{track.Position:hh\\:mm\\:ss}/{track.Duration:hh\\:mm\\:ss} | {track.QueueUser} | {track.QueuedPlatform} | {qcount} Tracks in queue");
        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    [MewdekoCommand, Description, Aliases, RequireContext(ContextType.Guild)]
    public async Task Queue()
    {
        if (!_lavaNode.TryGetPlayer(Context.Guild, out _))
        {
            await ctx.Channel.SendErrorAsync("I am not playing anything at the moment!");
            return;
        }

        var queue = Service.GetQueue(ctx.Guild.Id);
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(queue.Count / 10)
            .WithDefaultCanceledPage()
            .WithDefaultEmotes()
            .Build();

        await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

        Task<PageBuilder> PageFactory(int page)
        {
            var tracks = queue.OrderBy(x => x.Index).Skip(page * 10).Take(10);
            return Task.FromResult(new PageBuilder()
                .WithDescription(string.Join("\n", tracks.Select(x =>
                    $"`{x.Index}.` [{x.Title}]({x.Url})\n" +
                    $"`{x.Duration:mm\\:ss} {x.QueueUser} {x.QueuedPlatform}`")))
                .WithOkColor());
        }
    }
}