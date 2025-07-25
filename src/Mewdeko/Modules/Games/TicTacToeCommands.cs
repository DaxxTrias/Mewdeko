﻿using System.Threading;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    /// <summary>
    ///     A module containing TicTacToe commands.
    /// </summary>
    [Group]
    public class TicTacToeCommands(EventHandler handler) : MewdekoSubmodule<GamesService>
    {
        private readonly SemaphoreSlim sem = new(1, 1);

        /// <summary>
        ///     Starts a game of TicTacToe.
        /// </summary>
        /// <param name="args">Options for ttt</param>
        /// <example>.ttt</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [MewdekoOptions(typeof(TicTacToe.Options))]
        public async Task TicTacToe(params string[] args)
        {
            var (options, _) = OptionsParser.ParseFrom(new TicTacToe.Options(), args);
            var channel = (ITextChannel)ctx.Channel;

            await sem.WaitAsync(1000).ConfigureAwait(false);
            try
            {
                if (Service.TicTacToeGames.TryGetValue(channel.Id, out var game))
                {
                    _ = Task.Run(() => game.Start((IGuildUser)ctx.User));
                    return;
                }

                game = new TicTacToe(Strings, channel, (IGuildUser)ctx.User, options, Config, handler);
                Service.TicTacToeGames.Add(channel.Id, game);
                await ReplyConfirmAsync(Strings.TttCreated(ctx.Guild.Id)).ConfigureAwait(false);

                game.OnEnded += _ =>
                {
                    Service.TicTacToeGames.Remove(channel.Id);
                    sem.Dispose();
                };
            }
            finally
            {
                sem.Release();
            }
        }
    }
}