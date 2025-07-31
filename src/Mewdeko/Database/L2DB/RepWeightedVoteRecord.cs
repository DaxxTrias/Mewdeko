using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("RepWeightedVoteRecord")]
public class RepWeightedVoteRecord
{
    [Column("VoteId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public int VoteId { get; set; }

    [Column("UserId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong UserId { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChosenOptionsJson", CanBeNull = false)]
    public string ChosenOptionsJson { get; set; } = "[]";

    [Column("UserReputation")]
    public int UserReputation { get; set; }

    [Column("VoteWeight")]
    public decimal VoteWeight { get; set; }

    [Column("IsAnonymous")]
    public bool IsAnonymous { get; set; } = false;

    [Column("VotedAt")]
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}