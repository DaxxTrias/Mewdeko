using System.Data;
using Mewdeko.Database.EF.EFCore;
using Mewdeko.Database.EF.EFCore.GuildConfigs;
using Mewdeko.Modules.Administration.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mewdeko.Database.Extensions;

/// <summary>
///     Provides extension methods for working with GuildConfig entities in the MewdekoContext.
/// </summary>
public static class GuildConfigExtensions
{
    private static List<WarningPunishment> DefaultWarnPunishments
    {
        get
        {
            return
            [
                new WarningPunishment
                {
                    Count = 3, Punishment = PunishmentAction.Kick
                },

                new WarningPunishment
                {
                    Count = 5, Punishment = PunishmentAction.Ban
                }
            ];
        }
    }


    /// <summary>
    ///     Retrieves or creates a GuildConfig for a specific guild in a thread-safe manner.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="includes">Optional function to include related entities.</param>
    /// <returns>The GuildConfig for the guild.</returns>
    /// <remarks>
    ///     This method uses a serializable transaction to prevent duplicate entries when
    ///     multiple threads simultaneously attempt to create configurations for the same guild.
    ///     If a duplicate entry is detected, it rolls back and retrieves the existing config.
    /// </remarks>
    /// <exception cref="DbUpdateException">Thrown when a database error occurs other than a duplicate key violation.</exception>
    /// <exception cref="Exception">Thrown when unable to retrieve guild configuration after conflict resolution.</exception>
    public static async Task<GuildConfig> ForGuildId(this MewdekoContext ctx, ulong guildId,
        Func<DbSet<GuildConfig>, IQueryable<GuildConfig>>? includes = null)
    {
        // Use Read Committed instead of Serializable for better concurrency
        // Only escalate to Serializable when actually inserting
        var existingConfig = includes == null
            ? await ctx.GuildConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.GuildId == guildId)
            : await includes(ctx.GuildConfigs).FirstOrDefaultAsync(c => c.GuildId == guildId);

        if (existingConfig != null)
        {
            // Handle warnings initialization outside transaction if needed
            if (!existingConfig.WarningsInitialized)
            {
                existingConfig.WarningsInitialized = true;
                await ctx.SaveChangesAsync();
            }

            return existingConfig;
        }

        // Only use transaction and retry for the insertion case
        var strategy = ctx.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Use a shorter transaction with more targeted scope
            await using var transaction = await ctx.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                // One more check before inserting
                var config = await ctx.GuildConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.GuildId == guildId);
                if (config == null)
                {
                    config = new GuildConfig
                    {
                        GuildId = guildId, WarningsInitialized = true
                    };
                    ctx.GuildConfigs.Add(config);
                    await ctx.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return config;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx &&
                                               pgEx.SqlState == "23505") // Unique violation
            {
                await transaction.RollbackAsync();
                return await ctx.GuildConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.GuildId == guildId);
            }
        });
    }


    /// <summary>
    ///     Retrieves or creates a GuildConfig with LogSettings for a specific guild.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The GuildConfig for the guild.</returns>
    public static async Task<LoggingV2> LogSettingsFor(this MewdekoContext ctx, ulong guildId)
    {
        var log = await ctx.LoggingV2.FirstOrDefaultAsync(x => x.GuildId == guildId) ?? new LoggingV2
        {
            GuildId = guildId
        };
        return log;
    }

    /// <summary>
    ///     Represents a channel for generating content.
    /// </summary>
    public class GeneratingChannel
    {
        /// <summary>
        ///     Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        ///     Gets or sets the channel ID.
        /// </summary>
        public ulong ChannelId { get; set; }
    }
}