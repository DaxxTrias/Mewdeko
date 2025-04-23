using System.Reflection;
using Mewdeko.Database.Common;
using Mewdeko.Services.Impl;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mewdeko.Database.EF.EFCore;

/// <inheritdoc />
public class MewdekoPostgresContext : MewdekoContext
{
    /// <inheritdoc />
    public MewdekoPostgresContext(DbContextOptions<MewdekoPostgresContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var credentials = new BotCredentials();
        var connString = new NpgsqlConnectionStringBuilder(credentials.PsqlConnectionString)
        {
            Pooling = true,
            MinPoolSize = 20,
            MaxPoolSize = 100,
            ConnectionIdleLifetime = 300,
            ConnectionPruningInterval = 10
        }.ToString();

        optionsBuilder
            .UseNpgsql(connString, npgsqlOptions =>
            {
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                npgsqlOptions.MaxBatchSize(1000);

                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(3),
                    errorCodesToAdd: null);
                npgsqlOptions.CommandTimeout(30);
            })
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging();
    }

    /// <summary>
    ///     Applies migrations to the database context.
    /// </summary>
    public async Task ApplyMigrations()
    {
        var toApply = (await this.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();
        if (toApply.Count != 0)
        {
            await this.Database.MigrateAsync().ConfigureAwait(false);
            await this.SaveChangesAsync().ConfigureAwait(false);

            var env = Assembly.GetExecutingAssembly();
            var pmhs = env.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IPostMigrationHandler)))
                .ToList();
            foreach (var id in toApply)
            {
                var pmhToRuns = pmhs.Where(pmh => pmh.GetCustomAttribute<MigrationAttribute>()?.Id == id).ToList();
                foreach (var pmh in pmhToRuns)
                {
                    pmh.GetMethod("PostMigrationHandler")?.Invoke(null, [id, this]);
                }
            }
        }

        await this.SaveChangesAsync();
    }
}