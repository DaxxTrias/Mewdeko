using System.Threading;
using DataModel;
using LinqToDB;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Services;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Provides anti-alt, anti-raid, and antispam protection services.
/// </summary>
public class ProtectionService : INService, IReadyExecutor
{
    private readonly ConcurrentDictionary<ulong, AntiAltStats> antiAltGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiMassMentionStats> antiMassMentionGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiRaidStats> antiRaidGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiSpamStats> antiSpamGuilds = new();

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService gss;
    private readonly MuteService mute;
    private readonly UserPunishService punishService;

    private readonly Channel<PunishQueueItem> punishUserQueue =
        Channel.CreateBounded<PunishQueueItem>(new BoundedChannelOptions(200)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    /// <summary>
    ///     Constructs a new instance of the ProtectionService.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="bot">The Mewdeko bot.</param>
    /// <param name="mute">The mute service.</param>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="punishService">The user punish service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="gss">The guild settings service.</param>
    public ProtectionService(DiscordShardedClient client, Mewdeko bot,
        MuteService mute, IDataConnectionFactory dbFactory, UserPunishService punishService, EventHandler eventHandler,
        GuildSettingsService gss)
    {
        this.client = client;
        this.mute = mute;
        this.dbFactory = dbFactory;
        this.punishService = punishService;
        this.gss = gss;

        eventHandler.MessageReceived += HandleAntiSpam;
        eventHandler.UserJoined += HandleUserJoined;
        eventHandler.MessageReceived += HandleAntiMassMention;

        bot.JoinedGuild += _bot_JoinedGuild;
        this.client.LeftGuild += _client_LeftGuild;

        _ = Task.Run(RunQueue);
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        foreach (var guild in client.Guilds)
        {
            try
            {
                 await Initialize(guild.Id);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error initializing protections for Guild {GuildId}", guild.Id);
            }
        }
    }

    /// <summary>
    ///     An event that is triggered when the anti-protection is triggered.
    /// </summary>
    public event Func<PunishmentAction, ProtectionType, IGuildUser[], Task> OnAntiProtectionTriggered = delegate { return Task.CompletedTask; };

    /// <summary>
    ///     The task that runs the punish queue.
    /// </summary>
    private async Task RunQueue()
    {
        while (true)
        {
            try
            {
                var item = await punishUserQueue.Reader.ReadAsync().ConfigureAwait(false);
                var muteTime = item.MuteTime;
                var gu = item.User;
                if (gu == null) continue;

                var currentUser = client.CurrentUser;
                if (currentUser == null)
                {
                     Log.Warning("Cannot apply punishment; CurrentUser is null.");
                     continue;
                }

                await punishService.ApplyPunishment(gu.Guild, gu, currentUser, (PunishmentAction)item.Action, muteTime,
                    item.RoleId, $"{item.Type} Protection").ConfigureAwait(false);

                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in punish queue: {Message}", ex.Message);
                await Task.Delay(5000);
            }
        }
    }


    /// <summary>
    ///     Handles the event when the bot leaves a guild.
    /// </summary>
    /// <param name="guild">The guild that the bot has left.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task _client_LeftGuild(SocketGuild guild)
    {
        antiRaidGuilds.TryRemove(guild.Id, out _);
        antiSpamGuilds.TryRemove(guild.Id, out _);
        antiAltGuilds.TryRemove(guild.Id, out _);
        antiMassMentionGuilds.TryRemove(guild.Id, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the event when the bot joins a guild.
    /// </summary>
    /// <param name="gc">The configuration of the guild that the bot has joined.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task _bot_JoinedGuild(GuildConfig gc)
    {
        await Initialize(gc.GuildId);
    }

    /// <summary>
    ///     Initializes the anti-raid, anti-spam, and anti-alt settings for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to initialize the settings for.</param>
    private async Task Initialize(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var raid = await db.GetTable<AntiRaidSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId).ConfigureAwait(false);
        var spam = await db.GetTable<AntiSpamSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId).ConfigureAwait(false);

        if (spam != null)
        {
             spam.AntiSpamIgnores = (await db.GetTable<AntiSpamIgnore>()
                 .Where(i => i.AntiSpamSettingId == spam.Id)
                 .ToListAsync().ConfigureAwait(false)).ToHashSet();
        }

        var alt = await db.GetTable<AntiAltSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId).ConfigureAwait(false);
        var mention = await db.GetTable<AntiMassMentionSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId).ConfigureAwait(false);

        if (raid != null) antiRaidGuilds[guildId] = new AntiRaidStats { AntiRaidSettings = raid };
        else antiRaidGuilds.TryRemove(guildId, out _);

        if (spam != null) antiSpamGuilds[guildId] = new AntiSpamStats { AntiSpamSettings = spam };
        else antiSpamGuilds.TryRemove(guildId, out _);

        if (alt != null) antiAltGuilds[guildId] = new AntiAltStats(alt);
        else antiAltGuilds.TryRemove(guildId, out _);

        if (mention != null) antiMassMentionGuilds[guildId] = new AntiMassMentionStats { AntiMassMentionSettings = mention };
        else antiMassMentionGuilds.TryRemove(guildId, out _);
    }

    /// <summary>
    ///     Handles the event when a user joins a guild.
    /// </summary>
    /// <param name="user">The user that has joined the guild.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task HandleUserJoined(IGuildUser user)
    {
        if (user.IsBot) return;

        antiRaidGuilds.TryGetValue(user.Guild.Id, out var raidStats);
        antiAltGuilds.TryGetValue(user.Guild.Id, out var altStats);

        if (raidStats is null && altStats is null) return;

        if (altStats is { } alts && altStats.Action != (int)PunishmentAction.Warn)
        {
            if (user.CreatedAt != default)
            {
                var diff = DateTime.UtcNow - user.CreatedAt.UtcDateTime;
                if (double.TryParse(alts.MinAge, out var minAgeMinutes))
                {
                     var minAgeSpan = TimeSpan.FromMinutes(minAgeMinutes);
                     if (diff < minAgeSpan)
                     {
                          await PunishUsers(alts.Action, ProtectionType.Alting, alts.ActionDurationMinutes, alts.RoleId, user).ConfigureAwait(false);
                          return;
                     }
                }
            }
        }

        if (raidStats is { } stats && stats.AntiRaidSettings.Action != (int)PunishmentAction.Warn)
        {
            try
            {
                stats.RaidUsers.Add(user); // Add user to the collection

                var statsUsersCount = stats.UsersCount;
                var currentCount = Interlocked.Increment(ref statsUsersCount);

                if (currentCount >= stats.AntiRaidSettings.UserThreshold)
                {
                    var usersToPunish = stats.RaidUsers.ToList();
                    stats.RaidUsers.Clear();
                    Interlocked.Add(ref statsUsersCount, -usersToPunish.Count);

                    if(usersToPunish.Any())
                    {
                        var settings = stats.AntiRaidSettings;
                        await PunishUsers(settings.Action, ProtectionType.Raiding, settings.PunishDuration, null, usersToPunish.Where(u => u!= null).ToArray()).ConfigureAwait(false);
                    }
                }
                else
                {
                    // Schedule count decrement after delay
                    _ = Task.Delay(TimeSpan.FromSeconds(stats.AntiRaidSettings.Seconds)).ContinueWith(t =>
                    {
                        // Just decrement the count after the delay
                        Interlocked.Decrement(ref statsUsersCount);
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error processing anti-raid for user {UserId}", user.Id);
            }
        }
    }

    /// <summary>
    ///     Handles the event when a message is received in a guild for anti-spam protection.
    /// </summary>
    /// <param name="arg">The message that was received.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task HandleAntiSpam(IMessage arg)
    {
        if (arg is not SocketUserMessage msg || msg.Author.IsBot || msg.Author is IGuildUser { GuildPermissions.Administrator: true })
            return Task.CompletedTask;

        if (msg.Channel is not ITextChannel channel)
            return Task.CompletedTask;

        if (!antiSpamGuilds.TryGetValue(channel.Guild.Id, out var spamStats))
            return Task.CompletedTask;

        if (spamStats.AntiSpamSettings.AntiSpamIgnores.Any(i => i.ChannelId == channel.Id))
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                var stats = spamStats.UserStats.AddOrUpdate(msg.Author.Id, _ => new UserSpamStats(msg), (_, old) =>
                {
                    old.ApplyNextMessage(msg);
                    return old;
                });

                if (stats.Count >= spamStats.AntiSpamSettings.MessageThreshold)
                {
                    if (spamStats.UserStats.TryRemove(msg.Author.Id, out var removedStats))
                    {
                        removedStats.Dispose();
                        var settings = spamStats.AntiSpamSettings;
                        await PunishUsers(settings.Action, ProtectionType.Spamming, settings.MuteTime, settings.RoleId, (IGuildUser)msg.Author).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                 Log.Warning(ex, "Error processing anti-spam for user {UserId}", msg.Author.Id);
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Punishes a set of users based on the provided punishment action and protection type.
    /// </summary>
    /// <param name="action">The punishment action to be applied.</param>
    /// <param name="pt">The type of protection triggering the punishment.</param>
    /// <param name="muteTime">The duration of the mute punishment, if applicable.</param>
    /// <param name="roleId">The ID of the role to be added, if applicable.</param>
    /// <param name="gus">The users to be punished.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task PunishUsers(int action, ProtectionType pt, int muteTime, ulong? roleId, params IGuildUser[] gus)
    {
        if (gus == null || gus.Length == 0 || gus[0] == null) return;

        Log.Information("[{PunishType}] - Punishing [{Count}] users with [{PunishAction}] in {GuildName} guild", pt, gus.Length, action, gus[0].Guild.Name);

        foreach (var gu in gus)
        {
            if (gu == null) continue;
            await punishUserQueue.Writer.WriteAsync(new PunishQueueItem
            {
                Action = action, Type = pt, User = gu, MuteTime = muteTime, RoleId = roleId
            }).ConfigureAwait(false);
        }
        _ = OnAntiProtectionTriggered((PunishmentAction)action, pt, gus);
    }

    /// <summary>
    ///     Starts the anti-raid protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="userThreshold">The number of users that triggers the anti-raid protection.</param>
    /// <param name="seconds">The time period in seconds in which the user threshold must be reached to trigger the protection.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="minutesDuration">The duration of the punishment, if applicable.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains the anti-raid stats if the protection was
    ///     successfully started.
    /// </returns>
    public async Task<AntiRaidStats?> StartAntiRaidAsync(ulong guildId, int userThreshold, int seconds, PunishmentAction action, int minutesDuration)
    {
        var g = client.GetGuild(guildId);
        if (g == null) return null;
        await mute.GetMuteRole(g).ConfigureAwait(false);

        if (action == PunishmentAction.AddRole) return null;

        if (!IsDurationAllowed(action)) minutesDuration = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiRaidSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId).ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiRaidSetting { GuildId = guildId };

        settings.Action = (int)action;
        settings.Seconds = seconds;
        settings.UserThreshold = userThreshold;
        settings.PunishDuration = minutesDuration;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        var stats = new AntiRaidStats { AntiRaidSettings = settings };
        antiRaidGuilds.AddOrUpdate(guildId, stats, (_, _) => stats);

        return stats;
    }

    /// <summary>
    ///     Handles the event when a message is received for anti-mass mention protection.
    /// </summary>
    /// <param name="arg">The message that was received.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task HandleAntiMassMention(IMessage arg)
    {
        if (arg is not SocketUserMessage msg || msg.Author is IGuildUser { GuildPermissions.Administrator: true })
            return Task.CompletedTask;

        if (msg.Channel is not ITextChannel channel)
            return Task.CompletedTask;

        if (!antiMassMentionGuilds.TryGetValue(channel.Guild.Id, out var massMentionStats))
            return Task.CompletedTask;

        var settings = massMentionStats.AntiMassMentionSettings;
        if (settings.IgnoreBots && msg.Author.IsBot)
            return Task.CompletedTask;

        var mentionCount = msg.MentionedUsers.Count + msg.MentionedRoles.Count;
        if (mentionCount == 0) return Task.CompletedTask;


        _ = Task.Run(async () =>
        {
             try
             {
                if (mentionCount >= settings.MentionThreshold)
                {
                    await PunishUsers(settings.Action, ProtectionType.MassMention, settings.MuteTime, settings.RoleId, (IGuildUser)msg.Author).ConfigureAwait(false);
                    if (massMentionStats.UserStats.TryRemove(msg.Author.Id, out var removedStats)) removedStats.Dispose();
                    return;
                }

                var userStats = massMentionStats.UserStats.AddOrUpdate(msg.Author.Id, _ => new UserMentionStats(settings.TimeWindowSeconds), (_, old) => old);

                if (userStats.AddMentions(mentionCount, settings.MaxMentionsInTimeWindow))
                {
                     await PunishUsers(settings.Action, ProtectionType.MassMention, settings.MuteTime, settings.RoleId, (IGuildUser)msg.Author).ConfigureAwait(false);
                     if (massMentionStats.UserStats.TryRemove(msg.Author.Id, out var removedStats)) removedStats.Dispose();
                }
             }
             catch(Exception ex)
             {
                 Log.Warning(ex, "Error processing anti-mass-mention for user {UserId}", msg.Author.Id);
             }
        });
        return Task.CompletedTask;
    }


    /// <summary>
    ///     Attempts to stop the anti-raid protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> TryStopAntiRaid(ulong guildId)
    {
        var removed = antiRaidGuilds.TryRemove(guildId, out _);
        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.GetTable<AntiRaidSetting>()
            .Where(x => x.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);
        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Attempts to stop the anti-spam protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> TryStopAntiSpam(ulong guildId)
    {
        var removed = antiSpamGuilds.TryRemove(guildId, out var removedStats);
        if(removed) removedStats.UserStats.ForEach(x => x.Value.Dispose());

        await using var db = await dbFactory.CreateConnectionAsync();
        var setting = await db.GetTable<AntiSpamSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId).ConfigureAwait(false);
        var deletedCount = 0;
        if (setting != null)
        {
             await db.GetTable<AntiSpamIgnore>().Where(i => i.AntiSpamSettingId == setting.Id).DeleteAsync().ConfigureAwait(false);
             // Use DeleteAsync with the fetched entity for single deletion
             deletedCount = await db.DeleteAsync(setting).ConfigureAwait(false);
        }
        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Starts the anti-spam protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="messageCount">The number of messages that triggers the anti-spam protection.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="punishDurationMinutes">The duration of the punishment, if applicable.</param>
    /// <param name="roleId">The ID of the role to be added, if applicable.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains the anti-spam stats if the protection was
    ///     successfully started.
    /// </returns>
    public async Task<AntiSpamStats?> StartAntiSpamAsync(ulong guildId, int messageCount, PunishmentAction action, int punishDurationMinutes, ulong? roleId)
    {
        var g = client.GetGuild(guildId);
        if (g == null) return null;
        await mute.GetMuteRole(g).ConfigureAwait(false);

        if (!IsDurationAllowed(action)) punishDurationMinutes = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiSpamSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId).ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiSpamSetting { GuildId = guildId };

        settings.Action = (int)action;
        settings.MessageThreshold = messageCount;
        settings.MuteTime = punishDurationMinutes;
        settings.RoleId = roleId;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        // Reload ignored channels after insert/update for consistency
        settings.AntiSpamIgnores = (await db.GetTable<AntiSpamIgnore>()
            .Where(i => i.AntiSpamSettingId == settings.Id)
            .ToListAsync().ConfigureAwait(false)).ToHashSet();

        var stats = new AntiSpamStats { AntiSpamSettings = settings };
        antiSpamGuilds.AddOrUpdate(guildId, stats, (_, _) => stats);

        return stats;
    }

    /// <summary>
    ///     Starts the anti-mass mention protection for a guild with the specified settings.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="mentionThreshold">The number of mentions allowed in a single message before triggering protection.</param>
    /// <param name="timeWindowSeconds">The time window in seconds during which mentions are tracked.</param>
    /// <param name="maxMentionsInTimeWindow">The maximum number of mentions allowed within the specified time window before triggering protection.</param>
    /// <param name="ignoreBots">Whether to ignore bots.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="muteTime">The duration of the mute punishment in minutes, if applicable.</param>
    /// <param name="roleId">The ID of the role to be assigned as punishment, if applicable.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAntiMassMentionAsync(ulong guildId, int mentionThreshold, int timeWindowSeconds, int maxMentionsInTimeWindow, bool ignoreBots, PunishmentAction action, int muteTime, ulong? roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiMassMentionSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId).ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiMassMentionSetting { GuildId = guildId };

        settings.MentionThreshold = mentionThreshold;
        settings.TimeWindowSeconds = timeWindowSeconds;
        settings.MaxMentionsInTimeWindow = maxMentionsInTimeWindow;
        settings.IgnoreBots = ignoreBots;
        settings.Action = (int)action;
        settings.MuteTime = muteTime;
        settings.RoleId = roleId;

        if (isNew)
             await db.InsertAsync(settings).ConfigureAwait(false);
        else
             await db.UpdateAsync(settings).ConfigureAwait(false);

        var stats = new AntiMassMentionStats { AntiMassMentionSettings = settings };
        antiMassMentionGuilds.AddOrUpdate(guildId, stats, (_, _) => stats);
    }

    /// <summary>
    ///     Attempts to stop the anti-mass mention protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. Returns true if the protection was successfully stopped;
    ///     otherwise, false.
    /// </returns>
    public async Task<bool> TryStopAntiMassMention(ulong guildId)
    {
        var removed = antiMassMentionGuilds.TryRemove(guildId, out var removedStats);
        if (removed) removedStats.UserStats.ForEach(x => x.Value.Dispose());

        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.GetTable<AntiMassMentionSetting>()
            .Where(x => x.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);

        return removed || deletedCount > 0;
    }

/// <summary>
    ///     Ignores a channel for the anti-spam protection in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to ignore the channel for.</param>
    /// <param name="channelId">The ID of the channel to ignore.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful (true if added, false if removed). Returns null if spam settings don't exist.
    /// </returns>
    public async Task<bool?> AntiSpamIgnoreAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var spamSettingId = await db.GetTable<AntiSpamSetting>()
            .Where(x => x.GuildId == guildId)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (spamSettingId is null)
        {
            Log.Warning("Attempted to modify AntiSpamIgnore for non-existent AntiSpamSetting GuildId: {GuildId}", guildId);
            return null;
        }

        var deletedCount = await db.GetTable<AntiSpamIgnore>()
           .Where(i => i.AntiSpamSettingId == spamSettingId.Value && i.ChannelId == channelId)
           .DeleteAsync().ConfigureAwait(false);

        bool added;
        if (deletedCount > 0)
        {
            added = false;
        }
        else
        {
            var newIgnore = new AntiSpamIgnore { AntiSpamSettingId = spamSettingId.Value, ChannelId = channelId };
            await db.InsertAsync(newIgnore).ConfigureAwait(false);
            added = true;
        }

        var updatedSpamSetting = await db.GetTable<AntiSpamSetting>()
                                     .FirstOrDefaultAsync(x => x.Id == spamSettingId.Value)
                                     .ConfigureAwait(false);

        if (updatedSpamSetting != null)
        {
            updatedSpamSetting.AntiSpamIgnores = (await db.GetTable<AntiSpamIgnore>()
                .Where(i => i.AntiSpamSettingId == updatedSpamSetting.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();

            var newStats = new AntiSpamStats { AntiSpamSettings = updatedSpamSetting };
            antiSpamGuilds.AddOrUpdate(guildId, newStats, (_, _) => newStats);
        }
        else
        {
            // Setting was somehow deleted between steps, remove from cache
            antiSpamGuilds.TryRemove(guildId, out _);
        }

        return added;
    }

    /// <summary>
    ///     Retrieves the anti-spam, anti-raid, and anti-alt statistics for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the statistics for.</param>
    /// <returns>A tuple containing the anti-spam, anti-raid, anti-alt, and anti-mass-mention statistics for the guild.</returns>
    public (AntiSpamStats?, AntiRaidStats?, AntiAltStats?, AntiMassMentionStats?) GetAntiStats(ulong guildId)
    {
        antiSpamGuilds.TryGetValue(guildId, out var antiSpamStats);
        antiRaidGuilds.TryGetValue(guildId, out var antiRaidStats);
        antiAltGuilds.TryGetValue(guildId, out var antiAltStats);
        antiMassMentionGuilds.TryGetValue(guildId, out var antiMassMentionStats);
        return (antiSpamStats, antiRaidStats, antiAltStats, antiMassMentionStats);
    }

    /// <summary>
    ///     Checks if a duration is allowed for a specific punishment action.
    /// </summary>
    /// <param name="action">The punishment action to check.</param>
    /// <returns>A boolean indicating whether a duration is allowed for the punishment action.</returns>
    public static bool IsDurationAllowed(PunishmentAction action)
    {
        return action switch
        {
            PunishmentAction.Ban => true, PunishmentAction.Mute => true, PunishmentAction.ChatMute => true,
            PunishmentAction.VoiceMute => true, PunishmentAction.AddRole => true, PunishmentAction.Timeout => true, _ => false
        };
    }

    /// <summary>
    ///     Starts the anti-alt protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="minAgeMinutes">The minimum age of an account to not be considered an alt.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="actionDurationMinutes">The duration of the punishment, if applicable.</param>
    /// <param name="roleId">The ID of the role to be added, if applicable.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task StartAntiAltAsync(ulong guildId, int minAgeMinutes, PunishmentAction action, int actionDurationMinutes = 0, ulong? roleId = null)
    {
        if (!IsDurationAllowed(action)) actionDurationMinutes = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiAltSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId).ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiAltSetting { GuildId = guildId };

        settings.Action = (int)action;
        settings.ActionDurationMinutes = actionDurationMinutes;
        settings.MinAge = minAgeMinutes.ToString();
        settings.RoleId = roleId;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        antiAltGuilds[guildId] = new AntiAltStats(settings);
    }

    /// <summary>
    ///     Attempts to stop the anti-alt protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> TryStopAntiAlt(ulong guildId)
    {
        var removed = antiAltGuilds.TryRemove(guildId, out _);
        await using var db = await dbFactory.CreateConnectionAsync();
        var deletedCount = await db.GetTable<AntiAltSetting>()
            .Where(x => x.GuildId == guildId)
            .DeleteAsync().ConfigureAwait(false);
        return removed || deletedCount > 0;
    }
}