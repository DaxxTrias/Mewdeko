using System.Threading;
using DataModel;
using Poll = DataModel.Poll;

namespace Mewdeko.Modules.Games.Common;

/// <summary>
///     Represents a runner for managing and handling votes in a poll.
///     This runner operates on an in-memory representation of the poll.
/// </summary>
public class PollRunner
{
    private readonly SemaphoreSlim _locker = new(1, 1);

    /// <summary>
    ///     Initializes a new instance of the <see cref="PollRunner" /> class with the specified poll.
    /// </summary>
    /// <param name="polls">The poll to manage in-memory.</param>
    public PollRunner(Poll polls)
    {
        Polls = polls;
    }

    /// <summary>
    ///     Gets the poll managed by this poll runner.
    /// </summary>
    public Poll Polls { get; }

    /// <summary>
    ///     Attempts to register or remove a user's vote for a specific poll option within the in-memory collection.
    /// </summary>
    /// <param name="num">The index of the vote option.</param>
    /// <param name="user">The user attempting to vote.</param>
    /// <returns>A tuple indicating if the vote was allowed/processed successfully and the poll type.</returns>
    public (bool allowed, PollType type) TryVote(int num, IUser user)
    {
        _locker.Wait();
        try
        {
            var pollType = (PollType)Polls.PollType;
            switch (pollType)
            {
                case PollType.SingleAnswer:
                {
                    if (Polls.PollVotes.Any(x => x.UserId == user.Id))
                        return (false, pollType);

                    Polls.PollVotes.Add(new PollVote
                    {
                        PollId = Polls.Id, UserId = user.Id, VoteIndex = num
                    });
                    return (true, pollType);
                }

                case PollType.AllowChange:
                {
                    var existingVote = Polls.PollVotes.FirstOrDefault(v => v.UserId == user.Id);
                    if (existingVote?.VoteIndex == num)
                        return (false, pollType);

                    if (existingVote != null)
                        Polls.PollVotes.Remove(existingVote);

                    Polls.PollVotes.Add(new PollVote
                    {
                        PollId = Polls.Id, UserId = user.Id, VoteIndex = num
                    });
                    return (true, pollType);
                }

                case PollType.MultiAnswer:
                {
                    var existingVote = Polls.PollVotes.FirstOrDefault(v => v.UserId == user.Id && v.VoteIndex == num);
                    if (existingVote != null)
                    {
                        Polls.PollVotes.Remove(existingVote);
                        return (false, pollType);
                    }

                    Polls.PollVotes.Add(new PollVote
                    {
                        PollId = Polls.Id, UserId = user.Id, VoteIndex = num
                    });
                    return (true, pollType);
                }

                default:
                    return (false, pollType);
            }
        }
        finally
        {
            _locker.Release();
        }
    }
}