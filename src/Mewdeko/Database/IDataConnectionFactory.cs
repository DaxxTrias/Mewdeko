using System.Threading;
using Mewdeko.Database.DbContextStuff; // Namespace for MewdekoDb

// Namespace where your base models might live if separate

namespace Mewdeko.Database;

/// <summary>
/// Factory for creating LinqToDB data connections (MewdekoDb instances).
/// </summary>
public interface IDataConnectionFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="MewdekoDb"/> data connection.
    /// </summary>
    /// <returns>A new instance of a data connection (<see cref="MewdekoDb"/>).</returns>
    MewdekoDb CreateConnection();

    /// <summary>
    /// Creates a new instance of <see cref="MewdekoDb"/> data connection asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a new instance of <see cref="MewdekoDb"/>.</returns>
    Task<MewdekoDb> CreateConnectionAsync(CancellationToken cancellationToken = default);
}