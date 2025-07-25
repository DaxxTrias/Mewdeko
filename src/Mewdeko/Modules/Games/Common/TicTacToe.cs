﻿using System.Text;
using System.Threading;
using CommandLine;
using Mewdeko.Common.Configs;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Games.Common;

/// <summary>
///     Represents a Tic-Tac-Toe game.
/// </summary>
public class TicTacToe
{
    private readonly ITextChannel channel;
    private readonly BotConfig config;
    private readonly EventHandler handler;
    private readonly SemaphoreSlim moveLock;

    private readonly string[] numbers =
    [
        ":one:", ":two:", ":three:", ":four:", ":five:", ":six:", ":seven:", ":eight:", ":nine:"
    ];

    private readonly Options options;
    private readonly int?[,] state;
    private readonly GeneratedBotStrings Strings;
    private readonly IGuildUser?[] users;
    private int curUserIndex;
    private Phase phase;

    private IUserMessage? previousMessage;
    private Timer timeoutTimer;

    private IGuildUser? winner;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TicTacToe" /> class with the specified strings, client, channel, first
    ///     user, and options.
    /// </summary>
    /// <param name="strings">Localization Strings</param>
    /// <param name="channel">Channel trivia will run in</param>
    /// <param name="firstUser">User who started tic tac toe</param>
    /// <param name="options">Options along with the game</param>
    /// <param name="config">Bot Configuration</param>
    /// <param name="handler">Event Handler</param>
    public TicTacToe(GeneratedBotStrings strings, ITextChannel channel,
        IGuildUser firstUser, Options options, BotConfig config, EventHandler handler)
    {
        this.channel = channel;
        this.Strings = strings;
        this.options = options;
        this.config = config;
        this.handler = handler;

        users =
        [
            firstUser, null
        ];
        state = new int?[,]
        {
            {
                null, null, null
            },
            {
                null, null, null
            },
            {
                null, null, null
            }
        };

        phase = Phase.Starting;
        moveLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    ///     Represents the event that occurs when the game ends.
    /// </summary>
    public event Action<TicTacToe> OnEnded;


    /// <summary>
    ///     Gets the current state of the game.
    /// </summary>
    public string GetState()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < state.GetLength(0); i++)
        {
            for (var j = 0; j < state.GetLength(1); j++)
            {
                sb.Append(state[i, j] == null ? numbers[i * 3 + j] : GetIcon(state[i, j]));
                if (j < state.GetLength(1) - 1)
                    sb.Append('┃');
            }

            if (i < state.GetLength(0) - 1)
                sb.AppendLine("\n──────────");
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Gets the embed of the game.
    /// </summary>
    /// <param name="title"></param>
    /// <returns>EmbedBuilder object</returns>
    public EmbedBuilder GetEmbed(string? title = null)
    {
        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Environment.NewLine + GetState())
            .WithAuthor(eab => eab.WithName(Strings.Vs(users[0].Guild.Id, users[0], users[1])));

        if (!string.IsNullOrWhiteSpace(title))
            embed.WithTitle(title);

        if (winner == null)
        {
            if (phase == Phase.Ended)
                embed.WithFooter(efb => efb.WithText(Strings.TttNoMoves(users[0].Guild.Id)));
            else
                embed.WithFooter(efb => efb.WithText(Strings.TttUsersMove(users[0].Guild.Id, users[curUserIndex])));
        }
        else
        {
            embed.WithFooter(efb => efb.WithText(Strings.TttHasWon(users[0].Guild.Id, winner)));
        }

        return embed;
    }

    private static string GetIcon(int? val)
    {
        return val switch
        {
            0 => "❌",
            1 => "⭕",
            2 => "❎",
            3 => "🅾",
            _ => "⬛"
        };
    }

    /// <summary>
    ///     Starts the game with the specified user.
    /// </summary>
    /// <param name="user"></param>
    public async Task Start(IGuildUser? user)
    {
        if (phase is Phase.Started or Phase.Ended)
        {
            await channel.SendErrorAsync(user.Mention + Strings.TttAlreadyRunning(users[0].Guild.Id), config)
                .ConfigureAwait(false);
            return;
        }

        if (users[0] == user)
        {
            await channel.SendErrorAsync(user.Mention + Strings.TttAgainstYourself(users[0].Guild.Id), config)
                .ConfigureAwait(false);
            return;
        }

        users[1] = user;

        phase = Phase.Started;

        timeoutTimer = new Timer(async _ =>
        {
            await moveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (phase == Phase.Ended)
                    return;

                phase = Phase.Ended;
                if (users[1].Username != null)
                {
                    winner = users[curUserIndex ^= 1];
                    var del = previousMessage?.DeleteAsync();
                    try
                    {
                        await channel.EmbedAsync(GetEmbed(Strings.TttTimeExpired(users[0].Guild.Id)))
                            .ConfigureAwait(false);
                        if (del != null)
                            await del.ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                OnEnded.Invoke(this);
            }
            catch
            {
                // ignored
            }
            finally
            {
                moveLock.Release();
            }
        }, null, options.TurnTimer * 1000, Timeout.Infinite);

        handler.Subscribe("MessageReceived", "TicTacToe", Client_MessageReceived);

        previousMessage =
            await channel.EmbedAsync(GetEmbed(Strings.GameStarted(users[0].Guild.Id))).ConfigureAwait(false);
    }

    private bool IsDraw()
    {
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                if (state[i, j] == null)
                    return false;
            }
        }

        return true;
    }

    private async Task Client_MessageReceived(SocketMessage msg)
    {
        await moveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var curUser = users[curUserIndex];
            if (phase == Phase.Ended || msg.Author?.Id != curUser.Id)
                return;

            if (int.TryParse(msg.Content, out var index) &&
                --index >= 0 &&
                index <= 9 &&
                state[index / 3, index % 3] == null)
            {
                state[index / 3, index % 3] = curUserIndex;

                // i'm lazy
                if (state[index / 3, 0] == state[index / 3, 1] &&
                    state[index / 3, 1] == state[index / 3, 2])
                {
                    state[index / 3, 0] = curUserIndex + 2;
                    state[index / 3, 1] = curUserIndex + 2;
                    state[index / 3, 2] = curUserIndex + 2;

                    phase = Phase.Ended;
                }
                else if (state[0, index % 3] == state[1, index % 3] &&
                         state[1, index % 3] == state[2, index % 3])
                {
                    state[0, index % 3] = curUserIndex + 2;
                    state[1, index % 3] = curUserIndex + 2;
                    state[2, index % 3] = curUserIndex + 2;

                    phase = Phase.Ended;
                }
                else if (curUserIndex == state[0, 0] && state[0, 0] == state[1, 1] &&
                         state[1, 1] == state[2, 2])
                {
                    state[0, 0] = curUserIndex + 2;
                    state[1, 1] = curUserIndex + 2;
                    state[2, 2] = curUserIndex + 2;

                    phase = Phase.Ended;
                }
                else if (curUserIndex == state[0, 2] && state[0, 2] == state[1, 1] &&
                         state[1, 1] == state[2, 0])
                {
                    state[0, 2] = curUserIndex + 2;
                    state[1, 1] = curUserIndex + 2;
                    state[2, 0] = curUserIndex + 2;

                    phase = Phase.Ended;
                }

                var reason = "";

                if (phase == Phase.Ended) // if user won, stop receiving moves
                {
                    reason = Strings.TttMatchedThree(users[0].Guild.Id);
                    winner = users[curUserIndex];
                    handler.Unsubscribe("MessageReceived", "TicTacToe", Client_MessageReceived);
                    OnEnded.Invoke(this);
                }
                else if (IsDraw())
                {
                    reason = Strings.TttADraw(users[0].Guild.Id);
                    phase = Phase.Ended;
                    handler.Unsubscribe("MessageReceived", "TicTacToe", Client_MessageReceived);
                    OnEnded.Invoke(this);
                }

                await Task.Run(async () =>
                {
                    var del1 = msg.DeleteAsync();
                    var del2 = previousMessage?.DeleteAsync();
                    try
                    {
                        previousMessage = await channel.EmbedAsync(GetEmbed(reason)).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        await del1.ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }

                    try
                    {
                        if (del2 != null) await del2.ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                });
                curUserIndex ^= 1;

                timeoutTimer.Change(options.TurnTimer * 1000, Timeout.Infinite);
            }
        }
        finally
        {
            moveLock.Release();
        }
    }

    /// <summary>
    ///     Options for configuring a Tic-Tac-Toe game.
    /// </summary>
    public class Options : IMewdekoCommandOptions
    {
        /// <summary>
        ///     Gets or sets the turn timer in seconds.
        /// </summary>
        [Option('t', "turn-timer", Required = false, Default = 15, HelpText = "Turn time in seconds. Default 15.")]
        public int TurnTimer { get; set; } = 15;

        /// <summary>
        ///     Normalizes the Tic-Tac-Toe game options.
        /// </summary>
        public void NormalizeOptions()
        {
            if (TurnTimer is < 5 or > 60)
                TurnTimer = 15;
        }
    }

    private enum Phase
    {
        Starting,
        Started,
        Ended
    }
}