using System.Threading;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Mewdeko;

/// <summary>
///     A hosted service that manages the lifecycle of the Mewdeko bot.
/// </summary>
/// <remarks>
///     This class implements <see cref="IHostedService" /> to integrate with the .NET Core hosting model.
///     It's responsible for starting and stopping the Mewdeko bot as part of the application's lifecycle.
/// </remarks>
public class MewdekoService : IHostedService
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Mewdeko mewdeko;
    private Task? runningTask;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MewdekoService" /> class.
    /// </summary>
    /// <param name="mewdeko">The Mewdeko bot instance to be managed by this service.</param>
    public MewdekoService(Mewdeko mewdeko)
    {
        this.mewdeko = mewdeko;
    }

    /// <summary>
    ///     Starts the Mewdeko bot.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the start operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation of starting the bot.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            cancellationTokenSource.Token
        ).Token;

        // Start the bot initialization
        await mewdeko.RunAsync();

        // Keep the service running until cancellation is requested
        runningTask = Task.Run(async () =>
        {
            try
            {
                // Wait indefinitely until cancellation is requested
                await Task.Delay(Timeout.Infinite, combinedToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                Log.Information("Mewdeko service is shutting down gracefully");
            }
        }, combinedToken);
    }

    /// <summary>
    ///     Stops the Mewdeko bot.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the stop operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation of stopping the bot.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("Stopping Mewdeko service...");

        try
        {
            // Signal cancellation
            await cancellationTokenSource.CancelAsync();

            // Wait for the running task to complete with timeout
            if (runningTask != null)
            {
                await runningTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }

            // Gracefully disconnect Discord client
            if (mewdeko.Client?.ConnectionState == ConnectionState.Connected)
            {
                Log.Information("Disconnecting Discord client...");
                await mewdeko.Client.StopAsync();
                await mewdeko.Client.LogoutAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during graceful shutdown, forcing exit");
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }

        Log.Information("Mewdeko service stopped");
    }
}