using Mewdeko.Modules.Currency.Services;
using Mewdeko.Services.Strings;
using Serilog;
using Mewdeko.Modules.Xp.Models;

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
    internal readonly CommandHandler CommandHandler;
    internal readonly IDataConnectionFactory dbFactory;
    internal readonly IDataCache DataCache;
    internal readonly IBotCredentials Credentials;
    internal readonly EventHandler EventHandler;
    internal readonly ICurrencyService CurrencyService;
    internal readonly GeneratedBotStrings Strings;


    // Core components
    private readonly XpBackgroundProcessor backgroundProcessor;
    private readonly XpVoiceTracker voiceTracker;
    private readonly XpCompetitionManager competitionManager;
    private readonly XpCacheManager cacheManager;
    private readonly XpRewardManager rewardManager;

    #endregion

    #region Constructor and Initialization

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpService"/> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="commandHandler">The command handler.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="strings">The string localization service.</param>
    /// <param name="dataCache">The data cache service.</param>
    /// <param name="credentials">The bot credentials.</param>
    /// <param name="eventHandler">The Discord event handler.</param>
    /// <param name="currencyService">The currency service.</param>
    public XpService(
        DiscordShardedClient client,
        CommandHandler commandHandler,
        IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings,
        IDataCache dataCache,
        IBotCredentials credentials,
        EventHandler eventHandler,
        ICurrencyService currencyService)
    {
        Client = client;
        CommandHandler = commandHandler;
        this.dbFactory = dbFactory;
        Strings = strings;
        DataCache = dataCache;
        Credentials = credentials;
        EventHandler = eventHandler;
        CurrencyService = currencyService;

        // Initialize sub-components
        cacheManager = new XpCacheManager(dataCache, dbFactory, client);
        rewardManager = new XpRewardManager(client, dbFactory, currencyService, cacheManager);
        competitionManager = new XpCompetitionManager(client, dbFactory);
        voiceTracker = new XpVoiceTracker(client, dbFactory, cacheManager);
        backgroundProcessor = new XpBackgroundProcessor(
            dbFactory,
            cacheManager,
            rewardManager,
            competitionManager);

        // Register event handlers
        EventHandler.MessageReceived += HandleMessageXp;
        EventHandler.UserVoiceStateUpdated += voiceTracker.HandleVoiceStateUpdate;
        EventHandler.GuildAvailable += OnGuildAvailable;
        Client.MessageReceived += OnMessageReceived;

        Log.Information("XP Service initialized");
    }

    /// <summary>
    ///     Unloads the service and cleans up resources.
    /// </summary>
    public Task Unload()
    {
        // Unregister event handlers
        EventHandler.MessageReceived -= HandleMessageXp;
        EventHandler.UserVoiceStateUpdated -= voiceTracker.HandleVoiceStateUpdate;
        EventHandler.GuildAvailable -= OnGuildAvailable;
        Client.MessageReceived -= OnMessageReceived;

        // Clean up resources
        backgroundProcessor.Dispose();
        voiceTracker.Dispose();
        competitionManager.Dispose();

        Log.Information("XP Service unloaded");
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
            Log.Error($"Error handling message XP for {user.Id} in {user.Guild.Id}\n{ex}");
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
            Log.Error(ex, "Error handling guild available for {GuildId}", guild.Id);
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
            Log.Error(ex, "Error handling first message of day for {UserId} in {GuildId}", user.Id, user.Guild.Id);
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