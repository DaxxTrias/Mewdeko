using System.Threading;
using LinqToDB;
using LinqToDB.DataProvider.PostgreSQL;
using LinqToDB.Mapping;
using Mewdeko.Database.DbContextStuff;
using Npgsql;

namespace Mewdeko.Database.Impl;

/// <summary>
/// Implementation of <see cref="IDataConnectionFactory"/> for PostgreSQL database, creating <see cref="MewdekoDb"/> instances.
/// </summary>
public class PostgreSqlConnectionFactory : IDataConnectionFactory
{
    private readonly DataOptions dataOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlConnectionFactory"/> class.
    /// </summary>
    /// <param name="connectionString">The connection string to the PostgreSQL database.</param>
    public PostgreSqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        dataOptions = new DataOptions()
            .UsePostgreSQL(connectionString, PostgreSQLVersion.v15);
    }


    /// <summary>
    /// Creates a new instance of <see cref="MewdekoDb"/> data connection.
    /// </summary>
    /// <returns>A new instance of a <see cref="MewdekoDb"/> data connection.</returns>
    public MewdekoDb CreateConnection()
    {
        return new MewdekoDb(dataOptions);
    }

    /// <summary>
    /// Creates a new instance of <see cref="MewdekoDb"/> data connection asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a new instance of <see cref="MewdekoDb"/>.</returns>
    public Task<MewdekoDb> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateConnection());
    }
}