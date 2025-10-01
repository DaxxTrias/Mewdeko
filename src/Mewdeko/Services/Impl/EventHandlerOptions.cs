namespace Mewdeko.Services.Impl;

/// <summary>
///     Configuration options for the enhanced EventHandler system.
/// </summary>
public class EventHandlerOptions
{
    /// <summary>
    ///     Gets or sets the maximum queue size for batched event processing.
    ///     Default value is 10,000.
    /// </summary>
    public int MaxQueueSize { get; set; } = 10000;

    /// <summary>
    ///     Gets or sets the rate limit for PresenceUpdated events (events per second).
    ///     Default value is 100 events per second.
    /// </summary>
    public int PresenceUpdateRateLimit { get; set; } = 100;

    /// <summary>
    ///     Gets or sets the rate limit for UserIsTyping events (events per second).
    ///     Default value is 50 events per second.
    /// </summary>
    public int TypingRateLimit { get; set; } = 50;

    /// <summary>
    ///     Gets or sets a value indicating whether circuit breaker functionality is enabled.
    ///     When enabled, failing event handlers will be temporarily disabled.
    ///     Default value is true.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    ///     Gets or sets the failure threshold for circuit breakers.
    ///     When a handler fails this many times, the circuit breaker will open.
    ///     Default value is 5.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the timeout duration for circuit breakers.
    ///     After this duration, the circuit breaker will attempt to close.
    ///     Default value is 5 minutes.
    /// </summary>
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Gets or sets a value indicating whether weak references are used for event handlers.
    ///     When enabled, helps prevent memory leaks from modules that forget to unsubscribe.
    ///     Default value is true.
    /// </summary>
    public bool EnableWeakReferences { get; set; } = true;

    /// <summary>
    ///     Gets or sets the batch size for MessageReceived events.
    ///     Events will be processed in batches of this size.
    ///     Default value is 50.
    /// </summary>
    public int MessageBatchSize { get; set; } = 50;

    /// <summary>
    ///     Gets or sets the batch interval for MessageReceived events.
    ///     Events will be processed at least this often regardless of batch size.
    ///     Default value is 100 milliseconds.
    /// </summary>
    public TimeSpan MessageBatchInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///     Gets or sets the batch size for PresenceUpdated events.
    ///     Events will be processed in batches of this size.
    ///     Default value is 100.
    /// </summary>
    public int PresenceBatchSize { get; set; } = 100;

    /// <summary>
    ///     Gets or sets the batch interval for PresenceUpdated events.
    ///     Events will be processed at least this often regardless of batch size.
    ///     Default value is 1 second.
    /// </summary>
    public TimeSpan PresenceBatchInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Gets or sets the batch size for UserIsTyping events.
    ///     Events will be processed in batches of this size.
    ///     Default value is 25.
    /// </summary>
    public int TypingBatchSize { get; set; } = 25;

    /// <summary>
    ///     Gets or sets the batch interval for UserIsTyping events.
    ///     Events will be processed at least this often regardless of batch size.
    ///     Default value is 500 milliseconds.
    /// </summary>
    public TimeSpan TypingBatchInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    ///     Gets or sets the interval for resetting event and module metrics.
    ///     Metrics will be reset to zero at this interval to prevent overflow.
    ///     Default value is 1 hour.
    /// </summary>
    public TimeSpan MetricsResetInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    ///     Gets or sets a value indicating whether detailed performance metrics are collected.
    ///     When enabled, provides more detailed timing and performance data.
    ///     Default value is true.
    /// </summary>
    public bool EnableDetailedMetrics { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether event filtering is enabled.
    ///     When disabled, all events are processed regardless of filters.
    ///     Default value is true.
    /// </summary>
    public bool EnableEventFiltering { get; set; } = true;

    /// <summary>
    ///     Gets or sets the maximum number of concurrent event handlers that can execute.
    ///     Controls the level of parallelism in event processing.
    ///     Default value is Environment.ProcessorCount * 2.
    /// </summary>
    public int MaxConcurrentHandlers { get; set; } = Environment.ProcessorCount * 2;

    /// <summary>
    ///     Gets or sets the timeout for individual event handler execution.
    ///     Handlers taking longer than this will be cancelled and logged as errors.
    ///     Default value is 30 seconds.
    /// </summary>
    public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Gets or sets a value indicating whether to log slow event handlers.
    ///     When enabled, handlers exceeding SlowHandlerThreshold will be logged.
    ///     Default value is true.
    /// </summary>
    public bool LogSlowHandlers { get; set; } = true;

    /// <summary>
    ///     Gets or sets the threshold for considering an event handler slow.
    ///     Handlers taking longer than this will be logged if LogSlowHandlers is enabled.
    ///     Default value is 1 second.
    /// </summary>
    public TimeSpan SlowHandlerThreshold { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Gets or sets the interval for cleanup operations (weak reference cleanup, metrics consolidation, etc.).
    ///     Default value is 5 minutes.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Gets or sets a value indicating whether to automatically retry failed event handlers.
    ///     When enabled, failed handlers will be retried up to MaxRetryAttempts times.
    ///     Default value is false.
    /// </summary>
    public bool EnableRetry { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of retry attempts for failed event handlers.
    ///     Only used when EnableRetry is true.
    ///     Default value is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the delay between retry attempts.
    ///     Only used when EnableRetry is true.
    ///     Default value is 1 second.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Gets or sets a value indicating whether to use exponential backoff for retry delays.
    ///     When enabled, retry delays will increase exponentially with each attempt.
    ///     Only used when EnableRetry is true.
    ///     Default value is true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    ///     Validates the configuration options and throws exceptions for invalid values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option has an invalid value.</exception>
    public void Validate()
    {
        if (MaxQueueSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxQueueSize), "Must be greater than 0");

        if (PresenceUpdateRateLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(PresenceUpdateRateLimit), "Must be greater than 0");

        if (TypingRateLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(TypingRateLimit), "Must be greater than 0");

        if (CircuitBreakerThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(CircuitBreakerThreshold), "Must be greater than 0");

        if (CircuitBreakerTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(CircuitBreakerTimeout), "Must be greater than zero");

        if (MessageBatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(MessageBatchSize), "Must be greater than 0");

        if (MessageBatchInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MessageBatchInterval), "Must be greater than zero");

        if (PresenceBatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(PresenceBatchSize), "Must be greater than 0");

        if (PresenceBatchInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(PresenceBatchInterval), "Must be greater than zero");

        if (TypingBatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(TypingBatchSize), "Must be greater than 0");

        if (TypingBatchInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(TypingBatchInterval), "Must be greater than zero");

        if (MetricsResetInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MetricsResetInterval), "Must be greater than zero");

        if (MaxConcurrentHandlers <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrentHandlers), "Must be greater than 0");

        if (HandlerTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(HandlerTimeout), "Must be greater than zero");

        if (SlowHandlerThreshold <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(SlowHandlerThreshold), "Must be greater than zero");

        if (CleanupInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(CleanupInterval), "Must be greater than zero");

        if (MaxRetryAttempts < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxRetryAttempts), "Must be greater than or equal to 0");

        if (RetryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RetryDelay), "Must be greater than or equal to zero");
    }

    /// <summary>
    ///     Creates a copy of the current options with optimized settings for high-traffic servers.
    /// </summary>
    /// <returns>A new instance of EventHandlerOptions optimized for high traffic.</returns>
    public EventHandlerOptions CreateHighTrafficProfile()
    {
        return new EventHandlerOptions
        {
            MaxQueueSize = Math.Max(50000, MaxQueueSize * 5),
            PresenceUpdateRateLimit = Math.Max(500, PresenceUpdateRateLimit * 5),
            TypingRateLimit = Math.Max(250, TypingRateLimit * 5),
            EnableCircuitBreaker = true,
            CircuitBreakerThreshold = Math.Max(10, CircuitBreakerThreshold * 2),
            CircuitBreakerTimeout = TimeSpan.FromMinutes(2),
            EnableWeakReferences = true,
            MessageBatchSize = Math.Max(200, MessageBatchSize * 4),
            MessageBatchInterval = TimeSpan.FromMilliseconds(50),
            PresenceBatchSize = Math.Max(500, PresenceBatchSize * 5),
            PresenceBatchInterval = TimeSpan.FromMilliseconds(500),
            TypingBatchSize = Math.Max(100, TypingBatchSize * 4),
            TypingBatchInterval = TimeSpan.FromMilliseconds(250),
            MetricsResetInterval = TimeSpan.FromMinutes(30),
            EnableDetailedMetrics = false, // Disable for performance
            EnableEventFiltering = true,
            MaxConcurrentHandlers = Environment.ProcessorCount * 4,
            HandlerTimeout = TimeSpan.FromSeconds(15),
            LogSlowHandlers = true,
            SlowHandlerThreshold = TimeSpan.FromMilliseconds(500),
            CleanupInterval = TimeSpan.FromMinutes(2),
            EnableRetry = false,
            MaxRetryAttempts = 2,
            RetryDelay = TimeSpan.FromMilliseconds(500),
            UseExponentialBackoff = true
        };
    }

    /// <summary>
    ///     Creates a copy of the current options with optimized settings for low-traffic servers.
    /// </summary>
    /// <returns>A new instance of EventHandlerOptions optimized for low traffic.</returns>
    public EventHandlerOptions CreateLowTrafficProfile()
    {
        return new EventHandlerOptions
        {
            MaxQueueSize = Math.Min(5000, MaxQueueSize),
            PresenceUpdateRateLimit = Math.Min(50, PresenceUpdateRateLimit),
            TypingRateLimit = Math.Min(25, TypingRateLimit),
            EnableCircuitBreaker = true,
            CircuitBreakerThreshold = 3,
            CircuitBreakerTimeout = TimeSpan.FromMinutes(10),
            EnableWeakReferences = true,
            MessageBatchSize = Math.Min(25, MessageBatchSize),
            MessageBatchInterval = TimeSpan.FromMilliseconds(200),
            PresenceBatchSize = Math.Min(50, PresenceBatchSize),
            PresenceBatchInterval = TimeSpan.FromSeconds(2),
            TypingBatchSize = Math.Min(10, TypingBatchSize),
            TypingBatchInterval = TimeSpan.FromSeconds(1),
            MetricsResetInterval = TimeSpan.FromHours(2),
            EnableDetailedMetrics = true,
            EnableEventFiltering = true,
            MaxConcurrentHandlers = Math.Max(2, Environment.ProcessorCount),
            HandlerTimeout = TimeSpan.FromMinutes(1),
            LogSlowHandlers = true,
            SlowHandlerThreshold = TimeSpan.FromSeconds(2),
            CleanupInterval = TimeSpan.FromMinutes(10),
            EnableRetry = true,
            MaxRetryAttempts = 3,
            RetryDelay = TimeSpan.FromSeconds(1),
            UseExponentialBackoff = true
        };
    }

    /// <summary>
    ///     Creates a copy of the current options with debug-friendly settings.
    /// </summary>
    /// <returns>A new instance of EventHandlerOptions optimized for debugging.</returns>
    public EventHandlerOptions CreateDebugProfile()
    {
        return new EventHandlerOptions
        {
            MaxQueueSize = 1000,
            PresenceUpdateRateLimit = 10,
            TypingRateLimit = 5,
            EnableCircuitBreaker = false, // Disabled for debugging
            CircuitBreakerThreshold = 1,
            CircuitBreakerTimeout = TimeSpan.FromMinutes(1),
            EnableWeakReferences = false, // Disabled for debugging
            MessageBatchSize = 5,
            MessageBatchInterval = TimeSpan.FromSeconds(1),
            PresenceBatchSize = 10,
            PresenceBatchInterval = TimeSpan.FromSeconds(5),
            TypingBatchSize = 5,
            TypingBatchInterval = TimeSpan.FromSeconds(2),
            MetricsResetInterval = TimeSpan.FromMinutes(10),
            EnableDetailedMetrics = true,
            EnableEventFiltering = true,
            MaxConcurrentHandlers = 1, // Single-threaded for debugging
            HandlerTimeout = TimeSpan.FromMinutes(5),
            LogSlowHandlers = true,
            SlowHandlerThreshold = TimeSpan.FromMilliseconds(100),
            CleanupInterval = TimeSpan.FromMinutes(1),
            EnableRetry = false, // Disabled for debugging
            MaxRetryAttempts = 0,
            RetryDelay = TimeSpan.Zero,
            UseExponentialBackoff = false
        };
    }

    /// <summary>
    ///     Returns a string representation of the current configuration.
    /// </summary>
    /// <returns>A formatted string containing the current configuration values.</returns>
    public override string ToString()
    {
        return $"EventHandlerOptions {{ " +
               $"MaxQueueSize: {MaxQueueSize}, " +
               $"PresenceUpdateRateLimit: {PresenceUpdateRateLimit}, " +
               $"TypingRateLimit: {TypingRateLimit}, " +
               $"EnableCircuitBreaker: {EnableCircuitBreaker}, " +
               $"CircuitBreakerThreshold: {CircuitBreakerThreshold}, " +
               $"CircuitBreakerTimeout: {CircuitBreakerTimeout}, " +
               $"EnableWeakReferences: {EnableWeakReferences}, " +
               $"MessageBatchSize: {MessageBatchSize}, " +
               $"MaxConcurrentHandlers: {MaxConcurrentHandlers}, " +
               $"HandlerTimeout: {HandlerTimeout} " +
               "}}";
    }
}