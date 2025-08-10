// using System.Collections.Concurrent;
// using System.Threading;
// using LinqToDB;
// using Mewdeko.Common.ModuleBehaviors;
// using Mewdeko.Database.DbContextStuff;
// using Mewdeko.Database.L2DB;
// using Mewdeko.Modules.Reputation.Common;
// using Mewdeko.Services;
// using Mewdeko.Services.Strings;
// using Microsoft.Extensions.Logging;
// using Newtonsoft.Json;
//
// namespace Mewdeko.Modules.Reputation.Services;
//
// /// <summary>
// /// Service responsible for managing time-based reputation events like Happy Hours, Weekend Bonuses, and Seasonal Events.
// /// </summary>
// public class RepEventService : INService, IReadyExecutor, IUnloadableService
// {
//     private readonly IDataConnectionFactory dbFactory;
//     private readonly ILogger<RepEventService> logger;
//     private readonly GeneratedBotStrings strings;
//
//     // Caching for active events
//     private readonly ConcurrentDictionary<ulong, List<RepEvent>> activeEventsCache = new();
//     private readonly ConcurrentDictionary<string, decimal> currentMultipliersCache = new();
//
//     // Timer for periodic event checking
//     private Timer? eventCheckTimer;
//     private readonly object lockObject = new();
//
//     /// <summary>
//     /// Initializes a new instance of the <see cref="RepEventService"/> class.
//     /// </summary>
//     /// <param name="dbFactory">The database connection factory.</param>
//     /// <param name="logger">The logger instance.</param>
//     /// <param name="strings">The localized bot strings.</param>
//     public RepEventService(
//         IDataConnectionFactory dbFactory,
//         ILogger<RepEventService> logger,
//         GeneratedBotStrings strings)
//     {
//         this.dbFactory = dbFactory;
//         this.logger = logger;
//         this.strings = strings;
//     }
//
//     /// <summary>
//     /// Called when the bot is ready to start the event checking timer.
//     /// </summary>
//     /// <returns>A task that represents the asynchronous operation.</returns>
//     public Task OnReadyAsync()
//     {
//         // Start timer to check for active events every 5 minutes
//         eventCheckTimer = new Timer(CheckActiveEventsAsync, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
//         return Task.CompletedTask;
//     }
//
//     /// <summary>
//     /// Unloads the service and disposes the timer.
//     /// </summary>
//     /// <returns>A task that represents the asynchronous operation.</returns>
//     public Task Unload()
//     {
//         eventCheckTimer?.Dispose();
//         return Task.CompletedTask;
//     }
//
//     /// <summary>
//     /// Gets the current reputation multiplier for a guild based on active events.
//     /// </summary>
//     /// <param name="guildId">The guild ID.</param>
//     /// <param name="channelId">The channel ID (optional for channel-specific multipliers).</param>
//     /// <returns>The current multiplier (1.0 = no bonus).</returns>
//     public async Task<decimal> GetCurrentMultiplierAsync(ulong guildId, ulong? channelId = null)
//     {
//         var cacheKey = $"{guildId}_{channelId ?? 0}";
//
//         if (currentMultipliersCache.TryGetValue(cacheKey, out var cachedMultiplier))
//         {
//             return cachedMultiplier;
//         }
//
//         decimal totalMultiplier = 1.0m;
//
//         await using var db = await dbFactory.CreateConnectionAsync();
//         var now = DateTime.UtcNow;
//
//         var activeEvents = await db.RepEvents
//             .Where(e => e.GuildId == guildId &&
//                        e.IsActive &&
//                        e.StartTime <= now &&
//                        e.EndTime >= now)
//             .ToListAsync();
//
//         foreach (var repEvent in activeEvents)
//         {
//             if (IsEventCurrentlyActive(repEvent))
//             {
//                 // Apply channel-specific filtering if needed
//                 if (channelId.HasValue && !string.IsNullOrEmpty(repEvent.ChannelIds))
//                 {
//                     var channelList = JsonConvert.DeserializeObject<List<ulong>>(repEvent.ChannelIds) ?? new List<ulong>();
//                     if (channelList.Count > 0 && !channelList.Contains(channelId.Value))
//                     {
//                         continue; // Skip this event for this channel
//                     }
//                 }
//
//                 totalMultiplier += (repEvent.Multiplier - 1.0m); // Stack bonuses additively
//             }
//         }
//
//         // Cache the result for 5 minutes
//         currentMultipliersCache.TryAdd(cacheKey, totalMultiplier);
//         _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
//         {
//             currentMultipliersCache.TryRemove(cacheKey, out _);
//         });
//
//         return totalMultiplier;
//     }
//
//     /// <summary>
//     /// Creates a new reputation event.
//     /// </summary>
//     /// <param name="guildId">The guild ID.</param>
//     /// <param name="eventType">The type of event.</param>
//     /// <param name="name">The event name.</param>
//     /// <param name="description">The event description.</param>
//     /// <param name="multiplier">The reputation multiplier.</param>
//     /// <param name="startTime">The start time.</param>
//     /// <param name="endTime">The end time.</param>
//     /// <param name="recurringDays">Days when event is active (for recurring events).</param>
//     /// <param name="recurringHours">Hours when event is active (for happy hours).</param>
//     /// <param name="channelIds">Specific channels for the event (optional).</param>
//     /// <param name="createdBy">User who created the event.</param>
//     /// <returns>The created event ID.</returns>
//     public async Task<int> CreateEventAsync(
//         ulong guildId,
//         string eventType,
//         string name,
//         string description,
//         decimal multiplier,
//         DateTime startTime,
//         DateTime endTime,
//         List<int>? recurringDays = null,
//         List<int>? recurringHours = null,
//         List<ulong>? channelIds = null,
//         ulong? createdBy = null)
//     {
//         await using var db = await dbFactory.CreateConnectionAsync();
//
//         var repEvent = new DataModel.RepEvent()
//         {
//             GuildId = guildId,
//             EventType = eventType,
//             Name = name,
//             Description = description,
//             Multiplier = multiplier,
//             StartTime = startTime,
//             EndTime = endTime,
//             RecurringDays = recurringDays != null ? JsonConvert.SerializeObject(recurringDays) : null,
//             RecurringHours = recurringHours != null ? JsonConvert.SerializeObject(recurringHours) : null,
//             ChannelIds = channelIds != null ? JsonConvert.SerializeObject(channelIds) : null,
//             IsActive = true,
//             CreatedBy = createdBy,
//             CreatedAt = DateTime.UtcNow
//         };
//
//         await db.InsertWithIdentityAsync(repEvent);
//
//         // Clear cache to force refresh
//         ClearEventCache(guildId);
//
//         logger.LogInformation("Created reputation event {EventName} for guild {GuildId}", name, guildId);
//         return repEvent.Id;
//     }
//
//     /// <summary>
//     /// Creates a happy hour event.
//     /// </summary>
//     /// <param name="guildId">The guild ID.</param>
//     /// <param name="name">The event name.</param>
//     /// <param name="multiplier">The reputation multiplier.</param>
//     /// <param name="startHour">The start hour (0-23).</param>
//     /// <param name="endHour">The end hour (0-23).</param>
//     /// <param name="daysOfWeek">Days of the week (0=Sunday, 6=Saturday).</param>
//     /// <param name="channelIds">Specific channels (optional).</param>
//     /// <param name="createdBy">User who created the event.</param>
//     /// <returns>The created event ID.</returns>
//     public async Task<int> CreateHappyHourAsync(
//         ulong guildId,
//         string name,
//         decimal multiplier,
//         int startHour,
//         int endHour,
//         List<int> daysOfWeek,
//         List<ulong>? channelIds = null,
//         ulong? createdBy = null)
//     {
//         var hours = new List<int>();
//         if (startHour <= endHour)
//         {
//             for (int i = startHour; i <= endHour; i++)
//                 hours.Add(i);
//         }
//         else
//         {
//             // Cross midnight
//             for (int i = startHour; i <= 23; i++)
//                 hours.Add(i);
//             for (int i = 0; i <= endHour; i++)
//                 hours.Add(i);
//         }
//
//         return await CreateEventAsync(
//             guildId,
//             RepEventType.HappyHour,
//             name,
//             $"Happy hour from {startHour}:00 to {endHour}:00",
//             multiplier,
//             DateTime.UtcNow.Date, // Start today
//             DateTime.UtcNow.AddYears(1), // End in a year (recurring)
//             daysOfWeek,
//             hours,
//             channelIds,
//             createdBy);
//     }
//
//     /// <summary>
//     /// Creates a weekend bonus event.
//     /// </summary>
//     /// <param name="guildId">The guild ID.</param>
//     /// <param name="name">The event name.</param>
//     /// <param name="multiplier">The reputation multiplier.</param>
//     /// <param name="channelIds">Specific channels (optional).</param>
//     /// <param name="createdBy">User who created the event.</param>
//     /// <returns>The created event ID.</returns>
//     public async Task<int> CreateWeekendBonusAsync(
//         ulong guildId,
//         string name,
//         decimal multiplier,
//         List<ulong>? channelIds = null,
//         ulong? createdBy = null)
//     {
//         var weekendDays = new List<int> { 0, 6 }; // Sunday and Saturday
//
//         return await CreateEventAsync(
//             guildId,
//             RepEventType.WeekendBonus,
//             name,
//             "Weekend reputation bonus",
//             multiplier,
//             DateTime.UtcNow.Date,
//             DateTime.UtcNow.AddYears(1),
//             weekendDays,
//             null,
//             channelIds,
//             createdBy);
//     }
//
//     /// <summary>
//     /// Creates a seasonal event.
//     /// </summary>
//     /// <param name="guildId">The guild ID.</param>
//     /// <param name="name">The event name.</param>
//     /// <param name="description">The event description.</param>
//     /// <param name="multiplier">The reputation multiplier.</param>
//     /// <param name="startDate">The start date.</param>
//     /// <param name="endDate">The end date.</param>
//     /// <param name="channelIds">Specific channels (optional).</param>
//     /// <param name="createdBy">User who created the event.</param>
//     /// <returns>The created event ID.</returns>
//     public async Task<int> CreateSeasonalEventAsync(
//         ulong guildId,
//         string name,
//         string description,
//         decimal multiplier,
//         DateTime startDate,
//         DateTime endDate,
//         List<ulong>? channelIds = null,
//         ulong? createdBy = null)
//     {
//         return await CreateEventAsync(
//             guildId,
//             RepEventType.Seasonal,
//             name,
//             description,
//             multiplier,
//             startDate,
//             endDate,
//             null,
//             null,
//             channelIds,
//             createdBy);
//     }
//
//     /// <summary>
//     /// Gets all active events for a guild.
//     /// </summary>
//     /// <param name="guildId">The guild ID.</param>
//     /// <returns>List of active events.</returns>
//     public async Task<List<RepEvent>> GetActiveEventsAsync(ulong guildId)
//     {
//         if (activeEventsCache.TryGetValue(guildId, out var cachedEvents))
//         {
//             return cachedEvents.Where(IsEventCurrentlyActive).ToList();
//         }
//
//         await using var db = await dbFactory.CreateConnectionAsync();
//         var now = DateTime.UtcNow;
//
//         var events = await db.RepEvents
//             .Where(e => e.GuildId == guildId &&
//                        e.IsActive &&
//                        e.StartTime <= now &&
//                        e.EndTime >= now)
//             .ToListAsync();
//
//         activeEventsCache.TryAdd(guildId, events);
//         return events.Where(IsEventCurrentlyActive).ToList();
//     }
//
//     /// <summary>
//     /// Disables an event.
//     /// </summary>
//     /// <param name="eventId">The event ID.</param>
//     /// <param name="guildId">The guild ID for verification.</param>
//     /// <returns>True if the event was disabled.</returns>
//     public async Task<bool> DisableEventAsync(int eventId, ulong guildId)
//     {
//         await using var db = await dbFactory.CreateConnectionAsync();
//
//         var repEvent = await db.RepEvents
//             .Where(e => e.Id == eventId && e.GuildId == guildId)
//             .FirstOrDefaultAsync();
//
//         if (repEvent == null)
//             return false;
//
//         repEvent.IsActive = false;
//         await db.UpdateAsync(repEvent);
//
//         ClearEventCache(guildId);
//         logger.LogInformation("Disabled reputation event {EventId} for guild {GuildId}", eventId, guildId);
//         return true;
//     }
//
//     /// <summary>
//     /// Gets all events for a guild (active and inactive).
//     /// </summary>
//     /// <param name="guildId">The guild ID.</param>
//     /// <returns>List of all events.</returns>
//     public async Task<List<RepEvent>> GetAllEventsAsync(ulong guildId)
//     {
//         await using var db = await dbFactory.CreateConnectionAsync();
//         return await db.RepEvents
//             .Where(e => e.GuildId == guildId)
//             .OrderByDescending(e => e.CreatedAt)
//             .ToListAsync();
//     }
//
//     /// <summary>
//     /// Deletes an event.
//     /// </summary>
//     /// <param name="eventId">The event ID.</param>
//     /// <param name="guildId">The guild ID for verification.</param>
//     /// <returns>True if the event was deleted.</returns>
//     public async Task<bool> DeleteEventAsync(int eventId, ulong guildId)
//     {
//         await using var db = await dbFactory.CreateConnectionAsync();
//
//         var deleted = await db.RepEvents
//             .Where(e => e.Id == eventId && e.GuildId == guildId)
//             .DeleteAsync();
//
//         if (deleted > 0)
//         {
//             ClearEventCache(guildId);
//             logger.LogInformation("Deleted reputation event {EventId} for guild {GuildId}", eventId, guildId);
//         }
//
//         return deleted > 0;
//     }
//
//     /// <summary>
//     /// Checks if an event is currently active based on its schedule.
//     /// </summary>
//     /// <param name="repEvent">The event to check.</param>
//     /// <returns>True if the event is currently active.</returns>
//     private bool IsEventCurrentlyActive(RepEvent repEvent)
//     {
//         var now = DateTime.UtcNow;
//
//         // Check basic time window
//         if (now < repEvent.StartTime || now > repEvent.EndTime)
//             return false;
//
//         // Check recurring days (day of week)
//         if (!string.IsNullOrEmpty(repEvent.RecurringDays))
//         {
//             var recurringDays = JsonConvert.DeserializeObject<List<int>>(repEvent.RecurringDays) ?? new List<int>();
//             if (recurringDays.Count > 0 && !recurringDays.Contains((int)now.DayOfWeek))
//                 return false;
//         }
//
//         // Check recurring hours
//         if (!string.IsNullOrEmpty(repEvent.RecurringHours))
//         {
//             var recurringHours = JsonConvert.DeserializeObject<List<int>>(repEvent.RecurringHours) ?? new List<int>();
//             if (recurringHours.Count > 0 && !recurringHours.Contains(now.Hour))
//                 return false;
//         }
//
//         return true;
//     }
//
//     /// <summary>
//     /// Periodically checks for active events and updates caches.
//     /// </summary>
//     /// <param name="state">Timer state (not used).</param>
//     private async void CheckActiveEventsAsync(object? state)
//     {
//         try
//         {
//             lock (lockObject)
//             {
//                 // Clear multiplier cache to force refresh
//                 currentMultipliersCache.Clear();
//             }
//
//             await using var db = await dbFactory.CreateConnectionAsync();
//             var now = DateTime.UtcNow;
//
//             // Get all guilds with active events
//             var guildsWithEvents = await db.RepEvents
//                 .Where(e => e.IsActive && e.StartTime <= now && e.EndTime >= now)
//                 .Select(e => e.GuildId)
//                 .Distinct()
//                 .ToListAsync();
//
//             // Update cache for each guild
//             foreach (var guildId in guildsWithEvents)
//             {
//                 var events = await db.RepEvents
//                     .Where(e => e.GuildId == guildId &&
//                                e.IsActive &&
//                                e.StartTime <= now &&
//                                e.EndTime >= now)
//                     .ToListAsync();
//
//                 activeEventsCache.AddOrUpdate(guildId, events, (_, _) => events);
//             }
//
//             logger.LogDebug("Updated active events cache for {GuildCount} guilds", guildsWithEvents.Count);
//         }
//         catch (Exception ex)
//         {
//             logger.LogError(ex, "Error while checking active events");
//         }
//     }
//
//     /// <summary>
//     /// Clears the event cache for a specific guild.
//     /// </summary>
//     /// <param name="guildId">The guild ID.</param>
//     private void ClearEventCache(ulong guildId)
//     {
//         activeEventsCache.TryRemove(guildId, out _);
//
//         // Clear related multiplier cache entries
//         var keysToRemove = currentMultipliersCache.Keys
//             .Where(key => key.StartsWith($"{guildId}_"))
//             .ToList();
//
//         foreach (var key in keysToRemove)
//         {
//             currentMultipliersCache.TryRemove(key, out _);
//         }
//     }
// }

