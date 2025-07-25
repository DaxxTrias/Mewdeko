﻿using System.IO;
using System.Text.Json;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Common.Acrophobia;
using Mewdeko.Modules.Games.Common.Hangman;
using Mewdeko.Modules.Games.Common.Nunchi;
using Mewdeko.Modules.Games.Common.Trivia;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
///     Service for managing various games and game-related operations.
/// </summary>
public class GamesService : INService, IUnloadableService
{
    private const string TypingArticlesPath = "data/typing_articles3.json";
    private readonly GamesConfigService gamesConfig;
    private readonly ILogger<GamesService> logger;
    private readonly Random rng;


    /// <summary>
    ///     Initializes a new instance of the <see cref="GamesService" /> class.
    /// </summary>
    /// <param name="gamesConfig">The configuration service for games.</param>
    public GamesService(GamesConfigService gamesConfig, ILogger<GamesService> logger)
    {
        this.gamesConfig = gamesConfig;
        this.logger = logger;

        rng = new MewdekoRandom();

        // Attempt to load typing articles from JSON file
        try
        {
            TypingArticles =
                JsonSerializer.Deserialize<List<TypingArticle>>(File.ReadAllText(TypingArticlesPath));
        }
        catch (Exception ex)
        {
            // Log a warning if loading typing articles fails
            logger.LogWarning("Error while loading typing articles {0}", ex.ToString());
            TypingArticles = [];
        }
    }

    /// <summary>
    ///     Gets the responses used by the EightBall game.
    /// </summary>
    public IReadOnlyList<string> EightBallResponses
    {
        get
        {
            return gamesConfig.Data.EightBallResponses;
        }
    }

    /// <summary>
    ///     Gets the list of typing articles.
    /// </summary>
    public List<TypingArticle> TypingArticles { get; }

    //channelId, game
    /// <summary>
    ///     Represents a collection of Acrophobia games.
    /// </summary>
    public ConcurrentDictionary<ulong, AcrophobiaGame> AcrophobiaGames { get; } = new();

    /// <summary>
    ///     Represents a collection of Hangman games.
    /// </summary>
    public ConcurrentDictionary<ulong, Hangman> HangmanGames { get; } = new();

    /// <summary>
    ///     Represents the term pool for Hangman games.
    /// </summary>
    public TermPool TermPool { get; } = new();

    /// <summary>
    ///     Represents a collection of running Trivia games.
    /// </summary>
    public ConcurrentDictionary<ulong, TriviaGame> RunningTrivias { get; } = new();

    /// <summary>
    ///     Represents a collection of TicTacToe games.
    /// </summary>
    public Dictionary<ulong, TicTacToe> TicTacToeGames { get; } = new();

    /// <summary>
    ///     Represents a collection of running Typing contests.
    /// </summary>
    public ConcurrentDictionary<ulong, TypingGame> RunningContests { get; } = new();

    /// <summary>
    ///     Represents a collection of Nunchi games.
    /// </summary>
    public ConcurrentDictionary<ulong, NunchiGame> NunchiGames { get; } = new();

    /// <summary>
    ///     Unloads all active games and clears game-related data.
    /// </summary>
    public async Task Unload()
    {
        // Dispose and clear Acrophobia games
        AcrophobiaGames.ForEach(x => x.Value.Dispose());
        AcrophobiaGames.Clear();

        // Dispose and clear Hangman games
        HangmanGames.ForEach(x => x.Value.Dispose());
        HangmanGames.Clear();

        // Stop all running trivia games
        await Task.WhenAll(RunningTrivias.Select(x => x.Value.StopGame())).ConfigureAwait(false);
        RunningTrivias.Clear();

        // Clear TicTacToe games
        TicTacToeGames.Clear();

        // Stop all running typing contests
        await Task.WhenAll(RunningContests.Select(x => x.Value.Stop())).ConfigureAwait(false);
        RunningContests.Clear();

        // Dispose and clear Nunchi games
        NunchiGames.ForEach(x => x.Value.Dispose());
        NunchiGames.Clear();
    }

    /// <summary>
    ///     Adds a new typing article to the list.
    /// </summary>
    /// <param name="user">The user who added the article.</param>
    /// <param name="text">The text of the article.</param>
    public void AddTypingArticle(IUser user, string text)
    {
        TypingArticles.Add(new TypingArticle
        {
            Source = user.ToString(),
            Extra = $"Text added on {DateTime.UtcNow} by {user}.",
            Text = text.SanitizeMentions(true)
        });

        // Save the updated list to the JSON file
        File.WriteAllText(TypingArticlesPath, JsonSerializer.Serialize(TypingArticles));
    }

    /// <summary>
    ///     Retrieves a response from the EightBall game.
    /// </summary>
    /// <param name="_">The input string (not used).</param>
    /// <returns>A response from the EightBall game.</returns>
    public string GetEightballResponse(string _)
    {
        return EightBallResponses[rng.Next(0, EightBallResponses.Count)];
    }

    /// <summary>
    ///     Removes a typing article from the list.
    /// </summary>
    /// <param name="index">The index of the article to remove.</param>
    /// <returns>The removed typing article, or null if the index is out of range.</returns>
    public TypingArticle? RemoveTypingArticle(int index)
    {
        if (index < 0 || index >= TypingArticles.Count)
            return null;

        var removed = TypingArticles[index];
        TypingArticles.RemoveAt(index);

        // Save the updated list to the JSON file
        File.WriteAllText(TypingArticlesPath, JsonSerializer.Serialize(TypingArticles));
        return removed;
    }
}