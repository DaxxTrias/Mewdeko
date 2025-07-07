﻿using System.Diagnostics;
using CommandLine;
using Mewdeko.Modules.Games.Services;
using Mewdeko.Services.Strings;
using Serilog;

namespace Mewdeko.Modules.Games.Common;

/// <summary>
///     Represents a typing game.
/// </summary>
public class TypingGame
{
    /// <summary>
    ///     The value of a word in the typing game.
    /// </summary>
    public const float WordValue = 4.5f;

    private readonly DiscordShardedClient client;
    private readonly List<ulong> finishedUserIds;
    private readonly GamesService games;
    private readonly EventHandler handler;
    private readonly Options options;
    private readonly string? prefix;
    private readonly GeneratedBotStrings Strings;
    private readonly Stopwatch sw;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TypingGame" /> class.
    /// </summary>
    /// <param name="games">The games service for config grabbing</param>
    /// <param name="client">The discord client</param>
    /// <param name="channel">The channel the game will start in</param>
    /// <param name="prefix">The bots prefix</param>
    /// <param name="options">Options along with starting the game</param>
    /// <param name="handler">Asynchronous event handler because discord sucks</param>
    /// <param name="strings">Localized bot strings</param>
    public TypingGame(GamesService games, DiscordShardedClient client, ITextChannel channel,
        string? prefix, Options options, EventHandler handler, GeneratedBotStrings strings)
    {
        this.games = games;
        this.client = client;
        this.prefix = prefix;
        this.options = options;
        this.handler = handler;
        Strings = strings;

        Channel = channel;
        IsActive = false;
        sw = new Stopwatch();
        finishedUserIds = [];
    }

    /// <summary>
    ///     Gets the text channel associated with the typing game.
    /// </summary>
    public ITextChannel Channel { get; }

    /// <summary>
    ///     Gets or sets the current sentence being typed in the typing game.
    /// </summary>
    public string? CurrentSentence { get; private set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the typing game is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    ///     Stops the typing game.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains a boolean indicating whether the game
    ///     was successfully stopped.
    /// </returns>
    public async Task<bool> Stop()
    {
        if (!IsActive) return false;
        handler.Unsubscribe("MessageReceived", "TypingGame", AnswerReceived);
        finishedUserIds.Clear();
        IsActive = false;
        sw.Stop();
        sw.Reset();
        try
        {
            await Channel.SendConfirmAsync(Strings.TypingContestStopped(Channel.Guild.Id)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex.ToString());
        }

        return true;
    }

    /// <summary>
    ///     Starts the typing game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Start()
    {
        if (IsActive) return; // Can't start running game
        IsActive = true;
        CurrentSentence = GetRandomSentence();
        var i = (int)(CurrentSentence.Length / WordValue * 1.7f);
        try
        {
            await Channel
                .SendConfirmAsync(Strings.TypingContestStart(Channel.Guild.Id, i))
                .ConfigureAwait(false);

            var time = options.StartTime;

            var msg = await Channel.SendMessageAsync(
                Strings.TypingContestCountdown(Channel.Guild.Id, time, "..."),
                options: new RequestOptions
                {
                    RetryMode = RetryMode.AlwaysRetry
                }
            ).ConfigureAwait(false);

            do
            {
                await Task.Delay(2000).ConfigureAwait(false);
                time -= 2;
                try
                {
                    await msg.ModifyAsync(m =>
                            m.Content = Strings.TypingContestCountdown(Channel.Guild.Id, time, "..."))
                        .ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            } while (time > 2);

            await msg.ModifyAsync(m =>
                    m.Content = CurrentSentence.Replace(" ", " \x200B", StringComparison.InvariantCulture))
                .ConfigureAwait(false);
            sw.Start();
            HandleAnswers();

            while (i > 0)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                i--;
                if (!IsActive)
                    return;
            }
        }
        catch
        {
            // ignored
        }
        finally
        {
            await Stop().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Retrieves a random sentence for the typing game.
    /// </summary>
    /// <returns>A random sentence for typing, or a message indicating no typing articles were found.</returns>
    public string? GetRandomSentence()
    {
        if (games.TypingArticles.Count > 0)
            return games.TypingArticles[new MewdekoRandom().Next(0, games.TypingArticles.Count)].Text;
        return games.TypingArticles.Count > 0
            ? games.TypingArticles[new MewdekoRandom().Next(0, games.TypingArticles.Count)].Text
            : Strings.TypingNoArticles(Channel.Guild.Id, prefix);
    }


    private void HandleAnswers()
    {
        handler.Subscribe("MessageReceived", "TypingGame", AnswerReceived);
    }

    private async Task AnswerReceived(SocketMessage imsg)
    {
        try
        {
            if (imsg.Author.IsBot)
                return;
            if (imsg is not SocketUserMessage msg)
                return;

            if (Channel == null || Channel.Id != msg.Channel.Id) return;

            var guess = msg.Content;

            var distance = CurrentSentence.LevenshteinDistance(guess);
            var decision = Judge(distance, guess.Length);
            if (decision && !finishedUserIds.Contains(msg.Author.Id))
            {
                var elapsed = sw.Elapsed;
                var wpm = CurrentSentence.Length / WordValue / elapsed.TotalSeconds * 60;
                finishedUserIds.Add(msg.Author.Id);
                await Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle(Strings.TypingRaceFinishTitle(Channel.Guild.Id, msg.Author))
                        .AddField(efb => efb
                            .WithName("Place")
                            .WithValue(Strings.TypingRaceFinishPlace(Channel.Guild.Id, finishedUserIds.Count))
                            .WithIsInline(true))
                        .AddField(efb => efb
                            .WithName("WPM")
                            .WithValue(Strings.TypingRaceFinishWpm(
                                Channel.Guild.Id,
                                wpm.ToString("F1"),
                                elapsed.TotalSeconds.ToString("F2")))
                            .WithIsInline(true))
                        .AddField(efb => efb
                            .WithName("Errors")
                            .WithValue(Strings.TypingRaceFinishErrors(Channel.Guild.Id, distance))
                            .WithIsInline(true)))
                    .ConfigureAwait(false);
                if (finishedUserIds.Count % 4 == 0)
                {
                    await Channel.SendConfirmAsync(
                            Strings.TypingGameFinished(Channel.Guild.Id,
                                Format.Sanitize(CurrentSentence.Replace(" ", " \x200B",
                                    StringComparison.InvariantCulture)).SanitizeMentions(true)))
                        .ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex.ToString());
        }
    }

    private static bool Judge(int errors, int textLength)
    {
        return errors <= textLength / 25;
    }

    /// <summary>
    ///     Represents the options for the typing game.
    /// </summary>
    public class Options : IMewdekoCommandOptions
    {
        /// <summary>
        ///     Gets or sets the time in seconds for the race to start.
        /// </summary>
        [Option('s', "start-time", Default = 5, Required = false,
            HelpText = "How long does it take for the race to start. Default 5.")]
        public int StartTime { get; set; } = 5;

        /// <summary>
        ///     Normalizes the options, ensuring valid values.
        /// </summary>
        public void NormalizeOptions()
        {
            if (StartTime is < 3 or > 30)
                StartTime = 5;
        }
    }
}