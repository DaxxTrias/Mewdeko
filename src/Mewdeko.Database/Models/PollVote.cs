﻿using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

[Table("PollVote")]
public class PollVote : DbEntity
{
    public ulong UserId { get; set; }
    public int VoteIndex { get; set; }

    public override int GetHashCode() => UserId.GetHashCode();

    public override bool Equals(object obj) =>
        obj is PollVote p
        && p.UserId == UserId;
}