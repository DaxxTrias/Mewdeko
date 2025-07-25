using System.Net.Http;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     The service responsible for handling guild-specific XP functionality.
/// </summary>
public partial class XpService : INService, IUnloadableService
{
    #region Constants

    /// <summary>
    ///     Base XP required for level 1.
    /// </summary>
    public const int BaseXpLvl1 = 36;

    /// <summary>
    ///     Default XP per message.
    /// </summary>
    public const int DefaultXpPerMessage = 3;

    /// <summary>
    ///     Default message XP cooldown in seconds.
    /// </summary>
    public const int DefaultMessageXpCooldown = 60;

    /// <summary>
    ///     Default XP gained per minute in voice channels.
    /// </summary>
    public const int DefaultVoiceXpPerMinute = 2;

    /// <summary>
    ///     Default voice XP session timeout in minutes.
    /// </summary>
    public const int DefaultVoiceXpTimeout = 60;

    /// <summary>
    ///     Maximum XP per message to prevent abuse.
    /// </summary>
    public const int MaxXpPerMessage = 50;

    /// <summary>
    ///     Maximum voice XP per minute to prevent abuse.
    /// </summary>
    public const int MaxVoiceXpPerMinute = 10;

    #endregion

    #region Fields

    internal readonly DiscordShardedClient Client;
    internal readonly IDataConnectionFactory DbFactory;
    internal readonly EventHandler EventHandler;
    internal readonly IHttpClientFactory HttpClientFactory;
    private readonly ILogger<XpService> logger;
    private readonly IServiceProvider serviceProvider;


    // Core components
    private readonly XpBackgroundProcessor backgroundProcessor;
    private readonly XpVoiceTracker voiceTracker;
    private readonly XpCompetitionManager competitionManager;
    private readonly XpCacheManager cacheManager;
    private readonly XpRewardManager rewardManager;

    #endregion

    #region Constructor and Initialization

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="dataCache">The data cache service.</param>
    /// <param name="eventHandler">The Discord event handler.</param>
    /// <param name="currencyService">The currency service.</param>
    /// <param name="httpClientFactory">The http client factory</param>
    /// <param name="cacheManager">The XP cache manager.</param>
    /// <param name="rewardManager">The XP reward manager.</param>
    /// <param name="competitionManager">The XP competition manager.</param>
    /// <param name="voiceTracker">The XP voice tracker.</param>
    /// <param name="backgroundProcessor">The XP background processor.</param>
    public XpService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        IDataCache dataCache,
        EventHandler eventHandler,
        ICurrencyService currencyService,
        IHttpClientFactory httpClientFactory,
        GeneratedBotStrings strings,
        XpCacheManager cacheManager,
        XpRewardManager rewardManager,
        XpCompetitionManager competitionManager,
        XpVoiceTracker voiceTracker,
        XpBackgroundProcessor backgroundProcessor, ILogger<XpService> logger, IServiceProvider serviceProvider)
    {
        Client = client;
        DbFactory = dbFactory;
        EventHandler = eventHandler;
        HttpClientFactory = httpClientFactory;

        // Assign injected sub-components
        this.cacheManager = cacheManager;
        this.rewardManager = rewardManager;
        this.competitionManager = competitionManager;
        this.voiceTracker = voiceTracker;
        this.backgroundProcessor = backgroundProcessor;
        this.logger = logger;
        this.serviceProvider = serviceProvider;

        // Register event handlers
        EventHandler.Subscribe("MessageReceived", "XpService", HandleMessageXp);
        EventHandler.Subscribe("UserVoiceStateUpdated", "XpService", voiceTracker.HandleVoiceStateUpdate);
        EventHandler.Subscribe("GuildAvailable", "XpService", OnGuildAvailable);
        EventHandler.Subscribe("MessageReceived", "XpService", OnMessageReceived);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                await cacheManager.PreloadAllRewardCachesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during reward cache preload");
            }
        });

        logger.LogInformation("XP Service initialized");
    }

    /// <summary>
    ///     Unloads the service and cleans up resources.
    /// </summary>
    public Task Unload()
    {
        // Unregister event handlers
        EventHandler.Unsubscribe("MessageReceived", "XpService", HandleMessageXp);
        EventHandler.Unsubscribe("UserVoiceStateUpdated", "XpService", voiceTracker.HandleVoiceStateUpdate);
        EventHandler.Unsubscribe("GuildAvailable", "XpService", OnGuildAvailable);
        EventHandler.Unsubscribe("MessageReceived", "XpService", OnMessageReceived);

        // Clean up resources
        backgroundProcessor.Dispose();
        voiceTracker.Dispose();
        competitionManager.Dispose();

        logger.LogInformation("XP Service unloaded");
        return Task.CompletedTask;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Handles XP for regular messages that don't trigger commands.
    /// </summary>
    private async Task HandleMessageXp(SocketMessage message)
    {
        if (message.Author is not IGuildUser user || user.IsBot)
            return;

        try
        {
            // Quick check for server exclusion (cached)
            if (await cacheManager.IsServerExcludedAsync(user.GuildId))
                return;

            // Quick check if message is too short
            if (message.Content.Length < 5 && !message.Content.Contains(' '))
                return;

            // Check if user is on cooldown or excluded
            if (!await CanGainXp(user, message.Channel.Id))
                return;

            // Calculate XP to award
            var xpAmount = await CalculateMessageXpAmount(user, message.Channel.Id);
            if (xpAmount <= 0)
                return;

            // Add to processing queue
            backgroundProcessor.QueueXpGain(user.GuildId, user.Id, xpAmount, message.Channel.Id, XpSource.Message);
        }
        catch (Exception ex)
        {
            logger.LogError($"Error handling message XP for {user.Id} in {user.Guild.Id}\n{ex}");
        }
    }

    /// <summary>
    ///     Handles guild becoming available.
    /// </summary>
    private async Task OnGuildAvailable(SocketGuild guild)
    {
        try
        {
            // Load guild settings into cache
            await cacheManager.GetGuildXpSettingsAsync(guild.Id);

            // Load active competitions
            await competitionManager.LoadActiveCompetitionsAsync(guild.Id);

            // Scan voice channels to restart voice sessions
            await voiceTracker.ScanGuildVoiceChannels(guild);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling guild available for {GuildId}", guild.Id);
        }
    }

    /// <summary>
    ///     Handles message received for tracking first message of day.
    /// </summary>
    private async Task OnMessageReceived(SocketMessage msg)
    {
        if (msg.Author.IsBot || msg is not SocketUserMessage { Author: SocketGuildUser user })
            return;

        try
        {
            // Check for first message of the day
            await backgroundProcessor.ProcessFirstMessageOfDay(user, msg.Channel.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling first message of day for {UserId} in {GuildId}", user.Id,
                user.Guild.Id);
        }
    }

    /// <summary>
    ///     Checks if a user can gain XP in a channel.
    /// </summary>
    private async Task<bool> CanGainXp(IGuildUser user, ulong channelId)
    {
        // Check server exclusion
        if (await cacheManager.IsServerExcludedAsync(user.GuildId))
            return false;

        // Check user, role, and channel exclusions
        if (!await cacheManager.CanUserGainXpAsync(user, channelId))
            return false;

        // Check cooldown
        var settings = await cacheManager.GetGuildXpSettingsAsync(user.GuildId);
        var cooldownSeconds = settings.MessageXpCooldown <= 0
            ? DefaultMessageXpCooldown
            : settings.MessageXpCooldown;

        return await cacheManager.CheckAndSetCooldownAsync(user.GuildId, user.Id, cooldownSeconds);
    }

    /// <summary>
    ///     Calculates the message XP amount based on user and channel context.
    /// </summary>
    private async Task<int> CalculateMessageXpAmount(IGuildUser user, ulong channelId)
    {
        var settings = await cacheManager.GetGuildXpSettingsAsync(user.GuildId);
        var baseAmount = settings.XpPerMessage <= 0 ? DefaultXpPerMessage : settings.XpPerMessage;

        // Cap the base amount to prevent abuse
        baseAmount = Math.Min(baseAmount, MaxXpPerMessage);

        // Apply multipliers
        var multiplier = await cacheManager.GetEffectiveMultiplierAsync(user.Id, user.GuildId, channelId);
        var finalAmount = (int)(baseAmount * multiplier);

        return finalAmount;
    }

    #endregion
}