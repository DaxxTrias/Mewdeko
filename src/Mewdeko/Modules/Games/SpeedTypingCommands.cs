﻿using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    /// <summary>
    ///     Group containing commands for speed typing games.
    /// </summary>
    [Group]
    public class SpeedTypingCommands : MewdekoSubmodule<GamesService>
    {
        private readonly DiscordShardedClient client;
        private readonly GamesService games;
        private readonly GuildSettingsService guildSettings;
        private readonly EventHandler handler;

        /// <summary>
        ///     Initializes a new instance of <see cref="SpeedTypingCommands" />.
        /// </summary>
        /// <param name="client">The discord client</param>
        /// <param name="games">The games service for fetching configs</param>
        /// <param name="guildSettings">The guild settings service</param>
        /// <param name="handler">Async Event handler because again, discord sucks.</param>
        public SpeedTypingCommands(DiscordShardedClient client, GamesService games,
            GuildSettingsService guildSettings, EventHandler handler)
        {
            this.client = client;
            this.games = games;
            this.guildSettings = guildSettings;
            this.handler = handler;
        }

        /// <summary>
        ///     Starts a speed typing game.
        /// </summary>
        /// <param name="args">Arguments for configuring the game.</param>
        /// <example>.typestart</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [MewdekoOptions(typeof(TypingGame.Options))]
        public async Task TypeStart(params string[] args)
        {
            var (options, _) = OptionsParser.ParseFrom(new TypingGame.Options(), args);
            var channel = (ITextChannel)ctx.Channel;

            var game = Service.RunningContests.GetOrAdd(channel.Guild.Id,
                _ => new TypingGame(games, client, channel,
                    guildSettings.GetPrefix(ctx.Guild).GetAwaiter().GetResult(),
                    options, handler, Strings));

            if (game.IsActive)
            {
                await channel.SendErrorAsync(Strings.TypingContestRunning(ctx.Guild.Id, game.Channel.Mention), Config);
            }
            else
            {
                await game.Start().ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Stops the current speed typing game.
        /// </summary>
        /// <example>.typestop</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task TypeStop()
        {
            var channel = (ITextChannel)ctx.Channel;
            if (Service.RunningContests.TryRemove(channel.Guild.Id, out var game))
            {
                await game.Stop().ConfigureAwait(false);
                return;
            }

            await channel.SendErrorAsync(Strings.TypingNoContest(ctx.Guild.Id), Config);
        }

        /// <summary>
        ///     Adds a new article for the typing game.
        /// </summary>
        /// <param name="text">The text of the article to add.</param>
        /// <example>.typeadd The quick brown fox jumps over the lazy dog.</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task Typeadd([Remainder] string text)
        {
            var channel = (ITextChannel)ctx.Channel;
            if (string.IsNullOrWhiteSpace(text))
                return;

            games.AddTypingArticle(ctx.User, text);

            await channel.SendConfirmAsync(Strings.TypingArticleAdded(ctx.Guild.Id));
        }

        /// <summary>
        ///     Lists the articles available for the typing game.
        /// </summary>
        /// <param name="page">The page number to display.</param>
        /// <example>.typelist 2</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Typelist(int page = 1)
        {
            var channel = (ITextChannel)ctx.Channel;

            if (page < 1)
                return;

            var articles = games.TypingArticles.Skip((page - 1) * 15).Take(15).ToArray();

            if (articles.Length == 0)
            {
                await channel.SendErrorAsync($"{ctx.User.Mention} `No articles found on that page.`", Config)
                    .ConfigureAwait(false);
                return;
            }

            var i = (page - 1) * 15;
            await channel.SendConfirmAsync(Strings.TypingArticleList(ctx.Guild.Id,
                string.Join("\n", articles.Select(a => $"`#{++i}` - {a.Text.TrimTo(50)}"))));
        }

        /// <summary>
        ///     Deletes a typing article by its index.
        /// </summary>
        /// <param name="index">The index of the article to delete.</param>
        /// <example>.typedel 2</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task Typedel(int index)
        {
            var removed = Service.RemoveTypingArticle(--index);

            if (removed is null) return;

            var embed = new EmbedBuilder()
                .WithTitle(Strings.TypingArticleRemoved(ctx.Guild.Id, index + 1))
                .WithDescription(removed.Text.TrimTo(50))
                .WithOkColor();

            await ctx.Channel.EmbedAsync(embed);
        }
    }
}