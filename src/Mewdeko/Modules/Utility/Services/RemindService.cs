using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Services.Strings;
using Swan;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Manages and executes reminders for users at specified times.
/// </summary>
public partial class RemindService : INService, IReadyExecutor
{
    private const int MaxReminderRetryAttempts = 8;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly ILogger<RemindService> logger;

    private bool remindersInitialized;
    private readonly Regex regex = MyRegex();
    private readonly ConcurrentDictionary<int, Timer> reminderTimers;
    private readonly ConcurrentDictionary<int, int> reminderRetryAttempts = new();
    private readonly GeneratedBotStrings strings;
    private readonly GuildTimezoneService tz;

    /// <summary>
    ///     Initializes the reminder service, starting the background task to check for and execute reminders.
    /// </summary>
    /// <param name="client">The Discord client used for sending reminder notifications.</param>
    /// <param name="dbFactory">The database service for managing reminders.</param>
    /// <param name="tz">The timezone service for guild timezones.</param>
    /// <param name="strings">The bot strings service for localized messages.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public RemindService(DiscordShardedClient client, IDataConnectionFactory dbFactory, GuildTimezoneService tz,
        GeneratedBotStrings strings, ILogger<RemindService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.tz = tz;
        this.strings = strings;
        this.logger = logger;
        reminderTimers = new ConcurrentDictionary<int, Timer>();
    }

    /// <summary>
    ///     Initializes reminder timers once the bot is fully ready.
    /// </summary>
    public async Task OnReadyAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
    }

    private async Task EnsureInitializedAsync()
    {
        if (remindersInitialized)
            return;

        await initializationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (remindersInitialized)
                return;

            await InitializeRemindersAsync().ConfigureAwait(false);
            remindersInitialized = true;
            logger.LogInformation("Reminder service initialized and existing reminders scheduled.");
        }
        finally
        {
            initializationLock.Release();
        }
    }

    /// <summary>
    ///     Initializes the reminders by loading them from the database and setting timers.
    /// </summary>
    private async Task InitializeRemindersAsync()
    {
        var reminders = await GetRemindersAsync();
        foreach (var reminder in reminders)
        {
            // Only schedule reminders that haven't occurred yet
            if (reminder.When > DateTime.UtcNow)
            {
                _ = ScheduleReminder(reminder);
            }
            else
            {
                // For past reminders, either execute them immediately or clean them up
                _ = ExecuteReminderAsync(reminder);
            }
        }
    }

    /// <summary>
    ///     Schedules a reminder by setting a timer.
    /// </summary>
    /// <param name="reminder">The reminder to be scheduled.</param>
    public Task ScheduleReminder(Reminder reminder, TimeSpan? retryDelay = null)
    {
        var timeToGo = retryDelay ?? (reminder.When - DateTime.UtcNow);
        if (timeToGo <= TimeSpan.Zero)
        {
            timeToGo = TimeSpan.Zero;
        }

        var timer = new Timer(async _ => await ExecuteReminderAsync(reminder), null, timeToGo,
            Timeout.InfiniteTimeSpan);
        if (reminderTimers.TryGetValue(reminder.Id, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        reminderTimers[reminder.Id] = timer;
        return Task.CompletedTask;
    }

    private async Task<List<Reminder>> GetRemindersAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.Reminders.ToListAsync();
    }

    /// <summary>
    ///     Executes the reminder action.
    /// </summary>
    /// <param name="reminder">The reminder to be executed.</param>
    private async Task ExecuteReminderAsync(Reminder reminder)
    {
        try
        {
            var ch = await ResolveReminderChannelAsync(reminder).ConfigureAwait(false);

            if (ch == null)
            {
                if (!TryScheduleRetry(reminder, "target channel/user is unavailable"))
                    await RemoveReminder(reminder).ConfigureAwait(false);
                return;
            }

            var reminderUser = reminder.UserId.ToString();
            try
            {
                var fetchedUser = await ch.GetUserAsync(reminder.UserId).ConfigureAwait(false);
                if (fetchedUser != null)
                    reminderUser = fetchedUser.ToString();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to resolve reminder author {UserId} for reminder {ReminderId}",
                    reminder.UserId, reminder.Id);
            }

            await ch.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(strings.ReminderTitle(reminder.ServerId))
                    .AddField("Created At",
                        reminder.DateAdded.HasValue ? reminder.DateAdded.Value.ToLongDateString() : "?")
                    .AddField("By", reminderUser),
                reminder.Message).ConfigureAwait(false);

            // Remove the executed reminder from the database and timer
            await RemoveReminder(reminder);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.CannotSendMessageToUser)
        {
            if (!TryScheduleRetry(reminder, $"cannot send message to user {reminder.UserId}", ex))
                await RemoveReminder(reminder).ConfigureAwait(false);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownChannel)
        {
            logger.LogWarning("Channel {ChannelId} no longer exists, removing reminder {ReminderId}",
                reminder.ChannelId, reminder.Id);
            await RemoveReminder(reminder);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
        {
            logger.LogWarning(
                "Missing permissions to send reminder in channel {ChannelId}, removing reminder {ReminderId}",
                reminder.ChannelId, reminder.Id);
            await RemoveReminder(reminder);
        }
        catch (HttpException ex)
        {
            if (!TryScheduleRetry(reminder, $"discord API error {ex.DiscordCode}", ex))
                await RemoveReminder(reminder).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (!TryScheduleRetry(reminder, "unexpected exception", ex))
                await RemoveReminder(reminder).ConfigureAwait(false);
        }
    }

    private bool TryScheduleRetry(Reminder reminder, string reason, Exception? ex = null)
    {
        var attempt = reminderRetryAttempts.AddOrUpdate(reminder.Id, 1, (_, current) => current + 1);
        if (attempt > MaxReminderRetryAttempts)
        {
            reminderRetryAttempts.TryRemove(reminder.Id, out _);
            logger.LogWarning(ex,
                "Giving up reminder {ReminderId} after {Attempts} failed attempts ({Reason}), removing reminder.",
                reminder.Id, attempt - 1, reason);
            return false;
        }

        var retryDelay = CalculateRetryDelay(attempt);
        logger.LogWarning(ex,
            "Retrying reminder {ReminderId} in {Delay} (attempt {Attempt}/{MaxAttempts}) due to {Reason}.",
            reminder.Id, retryDelay, attempt, MaxReminderRetryAttempts, reason);
        _ = ScheduleReminder(reminder, retryDelay);
        return true;
    }

    private static TimeSpan CalculateRetryDelay(int attempt)
    {
        var seconds = Math.Min(900, 15 * Math.Pow(2, Math.Max(0, attempt - 1)));
        return TimeSpan.FromSeconds(seconds);
    }

    private async Task<IMessageChannel?> ResolveReminderChannelAsync(Reminder reminder)
    {
        if (reminder.IsPrivate)
        {
            IUser? user = client.GetUser(reminder.ChannelId);
            user ??= await client.Rest.GetUserAsync(reminder.ChannelId).ConfigureAwait(false);
            return user == null
                ? null
                : await user.CreateDMChannelAsync().ConfigureAwait(false);
        }

        var channel = client.GetChannel(reminder.ChannelId) as IMessageChannel;
        if (channel != null)
            return channel;

        var restChannel = await client.Rest.GetChannelAsync(reminder.ChannelId).ConfigureAwait(false);
        return restChannel as IMessageChannel;
    }

    /// <summary>
    ///     Removes the reminder from the database and disposes of its timer.
    /// </summary>
    /// <param name="reminder">The reminder to be removed.</param>
    private async Task RemoveReminder(Reminder reminder)
    {
        reminderRetryAttempts.TryRemove(reminder.Id, out _);
        if (reminderTimers.TryRemove(reminder.Id, out var timer))
        {
            await timer.DisposeAsync();
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        // Use LinqToDB to delete the reminder
        await db.Reminders
            .Where(r => r.Id == reminder.Id)
            .DeleteAsync();
    }

    /// <summary>
    ///     Creates a new reminder and schedules it for execution.
    /// </summary>
    /// <param name="targetId">The ID of the target channel or user.</param>
    /// <param name="isPrivate">Whether the reminder should be sent as a private message.</param>
    /// <param name="ts">The time span until the reminder should be triggered.</param>
    /// <param name="message">The reminder message content.</param>
    /// <param name="userId">The ID of the user who created the reminder.</param>
    /// <param name="serverId">The ID of the server where the reminder was created, or null for private reminders.</param>
    /// <param name="sanitizeMentions">Whether to sanitize mentions in the message.</param>
    /// <returns>A tuple containing success status and response message.</returns>
    public async Task<(bool success, string message)> CreateReminderAsync(
        ulong targetId,
        bool isPrivate,
        TimeSpan ts,
        string message,
        ulong userId,
        ulong? serverId,
        bool sanitizeMentions)
    {
        if (ts > TimeSpan.FromDays(60))
            return (false, string.Empty);

        var time = DateTime.UtcNow + ts;

        if (sanitizeMentions)
        {
            message = message.SanitizeAllMentions();
        }

        var rem = new Reminder
        {
            ChannelId = targetId,
            IsPrivate = isPrivate,
            When = time,
            DateAdded = DateTime.UtcNow,
            Message = message,
            UserId = userId,
            ServerId = serverId ?? 0
        };

        await using var db = await dbFactory.CreateConnectionAsync();

        // Insert using LinqToDB
        rem.Id = await db.InsertWithInt32IdentityAsync(rem);

        await ScheduleReminder(rem);

        var gTime = serverId == null
            ? time
            : TimeZoneInfo.ConvertTime(time, tz.GetTimeZoneOrUtc(serverId.Value));

        var unixTime = gTime.ToUnixEpochDate();
        var response = $"⏰ {strings.Remind(serverId ?? 0,
            Format.Bold(!isPrivate ? $"<#{targetId}>" : userId.ToString()),
            Format.Bold(message),
            $"<t:{unixTime}:R>")}";

        return (true, response);
    }

    /// <summary>
    ///     Retrieves all reminders for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user whose reminders to retrieve.</param>
    /// <returns>A list of all reminders for the specified user.</returns>
    public async Task<List<Reminder>> GetUserRemindersAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.Reminders
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.When)
            .ToListAsync();
    }

    /// <summary>
    ///     Deletes a specific reminder for a user.
    /// </summary>
    /// <param name="userId">The ID of the user who owns the reminder.</param>
    /// <param name="index">The index of the reminder to delete.</param>
    /// <returns>True if the reminder was deleted successfully, false if it wasn't found.</returns>
    public async Task<bool> DeleteReminderAsync(ulong userId, int index)
    {
        if (index < 0)
            return false;

        var rems = await GetUserRemindersAsync(userId);

        var pageIndex = index % 10;
        if (rems.Count <= pageIndex)
            return false;

        var rem = rems[pageIndex];

        await using var db = await dbFactory.CreateConnectionAsync();

        // Delete using LinqToDB
        await db.Reminders
            .Where(r => r.Id == rem.Id)
            .DeleteAsync();

        if (reminderTimers.TryRemove(rem.Id, out var timer))
        {
            await timer.DisposeAsync();
        }

        return true;
    }

    /// <summary>
    ///     Parses a remind command input and extracts the reminder details.
    /// </summary>
    /// <param name="input">The input string containing the remind command and its parameters.</param>
    /// <param name="obj">When this method returns, contains the reminder object created from the input.</param>
    /// <returns>true if the input could be parsed; otherwise, false.</returns>
    /// <remarks>
    ///     The method uses a regular expression to parse the input and extract reminder details like time and message.
    /// </remarks>
    public bool TryParseRemindMessage(string input, out RemindObject obj)
    {
        var m = MyRegex().Match(input);

        obj = default;
        if (m.Length == 0) return false;

        var values = new Dictionary<string, int>();

        var what = m.Groups["what"].Value;

        if (string.IsNullOrWhiteSpace(what))
        {
            logger.LogWarning("No message provided for the reminder");
            return false;
        }

        foreach (var groupName in regex.GetGroupNames())
        {
            if (groupName is "0" or "what") continue;
            if (string.IsNullOrWhiteSpace(m.Groups[groupName].Value))
            {
                values[groupName] = 0;
                continue;
            }

            if (!int.TryParse(m.Groups[groupName].Value, out var value))
            {
                logger.LogWarning($"Reminder regex group {groupName} has invalid value.");
                return false;
            }

            if (value < 1)
            {
                logger.LogWarning("Reminder time value has to be an integer greater than 0");
                return false;
            }

            values[groupName] = value;
        }

        var ts = new TimeSpan
        (
            30 * values["mo"] + 7 * values["w"] + values["d"],
            values["h"],
            values["m"],
            values["s"]
        );

        obj = new RemindObject
        {
            Time = ts, What = what
        };

        return true;
    }

    [GeneratedRegex(
        @"^(?:in\s?)?\s*(?:(?<mo>\d+)(?:\s?(?:months?|mos?),?))?(?:(?:\sand\s|\s*)?(?<w>\d+)(?:\s?(?:weeks?|w),?))?(?:(?:\sand\s|\s*)?(?<d>\d+)(?:\s?(?:days?|d),?))?(?:(?:\sand\s|\s*)?(?<h>\d+)(?:\s?(?:hours?|h),?))?(?:(?:\sand\s|\s*)?(?<m>\d+)(?:\s?(?:minutes?|mins?|m),?))?(?:(?:\sand\s|\s*)?(?<s>\d+)(?:\s?(?:seconds?|secs?|s),?))?\s+(?:to:?\s+)?(?<what>(?:\r\n|[\r\n]|.)+)",
        RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    /// <summary>
    ///     Represents the details of a reminder, including what the reminder is for and the time until the reminder should
    ///     occur.
    /// </summary>
    public struct RemindObject
    {
        /// <summary>
        ///     Gets or sets the message or content of the reminder.
        /// </summary>
        /// <value>
        ///     The content of the reminder, describing what the reminder is for.
        /// </value>
        public string? What { get; set; }

        /// <summary>
        ///     Gets or sets the duration of time until the reminder should be triggered.
        /// </summary>
        /// <value>
        ///     A <see cref="TimeSpan" /> representing the amount of time until the reminder occurs.
        /// </value>
        /// <remarks>
        ///     This value is used to calculate the specific datetime when the reminder will be triggered, based on the current
        ///     time plus the TimeSpan.
        /// </remarks>
        public TimeSpan Time { get; set; }
    }
}