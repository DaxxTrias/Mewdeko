﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using Discord;
using Discord.WebSocket;
using Humanizer.Bytes;
using Mewdeko.Modules.Utility.Services;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

public class StatsService : IStatsService
{
    public const string BotVersion = "3.82";
    private readonly Mewdeko _bot;

    private readonly Timer _botlistTimer;
    private readonly DiscordSocketClient _client;
    private readonly ICoordinator _coord;
    private readonly IBotCredentials _creds;
    private readonly DllVersionChecker _dllVersionChecker;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ConnectionMultiplexer _redis;
    private readonly DateTime _started;
    private long _commandsRan;
    private long _messageCounter;

    private long _textChannels;
    private long _voiceChannels;

    public StatsService(DiscordSocketClient client, CommandHandler cmdHandler,
        IBotCredentials creds, Mewdeko Mewdeko, IDataCache cache, IHttpClientFactory factory, ICoordinator coord)
    {
        _coord = coord;
        _client = client;
        _creds = creds;
        _redis = cache.Redis;
        _httpFactory = factory;
        _bot = Mewdeko;
        _dllVersionChecker = new DllVersionChecker();

        _started = DateTime.UtcNow;
        _client.MessageReceived += _ => Task.FromResult(Interlocked.Increment(ref _messageCounter));
        cmdHandler.CommandExecuted += (_, _) => Task.FromResult(Interlocked.Increment(ref _commandsRan));

        _client.ChannelCreated += c =>
        {
            var _ = Task.Run(() =>
            {
                if (c is ITextChannel)
                    Interlocked.Increment(ref _textChannels);
                else if (c is IVoiceChannel)
                    Interlocked.Increment(ref _voiceChannels);
            });

            return Task.CompletedTask;
        };

        _client.ChannelDestroyed += c =>
        {
            var _ = Task.Run(() =>
            {
                if (c is ITextChannel)
                    Interlocked.Decrement(ref _textChannels);
                else if (c is IVoiceChannel)
                    Interlocked.Decrement(ref _voiceChannels);
            });

            return Task.CompletedTask;
        };

        _client.GuildAvailable += g =>
        {
            var _ = Task.Run(() =>
            {
                var tc = g.Channels.Count(cx => cx is ITextChannel);
                var vc = g.Channels.Count - tc;
                Interlocked.Add(ref _textChannels, tc);
                Interlocked.Add(ref _voiceChannels, vc);
            });
            return Task.CompletedTask;
        };

        _client.JoinedGuild += g =>
        {
            var _ = Task.Run(() =>
            {
                var tc = g.Channels.Count(cx => cx is ITextChannel);
                var vc = g.Channels.Count - tc;
                Interlocked.Add(ref _textChannels, tc);
                Interlocked.Add(ref _voiceChannels, vc);
            });
            return Task.CompletedTask;
        };

        _client.GuildUnavailable += g =>
        {
            var _ = Task.Run(() =>
            {
                var tc = g.Channels.Count(cx => cx is ITextChannel);
                var vc = g.Channels.Count - tc;
                Interlocked.Add(ref _textChannels, -tc);
                Interlocked.Add(ref _voiceChannels, -vc);
            });

            return Task.CompletedTask;
        };

        _client.LeftGuild += g =>
        {
            var _ = Task.Run(() =>
            {
                var tc = g.Channels.Count(cx => cx is ITextChannel);
                var vc = g.Channels.Count - tc;
                Interlocked.Add(ref _textChannels, -tc);
                Interlocked.Add(ref _voiceChannels, -vc);
            });

            return Task.CompletedTask;
        };

        if (_client.ShardId == 0)

            _botlistTimer = new Timer(async _ =>
            {
                try
                {
                    using var http = _httpFactory.CreateClient();
                    using var content = new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            {"shard_count", _creds.TotalShards.ToString()},
                            {"shard_id", _client.ShardId.ToString()},
                            {"server_count", _coord.GetGuildCount().ToString()}
                        });
                    content.Headers.Clear();
                    content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                    http.DefaultRequestHeaders.Add("Authorization",
                        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6Ijc1MjIzNjI3NDI2MTQyNjIxMiIsImJvdCI6dHJ1ZSwiaWF0IjoxNjA3Mzg3MDk4fQ.1VATJIr_WqRImXlx5hywaAV6BVk-V4NzybRo0e-E3T8");

                    using (await http
                               .PostAsync(new Uri($"https://top.gg/api/bots/{client.CurrentUser.Id}/stats"),
                                   content).ConfigureAwait(false))
                    {
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                    // ignored
                }
            }, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    public string Library => $"Discord.Net Labs {_dllVersionChecker.GetDllVersion()} ";

    public string Heap => ByteSize.FromBytes(Process.GetCurrentProcess().PrivateMemorySize64).Megabytes
        .ToString(CultureInfo.InvariantCulture);

    public double MessagesPerSecond => MessageCounter / GetUptime().TotalSeconds;
    public long TextChannels => Interlocked.Read(ref _textChannels);
    public long VoiceChannels => Interlocked.Read(ref _voiceChannels);
    public long MessageCounter => Interlocked.Read(ref _messageCounter);
    public long CommandsRan => Interlocked.Read(ref _commandsRan);

    public void Initialize()
    {
        var guilds = _client.Guilds.ToArray();
        _textChannels = guilds.Sum(g => g.Channels.Count(cx => cx is ITextChannel));
        _voiceChannels = guilds.Sum(g => g.Channels.Count(cx => cx is IVoiceChannel));
    }

    public TimeSpan GetUptime() => DateTime.UtcNow - _started;

    public string GetUptimeString(string separator = ", ")
    {
        var time = GetUptime();
        return $"{time.Days} days{separator}{time.Hours} hours{separator}{time.Minutes} minutes";
    }
}