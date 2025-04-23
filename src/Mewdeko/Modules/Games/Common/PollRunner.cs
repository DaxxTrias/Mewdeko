using System.Threading;
using DataModel;
using LinqToDB;
using Serilog;
using Poll = DataModel.Poll;

namespace Mewdeko.Modules.Games.Common;

/// <summary>
///     Represents a runner for managing and handling votes in a poll.
/// </summary>
public class PollRunner
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly SemaphoreSlim locker = new(1, 1);

    /// <summary>
    ///     Initializes a new instance of the <see cref="PollRunner" /> class with the specified database service and poll.
    /// </summary>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="polls">The poll to manage.</param>
    public PollRunner(IDataConnectionFactory dbFactory, Poll polls)
    {
        this.dbFactory = dbFactory;
        Polls = polls;
    }

    /// <summary>
    ///     Gets the poll managed by this poll runner.
    /// </summary>
    public Poll Polls { get; }

    /// <summary>
/// Attempts to register or remove a user's vote for a specific poll option.
/// Updates the database directly. The caller is responsible for refreshing the
/// in-memory poll state if immediate consistency is required after this method returns.
/// </summary>
/// <param name="num">The index of the vote option.</param>
/// <param name="user">The user attempting to vote.</param>
/// <returns>A tuple indicating if the vote was allowed/processed successfully and the poll type.</returns>
public async Task<(bool allowed, PollType type)> TryVote(int num, IUser user)
{
    await locker.WaitAsync().ConfigureAwait(false);
    try
    {
        var currentVotesEnumerable = Polls.PollVotes;
        if (currentVotesEnumerable == null)
        {
             Log.Warning("PollVotes collection is null for poll {PollId} during vote attempt.", Polls.Id);
             return (false, (PollType)Polls.PollType);
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        switch ((PollType)Polls.PollType)
        {
            case PollType.SingleAnswer:
            {
                if (currentVotesEnumerable.Any(x => x.UserId == user.Id))
                    return (false, PollType.SingleAnswer);

                var voteObj = new PollVote { PollId = Polls.Id, UserId = user.Id, VoteIndex = num };
                await db.InsertAsync(voteObj).ConfigureAwait(false);
                return (true, PollType.SingleAnswer);
            }

            case PollType.AllowChange:
            {
                var existingVoteInMemory = currentVotesEnumerable.FirstOrDefault(v => v.UserId == user.Id);

                if (existingVoteInMemory?.VoteIndex == num)
                    return (false, PollType.AllowChange);

                if (existingVoteInMemory != null)
                {
                    await db.GetTable<PollVote>()
                        .Where(v => v.PollId == Polls.Id && v.UserId == user.Id)
                        .DeleteAsync().ConfigureAwait(false);
                }

                var voteObj = new PollVote { PollId = Polls.Id, UserId = user.Id, VoteIndex = num };
                await db.InsertAsync(voteObj).ConfigureAwait(false);
                return (true, PollType.AllowChange);
            }

            case PollType.MultiAnswer:
            {
                var existingVoteInMemory = currentVotesEnumerable.FirstOrDefault(v => v.UserId == user.Id && v.VoteIndex == num);

                if (existingVoteInMemory != null)
                {
                    await db.GetTable<PollVote>()
                        .Where(v => v.PollId == Polls.Id && v.UserId == user.Id && v.VoteIndex == num)
                        .DeleteAsync().ConfigureAwait(false);
                    return (false, PollType.MultiAnswer);
                }
                else
                {
                    var voteObj = new PollVote { PollId = Polls.Id, UserId = user.Id, VoteIndex = num };
                    await db.InsertAsync(voteObj).ConfigureAwait(false);
                    return (true, PollType.MultiAnswer);
                }
            }

            default:
                return (false, (PollType)Polls.PollType);
        }
    }
    catch(Exception ex)
    {
         Log.Error(ex, "Error processing vote for user {UserId} on poll {PollId}", user.Id, Polls.Id);
         return (false, (PollType)Polls.PollType);
    }
    finally
    {
        locker.Release();
    }
}
}