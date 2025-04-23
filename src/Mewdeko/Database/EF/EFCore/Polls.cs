using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;
using Mewdeko.Modules.Games.Common;

namespace Mewdeko.Database.EF.EFCore;

/// <summary>
///     Represents a poll in a guild.
/// </summary>
[Table("Poll")]
public class Polls : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the poll question.
    /// </summary>
    public string? Question { get; set; }

    /// <summary>
    ///     Gets or sets the answers for the poll.
    /// </summary>
    public IList<PollAnswers> Answers { get; set; }

    /// <summary>
    ///     Gets or sets the type of the poll.
    /// </summary>
    public PollType PollType { get; set; }

    /// <summary>
    ///     Gets or sets the votes for the poll.
    /// </summary>
    public List<PollVote> Votes { get; set; } = [];
}

/// <summary>
///     Represents an answer for a poll.
/// </summary>
[Table("PollAnswer")]
public class PollAnswers : DbEntity, IIndexed
{
    /// <summary>
    ///     Gets or sets the text of the answer.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    ///     Gets or sets the index of the answer.
    /// </summary>
    public int Index { get; set; }
}

