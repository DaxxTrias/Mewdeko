using DataModel;
using LinqToDB;
using LinqToDB.Data;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Games.Common;
using Poll = DataModel.Poll;
using PollAnswer = DataModel.PollAnswer;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
///     Service for managing polls in a guild.
/// </summary>
public class PollService : INService, IReadyExecutor
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<PollService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PollService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="logger">The logger.</param>
    public PollService(IDataConnectionFactory dbFactory, ILogger<PollService> logger)
    {
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets the active polls in the guilds.
    /// </summary>
    public ConcurrentDictionary<ulong, PollRunner> ActivePolls { get; set; } = new();

    /// <summary>
    ///     Loads active polls from the database into memory on startup.
    /// </summary>
    public async Task OnReadyAsync()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var polls = await dbContext.Polls.LoadWithAsTable(x => x.PollAnswers)
            .LoadWithAsTable(x => x.PollVotes)
            .ToListAsync();

        ActivePolls = new ConcurrentDictionary<ulong, PollRunner>(
            polls.ToDictionary(x => x.GuildId, x => new PollRunner(x))
        );
    }

    /// <summary>
    ///     Tries to vote in the specified poll for the user.
    /// </summary>
    /// <param name="guild">The guild where the poll is taking place.</param>
    /// <param name="num">The number representing the option selected.</param>
    /// <param name="user">The user who is voting.</param>
    /// <returns>A tuple indicating whether the vote was allowed and the type of poll.</returns>
    public (bool allowed, PollType type) TryVote(IGuild guild, int num, IUser user)
    {
        if (!ActivePolls.TryGetValue(guild.Id, out var poll))
            return (false, PollType.PollEnded);

        try
        {
            return poll.TryVote(num, user);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error voting");
        }

        return (true, (PollType)poll.Polls.PollType);
    }

    /// <summary>
    ///     Creates a new poll object.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the poll is created.</param>
    /// <param name="channelId">The ID of the channel where the poll is created.</param>
    /// <param name="input">The input string for creating the poll.</param>
    /// <param name="type">The type of the poll.</param>
    /// <returns>The created poll.</returns>
    public static Poll? CreatePoll(ulong guildId, ulong channelId, string input, PollType type)
    {
        if (string.IsNullOrWhiteSpace(input) || !input.Contains(';'))
            return null;
        var data = input.Split(';');
        if (data.Length < 3)
            return null;

        var col = new List<PollAnswer>(data.Skip(1)
            .Select(x => new PollAnswer
            {
                Text = x
            }));

        return new Poll
        {
            PollAnswers = col,
            Question = data[0],
            ChannelId = channelId,
            GuildId = guildId,
            PollVotes = new List<PollVote>(),
            PollType = (int)type
        };
    }

    /// <summary>
    ///     Starts a poll by inserting it into the database and adding it to the active in-memory collection.
    /// </summary>
    /// <param name="p">The poll to start.</param>
    /// <returns>True if the poll started successfully, otherwise false.</returns>
    public async Task<bool> StartPoll(Poll p)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        if (await dbContext.GetTable<Poll>().AnyAsync(x => x.GuildId == p.GuildId))
            return false;

        p.Id = await dbContext.InsertWithInt32IdentityAsync(p);

        var pr = new PollRunner(p);
        return ActivePolls.TryAdd(p.GuildId, pr);
    }

    /// <summary>
    ///     Stops a poll, persists its final state to the database, and removes it from the active collection.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the poll is taking place.</param>
    /// <returns>The stopped poll with its final vote counts.</returns>
    public async Task<Poll?> StopPoll(ulong guildId)
    {
        if (!ActivePolls.TryRemove(guildId, out var pr))
            return null;

        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            await using var tran = await dbContext.BeginTransactionAsync();

            await dbContext.GetTable<PollVote>().Where(v => v.PollId == pr.Polls.Id).DeleteAsync();

            if (pr.Polls.PollVotes.Count != 0)
            {
                foreach (var vote in pr.Polls.PollVotes) vote.PollId = pr.Polls.Id;
                await dbContext.BulkCopyAsync(pr.Polls.PollVotes);
            }

            await dbContext.GetTable<Poll>().Where(x => x.Id == pr.Polls.Id).DeleteAsync();

            await tran.CommitAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to properly stop and persist poll {PollId}", pr.Polls.Id);
        }

        return pr.Polls;
    }
}