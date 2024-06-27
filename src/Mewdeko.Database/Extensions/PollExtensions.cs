﻿using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class PollExtensions
{
    public async static Task<IEnumerable<Poll>> GetAllPolls(this DbSet<Poll> set) =>
        await set.Include(x => x.Answers)
            .Include(x => x.Votes)
            .ToArrayAsyncEF();

    public static async Task RemovePoll(this MewdekoContext ctx, int id)
    {
        var p = await ctx
            .Poll
            .Include(x => x.Answers)
            .Include(x => x.Votes)
            .FirstOrDefaultAsyncEF(x => x.Id == id);
        if (p?.Votes != null)
        {
            ctx.RemoveRange(p.Votes);
            p.Votes.Clear();
        }

        if (p?.Answers != null)
        {
            ctx.RemoveRange(p.Answers);
            p.Answers.Clear();
        }

        ctx.Remove(p!);
    }
}