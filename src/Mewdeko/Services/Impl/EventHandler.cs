using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Provides asynchronous event handling for Discord.NET
/// </summary>
public sealed class EventHandler : IDisposable
{
    private readonly ConcurrentDictionary<string, CircuitBreaker> circuitBreakers = new();
    private readonly DiscordShardedClient client;

    // Metrics tracking
    private readonly ConcurrentDictionary<string, EventMetrics> eventMetrics = new();

    // Multi-parameter handler storage for proper unsubscription
    private readonly ConcurrentDictionary<object, object> handlerMappings = new();
    private readonly ILogger<EventHandler> logger;
    private readonly BatchedEventProcessor<SocketMessage> messageProcessor;
    private readonly Timer metricsResetTimer;
    private readonly ConcurrentDictionary<string, ModuleMetrics> moduleMetrics = new();
    private readonly EventHandlerOptions options;
    private readonly PerformanceMonitorService perfService;
    private readonly BatchedEventProcessor<(SocketUser, SocketPresence, SocketPresence)> presenceProcessor;

    // Rate limiting
    private readonly ConcurrentDictionary<string, RateLimiter> rateLimiters = new();

    // Event subscription management
    private readonly ConcurrentDictionary<string, List<IEventSubscription>> subscriptions = new();

    private readonly BatchedEventProcessor<(Cacheable<IUser, ulong>, Cacheable<IMessageChannel, ulong>)>
        typingProcessor;

    private readonly ConcurrentDictionary<string, WeakEventManager> weakEventManagers = new();

    private volatile bool disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EventHandler" /> class.
    /// </summary>
    /// <param name="client">The Discord sharded client instance to handle events for.</param>
    /// <param name="perfService">The performance monitoring service to track event execution times.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="options">Configuration options for the event handler.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public EventHandler(
        DiscordShardedClient client,
        PerformanceMonitorService perfService,
        ILogger<EventHandler> logger,
        EventHandlerOptions options)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.perfService = perfService ?? throw new ArgumentNullException(nameof(perfService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize rate limiters
        rateLimiters["PresenceUpdated"] =
            new RateLimiter(this.options.PresenceUpdateRateLimit, TimeSpan.FromSeconds(1));
        rateLimiters["UserIsTyping"] = new RateLimiter(this.options.TypingRateLimit, TimeSpan.FromSeconds(1));

        // Initialize batched processors
        messageProcessor = new BatchedEventProcessor<SocketMessage>(
            TimeSpan.FromMilliseconds(100), 50, this.options.MaxQueueSize);
        presenceProcessor = new BatchedEventProcessor<(SocketUser, SocketPresence, SocketPresence)>(
            TimeSpan.FromSeconds(1), 100, this.options.MaxQueueSize);
        typingProcessor = new BatchedEventProcessor<(Cacheable<IUser, ulong>, Cacheable<IMessageChannel, ulong>)>(
            TimeSpan.FromMilliseconds(500), 25, this.options.MaxQueueSize);

        // Initialize metrics reset timer
        metricsResetTimer = new Timer(ResetMetrics, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

        RegisterEvents();
        StartBatchProcessors();

        this.logger.LogInformation("EventHandler initialized with {Options}", this.options);
    }

    #region Public API

    /// <summary>
    ///     Subscribes a module to a specific event type with optional filtering and priority.
    /// </summary>
    /// <typeparam name="T">The event argument type.</typeparam>
    /// <param name="moduleName">The name of the module subscribing to the event.</param>
    /// <param name="handler">The event handler delegate.</param>
    /// <param name="filter">Optional filter to determine which events to process.</param>
    /// <param name="priority">Event handler priority (higher values execute first).</param>
    /// <exception cref="ArgumentNullException">Thrown when moduleName or handler is null.</exception>
    /// <exception cref="ArgumentException">Thrown when moduleName is empty or whitespace.</exception>
    public void Subscribe<T>(string moduleName, AsyncEventHandler<T> handler, IEventFilter? filter = null,
        int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(T).Name;
        var subscription = new EventSubscription<T>
        {
            Handler = handler, Filter = filter, Priority = priority, ModuleName = moduleName
        };

        subscriptions.AddOrUpdate(eventType,
            [subscription],
            (_, existing) =>
            {
                var newList = new List<IEventSubscription>(existing)
                {
                    subscription
                };
                return newList.OrderByDescending(s => s.Priority).ToList();
            });

        // Initialize circuit breaker for this module if enabled
        if (options.EnableCircuitBreaker)
        {
            var breakerKey = $"{moduleName}_{eventType}";
            circuitBreakers.TryAdd(breakerKey, new CircuitBreaker(
                options.CircuitBreakerThreshold,
                options.CircuitBreakerTimeout));
        }

        // Initialize weak event manager if enabled
        if (options.EnableWeakReferences)
        {
            weakEventManagers.TryAdd(eventType, new WeakEventManager());
        }

        logger.LogDebug("Module {ModuleName} subscribed to {EventType} with priority {Priority}",
            moduleName, eventType, priority);
    }

    /// <summary>
    ///     Unsubscribes a module's handler from a specific event type.
    /// </summary>
    /// <typeparam name="T">The event argument type.</typeparam>
    /// <param name="moduleName">The name of the module to unsubscribe.</param>
    /// <param name="handler">The event handler delegate to remove.</param>
    public void Unsubscribe<T>(string moduleName, AsyncEventHandler<T> handler)
    {
        if (string.IsNullOrWhiteSpace(moduleName) || handler == null)
            return;

        var eventType = typeof(T).Name;
        if (!subscriptions.TryGetValue(eventType, out var existing))
            return;

        var updated = existing
            .Where(s => !(s is EventSubscription<T> typed &&
                          typed.ModuleName == moduleName &&
                          ReferenceEquals(typed.Handler, handler)))
            .ToList();

        if (updated.Count != existing.Count)
        {
            subscriptions.TryUpdate(eventType, updated, existing);
            logger.LogDebug("Module {ModuleName} unsubscribed from {EventType}", moduleName, eventType);
        }
    }

    /// <summary>
    ///     Subscribes a module to a specific event type with two parameters.
    /// </summary>
    /// <typeparam name="T1">The first event argument type.</typeparam>
    /// <typeparam name="T2">The second event argument type.</typeparam>
    /// <param name="moduleName">The name of the module subscribing to the event.</param>
    /// <param name="handler">The event handler delegate.</param>
    /// <param name="filter">Optional filter to determine which events to process.</param>
    /// <param name="priority">Event handler priority (higher values execute first).</param>
    public void Subscribe<T1, T2>(string moduleName, AsyncEventHandler<T1, T2> handler, IEventFilter? filter = null,
        int priority = 0)
    {
        var wrappedHandler = new AsyncEventHandler<(T1, T2)>(args => handler(args.Item1, args.Item2));
        handlerMappings[handler] = wrappedHandler;
        Subscribe(moduleName, wrappedHandler, filter, priority);
    }

    /// <summary>
    ///     Unsubscribes a module's handler from a specific event type with two parameters.
    /// </summary>
    /// <typeparam name="T1">The first event argument type.</typeparam>
    /// <typeparam name="T2">The second event argument type.</typeparam>
    /// <param name="moduleName">The name of the module to unsubscribe.</param>
    /// <param name="handler">The event handler delegate to remove.</param>
    public void Unsubscribe<T1, T2>(string moduleName, AsyncEventHandler<T1, T2> handler)
    {
        if (handlerMappings.TryRemove(handler, out var wrappedHandler) &&
            wrappedHandler is AsyncEventHandler<(T1, T2)> typedWrapper)
        {
            Unsubscribe(moduleName, typedWrapper);
        }
    }

    /// <summary>
    ///     Subscribes a module to a specific event type with three parameters.
    /// </summary>
    /// <typeparam name="T1">The first event argument type.</typeparam>
    /// <typeparam name="T2">The second event argument type.</typeparam>
    /// <typeparam name="T3">The third event argument type.</typeparam>
    /// <param name="moduleName">The name of the module subscribing to the event.</param>
    /// <param name="handler">The event handler delegate.</param>
    /// <param name="filter">Optional filter to determine which events to process.</param>
    /// <param name="priority">Event handler priority (higher values execute first).</param>
    public void Subscribe<T1, T2, T3>(string moduleName, AsyncEventHandler<T1, T2, T3> handler,
        IEventFilter? filter = null, int priority = 0)
    {
        var wrappedHandler = new AsyncEventHandler<(T1, T2, T3)>(args => handler(args.Item1, args.Item2, args.Item3));
        handlerMappings[handler] = wrappedHandler;
        Subscribe(moduleName, wrappedHandler, filter, priority);
    }

    /// <summary>
    ///     Unsubscribes a module's handler from a specific event type with three parameters.
    /// </summary>
    /// <typeparam name="T1">The first event argument type.</typeparam>
    /// <typeparam name="T2">The second event argument type.</typeparam>
    /// <typeparam name="T3">The third event argument type.</typeparam>
    /// <param name="moduleName">The name of the module to unsubscribe.</param>
    /// <param name="handler">The event handler delegate to remove.</param>
    public void Unsubscribe<T1, T2, T3>(string moduleName, AsyncEventHandler<T1, T2, T3> handler)
    {
        if (handlerMappings.TryRemove(handler, out var wrappedHandler) &&
            wrappedHandler is AsyncEventHandler<(T1, T2, T3)> typedWrapper)
        {
            Unsubscribe(moduleName, typedWrapper);
        }
    }

    /// <summary>
    ///     Gets current event metrics for all tracked events.
    /// </summary>
    /// <returns>A read-only dictionary of event metrics.</returns>
    public IReadOnlyDictionary<string, EventMetrics> GetEventMetrics()
    {
        return new Dictionary<string, EventMetrics>(eventMetrics);
    }

    /// <summary>
    ///     Gets current module metrics for all registered modules.
    /// </summary>
    /// <returns>A read-only dictionary of module metrics.</returns>
    public IReadOnlyDictionary<string, ModuleMetrics> GetModuleMetrics()
    {
        return new Dictionary<string, ModuleMetrics>(moduleMetrics);
    }

    #endregion

    #region Event Registration and Processing

    private void RegisterEvents()
    {
        client.MessageReceived += ClientOnMessageReceived;
        client.UserJoined += ClientOnUserJoined;
        client.UserLeft += ClientOnUserLeft;
        client.MessageDeleted += ClientOnMessageDeleted;
        client.GuildMemberUpdated += ClientOnGuildMemberUpdated;
        client.MessageUpdated += ClientOnMessageUpdated;
        client.MessagesBulkDeleted += ClientOnMessagesBulkDeleted;
        client.UserBanned += ClientOnUserBanned;
        client.UserUnbanned += ClientOnUserUnbanned;
        client.UserVoiceStateUpdated += ClientOnUserVoiceStateUpdated;
        client.UserUpdated += ClientOnUserUpdated;
        client.ChannelCreated += ClientOnChannelCreated;
        client.ChannelDestroyed += ClientOnChannelDestroyed;
        client.ChannelUpdated += ClientOnChannelUpdated;
        client.RoleDeleted += ClientOnRoleDeleted;
        client.ReactionAdded += ClientOnReactionAdded;
        client.ReactionRemoved += ClientOnReactionRemoved;
        client.ReactionsCleared += ClientOnReactionsCleared;
        client.InteractionCreated += ClientOnInteractionCreated;
        client.UserIsTyping += ClientOnUserIsTyping;
        client.PresenceUpdated += ClientOnPresenceUpdated;
        client.JoinedGuild += ClientOnJoinedGuild;
        client.GuildScheduledEventCreated += ClientOnEventCreated;
        client.RoleUpdated += ClientOnRoleUpdated;
        client.GuildUpdated += ClientOnGuildUpdated;
        client.RoleCreated += ClientOnRoleCreated;
        client.ThreadCreated += ClientOnThreadCreated;
        client.ThreadUpdated += ClientOnThreadUpdated;
        client.ThreadDeleted += ClientOnThreadDeleted;
        client.ThreadMemberJoined += ClientOnThreadMemberJoined;
        client.ThreadMemberLeft += ClientOnThreadMemberLeft;
        client.AuditLogCreated += ClientOnAuditLogCreated;
        client.GuildAvailable += ClientOnGuildAvailable;
        client.GuildUnavailable += ClientOnGuildUnavailable;
        client.LeftGuild += ClientOnLeftGuild;
        client.InviteCreated += ClientOnInviteCreated;
        client.InviteDeleted += ClientOnInviteDeleted;
        client.ModalSubmitted += ClientOnModalSubmitted;
    }

    private void StartBatchProcessors()
    {
        messageProcessor.BatchReady +=
            async batch => await ProcessEventBatch("SocketMessage", batch).ConfigureAwait(false);
        presenceProcessor.BatchReady += async batch =>
            await ProcessEventBatch("PresenceUpdated", batch).ConfigureAwait(false);
        typingProcessor.BatchReady +=
            async batch => await ProcessEventBatch("UserIsTyping", batch).ConfigureAwait(false);
    }

    private async Task ProcessEventBatch<T>(string eventType, IReadOnlyList<T> batch)
    {
        if (disposed || !this.subscriptions.TryGetValue(eventType, out var subscriptions))
            return;

        var metrics = GetOrCreateEventMetrics(eventType);
        var sw = Stopwatch.StartNew();

        try
        {
            foreach (var item in batch)
            {
                await ProcessSingleEvent(eventType, item, subscriptions).ConfigureAwait(false);
            }

            sw.Stop();
            Interlocked.Add(ref metrics.TotalProcessed, batch.Count);
            Interlocked.Add(ref metrics.TotalExecutionTime, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref metrics.TotalErrors);
            logger.LogError(ex, "Error processing batch for {EventType}", eventType);
        }
    }

    private async Task ProcessSingleEvent<T>(string eventType, T args, List<IEventSubscription> subscriptions)
    {
        var guildId = ExtractGuildId(args);
        var channelId = ExtractChannelId(args);
        var userId = ExtractUserId(args);

        foreach (var subscription in subscriptions)
        {
            if (subscription is not EventSubscription<T> typedSubscription)
                continue;

            try
            {
                // Apply filtering
                if (typedSubscription.Filter != null &&
                    !typedSubscription.Filter.ShouldProcess(guildId, channelId, userId))
                    continue;

                // Check circuit breaker
                var breakerKey = $"{typedSubscription.ModuleName}_{eventType}";
                if (options.EnableCircuitBreaker &&
                    circuitBreakers.TryGetValue(breakerKey, out var breaker) &&
                    breaker.IsOpen)
                    continue;

                // Execute handler with metrics tracking
                await ExecuteHandlerWithMetrics(typedSubscription, args, eventType).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                HandleSubscriptionError(typedSubscription, eventType, ex);
            }
        }
    }

    private async Task ExecuteHandlerWithMetrics<T>(EventSubscription<T> subscription, T args, string eventType)
    {
        var createModuleMetrics = GetOrCreateModuleMetrics(subscription.ModuleName);
        var sw = Stopwatch.StartNew();

        try
        {
            using (perfService.Measure($"Event_{eventType}_{subscription.ModuleName}"))
            {
                await subscription.Handler(args).ConfigureAwait(false);
            }

            sw.Stop();
            Interlocked.Increment(ref createModuleMetrics.EventsProcessed);
            Interlocked.Add(ref createModuleMetrics.TotalExecutionTime, sw.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            sw.Stop();
            Interlocked.Increment(ref createModuleMetrics.Errors);

            // Update circuit breaker
            if (options.EnableCircuitBreaker)
            {
                var breakerKey = $"{subscription.ModuleName}_{eventType}";
                if (circuitBreakers.TryGetValue(breakerKey, out var breaker))
                {
                    breaker.RecordFailure();
                }
            }

            throw;
        }
    }

    private void HandleSubscriptionError<T>(EventSubscription<T> subscription, string eventType, Exception ex)
    {
        var moduleMetrics = GetOrCreateModuleMetrics(subscription.ModuleName);
        Interlocked.Increment(ref moduleMetrics.Errors);

        logger.LogError(ex,
            "Error in {ModuleName} handler for {EventType}: {ErrorMessage}",
            subscription.ModuleName, eventType, ex.Message);
    }

    #endregion

    #region Event Handlers

    private Task ClientOnMessageReceived(SocketMessage arg)
    {
        if (messageProcessor != null)
            messageProcessor.Enqueue(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnPresenceUpdated(SocketUser arg1, SocketPresence arg2, SocketPresence arg3)
    {
        if (rateLimiters.TryGetValue("PresenceUpdated", out var limiter) && !limiter.TryAcquire())
            return Task.CompletedTask;

        if (presenceProcessor != null)
            presenceProcessor.Enqueue((arg1, arg2, arg3));
        return Task.CompletedTask;
    }

    private Task ClientOnUserIsTyping(Cacheable<IUser, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (rateLimiters.TryGetValue("UserIsTyping", out var limiter) && !limiter.TryAcquire())
            return Task.CompletedTask;

        if (typingProcessor != null)
            typingProcessor.Enqueue((arg1, arg2));
        return Task.CompletedTask;
    }

    // Direct processing for lower-frequency events
    private Task ClientOnUserJoined(SocketGuildUser arg)
    {
        return ProcessDirectEvent("IGuildUser", arg);
    }

    private Task ClientOnUserLeft(SocketGuild arg1, SocketUser arg2)
    {
        return ProcessDirectEvent("UserLeft", (arg1, arg2));
    }

    private Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        return ProcessDirectEvent("MessageDeleted", (arg1, arg2));
    }

    private Task ClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> arg1, SocketGuildUser arg2)
    {
        return ProcessDirectEvent("GuildMemberUpdated", (arg1, arg2));
    }

    private Task ClientOnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
    {
        return ProcessDirectEvent("MessageUpdated", (arg1, arg2, arg3));
    }

    private Task ClientOnMessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1,
        Cacheable<IMessageChannel, ulong> arg2)
    {
        return ProcessDirectEvent("MessagesBulkDeleted", (arg1, arg2));
    }

    private Task ClientOnUserBanned(SocketUser arg1, SocketGuild arg2)
    {
        return ProcessDirectEvent("UserBanned", (arg1, arg2));
    }

    private Task ClientOnUserUnbanned(SocketUser arg1, SocketGuild arg2)
    {
        return ProcessDirectEvent("UserUnbanned", (arg1, arg2));
    }

    private Task ClientOnUserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
    {
        return ProcessDirectEvent("UserVoiceStateUpdated", (arg1, arg2, arg3));
    }

    private Task ClientOnUserUpdated(SocketUser arg1, SocketUser arg2)
    {
        return ProcessDirectEvent("UserUpdated", (arg1, arg2));
    }

    private Task ClientOnChannelCreated(SocketChannel arg)
    {
        return ProcessDirectEvent("SocketChannel", arg);
    }

    private Task ClientOnChannelDestroyed(SocketChannel arg)
    {
        return ProcessDirectEvent("ChannelDestroyed", arg);
    }

    private Task ClientOnChannelUpdated(SocketChannel arg1, SocketChannel arg2)
    {
        return ProcessDirectEvent("ChannelUpdated", (arg1, arg2));
    }

    private Task ClientOnRoleDeleted(SocketRole arg)
    {
        return ProcessDirectEvent("SocketRole", arg);
    }

    private Task ClientOnReactionAdded(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2,
        SocketReaction arg3)
    {
        return ProcessDirectEvent("ReactionAdded", (arg1, arg2, arg3));
    }

    private Task ClientOnReactionRemoved(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2,
        SocketReaction arg3)
    {
        return ProcessDirectEvent("ReactionRemoved", (arg1, arg2, arg3));
    }

    private Task ClientOnReactionsCleared(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        return ProcessDirectEvent("ReactionsCleared", (arg1, arg2));
    }

    private Task ClientOnInteractionCreated(SocketInteraction arg)
    {
        return ProcessDirectEvent("SocketInteraction", arg);
    }

    private Task ClientOnJoinedGuild(SocketGuild arg)
    {
        return ProcessDirectEvent("IGuild", arg);
    }

    private Task ClientOnEventCreated(SocketGuildEvent arg)
    {
        return ProcessDirectEvent("SocketGuildEvent", arg);
    }

    private Task ClientOnRoleUpdated(SocketRole arg1, SocketRole arg2)
    {
        return ProcessDirectEvent("RoleUpdated", (arg1, arg2));
    }

    private Task ClientOnGuildUpdated(SocketGuild arg1, SocketGuild arg2)
    {
        return ProcessDirectEvent("GuildUpdated", (arg1, arg2));
    }

    private Task ClientOnRoleCreated(SocketRole arg)
    {
        return ProcessDirectEvent("RoleCreated", arg);
    }

    private Task ClientOnThreadCreated(SocketThreadChannel arg)
    {
        return ProcessDirectEvent("SocketThreadChannel", arg);
    }

    private Task ClientOnThreadUpdated(Cacheable<SocketThreadChannel, ulong> arg1, SocketThreadChannel arg2)
    {
        return ProcessDirectEvent("ThreadUpdated", (arg1, arg2));
    }

    private Task ClientOnThreadDeleted(Cacheable<SocketThreadChannel, ulong> arg)
    {
        return ProcessDirectEvent("ThreadDeleted", arg);
    }

    private Task ClientOnThreadMemberJoined(SocketThreadUser arg)
    {
        return ProcessDirectEvent("SocketThreadUser", arg);
    }

    private Task ClientOnThreadMemberLeft(SocketThreadUser arg)
    {
        return ProcessDirectEvent("ThreadMemberLeft", arg);
    }

    private Task ClientOnAuditLogCreated(SocketAuditLogEntry arg1, SocketGuild arg2)
    {
        return ProcessDirectEvent("AuditLogCreated", (arg1, arg2));
    }

    private Task ClientOnGuildAvailable(SocketGuild arg)
    {
        return ProcessDirectEvent("GuildAvailable", arg);
    }

    private Task ClientOnGuildUnavailable(SocketGuild arg)
    {
        return ProcessDirectEvent("GuildUnavailable", arg);
    }

    private Task ClientOnLeftGuild(SocketGuild arg)
    {
        return ProcessDirectEvent("LeftGuild", arg);
    }

    private Task ClientOnInviteCreated(SocketInvite arg)
    {
        return ProcessDirectEvent("IInvite", arg);
    }

    private Task ClientOnInviteDeleted(SocketGuildChannel arg1, string arg2)
    {
        return ProcessDirectEvent("InviteDeleted", (arg1, arg2));
    }

    private Task ClientOnModalSubmitted(SocketModal arg)
    {
        return ProcessDirectEvent("SocketModal", arg);
    }

    private Task ProcessDirectEvent<T>(string eventType, T args)
    {
        if (disposed || !this.subscriptions.TryGetValue(eventType, out var subscriptions))
            return Task.CompletedTask;

        _ = Task.Run(async () => await ProcessSingleEvent(eventType, args, subscriptions).ConfigureAwait(false));
        return Task.CompletedTask;
    }

    #endregion

    #region Utility Methods

    private ulong? ExtractGuildId<T>(T args)
    {
        return args switch
        {
            SocketGuildUser guildUser => guildUser.Guild.Id,
            SocketGuild guild => guild.Id,
            SocketGuildChannel guildChannel => guildChannel.Guild.Id,
            SocketMessage { Channel: SocketGuildChannel channel } => channel.Guild.Id,
            _ => null
        };
    }

    private ulong? ExtractChannelId<T>(T args)
    {
        return args switch
        {
            SocketMessage message => message.Channel.Id,
            SocketGuildChannel channel => channel.Id,
            _ => null
        };
    }

    private ulong? ExtractUserId<T>(T args)
    {
        return args switch
        {
            SocketGuildUser guildUser => guildUser.Id,
            SocketUser user => user.Id,
            SocketMessage message => message.Author.Id,
            _ => null
        };
    }

    private EventMetrics GetOrCreateEventMetrics(string eventType)
    {
        return eventMetrics.GetOrAdd(eventType, _ => new EventMetrics());
    }

    private ModuleMetrics GetOrCreateModuleMetrics(string moduleName)
    {
        return moduleMetrics.GetOrAdd(moduleName, _ => new ModuleMetrics());
    }

    private void ResetMetrics(object? state)
    {
        foreach (var metric in eventMetrics.Values)
        {
            Interlocked.Exchange(ref metric.TotalProcessed, 0);
            Interlocked.Exchange(ref metric.TotalErrors, 0);
            Interlocked.Exchange(ref metric.TotalExecutionTime, 0);
        }

        foreach (var metric in moduleMetrics.Values)
        {
            Interlocked.Exchange(ref metric.EventsProcessed, 0);
            Interlocked.Exchange(ref metric.Errors, 0);
            Interlocked.Exchange(ref metric.TotalExecutionTime, 0);
        }

        logger.LogDebug("Event and module metrics reset");
    }

    #endregion


    #region Backward Compatibility Events

    /// <summary>
    ///     Occurs when a message is received in any channel the bot has access to.
    /// </summary>
    public event AsyncEventHandler<SocketMessage>? MessageReceived
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a modal gets submitted to the bot.
    /// </summary>
    public event AsyncEventHandler<SocketModal>? ModalSubmitted
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when an invite is created in a guild.
    /// </summary>
    public event AsyncEventHandler<IInvite>? InviteCreated
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when an invite is deleted from a guild channel.
    /// </summary>
    public event AsyncEventHandler<IGuildChannel, string>? InviteDeleted
    {
        add
        {
            if (value != null)
                Subscribe<(IGuildChannel, string)>("Legacy", args => value(args.Item1, args.Item2));
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a guild scheduled event is created.
    /// </summary>
    public event AsyncEventHandler<SocketGuildEvent>? EventCreated
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a role is created in a guild.
    /// </summary>
    public event AsyncEventHandler<SocketRole>? RoleCreated
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a guild's settings are updated.
    /// </summary>
    public event AsyncEventHandler<SocketGuild, SocketGuild>? GuildUpdated
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a user joins a guild.
    /// </summary>
    public event AsyncEventHandler<IGuildUser>? UserJoined
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a role is updated in a guild.
    /// </summary>
    public event AsyncEventHandler<SocketRole, SocketRole>? RoleUpdated
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a user leaves a guild.
    /// </summary>
    public event AsyncEventHandler<IGuild, IUser>? UserLeft
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a message is deleted.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>>? MessageDeleted
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a guild member's information is updated.
    /// </summary>
    public event AsyncEventHandler<Cacheable<SocketGuildUser, ulong>, SocketGuildUser>? GuildMemberUpdated
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a message is edited.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IMessage, ulong>, SocketMessage, ISocketMessageChannel>? MessageUpdated
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when multiple messages are deleted at once.
    /// </summary>
    public event AsyncEventHandler<IReadOnlyCollection<Cacheable<IMessage, ulong>>, Cacheable<IMessageChannel, ulong>>?
        MessagesBulkDeleted
        {
            add
            {
                if (value != null)
                    Subscribe("Legacy", value);
            }
            remove
            {
                if (value != null)
                    Unsubscribe("Legacy", value);
            }
        }

    /// <summary>
    ///     Occurs when a user is banned from a guild.
    /// </summary>
    public event AsyncEventHandler<SocketUser, SocketGuild>? UserBanned
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a user is unbanned from a guild.
    /// </summary>
    public event AsyncEventHandler<SocketUser, SocketGuild>? UserUnbanned
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a user's information is updated.
    /// </summary>
    public event AsyncEventHandler<SocketUser, SocketUser>? UserUpdated
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a user's voice state changes.
    /// </summary>
    public event AsyncEventHandler<SocketUser, SocketVoiceState, SocketVoiceState>? UserVoiceStateUpdated
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a channel is created in a guild.
    /// </summary>
    public event AsyncEventHandler<SocketChannel>? ChannelCreated
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a channel is deleted from a guild.
    /// </summary>
    public event AsyncEventHandler<SocketChannel>? ChannelDestroyed
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a channel's settings are updated.
    /// </summary>
    public event AsyncEventHandler<SocketChannel, SocketChannel>? ChannelUpdated
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a role is deleted from a guild.
    /// </summary>
    public event AsyncEventHandler<SocketRole>? RoleDeleted
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a reaction is added to a message.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction>?
        ReactionAdded
        {
            add
            {
                if (value != null)
                    Subscribe("Legacy", value);
            }
            remove
            {
                if (value != null)
                    Unsubscribe("Legacy", value);
            }
        }

    /// <summary>
    ///     Occurs when a reaction is removed from a message.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction>?
        ReactionRemoved
        {
            add
            {
                if (value != null)
                    Subscribe("Legacy", value);
            }
            remove
            {
                if (value != null)
                    Unsubscribe("Legacy", value);
            }
        }

    /// <summary>
    ///     Occurs when all reactions are removed from a message.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>>? ReactionsCleared
    {
        add
        {
            if (value != null)
                Subscribe("Legacy", value);
        }
        remove
        {
            if (value != null)
                Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when an interaction (slash command, button, etc.) is created.
    /// </summary>
    public event AsyncEventHandler<SocketInteraction>? InteractionCreated
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a user starts typing in a channel.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IUser, ulong>, Cacheable<IMessageChannel, ulong>>? UserIsTyping
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a user's presence (status, activity) is updated.
    /// </summary>
    public event AsyncEventHandler<SocketUser, SocketPresence, SocketPresence>? PresenceUpdated
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when the bot joins a new guild.
    /// </summary>
    public event AsyncEventHandler<IGuild>? JoinedGuild
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a thread is created in a guild.
    /// </summary>
    public event AsyncEventHandler<SocketThreadChannel>? ThreadCreated
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a thread's settings are updated.
    /// </summary>
    public event AsyncEventHandler<Cacheable<SocketThreadChannel, ulong>, SocketThreadChannel>? ThreadUpdated
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a thread is deleted from a guild.
    /// </summary>
    public event AsyncEventHandler<Cacheable<SocketThreadChannel, ulong>>? ThreadDeleted
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a user joins a thread.
    /// </summary>
    public event AsyncEventHandler<SocketThreadUser>? ThreadMemberJoined
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a user leaves a thread.
    /// </summary>
    public event AsyncEventHandler<SocketThreadUser>? ThreadMemberLeft
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a new audit log entry is created.
    /// </summary>
    public event AsyncEventHandler<SocketAuditLogEntry, SocketGuild>? AuditLogCreated
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when the bot has connected and is ready to process events.
    /// </summary>
    public event AsyncEventHandler<DiscordShardedClient>? Ready
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a guild becomes available.
    /// </summary>
    public event AsyncEventHandler<SocketGuild>? GuildAvailable
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when a guild becomes unavailable.
    /// </summary>
    public event AsyncEventHandler<SocketGuild>? GuildUnavailable
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    /// <summary>
    ///     Occurs when the bot leaves or is removed from a guild.
    /// </summary>
    public event AsyncEventHandler<SocketGuild>? LeftGuild
    {
        add
        {
            Subscribe("Legacy", value);
        }
        remove
        {
            Unsubscribe("Legacy", value);
        }
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        try
        {
            // Stop timers
            metricsResetTimer?.Dispose();

            // Dispose processors
            messageProcessor?.Dispose();
            presenceProcessor?.Dispose();
            typingProcessor?.Dispose();

            // Unregister Discord events
            UnregisterEvents();

            logger.LogInformation("EventHandler disposed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while disposing EventHandler");
        }
    }

    private void UnregisterEvents()
    {
        client.MessageReceived -= ClientOnMessageReceived;
        client.UserJoined -= ClientOnUserJoined;
        client.UserLeft -= ClientOnUserLeft;
        client.MessageDeleted -= ClientOnMessageDeleted;
        client.GuildMemberUpdated -= ClientOnGuildMemberUpdated;
        client.MessageUpdated -= ClientOnMessageUpdated;
        client.MessagesBulkDeleted -= ClientOnMessagesBulkDeleted;
        client.UserBanned -= ClientOnUserBanned;
        client.UserUnbanned -= ClientOnUserUnbanned;
        client.UserVoiceStateUpdated -= ClientOnUserVoiceStateUpdated;
        client.UserUpdated -= ClientOnUserUpdated;
        client.ChannelCreated -= ClientOnChannelCreated;
        client.ChannelDestroyed -= ClientOnChannelDestroyed;
        client.ChannelUpdated -= ClientOnChannelUpdated;
        client.RoleDeleted -= ClientOnRoleDeleted;
        client.ReactionAdded -= ClientOnReactionAdded;
        client.ReactionRemoved -= ClientOnReactionRemoved;
        client.ReactionsCleared -= ClientOnReactionsCleared;
        client.InteractionCreated -= ClientOnInteractionCreated;
        client.UserIsTyping -= ClientOnUserIsTyping;
        client.PresenceUpdated -= ClientOnPresenceUpdated;
        client.JoinedGuild -= ClientOnJoinedGuild;
        client.GuildScheduledEventCreated -= ClientOnEventCreated;
        client.RoleUpdated -= ClientOnRoleUpdated;
        client.GuildUpdated -= ClientOnGuildUpdated;
        client.RoleCreated -= ClientOnRoleCreated;
        client.ThreadCreated -= ClientOnThreadCreated;
        client.ThreadUpdated -= ClientOnThreadUpdated;
        client.ThreadDeleted -= ClientOnThreadDeleted;
        client.ThreadMemberJoined -= ClientOnThreadMemberJoined;
        client.ThreadMemberLeft -= ClientOnThreadMemberLeft;
        client.AuditLogCreated -= ClientOnAuditLogCreated;
        client.GuildAvailable -= ClientOnGuildAvailable;
        client.GuildUnavailable -= ClientOnGuildUnavailable;
        client.LeftGuild -= ClientOnLeftGuild;
        client.InviteCreated -= ClientOnInviteCreated;
        client.InviteDeleted -= ClientOnInviteDeleted;
        client.ModalSubmitted -= ClientOnModalSubmitted;
    }

    #endregion

    #region Delegates

    /// <summary>
    ///     Represents an asynchronous event handler with a single parameter.
    /// </summary>
    /// <typeparam name="T">The type of the event argument.</typeparam>
    /// <param name="args">The event data.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public delegate Task AsyncEventHandler<in T>(T args);

    /// <summary>
    ///     Represents an asynchronous event handler with two parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the first event argument.</typeparam>
    /// <typeparam name="T2">The type of the second event argument.</typeparam>
    /// <param name="args1">The first event argument.</param>
    /// <param name="args2">The second event argument.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public delegate Task AsyncEventHandler<in T1, in T2>(T1 args1, T2 args2);

    /// <summary>
    ///     Represents an asynchronous event handler with three parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the first event argument.</typeparam>
    /// <typeparam name="T2">The type of the second event argument.</typeparam>
    /// <typeparam name="T3">The type of the third event argument.</typeparam>
    /// <param name="args1">The first event argument.</param>
    /// <param name="args2">The second event argument.</param>
    /// <param name="args3">The third event argument.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public delegate Task AsyncEventHandler<in T1, in T2, in T3>(T1 args1, T2 args2, T3 args3);

    /// <summary>
    ///     Represents an asynchronous event handler with four parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the first event argument.</typeparam>
    /// <typeparam name="T2">The type of the second event argument.</typeparam>
    /// <typeparam name="T3">The type of the third event argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth event argument.</typeparam>
    /// <param name="args1">The first event argument.</param>
    /// <param name="args2">The second event argument.</param>
    /// <param name="args3">The third event argument.</param>
    /// <param name="args4">The fourth event argument.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public delegate Task AsyncEventHandler<in T1, in T2, in T3, in T4>(T1 args1, T2 args2, T3 args3, T4 args4);

    #endregion
}

#region Supporting Classes

/// <summary>
///     Defines a contract for filtering events based on guild, channel, and user criteria.
/// </summary>
public interface IEventFilter
{
    /// <summary>
    ///     Determines whether an event should be processed based on the provided identifiers.
    /// </summary>
    /// <param name="guildId">The guild ID associated with the event, if any.</param>
    /// <param name="channelId">The channel ID associated with the event, if any.</param>
    /// <param name="userId">The user ID associated with the event, if any.</param>
    /// <returns>True if the event should be processed, false otherwise.</returns>
    bool ShouldProcess(ulong? guildId, ulong? channelId, ulong? userId);
}

/// <summary>
///     Base interface for event subscriptions.
/// </summary>
public interface IEventSubscription
{
    /// <summary>
    ///     Gets the priority of this subscription.
    /// </summary>
    int Priority { get; }

    /// <summary>
    ///     Gets the name of the module that owns this subscription.
    /// </summary>
    string ModuleName { get; }
}

/// <summary>
///     Represents a subscription to an event with filtering and priority support.
/// </summary>
/// <typeparam name="T">The event argument type.</typeparam>
public class EventSubscription<T> : IEventSubscription
{
    /// <summary>
    ///     Gets or sets the event handler delegate.
    /// </summary>
    public EventHandler.AsyncEventHandler<T> Handler { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the optional filter for this subscription.
    /// </summary>
    public IEventFilter? Filter { get; set; }

    /// <summary>
    ///     Gets or sets the priority of this subscription (higher values execute first).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    ///     Gets or sets the name of the module that owns this subscription.
    /// </summary>
    public string ModuleName { get; set; } = string.Empty;
}

/// <summary>
///     Implements a circuit breaker pattern for event handlers.
/// </summary>
public class CircuitBreaker
{
    private readonly int threshold;
    private readonly TimeSpan timeout;
    private int failureCount;
    private DateTime lastFailureTime;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CircuitBreaker" /> class.
    /// </summary>
    /// <param name="threshold">The failure threshold before opening the circuit.</param>
    /// <param name="timeout">The timeout before attempting to close the circuit.</param>
    public CircuitBreaker(int threshold, TimeSpan timeout)
    {
        this.threshold = threshold;
        this.timeout = timeout;
    }

    /// <summary>
    ///     Gets a value indicating whether the circuit breaker is open (blocking requests).
    /// </summary>
    public bool IsOpen
    {
        get
        {
            return failureCount >= threshold && DateTime.UtcNow - lastFailureTime < timeout;
        }
    }

    /// <summary>
    ///     Records a failure and potentially opens the circuit.
    /// </summary>
    public void RecordFailure()
    {
        Interlocked.Increment(ref failureCount);
        lastFailureTime = DateTime.UtcNow;
    }

    /// <summary>
    ///     Records a success and potentially resets the failure count.
    /// </summary>
    public void RecordSuccess()
    {
        Interlocked.Exchange(ref failureCount, 0);
    }
}

/// <summary>
///     Manages weak references to event handlers to prevent memory leaks.
/// </summary>
public class WeakEventManager
{
    private readonly List<WeakReference> handlers = new();
    private readonly object @lock = new();

    /// <summary>
    ///     Subscribes a handler using a weak reference.
    /// </summary>
    /// <typeparam name="T">The event argument type.</typeparam>
    /// <param name="handler">The handler to subscribe.</param>
    public void Subscribe<T>(EventHandler.AsyncEventHandler<T> handler)
    {
        lock (@lock)
        {
            handlers.Add(new WeakReference(handler));
        }
    }

    /// <summary>
    ///     Cleans up dead weak references.
    /// </summary>
    public void Cleanup()
    {
        lock (@lock)
        {
            for (var i = handlers.Count - 1; i >= 0; i--)
            {
                if (!handlers[i].IsAlive)
                {
                    handlers.RemoveAt(i);
                }
            }
        }
    }
}

/// <summary>
///     Implements rate limiting using a token bucket algorithm.
/// </summary>
public class RateLimiter
{
    private readonly int maxTokens;
    private readonly TimeSpan refillInterval;
    private DateTime lastRefill;
    private int tokens;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RateLimiter" /> class.
    /// </summary>
    /// <param name="maxTokens">The maximum number of tokens in the bucket.</param>
    /// <param name="refillInterval">The interval at which to refill tokens.</param>
    public RateLimiter(int maxTokens, TimeSpan refillInterval)
    {
        this.maxTokens = maxTokens;
        this.refillInterval = refillInterval;
        tokens = maxTokens;
        lastRefill = DateTime.UtcNow;
    }

    /// <summary>
    ///     Attempts to acquire a token from the rate limiter.
    /// </summary>
    /// <returns>True if a token was acquired, false otherwise.</returns>
    public bool TryAcquire()
    {
        RefillTokens();

        if (tokens > 0)
        {
            Interlocked.Decrement(ref tokens);
            return true;
        }

        return false;
    }

    private void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var timeSinceRefill = now - lastRefill;

        if (timeSinceRefill >= refillInterval)
        {
            Interlocked.Exchange(ref tokens, maxTokens);
            lastRefill = now;
        }
    }
}

/// <summary>
///     Processes events in batches to improve performance.
/// </summary>
/// <typeparam name="T">The event argument type.</typeparam>
public class BatchedEventProcessor<T> : IDisposable
{
    private readonly object batchLock = new();
    private readonly Timer batchTimer;
    private readonly Channel<T> channel;
    private readonly List<T> currentBatch = new();
    private readonly int maxBatchSize;
    private volatile bool disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BatchedEventProcessor{T}" /> class.
    /// </summary>
    /// <param name="batchInterval">The interval at which to process batches.</param>
    /// <param name="maxBatchSize">The maximum number of items in a batch.</param>
    /// <param name="maxQueueSize">The maximum queue size.</param>
    public BatchedEventProcessor(TimeSpan batchInterval, int maxBatchSize, int maxQueueSize)
    {
        this.maxBatchSize = maxBatchSize;

        var options = new BoundedChannelOptions(maxQueueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false
        };

        channel = Channel.CreateBounded<T>(options);
        batchTimer = new Timer(ProcessBatch, null, batchInterval, batchInterval);

        // Start background processor
        _ = Task.Run(ProcessChannelAsync);
    }

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        channel.Writer.Complete();
        batchTimer?.Dispose();

        // Process any remaining items
        ProcessBatch(null);
    }

    /// <summary>
    ///     Occurs when a batch is ready for processing.
    /// </summary>
    public event Func<IReadOnlyList<T>, Task>? BatchReady;

    /// <summary>
    ///     Enqueues an item for batch processing.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    public void Enqueue(T item)
    {
        if (!disposed)
        {
            channel.Writer.TryWrite(item);
        }
    }

    private async Task ProcessChannelAsync()
    {
        await foreach (var item in channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (disposed)
                break;

            lock (batchLock)
            {
                currentBatch.Add(item);

                if (currentBatch.Count >= maxBatchSize)
                {
                    ProcessBatch(null);
                }
            }
        }
    }

    private void ProcessBatch(object? state)
    {
        List<T>? batchToProcess = null;

        lock (batchLock)
        {
            if (currentBatch.Count > 0)
            {
                batchToProcess = new List<T>(currentBatch);
                currentBatch.Clear();
            }
        }

        if (batchToProcess?.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (BatchReady != null)
                        await BatchReady(batchToProcess).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing batch in {ProcessorType}", typeof(T).Name);
                }
            });
        }
    }
}

/// <summary>
///     Contains metrics for event processing.
/// </summary>
public class EventMetrics
{
    /// <summary>
    ///     Gets the total number of errors encountered.
    /// </summary>
    public long TotalErrors;

    /// <summary>
    ///     Gets the total execution time in milliseconds.
    /// </summary>
    public long TotalExecutionTime;

    /// <summary>
    ///     Gets the total number of events processed.
    /// </summary>
    public long TotalProcessed;

    /// <summary>
    ///     Gets the average execution time per event in milliseconds.
    /// </summary>
    public double AverageExecutionTime
    {
        get
        {
            return TotalProcessed > 0 ? (double)TotalExecutionTime / TotalProcessed : 0;
        }
    }

    /// <summary>
    ///     Gets the error rate as a percentage.
    /// </summary>
    public double ErrorRate
    {
        get
        {
            return TotalProcessed > 0 ? (double)TotalErrors / TotalProcessed * 100 : 0;
        }
    }
}

/// <summary>
///     Contains metrics for module performance.
/// </summary>
public class ModuleMetrics
{
    /// <summary>
    ///     Gets the total number of errors in this module.
    /// </summary>
    public long Errors;

    /// <summary>
    ///     Gets the total number of events processed by this module.
    /// </summary>
    public long EventsProcessed;

    /// <summary>
    ///     Gets the total execution time for this module in milliseconds.
    /// </summary>
    public long TotalExecutionTime;

    /// <summary>
    ///     Gets the average execution time per event for this module in milliseconds.
    /// </summary>
    public double AverageExecutionTime
    {
        get
        {
            return EventsProcessed > 0 ? (double)TotalExecutionTime / EventsProcessed : 0;
        }
    }

    /// <summary>
    ///     Gets the error rate for this module as a percentage.
    /// </summary>
    public double ErrorRate
    {
        get
        {
            return EventsProcessed > 0 ? (double)Errors / EventsProcessed * 100 : 0;
        }
    }
}

#endregion