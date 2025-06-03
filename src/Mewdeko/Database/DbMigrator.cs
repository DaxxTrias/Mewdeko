using DbUp;
using DbUp.Engine;

namespace Mewdeko.Database;

/// <summary>
/// Database upgrade service using DbUp
/// </summary>
public class DatabaseUpgrader
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of the DatabaseUpgrader
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    public DatabaseUpgrader(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Checks if database upgrade is required
    /// </summary>
    /// <returns>True if upgrade is needed</returns>
    public bool IsUpgradeRequired()
    {
        var upgrader = BuildUpgrader();
        return upgrader.IsUpgradeRequired();
    }

    /// <summary>
    /// Gets the list of scripts that will be executed
    /// </summary>
    /// <returns>List of scripts to execute</returns>
    public IEnumerable<string> GetScriptsToExecute()
    {
        var upgrader = BuildUpgrader();
        return upgrader.GetScriptsToExecute().Select(s => s.Name);
    }

    /// <summary>
    /// Tests database connection
    /// </summary>
    /// <returns>True if connection is successful</returns>
    public bool TestConnection()
    {
        var upgrader = BuildUpgrader();
        return upgrader.TryConnect(out _);
    }

    /// <summary>
    /// Performs database upgrade with embedded SQL scripts
    /// </summary>
    /// <returns>Upgrade result with success status and error information</returns>
    public DatabaseUpgradeResult PerformUpgrade()
    {
        var upgrader = BuildUpgrader();
        return upgrader.PerformUpgrade();
    }

    /// <summary>
    /// Marks scripts as executed without running them (useful for syncing environments)
    /// </summary>
    /// <returns>True if operation was successful</returns>
    public bool MarkAsExecuted()
    {
        var upgrader = BuildUpgrader();
        return upgrader.MarkAsExecuted().Successful;
    }

    /// <summary>
    /// Builds the DbUp upgrader with configuration
    /// </summary>
    /// <returns>Configured upgrade engine</returns>
    private UpgradeEngine BuildUpgrader()
    {
        return DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseUpgrader).Assembly)
            .WithTransaction() // Wrap each script in a transaction
            .LogToConsole()
            .Build();
    }
}