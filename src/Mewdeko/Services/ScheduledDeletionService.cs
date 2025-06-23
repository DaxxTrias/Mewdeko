using System.Threading;
using LinqToDB;
using Mewdeko.Modules.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Mewdeko.Services;

/// <summary>
///     Background service that processes scheduled ticket deletions
/// </summary>
public class ScheduledDeletionService : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1); // Check every minute
    private readonly IDataConnectionFactory dbFactory;
    private readonly IServiceProvider services;

    /// <summary>
    ///     Initializes a new instance of the ScheduledDeletionService
    /// </summary>
    /// <param name="services">The service provider</param>
    /// <param name="dbFactory">The database connection factory</param>
    public ScheduledDeletionService(IServiceProvider services, IDataConnectionFactory dbFactory)
    {
        this.services = services;
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     Executes the background service
    /// </summary>
    /// <param name="stoppingToken">Cancellation token</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Scheduled Deletion Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var ticketService = scope.ServiceProvider.GetRequiredService<TicketService>();

                await ticketService.ProcessScheduledDeletionsAsync();

                // Also cleanup old processed records (older than 7 days)
                await CleanupOldDeletionRecordsAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing scheduled deletions");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        Log.Information("Scheduled Deletion Service stopped");
    }

    private async Task CleanupOldDeletionRecordsAsync(int daysOld = 7)
    {
        try
        {
            await using var ctx = await dbFactory.CreateConnectionAsync();

            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

            var deletedCount = await ctx.ScheduledTicketDeletions
                .Where(d => d.IsProcessed && d.ProcessedAt < cutoffDate)
                .DeleteAsync();

            if (deletedCount > 0)
            {
                Log.Information("Cleaned up {Count} old deletion records", deletedCount);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning up old deletion records");
        }
    }
}