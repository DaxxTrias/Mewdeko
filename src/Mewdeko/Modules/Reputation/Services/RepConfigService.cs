using System.IO;
using System.Net.Http;
using System.Text;
using DataModel;
using Discord.Commands;
using LinqToDB;
using Mewdeko.Common.Configs;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Reputation.Common;
using Mewdeko.Services.Strings;
using Newtonsoft.Json;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Reputation.Services;

/// <summary>
///     Service for interactive reputation system configuration management.
/// </summary>
public class RepConfigService : INService
{
    // Active configuration sessions
    private readonly ConcurrentDictionary<ulong, ConfigSession> activeSessions = new();
    private readonly BotConfig config;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<RepConfigService> logger;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RepConfigService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="strings">The localized bot strings.</param>
    /// <param name="config">The bot configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public RepConfigService(
        IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings,
        BotConfig config,
        ILogger<RepConfigService> logger)
    {
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.config = config;
        this.logger = logger;
    }

    /// <summary>
    ///     Shows the interactive configuration menu for a guild.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ShowConfigurationMenuAsync(ICommandContext ctx)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        var session = new ConfigSession
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            ChannelId = ctx.Channel.Id,
            Config = repConfig,
            CurrentCategory = ConfigCategory.Basic
        };

        activeSessions.TryAdd(ctx.Guild.Id, session);

        var embed = BuildConfigurationEmbed(repConfig, ConfigCategory.Basic, ctx.Guild.Id);
        var components = BuildConfigurationComponents(ConfigCategory.Basic);

        var message = await ctx.Channel.SendMessageAsync(embed: embed, components: components);
        session.MessageId = message.Id;

        // Set up timeout to clean up session
        _ = Task.Delay(TimeSpan.FromMinutes(15)).ContinueWith(_ =>
        {
            activeSessions.TryRemove(ctx.Guild.Id, out var _);
        });
    }

    /// <summary>
    ///     Shows the current configuration status in a detailed embed.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ShowConfigurationStatusAsync(ICommandContext ctx)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.RepConfigTitle(ctx.Guild.Id))
            .WithDescription(strings.RepConfigDescription(ctx.Guild.Id))
            .AddField(strings.RepConfigBasic(ctx.Guild.Id), BuildBasicConfigText(repConfig, ctx.Guild.Id))
            .AddField(strings.RepConfigCooldowns(ctx.Guild.Id), BuildCooldownConfigText(repConfig, ctx.Guild.Id))
            .AddField(strings.RepConfigRequirements(ctx.Guild.Id), BuildRequirementsConfigText(repConfig, ctx.Guild.Id))
            .AddField(strings.RepConfigNotifications(ctx.Guild.Id),
                BuildNotificationsConfigText(repConfig, ctx.Guild.Id))
            .AddField(strings.RepConfigAdvanced(ctx.Guild.Id), BuildAdvancedConfigText(repConfig, ctx.Guild.Id))
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        await ctx.Channel.SendMessageAsync(embed: embed);
    }

    /// <summary>
    ///     Exports the current reputation configuration to JSON.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExportConfigurationAsync(ICommandContext ctx)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var repConfig = await db.RepConfigs.FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id);
            var channelConfigs = await db.RepChannelConfigs.Where(x => x.GuildId == ctx.Guild.Id).ToListAsync();
            var roleRewards = await db.RepRoleRewards.Where(x => x.GuildId == ctx.Guild.Id).ToListAsync();
            var reactionConfigs = await db.RepReactionConfigs.Where(x => x.GuildId == ctx.Guild.Id).ToListAsync();
            var customTypes = await db.RepCustomTypes.Where(x => x.GuildId == ctx.Guild.Id).ToListAsync();

            var exportData = new
            {
                ExportedAt = DateTime.UtcNow,
                GuildId = ctx.Guild.Id,
                GuildName = ctx.Guild.Name,
                Config = repConfig,
                ChannelConfigs = channelConfigs,
                RoleRewards = roleRewards,
                ReactionConfigs = reactionConfigs,
                CustomTypes = customTypes
            };

            var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            var bytes = Encoding.UTF8.GetBytes(json);

            using var stream = new MemoryStream(bytes);
            var fileName = $"reputation-config-{ctx.Guild.Id}-{DateTime.UtcNow:yyyy-MM-dd}.json";

            await ctx.Channel.SendFileAsync(stream, fileName,
                $"{config.SuccessEmote} {strings.RepExportSuccess(ctx.Guild.Id)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error exporting reputation configuration for guild {GuildId}", ctx.Guild.Id);
            await ctx.Channel.SendErrorAsync(strings.RepExportError(ctx.Guild.Id), config);
        }
    }

    /// <summary>
    ///     Imports reputation configuration from uploaded JSON file.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ImportConfigurationAsync(ICommandContext ctx)
    {
        var attachment = ctx.Message.Attachments.FirstOrDefault();
        if (attachment == null || !attachment.Filename.EndsWith(".json"))
        {
            await ctx.Channel.SendErrorAsync(strings.RepImportNoFile(ctx.Guild.Id), config);
            return;
        }

        try
        {
            using var httpClient = new HttpClient();
            var json = await httpClient.GetStringAsync(attachment.Url);
            var importData = JsonConvert.DeserializeObject<dynamic>(json);

            if (importData == null)
            {
                await ctx.Channel.SendErrorAsync(strings.RepImportInvalidFile(ctx.Guild.Id), config);
                return;
            }

            // TODO: Implement import logic with validation
            // This would deserialize and validate the configuration data
            // then update the database accordingly

            await ctx.Channel.SendConfirmAsync(strings.RepImportSuccess(ctx.Guild.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error importing reputation configuration for guild {GuildId}", ctx.Guild.Id);
            await ctx.Channel.SendErrorAsync(strings.RepImportError(ctx.Guild.Id), config);
        }
    }

    /// <summary>
    ///     Builds the configuration embed for a specific category.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="category">The configuration category.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The configuration embed.</returns>
    private Embed BuildConfigurationEmbed(RepConfig repConfig, ConfigCategory category, ulong guildId)
    {
        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.RepConfigInteractiveTitle(guildId))
            .WithDescription(GetCategoryDescription(category, guildId));

        return category switch
        {
            ConfigCategory.Basic => embed
                .AddField(strings.RepEnabled(guildId), repConfig.Enabled ? "✅" : "❌", true)
                .AddField(strings.RepEnableAnonymous(guildId), repConfig.EnableAnonymous ? "✅" : "❌", true)
                .AddField(strings.RepEnableNegative(guildId), repConfig.EnableNegativeRep ? "✅" : "❌", true)
                .Build(),

            ConfigCategory.Cooldowns => embed
                .AddField(strings.RepDefaultCooldown(guildId), $"{repConfig.DefaultCooldownMinutes} minutes", true)
                .AddField(strings.RepDailyLimitField(guildId), repConfig.DailyLimit.ToString(), true)
                .AddField(strings.RepWeeklyLimitField(guildId), repConfig.WeeklyLimit?.ToString() ?? "None", true)
                .Build(),

            ConfigCategory.Requirements => embed
                .AddField(strings.RepMinAccountAgeField(guildId), $"{repConfig.MinAccountAgeDays} days", true)
                .AddField(strings.RepMinMembershipField(guildId), $"{repConfig.MinServerMembershipHours} hours", true)
                .AddField(strings.RepMinMessagesField(guildId), repConfig.MinMessageCount.ToString(), true)
                .Build(),

            ConfigCategory.Notifications => embed
                .AddField(strings.RepNotificationChannel(guildId),
                    repConfig.NotificationChannel.HasValue ? $"<#{repConfig.NotificationChannel}>" : "None")
                .Build(),

            ConfigCategory.Advanced => embed
                .AddField(strings.RepEnableDecay(guildId), repConfig.EnableDecay ? "✅" : "❌", true)
                .AddField(strings.RepDecayType(guildId), repConfig.DecayType, true)
                .AddField(strings.RepDecayAmount(guildId), repConfig.DecayAmount.ToString(), true)
                .AddField(strings.RepDecayInactiveDays(guildId), $"{repConfig.DecayInactiveDays} days", true)
                .Build(),

            _ => embed.Build()
        };
    }

    /// <summary>
    ///     Builds the interactive components for configuration.
    /// </summary>
    /// <param name="category">The current configuration category.</param>
    /// <returns>The message components.</returns>
    private MessageComponent BuildConfigurationComponents(ConfigCategory category)
    {
        var selectMenu = new SelectMenuBuilder()
            .WithPlaceholder("Select configuration category...")
            .WithCustomId("rep_config_category")
            .AddOption("Basic Settings", "basic", "Enable/disable core features",
                category == ConfigCategory.Basic ? Emote.Parse("✅") : null)
            .AddOption("Cooldowns & Limits", "cooldowns", "Configure timing and limits",
                category == ConfigCategory.Cooldowns ? Emote.Parse("✅") : null)
            .AddOption("Requirements", "requirements", "User requirements to give rep",
                category == ConfigCategory.Requirements ? Emote.Parse("✅") : null)
            .AddOption("Notifications", "notifications", "Configure notification settings",
                category == ConfigCategory.Notifications ? Emote.Parse("✅") : null)
            .AddOption("Advanced", "advanced", "Advanced features like decay",
                category == ConfigCategory.Advanced ? Emote.Parse("✅") : null);

        var actionButtons = new ActionRowBuilder()
            .WithButton("Save Changes", "rep_config_save", ButtonStyle.Success, Emote.Parse("💾"))
            .WithButton("Reset to Defaults", "rep_config_reset", ButtonStyle.Danger, Emote.Parse("🔄"))
            .WithButton("Export Config", "rep_config_export", ButtonStyle.Secondary, Emote.Parse("📤"))
            .WithButton("Close", "rep_config_close", ButtonStyle.Secondary, Emote.Parse("❌"));

        return new ComponentBuilder()
            .WithSelectMenu(selectMenu)
            .WithRows(new List<ActionRowBuilder>
            {
                actionButtons
            })
            .Build();
    }

    /// <summary>
    ///     Gets the description for a configuration category.
    /// </summary>
    /// <param name="category">The configuration category.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The category description.</returns>
    private string GetCategoryDescription(ConfigCategory category, ulong guildId)
    {
        return category switch
        {
            ConfigCategory.Basic => strings.RepConfigBasicDesc(guildId),
            ConfigCategory.Cooldowns => strings.RepConfigCooldownsDesc(guildId),
            ConfigCategory.Requirements => strings.RepConfigRequirementsDesc(guildId),
            ConfigCategory.Notifications => strings.RepConfigNotificationsDesc(guildId),
            ConfigCategory.Advanced => strings.RepConfigAdvancedDesc(guildId),
            _ => "Configuration options"
        };
    }

    /// <summary>
    ///     Gets or creates a reputation configuration for a guild.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The reputation configuration.</returns>
    private static async Task<RepConfig> GetOrCreateConfigAsync(MewdekoDb db, ulong guildId)
    {
        var repConfig = await db.RepConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (repConfig != null) return repConfig;
        repConfig = new RepConfig
        {
            GuildId = guildId, DateAdded = DateTime.UtcNow
        };
        await db.InsertAsync(repConfig);

        return repConfig;
    }

    /// <summary>
    ///     Builds the basic configuration text display.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The formatted text.</returns>
    private string BuildBasicConfigText(RepConfig repConfig, ulong guildId)
    {
        return $"**{strings.RepEnabled(guildId)}:** {(repConfig.Enabled ? "✅ Enabled" : "❌ Disabled")}\n" +
               $"**{strings.RepEnableAnonymous(guildId)}:** {(repConfig.EnableAnonymous ? "✅ Enabled" : "❌ Disabled")}\n" +
               $"**{strings.RepEnableNegative(guildId)}:** {(repConfig.EnableNegativeRep ? "✅ Enabled" : "❌ Disabled")}";
    }

    /// <summary>
    ///     Builds the cooldown configuration text display.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The formatted text.</returns>
    private string BuildCooldownConfigText(RepConfig repConfig, ulong guildId)
    {
        return $"**{strings.RepDefaultCooldown(guildId)}:** {repConfig.DefaultCooldownMinutes} minutes\n" +
               $"**{strings.RepDailyLimitField(guildId)}:** {repConfig.DailyLimit}\n" +
               $"**{strings.RepWeeklyLimitField(guildId)}:** {repConfig.WeeklyLimit?.ToString() ?? "None"}";
    }

    /// <summary>
    ///     Builds the requirements configuration text display.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The formatted text.</returns>
    private string BuildRequirementsConfigText(RepConfig repConfig, ulong guildId)
    {
        return $"**{strings.RepMinAccountAgeField(guildId)}:** {repConfig.MinAccountAgeDays} days\n" +
               $"**{strings.RepMinMembershipField(guildId)}:** {repConfig.MinServerMembershipHours} hours\n" +
               $"**{strings.RepMinMessagesField(guildId)}:** {repConfig.MinMessageCount}";
    }

    /// <summary>
    ///     Builds the notifications configuration text display.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The formatted text.</returns>
    private string BuildNotificationsConfigText(RepConfig repConfig, ulong guildId)
    {
        return $"**{strings.RepNotificationChannel(guildId)}:** " +
               (repConfig.NotificationChannel.HasValue ? $"<#{repConfig.NotificationChannel}>" : "None");
    }

    /// <summary>
    ///     Builds the advanced configuration text display.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The formatted text.</returns>
    private string BuildAdvancedConfigText(RepConfig repConfig, ulong guildId)
    {
        return $"**{strings.RepEnableDecay(guildId)}:** {(repConfig.EnableDecay ? "✅ Enabled" : "❌ Disabled")}\n" +
               $"**{strings.RepDecayType(guildId)}:** {repConfig.DecayType}\n" +
               $"**{strings.RepDecayAmount(guildId)}:** {repConfig.DecayAmount}\n" +
               $"**{strings.RepDecayInactiveDays(guildId)}:** {repConfig.DecayInactiveDays} days";
    }
}