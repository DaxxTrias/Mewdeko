using LinqToDB;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Games.Common;
using Serilog;
using PollAnswer = DataModel.PollAnswer;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
///     Service for managing polls in a guild.
/// </summary>
public class PollService : INService, IReadyExecutor
{
    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PollService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database service.</param>
    public PollService(IDataConnectionFactory dbFactory)
    {
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     Gets the active polls in the guilds.
    /// </summary>
    public ConcurrentDictionary<ulong, PollRunner> ActivePolls { get; set; }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        ActivePolls = (await dbContext.Polls.ToListAsync())
            .ToDictionary(x => x.GuildId, x => new PollRunner(dbFactory, x))
            .ToConcurrent();
    }

    /// <summary>
    ///     Tries to vote in the specified poll for the user.
    /// </summary>
    /// <param name="guild">The guild where the poll is taking place.</param>
    /// <param name="num">The number representing the option selected.</param>
    /// <param name="user">The user who is voting.</param>
    /// <returns>A tuple indicating whether the vote was allowed and the type of poll.</returns>
    public async Task<(bool allowed, PollType type)> TryVote(IGuild guild, int num, IUser user)
    {
        if (!ActivePolls.TryGetValue(guild.Id, out var poll))
            return (false, PollType.PollEnded);

        try
        {
            return await poll.TryVote(num, user).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error voting");
        }

        return (true, (PollType)poll.Polls.PollType);
    }

    /// <summary>
    ///     Creates a new poll.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the poll is created.</param>
    /// <param name="channelId">The ID of the channel where the poll is created.</param>
    /// <param name="input">The input string for creating the poll.</param>
    /// <param name="type">The type of the poll.</param>
    /// <returns>The created poll.</returns>
    public static DataModel.Poll? CreatePoll(ulong guildId, ulong channelId, string input, PollType type)
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

        return new DataModel.Poll
        {
            PollAnswers = col,
            Question = data[0],
            ChannelId = channelId,
            GuildId = guildId,
            PollVotes = [],
            PollType = (int)type
        };
    }

    /// <summary>
    ///     Starts a poll.
    /// </summary>
    /// <param name="p">The poll to start.</param>
    /// <returns>True if the poll started successfully, otherwise false.</returns>
    public async Task<bool> StartPoll(DataModel.Poll p)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var pr = new PollRunner(dbFactory, p);
        if (!ActivePolls.TryAdd(p.GuildId, pr)) return false;

        await dbContext.InsertAsync(p);
        return true;
    }

    /// <summary>
    ///     Stops a poll.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the poll is taking place.</param>
    /// <returns>The stopped poll.</returns>
    public async Task<DataModel.Poll?> StopPoll(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        if (!ActivePolls.TryRemove(guildId, out var pr)) return null;
        await dbContext.Polls.Select(x => pr.Polls).DeleteAsync();

        return pr.Polls;
    }
}