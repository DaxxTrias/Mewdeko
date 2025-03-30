using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Manages pooled redis connections
/// </summary>
public static class RedisConnectionManager
{
    private static ConnectionMultiplexer? _connection;

    /// <summary>
    ///     The actual connection used for redis
    /// </summary>
    /// <exception cref="InvalidOperationException">Oops!</exception>
    public static ConnectionMultiplexer Connection
    {
        get
        {
            return _connection ?? throw new InvalidOperationException("RedisConnectionManager is not initialized.");
        }
    }

    /// <summary>
    ///     Initializes the connections used for redis
    /// </summary>
    /// <param name="redisConnections">; Seperated connection IPs</param>
    public static void Initialize(string redisConnections, int shardCount)
    {
        if (_connection != null) return;
        var configurationOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            SocketManager = new SocketManager("", true),
            AsyncTimeout = 15000,        // Increase from 5000 to 15000
            SyncTimeout = 15000,         // Increase from 5000 to 15000
            ConnectTimeout = 15000,      // Add this
            DefaultDatabase = shardCount > 1 ? 8 : 0
        };

        var connections = redisConnections.Split(';');
        foreach (var connection in connections)
        {
            configurationOptions.EndPoints.Add(connection);
        }

        _connection = ConnectionMultiplexer.Connect(configurationOptions);
    }
}