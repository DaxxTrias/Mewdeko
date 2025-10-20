using System.Text.RegularExpressions;
using System.Text;
#nullable enable
using Mewdeko.Extensions;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Provides anti-alt, anti-raid, and antispam protection services.
/// </summary>
public class ProtectionService : INService, IReadyExecutor, IUnloadableService
{
    private readonly ConcurrentDictionary<ulong, AntiAltStats> antiAltGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiMassMentionStats> antiMassMentionGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiPatternStats> antiPatternGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiRaidStats> antiRaidGuilds = new();
    private readonly ConcurrentDictionary<ulong, AntiSpamStats> antiSpamGuilds = new();

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<ProtectionService> logger;
    private readonly MuteService mute;
    private readonly UserPunishService punishService;
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, int>> imageMentionSpamCounters = new();

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
    /// <param name="mute">The mute service.</param>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="punishService">The user punish service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public ProtectionService(DiscordShardedClient client,
        MuteService mute, IDataConnectionFactory dbFactory, UserPunishService punishService, EventHandler eventHandler,
        ILogger<ProtectionService> logger)
    {
        this.client = client;
        this.mute = mute;
        this.dbFactory = dbFactory;
        this.punishService = punishService;
        this.logger = logger;
        this.eventHandler = eventHandler;

        eventHandler.Subscribe("MessageReceived", "ProtectionService", HandleAntiSpam);
        eventHandler.Subscribe("UserJoined", "ProtectionService", HandleUserJoined);
        eventHandler.Subscribe("MessageReceived", "ProtectionService", HandleAntiMassMention);
        eventHandler.Subscribe("MessageDeleted", "ProtectionService", HandleSuspiciousDeletion);
        eventHandler.Subscribe("MessageReceived", "ProtectionService", HandleImageMentionSpam);

        eventHandler.Subscribe("JoinedGuild", "ProtectionService", _bot_JoinedGuild);
        eventHandler.Subscribe("LeftGuild", "ProtectionService", _client_LeftGuild);

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
                logger.LogWarning(ex, "Error initializing protections for Guild {GuildId}", guild.Id);
            }
        }
    }

    /// <summary>
    ///     Unloads the service and unsubscribes from events.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("MessageReceived", "ProtectionService", HandleAntiSpam);
        eventHandler.Unsubscribe("UserJoined", "ProtectionService", HandleUserJoined);
        eventHandler.Unsubscribe("MessageReceived", "ProtectionService", HandleAntiMassMention);
        eventHandler.Unsubscribe("MessageDeleted", "ProtectionService", HandleSuspiciousDeletion);
        eventHandler.Unsubscribe("MessageReceived", "ProtectionService", HandleImageMentionSpam);
        eventHandler.Unsubscribe("JoinedGuild", "ProtectionService", _bot_JoinedGuild);
        eventHandler.Unsubscribe("LeftGuild", "ProtectionService", _client_LeftGuild);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     An event that is triggered when the anti-protection is triggered.
    /// </summary>
    public event Func<PunishmentAction, ProtectionType, IGuildUser[], Task> OnAntiProtectionTriggered = delegate
    {
        return Task.CompletedTask;
    };

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

                var currentUser = client.CurrentUser;
                if (currentUser == null)
                {
                    logger.LogWarning("Cannot apply punishment; CurrentUser is null.");
                    continue;
                }

                await punishService.ApplyPunishment(gu.Guild, gu, currentUser, (PunishmentAction)item.Action, muteTime,
                    item.RoleId, $"{item.Type} Protection").ConfigureAwait(false);

                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error in punish queue: {Message}", ex.Message);
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
        antiPatternGuilds.TryRemove(guild.Id, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles the event when the bot joins a guild.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task _bot_JoinedGuild(SocketGuild guild)
    {
        await Initialize(guild.Id);
    }

    /// <summary>
    ///     Initializes the anti-raid, anti-spam, and anti-alt settings for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to initialize the settings for.</param>
    private async Task Initialize(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var raid = await db.GetTable<AntiRaidSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var spam = await db.GetTable<AntiSpamSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (spam != null)
        {
            spam.AntiSpamIgnores = (await db.GetTable<AntiSpamIgnore>()
                .Where(i => i.AntiSpamSettingId == spam.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
        }

        var alt = await db.GetTable<AntiAltSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var mention = await db.GetTable<AntiMassMentionSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var pattern = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (raid != null)
            antiRaidGuilds[guildId] = new AntiRaidStats
            {
                AntiRaidSettings = raid
            };
        else antiRaidGuilds.TryRemove(guildId, out _);

        if (spam != null)
            antiSpamGuilds[guildId] = new AntiSpamStats
            {
                AntiSpamSettings = spam
            };
        else antiSpamGuilds.TryRemove(guildId, out _);

        if (alt != null) antiAltGuilds[guildId] = new AntiAltStats(alt);
        else antiAltGuilds.TryRemove(guildId, out _);

        if (mention != null)
            antiMassMentionGuilds[guildId] = new AntiMassMentionStats
            {
                AntiMassMentionSettings = mention
            };
        else antiMassMentionGuilds.TryRemove(guildId, out _);

        if (pattern != null)
        {
            pattern.AntiPatternPatterns = (await db.GetTable<AntiPatternPattern>()
                .Where(p => p.AntiPatternSettingId == pattern.Id)
                .ToListAsync().ConfigureAwait(false)).ToHashSet();
            antiPatternGuilds[guildId] = new AntiPatternStats(pattern);
        }
        else antiPatternGuilds.TryRemove(guildId, out _);
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
        antiPatternGuilds.TryGetValue(user.Guild.Id, out var patternStats);

        if (raidStats is null && altStats is null && patternStats is null) return;

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
                        await PunishUsers(alts.Action, ProtectionType.Alting, alts.ActionDurationMinutes, alts.RoleId,
                            user).ConfigureAwait(false);
                        return;
                    }
                }
            }
        }

        if (patternStats is { } patterns && patterns.Action != (int)PunishmentAction.Warn)
        {
            try
            {
                var username = user.Username?.ToLower() ?? "";
                var displayName = user.DisplayName?.ToLower() ?? "";
                var settings = patterns.AntiPatternSettings;
                var score = 0;
                var reasons = new List<string>();
                var now = DateTimeOffset.UtcNow;

                // Account age check
                if (settings.CheckAccountAge)
                {
                    var accountAge = now - user.CreatedAt;
                    if (accountAge.TotalDays <= settings.MaxAccountAgeMonths * 30)
                    {
                        score += 5;
                        reasons.Add($"AccountAge({accountAge.TotalDays:F1}d)");
                    }
                }

                // Join timing check
                if (settings.CheckJoinTiming && user.JoinedAt.HasValue)
                {
                    var timeBetween = (user.JoinedAt.Value - user.CreatedAt).TotalHours;
                    if (timeBetween <= settings.MaxJoinHours)
                    {
                        score += timeBetween < 1 ? 10 : timeBetween < 6 ? 7 : 3;
                        reasons.Add($"QuickJoin({timeBetween:F1}h)");
                    }
                }

                // Batch creation check
                if (settings.CheckBatchCreation)
                {
                    var guild = user.Guild;
                    var creationHour = user.CreatedAt.ToString("yyyy-MM-dd HH");
                    var recentUsers = await guild.GetUsersAsync();
                    var batchCount = recentUsers.Count(u => !u.IsBot &&
                                                            u.CreatedAt.ToString("yyyy-MM-dd HH") == creationHour);
                    if (batchCount > 1)
                    {
                        score += Math.Min(batchCount, 10);
                        reasons.Add($"Batch({batchCount})");
                    }
                }

                // Offline status check
                if (settings.CheckOfflineStatus && user.Status == UserStatus.Offline)
                {
                    score += 2;
                    reasons.Add("Offline");
                }

                // New account check
                if (settings.CheckNewAccounts)
                {
                    var accountAge = (now - user.CreatedAt).TotalDays;
                    if (accountAge < settings.NewAccountDays)
                    {
                        score += 3;
                        reasons.Add($"NewAccount({accountAge:F1}d)");
                    }
                }

                // Pattern matching
                foreach (var pattern in patterns.AntiPatternSettings.AntiPatternPatterns)
                {
                    var regex = new Regex(pattern.Pattern, RegexOptions.IgnoreCase);

                    var isMatch = false;
                    if (pattern.CheckUsername && regex.IsMatch(username))
                    {
                        isMatch = true;
                        score += 15;
                        reasons.Add($"UsernamePattern({pattern.Name ?? "Unnamed"})");
                    }

                    if (pattern.CheckDisplayName && regex.IsMatch(displayName))
                    {
                        isMatch = true;
                        score += 12;
                        reasons.Add($"DisplayNamePattern({pattern.Name ?? "Unnamed"})");
                    }

                    if (isMatch && score >= settings.MinimumScore)
                    {
                        patterns.Increment();
                        await PunishUsers(patterns.Action, ProtectionType.PatternMatching, patterns.PunishDuration,
                            patterns.RoleId, user).ConfigureAwait(false);
                        logger.LogInformation(
                            "Anti-pattern triggered for user {UserId} ({Username}) - Score: {Score}, Reasons: {Reasons}",
                            user.Id, user.Username, score, string.Join("|", reasons));
                        return;
                    }
                }

                // Check if overall score meets threshold without pattern match
                if (score >= settings.MinimumScore && reasons.Any())
                {
                    patterns.Increment();
                    await PunishUsers(patterns.Action, ProtectionType.PatternMatching, patterns.PunishDuration,
                        patterns.RoleId, user).ConfigureAwait(false);
                    logger.LogInformation(
                        "Anti-pattern triggered for user {UserId} ({Username}) - Score: {Score}, Reasons: {Reasons}",
                        user.Id, user.Username, score, string.Join("|", reasons));
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-pattern for user {UserId}", user.Id);
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

                    if (usersToPunish.Any())
                    {
                        var settings = stats.AntiRaidSettings;
                        await PunishUsers(settings.Action, ProtectionType.Raiding, settings.PunishDuration, null,
                            usersToPunish.Where(u => u != null).ToArray()).ConfigureAwait(false);
                    }
                }
                else
                {
                    // Schedule count decrement after delay
                    _ = Task.Delay(TimeSpan.FromSeconds(stats.AntiRaidSettings.Seconds)).ContinueWith(_ =>
                    {
                        // Just decrement the count after the delay
                        Interlocked.Decrement(ref statsUsersCount);
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-raid for user {UserId}", user.Id);
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
        if (arg is not SocketUserMessage msg || msg.Author.IsBot || msg.Author is IGuildUser
            {
                GuildPermissions.Administrator: true
            })
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
                        await PunishUsers(settings.Action, ProtectionType.Spamming, settings.MuteTime, settings.RoleId,
                            (IGuildUser)msg.Author).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-spam for user {UserId}", msg.Author.Id);
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
    private async Task PunishUsers(int action, ProtectionType pt, int muteTime, ulong? roleId, params IGuildUser[]? gus)
    {
        if (gus == null || gus.Length == 0) return;

        logger.LogInformation("[{PunishType}] - Punishing [{Count}] users with [{PunishAction}] in {GuildName} guild",
            pt,
            gus.Length, action, gus[0].Guild.Name);

        foreach (var gu in gus)
        {
            await punishUserQueue.Writer.WriteAsync(new PunishQueueItem
            {
                Action = action,
                Type = pt,
                User = gu,
                MuteTime = muteTime,
                RoleId = roleId
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
    public async Task<AntiRaidStats?> StartAntiRaidAsync(ulong guildId, int userThreshold, int seconds,
        PunishmentAction action, int minutesDuration)
    {
        var g = client.GetGuild(guildId);
        if (g == null) return null;
        await mute.GetMuteRole(g).ConfigureAwait(false);

        if (action == PunishmentAction.AddRole) return null;

        if (!IsDurationAllowed(action)) minutesDuration = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiRaidSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiRaidSetting
        {
            GuildId = guildId
        };

        settings.Action = (int)action;
        settings.Seconds = seconds;
        settings.UserThreshold = userThreshold;
        settings.PunishDuration = minutesDuration;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        var stats = new AntiRaidStats
        {
            AntiRaidSettings = settings
        };
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
        if (arg is not SocketUserMessage msg || msg.Author.IsBot || msg.Author is IGuildUser { GuildPermissions.Administrator: true })
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
                    await PunishUsers(settings.Action, ProtectionType.MassMention, settings.MuteTime, settings.RoleId,
                        (IGuildUser)msg.Author).ConfigureAwait(false);
                    if (massMentionStats.UserStats.TryRemove(msg.Author.Id, out var removedStats))
                        removedStats.Dispose();
                    return;
                }

                var userStats = massMentionStats.UserStats.AddOrUpdate(msg.Author.Id,
                    _ => new UserMentionStats(settings.TimeWindowSeconds), (_, old) => old);

                if (userStats.AddMentions(mentionCount, settings.MaxMentionsInTimeWindow))
                {
                    await PunishUsers(settings.Action, ProtectionType.MassMention, settings.MuteTime, settings.RoleId,
                        (IGuildUser)msg.Author).ConfigureAwait(false);
                    if (massMentionStats.UserStats.TryRemove(msg.Author.Id, out var removedStats))
                        removedStats.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error processing anti-mass-mention for user {UserId}", msg.Author.Id);
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Detects a user posting 4 or more image messages in a row that also include a mention (user/role or reply),
    ///     and applies AntiSpam punishment, notifying the warnlog channel.
    /// </summary>
    private Task HandleImageMentionSpam(IMessage arg)
    {
        if (arg is not SocketUserMessage msg || msg.Author is IGuildUser { GuildPermissions.Administrator: true })
            return Task.CompletedTask;

        if (msg.Channel is not ITextChannel channel)
            return Task.CompletedTask;

        if (!antiSpamGuilds.TryGetValue(channel.Guild.Id, out var spamStats))
            return Task.CompletedTask;

        // Immediate rule: single message with >= 4 attachments and @everyone
        var mentionsEveryone = (msg.Content ?? string.Empty).IndexOf("@everyone", StringComparison.OrdinalIgnoreCase) >= 0;
        if (msg.Attachments.Count >= 4 && mentionsEveryone)
        {
            if (msg.Author is IGuildUser guNow)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var settings = spamStats.AntiSpamSettings;
                        await PunishUsers(settings.Action, ProtectionType.Spamming, settings.MuteTime, settings.RoleId, guNow)
                            .ConfigureAwait(false);

                        var warnlogChannelId = await punishService.GetWarnlogChannel(channel.Guild.Id).ConfigureAwait(false);
                        if (warnlogChannelId != 0)
                        {
                            var warnlog = await channel.Guild.GetTextChannelAsync(warnlogChannelId).ConfigureAwait(false);
                            if (warnlog != null)
                            {
                                var desc = new StringBuilder()
                                    .AppendLine($"User: {guNow.Mention} ({guNow.Id})")
                                    .AppendLine($"Channel: <#{channel.Id}>")
                                    .AppendLine($"Triggered: >=4 attachments in a single message with @everyone")
                                    .AppendLine($"Attachments: {msg.Attachments.Count}")
                                    .AppendLine("Preview:")
                                    .AppendLine(Format.Sanitize((msg.Content ?? string.Empty).TrimTo(300)));

                                var eb = new EmbedBuilder()
                                    .WithTitle("[Anti-Spam] Attachment Burst with @everyone Detected")
                                    .WithDescription(desc.ToString())
                                    .WithOkColor()
                                    .WithCurrentTimestamp();

                                await warnlog.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error handling attachment burst for user {UserId}", guNow.Id);
                    }
                });
            }
            return Task.CompletedTask;
        }

        // Qualifying message: has at least one image attachment and includes a mention (user/role) or is a reply
        var hasImageAttachment = msg.Attachments.Any(a =>
            (a.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false)
            || a.Url.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || a.Url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || a.Url.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || a.Url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            || a.ProxyUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || a.ProxyUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || a.ProxyUrl.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || a.ProxyUrl.EndsWith(".gif", StringComparison.OrdinalIgnoreCase));

        var includesMention = msg.MentionedUsers.Count > 0 || msg.MentionedRoles.Count > 0 || msg.ReferencedMessage != null;
        var qualifies = hasImageAttachment && includesMention;

        var guildCounters = imageMentionSpamCounters.GetOrAdd(channel.Guild.Id, _ => new ConcurrentDictionary<ulong, int>());
        if (!qualifies)
        {
            // reset streak on any non-qualifying message from the user
            guildCounters.AddOrUpdate(msg.Author.Id, _ => 0, (_, __) => 0);
            return Task.CompletedTask;
        }

        var newCount = guildCounters.AddOrUpdate(msg.Author.Id, _ => 1, (_, old) => old + 1);

        if (newCount >= 4)
        {
            // Reset counter after triggering
            guildCounters[msg.Author.Id] = 0;

            if (msg.Author is IGuildUser gu)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var settings = spamStats.AntiSpamSettings;
                        await PunishUsers(settings.Action, ProtectionType.Spamming, settings.MuteTime, settings.RoleId, gu)
                            .ConfigureAwait(false);

                        var warnlogChannelId = await punishService.GetWarnlogChannel(channel.Guild.Id).ConfigureAwait(false);
                        if (warnlogChannelId != 0)
                        {
                            var warnlog = await channel.Guild.GetTextChannelAsync(warnlogChannelId).ConfigureAwait(false);
                            if (warnlog != null)
                            {
                                var desc = new StringBuilder()
                                    .AppendLine($"User: {gu.Mention} ({gu.Id})")
                                    .AppendLine($"Channel: <#{channel.Id}>")
                                    .AppendLine("Triggered: 4+ image messages with mention (consecutive)")
                                    .AppendLine($"This message attachments: {msg.Attachments.Count}")
                                    .AppendLine("Preview:")
                                    .AppendLine(Format.Sanitize((msg.Content ?? string.Empty).TrimTo(300)));

                                var eb = new EmbedBuilder()
                                    .WithTitle("[Anti-Spam] Image Mention Spam Detected")
                                    .WithDescription(desc.ToString())
                                    .WithOkColor()
                                    .WithCurrentTimestamp();

                                await warnlog.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error handling image-mention spam for user {UserId}", gu.Id);
                    }
                });
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Detects suspicious behavior where a user posts a message mentioning someone and/or containing an invite/support
    ///     link and deletes it shortly after (within 2 minutes). Treat as spam using AntiSpam settings and notify warnlog.
    /// </summary>
    private Task HandleSuspiciousDeletion((Cacheable<IMessage, ulong> optMsg, Cacheable<IMessageChannel, ulong> ch) arg)
    {
        var (optMsg, ch) = arg;
        if (!ch.HasValue || ch.Value is not ITextChannel channel)
            return Task.CompletedTask;

        if (!antiSpamGuilds.TryGetValue(channel.Guild.Id, out var spamStats))
            return Task.CompletedTask;

        if (!optMsg.HasValue)
            return Task.CompletedTask;

        if (optMsg.Value is not SocketUserMessage msg)
            return Task.CompletedTask;

        if (msg.Author is not IGuildUser gu || gu.GuildPermissions.Administrator)
            return Task.CompletedTask;

        if (spamStats.AntiSpamSettings.AntiSpamIgnores.Any(i => i.ChannelId == channel.Id))
            return Task.CompletedTask;

        var age = DateTimeOffset.UtcNow - msg.Timestamp;
        if (age > TimeSpan.FromMinutes(2))
            return Task.CompletedTask;

        var content = msg.Content ?? string.Empty;
        var mentioned = msg.MentionedUsers.Count > 0 || msg.MentionedRoles.Count > 0 || msg.ReferencedMessage != null;
        var hasScamLikeLink = content.IsDiscordInvite() || content.Contains("discord.com/oauth2/authorize", StringComparison.OrdinalIgnoreCase) || content.Contains("discordapp.com/oauth2/authorize", StringComparison.OrdinalIgnoreCase);

        if (!mentioned && !hasScamLikeLink)
            return Task.CompletedTask;

        _ = Task.Run(async () =>
        {
            try
            {
                var settings = spamStats.AntiSpamSettings;
                await PunishUsers(settings.Action, ProtectionType.Spamming, settings.MuteTime, settings.RoleId, gu)
                    .ConfigureAwait(false);

                var warnlogChannelId = await punishService.GetWarnlogChannel(channel.Guild.Id).ConfigureAwait(false);
                if (warnlogChannelId != 0)
                {
                    var warnlog = await channel.Guild.GetTextChannelAsync(warnlogChannelId).ConfigureAwait(false);
                    if (warnlog != null)
                    {
                        var desc = new StringBuilder()
                            .AppendLine($"User: {gu.Mention} ({gu.Id})")
                            .AppendLine($"Channel: <#{channel.Id}>")
                            .AppendLine($"Deleted after: {age.TotalSeconds:F0}s")
                            .AppendLine($"Contained invite/oauth link: {(hasScamLikeLink ? "Yes" : "No")}")
                            .AppendLine($"Referenced/mentioned: {(mentioned ? "Yes" : "No")}")
                            .AppendLine("Preview:")
                            .AppendLine(Format.Sanitize(content.TrimTo(400)));

                        var eb = new EmbedBuilder()
                            .WithTitle("[Anti-Spam] Suspicious Deletion Detected")
                            .WithDescription(desc.ToString())
                            .WithOkColor()
                            .WithCurrentTimestamp();

                        await warnlog.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error handling suspicious deletion for user {UserId}", gu.Id);
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
        if (removed && removedStats != null)
            removedStats.UserStats.ForEach(x => x.Value.Dispose());

        await using var db = await dbFactory.CreateConnectionAsync();
        var setting = await db.GetTable<AntiSpamSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var deletedCount = 0;
        if (setting != null)
        {
            await db.GetTable<AntiSpamIgnore>().Where(i => i.AntiSpamSettingId == setting.Id).DeleteAsync()
                .ConfigureAwait(false);
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
    public async Task<AntiSpamStats?> StartAntiSpamAsync(ulong guildId, int messageCount, PunishmentAction action,
        int punishDurationMinutes, ulong? roleId)
    {
        var g = client.GetGuild(guildId);
        if (g == null) return null;
        await mute.GetMuteRole(g).ConfigureAwait(false);

        if (!IsDurationAllowed(action)) punishDurationMinutes = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiSpamSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiSpamSetting
        {
            GuildId = guildId
        };

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

        var stats = new AntiSpamStats
        {
            AntiSpamSettings = settings
        };
        antiSpamGuilds.AddOrUpdate(guildId, stats, (_, _) => stats);

        return stats;
    }

    /// <summary>
    ///     Starts the anti-mass mention protection for a guild with the specified settings.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="mentionThreshold">The number of mentions allowed in a single message before triggering protection.</param>
    /// <param name="timeWindowSeconds">The time window in seconds during which mentions are tracked.</param>
    /// <param name="maxMentionsInTimeWindow">
    ///     The maximum number of mentions allowed within the specified time window before
    ///     triggering protection.
    /// </param>
    /// <param name="ignoreBots">Whether to ignore bots.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="muteTime">The duration of the mute punishment in minutes, if applicable.</param>
    /// <param name="roleId">The ID of the role to be assigned as punishment, if applicable.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAntiMassMentionAsync(ulong guildId, int mentionThreshold, int timeWindowSeconds,
        int maxMentionsInTimeWindow, bool ignoreBots, PunishmentAction action, int muteTime, ulong? roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiMassMentionSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiMassMentionSetting
        {
            GuildId = guildId
        };

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

        var stats = new AntiMassMentionStats
        {
            AntiMassMentionSettings = settings
        };
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
        if (removed && removedStats != null)
            removedStats.UserStats.ForEach(x => x.Value.Dispose());

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
            logger.LogWarning("Attempted to modify AntiSpamIgnore for non-existent AntiSpamSetting GuildId: {GuildId}",
                guildId);
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
            var newIgnore = new AntiSpamIgnore
            {
                AntiSpamSettingId = spamSettingId.Value, ChannelId = channelId
            };
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

            var newStats = new AntiSpamStats
            {
                AntiSpamSettings = updatedSpamSetting
            };
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
    ///     Retrieves the anti-spam, anti-raid, anti-alt, anti-mass-mention, and anti-pattern statistics for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the statistics for.</param>
    /// <returns>A tuple containing the anti-spam, anti-raid, anti-alt, anti-mass-mention, and anti-pattern statistics for the guild.</returns>
    public (AntiSpamStats?, AntiRaidStats?, AntiAltStats?, AntiMassMentionStats?, AntiPatternStats?)
        GetAntiStats(ulong guildId)
    {
        antiSpamGuilds.TryGetValue(guildId, out var antiSpamStats);
        antiRaidGuilds.TryGetValue(guildId, out var antiRaidStats);
        antiAltGuilds.TryGetValue(guildId, out var antiAltStats);
        antiMassMentionGuilds.TryGetValue(guildId, out var antiMassMentionStats);
        antiPatternGuilds.TryGetValue(guildId, out var antiPatternStats);
        return (antiSpamStats, antiRaidStats, antiAltStats, antiMassMentionStats, antiPatternStats);
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
            PunishmentAction.VoiceMute => true, PunishmentAction.AddRole => true, PunishmentAction.Timeout => true,
            _ => false
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
    public async Task StartAntiAltAsync(ulong guildId, int minAgeMinutes, PunishmentAction action,
        int actionDurationMinutes = 0, ulong? roleId = null)
    {
        if (!IsDurationAllowed(action)) actionDurationMinutes = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiAltSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiAltSetting
        {
            GuildId = guildId
        };

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

    /// <summary>
    ///     Starts the anti-pattern protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to start the protection for.</param>
    /// <param name="action">The punishment action to be applied when the protection is triggered.</param>
    /// <param name="actionDurationMinutes">The duration of the punishment, if applicable.</param>
    /// <param name="roleId">The ID of the role to be added, if applicable.</param>
    /// <param name="checkAccountAge">Whether to check account age.</param>
    /// <param name="maxAccountAgeMonths">Maximum account age in months to flag.</param>
    /// <param name="checkJoinTiming">Whether to check join timing.</param>
    /// <param name="maxJoinHours">Maximum hours between account creation and join.</param>
    /// <param name="checkBatchCreation">Whether to check for batch account creation.</param>
    /// <param name="checkOfflineStatus">Whether to check if user is offline.</param>
    /// <param name="checkNewAccounts">Whether to flag very new accounts.</param>
    /// <param name="newAccountDays">Days to consider an account as new.</param>
    /// <param name="minimumScore">Minimum score to trigger punishment.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<AntiPatternStats?> StartAntiPatternAsync(ulong guildId, PunishmentAction action,
        int actionDurationMinutes = 0, ulong? roleId = null, bool checkAccountAge = false, int maxAccountAgeMonths = 6,
        bool checkJoinTiming = false, double maxJoinHours = 48.0, bool checkBatchCreation = false,
        bool checkOfflineStatus = false, bool checkNewAccounts = false, int newAccountDays = 7, int minimumScore = 15)
    {
        if (!IsDurationAllowed(action)) actionDurationMinutes = 0;

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var isNew = settings == null;
        settings ??= new AntiPatternSetting
        {
            GuildId = guildId,
            CheckAccountAge = checkAccountAge,
            MaxAccountAgeMonths = maxAccountAgeMonths,
            CheckJoinTiming = checkJoinTiming,
            MaxJoinHours = maxJoinHours,
            CheckBatchCreation = checkBatchCreation,
            CheckOfflineStatus = checkOfflineStatus,
            CheckNewAccounts = checkNewAccounts,
            NewAccountDays = newAccountDays,
            MinimumScore = minimumScore,
            DateAdded = DateTime.UtcNow
        };

        settings.Action = (int)action;
        settings.PunishDuration = actionDurationMinutes;
        settings.RoleId = roleId;

        if (isNew)
            await db.InsertAsync(settings).ConfigureAwait(false);
        else
            await db.UpdateAsync(settings).ConfigureAwait(false);

        // Load existing patterns
        settings.AntiPatternPatterns = (await db.GetTable<AntiPatternPattern>()
            .Where(p => p.AntiPatternSettingId == settings.Id)
            .ToListAsync().ConfigureAwait(false)).ToHashSet();

        var stats = new AntiPatternStats(settings);
        antiPatternGuilds[guildId] = stats;

        return stats;
    }

    /// <summary>
    ///     Updates the anti-pattern configuration settings for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to update settings for.</param>
    /// <param name="checkAccountAge">Whether to check account age.</param>
    /// <param name="maxAccountAgeMonths">Maximum account age in months to flag.</param>
    /// <param name="checkJoinTiming">Whether to check join timing.</param>
    /// <param name="maxJoinHours">Maximum hours between account creation and join.</param>
    /// <param name="checkBatchCreation">Whether to check for batch account creation.</param>
    /// <param name="checkOfflineStatus">Whether to check if user is offline.</param>
    /// <param name="checkNewAccounts">Whether to flag very new accounts.</param>
    /// <param name="newAccountDays">Days to consider an account as new.</param>
    /// <param name="minimumScore">Minimum score to trigger punishment.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating success.</returns>
    public async Task<bool> UpdateAntiPatternConfigAsync(ulong guildId, bool? checkAccountAge = null,
        int? maxAccountAgeMonths = null, bool? checkJoinTiming = null, double? maxJoinHours = null,
        bool? checkBatchCreation = null, bool? checkOfflineStatus = null, bool? checkNewAccounts = null,
        int? newAccountDays = null, int? minimumScore = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (settings == null) return false;

        if (checkAccountAge.HasValue) settings.CheckAccountAge = checkAccountAge.Value;
        if (maxAccountAgeMonths.HasValue) settings.MaxAccountAgeMonths = maxAccountAgeMonths.Value;
        if (checkJoinTiming.HasValue) settings.CheckJoinTiming = checkJoinTiming.Value;
        if (maxJoinHours.HasValue) settings.MaxJoinHours = maxJoinHours.Value;
        if (checkBatchCreation.HasValue) settings.CheckBatchCreation = checkBatchCreation.Value;
        if (checkOfflineStatus.HasValue) settings.CheckOfflineStatus = checkOfflineStatus.Value;
        if (checkNewAccounts.HasValue) settings.CheckNewAccounts = checkNewAccounts.Value;
        if (newAccountDays.HasValue) settings.NewAccountDays = newAccountDays.Value;
        if (minimumScore.HasValue) settings.MinimumScore = minimumScore.Value;

        await db.UpdateAsync(settings).ConfigureAwait(false);

        // Refresh the cache
        await Initialize(guildId);

        return true;
    }

    /// <summary>
    ///     Attempts to stop the anti-pattern protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to stop the protection for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> TryStopAntiPattern(ulong guildId)
    {
        var removed = antiPatternGuilds.TryRemove(guildId, out _);
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);
        var deletedCount = 0;
        if (setting != null)
        {
            // Delete all patterns first
            await db.GetTable<AntiPatternPattern>().Where(p => p.AntiPatternSettingId == setting.Id).DeleteAsync()
                .ConfigureAwait(false);
            // Then delete the setting
            deletedCount = await db.DeleteAsync(setting).ConfigureAwait(false);
        }

        return removed || deletedCount > 0;
    }

    /// <summary>
    ///     Adds a regex pattern to the anti-pattern protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the pattern for.</param>
    /// <param name="pattern">The regex pattern to match against usernames/display names.</param>
    /// <param name="name">Optional name for the pattern.</param>
    /// <param name="checkUsername">Whether to check usernames against this pattern.</param>
    /// <param name="checkDisplayName">Whether to check display names against this pattern.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating success.</returns>
    public async Task<bool> AddPatternAsync(ulong guildId, string pattern, string? name = null,
        bool checkUsername = true, bool checkDisplayName = true)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null)
        {
            logger.LogWarning("Attempted to add pattern to non-existent AntiPatternSetting for GuildId: {GuildId}",
                guildId);
            return false;
        }

        try
        {
            // Test if the pattern is valid regex
            _ = new Regex(pattern);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Invalid regex pattern attempted: {Pattern}", pattern);
            return false;
        }

        var newPattern = new AntiPatternPattern
        {
            AntiPatternSettingId = setting.Id,
            Pattern = pattern,
            Name = name,
            CheckUsername = checkUsername,
            CheckDisplayName = checkDisplayName,
            DateAdded = DateTime.UtcNow
        };

        await db.InsertAsync(newPattern).ConfigureAwait(false);

        // Refresh the cache
        await Initialize(guildId);

        return true;
    }

    /// <summary>
    ///     Removes a regex pattern from the anti-pattern protection for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to remove the pattern from.</param>
    /// <param name="patternId">The ID of the pattern to remove.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating success.</returns>
    public async Task<bool> RemovePatternAsync(ulong guildId, int patternId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return false;

        var deletedCount = await db.GetTable<AntiPatternPattern>()
            .Where(p => p.Id == patternId && p.AntiPatternSettingId == setting.Id)
            .DeleteAsync().ConfigureAwait(false);

        if (deletedCount > 0)
        {
            // Refresh the cache
            await Initialize(guildId);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets all anti-pattern patterns for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get patterns for.</param>
    /// <returns>A list of anti-pattern patterns.</returns>
    public async Task<List<AntiPatternPattern>> GetAntiPatternPatternsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var setting = await db.GetTable<AntiPatternSetting>().FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (setting == null) return new List<AntiPatternPattern>();

        return await db.GetTable<AntiPatternPattern>()
            .Where(p => p.AntiPatternSettingId == setting.Id)
            .ToListAsync().ConfigureAwait(false);
    }
}