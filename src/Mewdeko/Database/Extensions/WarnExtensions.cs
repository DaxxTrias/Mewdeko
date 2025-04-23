using DataModel;
using LinqToDB;
using Mewdeko.Database.DbContextStuff;

namespace Mewdeko.Database.Extensions;

/// <summary>
///
/// </summary>
public static class WarnExtensions
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="set"></param>
    /// <param name="guildId"></param>
    /// <param name="userId"></param>
    /// <param name="mod"></param>
    public static async Task ForgiveAll(this ITable<Warning> set, MewdekoDb db, ulong guildId, ulong userId, string mod)
    {

        var forgeev = set.Where(x => x.GuildId == guildId && x.UserId == userId && !x.Forgiven);
        foreach (var i in forgeev)
        {
            i.Forgiven = true;
            i.ForgivenBy = mod;
        }

        await db.UpdateAsync(forgeev);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="set"></param>
    /// <param name="db"></param>
    /// <param name="guildId"></param>
    /// <param name="userId"></param>
    /// <param name="mod"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public static async Task<bool> Forgive(this ITable<Warning> set, MewdekoDb db, ulong guildId, ulong userId, string mod, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var warn = await set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Skip(index)
            .FirstOrDefaultAsync();

        if (warn == null || warn.Forgiven)
            return false;

        warn.Forgiven = true;
        warn.ForgivenBy = mod;
        await db.UpdateAsync(warn);
        return true;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="set"></param>
    /// <param name="guildId"></param>
    /// <param name="userId"></param>
    /// <param name="mod"></param>
    public static async Task ForgiveAll(this ITable<Warnings2> set, MewdekoDb db, ulong guildId, ulong userId, string mod)
    {

        var forgeev = set.Where(x => x.GuildId == guildId && x.UserId == userId && !x.Forgiven);
        foreach (var i in forgeev)
        {
            i.Forgiven = true;
            i.ForgivenBy = mod;
        }

        await db.UpdateAsync(forgeev);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="set"></param>
    /// <param name="db"></param>
    /// <param name="guildId"></param>
    /// <param name="userId"></param>
    /// <param name="mod"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public static async Task<bool> Forgive(this ITable<Warnings2> set, MewdekoDb db, ulong guildId, ulong userId, string mod, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var warn = await set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Skip(index)
            .FirstOrDefaultAsync();

        if (warn == null || warn.Forgiven)
            return false;

        warn.Forgiven = true;
        warn.ForgivenBy = mod;
        await db.UpdateAsync(warn);
        return true;
    }
}