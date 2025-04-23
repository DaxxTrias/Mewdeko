using LinqToDB;
using Mewdeko.Database.DbContextStuff;
using DataModel;

namespace Mewdeko.Database.Extensions;

/// <summary>
///     Provides extension methods for working with DiscordUser entities.
/// </summary>
public static class DiscordUserExtensions
{
    /// <summary>
    ///     Ensures that a Discord user is created in the database. If the user already exists, updates the user information.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="userId">The ID of the Discord user.</param>
    /// <param name="username">The username of the Discord user.</param>
    /// <param name="avatarId">The avatar ID of the Discord user.</param>
    public static async Task EnsureUserCreated(
        this MewdekoDb db,
        ulong userId,
        string username,
        string avatarId)
    {
        try
        {
            // Check if user exists
            var exists = await db.DiscordUsers
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (exists is not null)
            {
                // Update existing user
                exists.Username = username;
                exists.AvatarId = avatarId;
                await db.UpdateAsync(exists);
            }
            else
            {
                // Insert new user
                await db.InsertAsync(new DiscordUser
                {
                    UserId = userId, Username = username, AvatarId = avatarId, TotalXp = 0
                });
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    ///     Retrieves or creates a Discord user in the database.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="userId">The ID of the Discord user.</param>
    /// <param name="username">The username of the Discord user.</param>
    /// <param name="avatarId">The avatar ID of the Discord user.</param>
    /// <returns>The Discord user entity.</returns>
    public static async Task<DiscordUser> GetOrCreateUser(
        this MewdekoDb db,
        ulong userId,
        string username,
        string avatarId)
    {
        await db.EnsureUserCreated(userId, username, avatarId);
        var toReturn = await db.DiscordUsers.FirstOrDefaultAsync(x => x.UserId == userId);
        return toReturn;
    }

    /// <summary>
    ///     Retrieves or creates a Discord user in the database from an IUser instance.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="original">The IUser instance representing the Discord user.</param>
    /// <returns>The Discord user entity.</returns>
    public static async Task<DiscordUser> GetOrCreateUser(this MewdekoDb db, IUser? original)
    {
        var toReturn = await db.GetOrCreateUser(original.Id, original.Username, original.AvatarId);
        return toReturn;
    }

    /// <summary>
    ///     Retrieves the global rank of a Discord user based on their total XP.
    /// </summary>
    /// <param name="users">The ITable of DiscordUser entities.</param>
    /// <param name="id">The ID of the Discord user.</param>
    /// <returns>The global rank of the Discord user.</returns>
    public static async Task<int> GetUserGlobalRank(this ITable<DiscordUser> users, ulong id)
    {
        // Get user's XP
        var userXp = await users
            .Where(u => u.UserId == id)
            .Select(u => u.TotalXp)
            .FirstOrDefaultAsync();

        // Count users with higher XP
        var rank = await users
            .CountAsync(u => u.TotalXp > userXp);

        return rank + 1;
    }
}