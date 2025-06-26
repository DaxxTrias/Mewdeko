﻿using DataModel;
using LinqToDB;

namespace Mewdeko.Services;

/// <summary>
///     Provides services for managing greeting settings and executing greetings and farewells in guilds.
/// </summary>
public class GreetSettingsService : INService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;

    private readonly Channel<(GreetSettings, IGuildUser, TaskCompletionSource<bool>)> greetDmQueue =
        Channel.CreateBounded<(GreetSettings, IGuildUser, TaskCompletionSource<bool>)>(new BoundedChannelOptions(60)
        {
            FullMode = BoundedChannelFullMode.DropNewest
        });

    private readonly GuildSettingsService gss;
    private readonly ILogger<GreetSettingsService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GreetSettingsService" /> class, setting up event handlers for user
    ///     join and leave events, and guild join and leave events.
    /// </summary>
    /// <param name="client">The Discord client instance to interact with the Discord API.</param>
    /// <param name="gss">The service managing guild settings.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The handler managing Discord events.</param>
    /// <remarks>
    ///     Event handlers are set up to listen for specific Discord events, allowing the service to respond to user and guild
    ///     activities such as joining, leaving, or boosting.
    /// </remarks>
    public GreetSettingsService(DiscordShardedClient client, GuildSettingsService gss, IDataConnectionFactory dbFactory,
        EventHandler eventHandler, ILogger<GreetSettingsService> logger)
    {
        this.dbFactory = dbFactory;
        this.logger = logger;
        this.client = client;
        this.gss = gss;

        eventHandler.Subscribe("UserJoined", "GreetSettingsService", UserJoined);
        eventHandler.Subscribe("UserLeft", "GreetSettingsService", UserLeft);
        eventHandler.Subscribe("GuildMemberUpdated", "GreetSettingsService", ClientOnGuildMemberUpdated);
        _ = RunGreetLoop();
    }

    private async Task<bool> RunGreetLoop()
    {
        while (true)
        {
            try
            {
                var (conf, user, compl) = await greetDmQueue.Reader.ReadAsync();
                var res = await GreetDmUserInternal(conf, user);
                compl.TrySetResult(res);
            }
            catch
            {
                // ignored
            }
        }
    }

    private async Task<bool> GreetDmUser(GreetSettings conf, IGuildUser user)
    {
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await greetDmQueue.Writer.WriteAsync((conf, user, completionSource));
        return await completionSource.Task;
    }

    private async Task<GreetSettings> GetGreetSettings(ulong guildId)
    {
        var guildConfig = await gss.GetGuildConfig(guildId);
        return GreetSettings.Create(guildConfig);
    }

    private async Task TriggerBoostMessage(GreetSettings conf, SocketGuildUser user)
    {
        var chan = user.Guild.GetTextChannel(conf.BoostMessageChannelId);

        if (chan is null)
            return;

        if (string.IsNullOrWhiteSpace(conf.BoostMessage))
            return;
        var rep = new ReplacementBuilder()
            .WithDefault(user, chan, user.Guild, client)
            .Build();
        if (SmartEmbed.TryParse(rep.Replace(conf.BoostMessage), user.Guild?.Id, out var embed, out var plainText,
                out var components))
        {
            try
            {
                var toDelete = await chan.SendMessageAsync(plainText, embeds: embed, components: components?.Build())
                    .ConfigureAwait(false);
                if (conf.BoostMessageDeleteAfter > 0) toDelete.DeleteAfter(conf.BoostMessageDeleteAfter);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending boost message");
            }
        }
        else
        {
            var msg = rep.Replace(conf.BoostMessage);
            try
            {
                var toDelete = await chan.SendMessageAsync(msg).ConfigureAwait(false);

                if (conf.BoostMessageDeleteAfter > 0) toDelete.DeleteAfter(conf.BoostMessageDeleteAfter);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending boost message");
            }
        }
    }

    private Task ClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> optOldUser, SocketGuildUser newUser)
    {
        _ = Task.Run(async () =>
        {
            // if user is a new booster
            // or boosted again the same server
            if ((optOldUser.Value is not { PremiumSince: null } || newUser is not { PremiumSince: not null })
                && (optOldUser.Value?.PremiumSince is not { } oldDate || newUser.PremiumSince is not { } newDate ||
                    newDate <= oldDate))
            {
                return;
            }

            var conf = await GetGreetSettings(newUser.Guild.Id);
            if (!conf.SendBoostMessage)
                return;

            await TriggerBoostMessage(conf, newUser).ConfigureAwait(false);
        });
        return Task.CompletedTask;
    }

    private async Task UserLeft(IGuild guild, IUser usr)
    {
        try
        {
            var user = usr as SocketGuildUser;
            var conf = await GetGreetSettings(guild.Id);

            if (!conf.SendChannelByeMessage) return;

            if ((await guild.GetTextChannelsAsync()).SingleOrDefault(c =>
                    c.Id == conf.ByeMessageChannelId) is not
                { } channel) //maybe warn the server owner that the channel is missing
            {
                return;
            }

            // if group is newly created, greet that user right away,
            // but any user which joins in the next 5 seconds will
            // be greeted in a group greet
            await ByeUsers(conf, channel, [
                user
            ]).ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    ///     Sets or updates the boost message for a specific guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="message">The boost message to be set. Mentions within the message will be sanitized.</param>
    /// <returns>A boolean value indicating whether the boost message feature is enabled.</returns>
    /// <remarks>
    ///     This method updates the guild's configuration in the database and refreshes the local cache with the new settings.
    /// </remarks>
    public async Task<bool> SetBoostMessage(ulong guildId, string? message)
    {
        message = message?.SanitizeMentions();

        var conf = await gss.GetGuildConfig(guildId);
        conf.BoostMessage = message;
        await gss.UpdateGuildConfig(guildId, conf);
        return conf.SendBoostMessage;
    }

    /// <summary>
    ///     Sets the deletion timer for boost messages in a guild.
    /// </summary>
    /// <param name="guildId">The guild's unique identifier.</param>
    /// <param name="timer">
    ///     The time in seconds after which the boost message should be automatically deleted. Must be between
    ///     0 and 600.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the timer is not within the valid range.</exception>
    /// <remarks>
    ///     A timer value of 0 means the message will not be automatically deleted.
    /// </remarks>
    public async Task SetBoostDel(ulong guildId, int timer)
    {
        if (timer is < 0 or > 600)
            throw new ArgumentOutOfRangeException(nameof(timer));

        var conf = await gss.GetGuildConfig(guildId);
        conf.BoostMessageDeleteAfter = timer;
        await gss.UpdateGuildConfig(guildId, conf);
    }

    /// <summary>
    ///     Retrieves the boost message configured for a guild.
    /// </summary>
    /// <param name="gid">The guild's unique identifier.</param>
    /// <returns>The boost message text.</returns>
    public async Task<string> GetBoostMessage(ulong gid)
    {
        return (await gss.GetGuildConfig(gid)).BoostMessage;
    }

    /// <summary>
    ///     Enables or disables the boost message feature for a guild.
    /// </summary>
    /// <param name="guildId">The guild's unique identifier.</param>
    /// <param name="channelId">The ID of the channel where boost messages should be sent.</param>
    /// <returns>A boolean indicating whether the boost message feature is now enabled.</returns>
    public async Task<bool> SetBoost(ulong guildId, ulong channelId)
    {
        var conf = await gss.GetGuildConfig(guildId);
        conf.SendBoostMessage = !conf.SendBoostMessage;
        conf.BoostMessageChannelId = channelId;
        await gss.UpdateGuildConfig(guildId, conf);

        return !conf.SendBoostMessage;
    }

    /// <summary>
    ///     Sets the webhook URL for leave messages in a guild.
    /// </summary>
    /// <param name="guild">The guild object.</param>
    /// <param name="url">The URL of the webhook to send leave messages.</param>
    /// <remarks>
    ///     This setting allows the guild to customize the destination of leave messages.
    /// </remarks>
    public async Task SetWebLeaveUrl(IGuild guild, string url)
    {
        var gc = await gss.GetGuildConfig(guild.Id);
        gc.LeaveHook = url;
        await gss.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Retrieves the webhook URL configured for leave messages in a guild.
    /// </summary>
    /// <param name="gid">The guild's unique identifier.</param>
    /// <returns>The webhook URL for leave messages.</returns>
    private async Task<string> GetLeaveHook(ulong? gid)
    {
        return (await gss.GetGuildConfig(gid.Value)).LeaveHook;
    }

    private Task ByeUsers(GreetSettings conf, ITextChannel channel, IUser user)
    {
        return ByeUsers(conf, channel, [
            user
        ]);
    }

    private async Task ByeUsers(GreetSettings conf, ITextChannel channel, IEnumerable<IUser> users)
    {
        if (!users.Any())
            return;

        var rep = new ReplacementBuilder()
            .WithChannel(channel)
            .WithClient(client)
            .WithServer(client, (SocketGuild)channel.Guild)
            .WithManyUsers(users)
            .Build();
        var lh = await GetLeaveHook(channel.GuildId);

        if (SmartEmbed.TryParse(rep.Replace(conf.ChannelByeMessageText), channel.GuildId, out var embed,
                out var plainText, out var components))
        {
            try
            {
                if (string.IsNullOrEmpty(lh) || lh == 0.ToString())
                {
                    var toDelete = await channel
                        .SendMessageAsync(plainText, embeds: embed, components: components?.Build())
                        .ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0) toDelete.DeleteAfter(conf.AutoDeleteByeMessagesTimer);
                }
                else
                {
                    var webhook = new DiscordWebhookClient(await GetLeaveHook(channel.GuildId));
                    var toDelete = await webhook
                        .SendMessageAsync(plainText, embeds: embed, components: components?.Build())
                        .ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0)
                    {
                        var msg = await channel.GetMessageAsync(toDelete).ConfigureAwait(false) as IUserMessage;
                        msg.DeleteAfter(conf.AutoDeleteByeMessagesTimer);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error embeding bye message");
            }
        }
        else
        {
            var msg = rep.Replace(conf.ChannelByeMessageText);
            if (string.IsNullOrWhiteSpace(msg))
                return;
            try
            {
                if (string.IsNullOrEmpty(lh) || lh == 0.ToString())
                {
                    var toDelete = await channel.SendMessageAsync(msg.SanitizeMentions()).ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0) toDelete.DeleteAfter(conf.AutoDeleteByeMessagesTimer);
                }
                else
                {
                    var webhook = new DiscordWebhookClient(await GetLeaveHook(channel.GuildId));
                    var toDel = await webhook.SendMessageAsync(msg.SanitizeMentions()).ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0)
                    {
                        var msg2 = await channel.GetMessageAsync(toDel).ConfigureAwait(false) as IUserMessage;
                        msg2.DeleteAfter(conf.AutoDeleteByeMessagesTimer);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error sending bye message");
            }
        }
    }

    /// <summary>
    ///     Retrieves the direct message (DM) greeting message text for a guild.
    /// </summary>
    /// <param name="id">The guild's unique identifier.</param>
    /// <returns>The DM greeting message text.</returns>
    public async Task<string?> GetDmGreetMsg(ulong id)
    {
        var gc = await gss.GetGuildConfig(id);
        return gc.DmGreetMessageText;
    }

    private async Task<bool> GreetDmUserInternal(GreetSettings conf, IGuildUser user)
    {
        if (!conf.SendDmGreetMessage)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        var dUser = await db.DiscordUsers.FirstOrDefaultAsync(x => x.UserId == user.Id).ConfigureAwait(false);
        if (dUser.GreetDmsOptOut)
            return false;

        var channel = await user.CreateDMChannelAsync();

        var rep = new ReplacementBuilder()
            .WithDefault(user, channel, (SocketGuild)user.Guild, client)
            .Build();

        if (SmartEmbed.TryParse(rep.Replace(conf.DmGreetMessageText), user.GuildId, out var embed, out var plainText,
                out var components))
        {
            try
            {
                await channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build())
                    .ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }
        else
        {
            var msg = rep.Replace(conf.DmGreetMessageText);
            if (string.IsNullOrWhiteSpace(msg)) return true;
            try
            {
                await channel.SendConfirmAsync(msg).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private Task UserJoined(IGuildUser user)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var conf = await GetGreetSettings(user.GuildId);

                if (conf.SendDmGreetMessage)
                {
                    var channel = await user.CreateDMChannelAsync().ConfigureAwait(false);

                    if (channel != null) await GreetDmUser(conf, user).ConfigureAwait(false);
                }
            }
            catch
            {
                // ignored
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Retrieves the farewell message configured for a specific guild.
    /// </summary>
    /// <param name="gid">The unique identifier of the guild.</param>
    /// <returns>The farewell message text for the guild.</returns>
    public async Task<string?> GetByeMessage(ulong gid)
    {
        var gc = await gss.GetGuildConfig(gid);

        return gc.ChannelByeMessageText;
    }

    /// <summary>
    ///     Enables or disables the direct message greeting feature for a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="value">
    ///     Optional. A boolean value indicating whether the feature should be enabled. If null, the setting
    ///     will be toggled.
    /// </param>
    /// <returns>A boolean indicating whether the DM greeting feature is enabled after the operation.</returns>
    public async Task<bool> SetGreetDm(ulong guildId, bool? value = null)
    {
        var conf = await gss.GetGuildConfig(guildId);
        conf.SendDmGreetMessage = !conf.SendDmGreetMessage;
        await gss.UpdateGuildConfig(guildId, conf);
        return conf.SendDmGreetMessage;
    }

    /// <summary>
    ///     Sets the direct message greeting text for a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="message">The direct message greeting text to be set. Mentions will be sanitized.</param>
    /// <returns>A boolean indicating whether the DM greeting message feature is enabled.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the message is null or whitespace.</exception>
    public async Task<bool> SetGreetDmMessage(ulong guildId, string? message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        var conf = await gss.GetGuildConfig(guildId);
        conf.DmGreetMessageText = message;
        await gss.UpdateGuildConfig(guildId, conf);
        return conf.SendDmGreetMessage;
    }

    /// <summary>
    ///     Enables or disables the channel farewell message feature for a guild and sets the channel for farewells.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="channelId">The channel ID where farewells should be sent.</param>
    /// <param name="value">
    ///     Optional. A boolean value indicating whether the feature should be enabled. If null, the setting
    ///     will be toggled.
    /// </param>
    /// <returns>A boolean indicating whether the farewell message feature is enabled after the operation.</returns>
    public async Task<bool> SetBye(ulong guildId, ulong channelId, bool? value = null)
    {
        var conf = await gss.GetGuildConfig(guildId);
        conf.SendChannelByeMessage = !conf.SendChannelByeMessage;
        conf.ByeMessageChannelId = channelId;
        await gss.UpdateGuildConfig(guildId, conf);

        return conf.SendChannelByeMessage;
    }

    /// <summary>
    ///     Sets the farewell message for a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="message">The farewell message to be set. Mentions will be sanitized.</param>
    /// <returns>A boolean indicating whether the farewell message feature is enabled.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the message is null or whitespace.</exception>
    public async Task<bool> SetByeMessage(ulong guildId, string? message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        var conf = await gss.GetGuildConfig(guildId);

        conf.ChannelByeMessageText = message;
        await gss.UpdateGuildConfig(guildId, conf);

        return conf.SendChannelByeMessage;
    }

    /// <summary>
    ///     Sets the timer for auto-deleting farewell messages in a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="timer">
    ///     The time in seconds after which farewell messages should be deleted. Must be between 0 and 600
    ///     seconds.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the timer is outside the allowed range.</exception>
    public async Task SetByeDel(ulong guildId, int timer)
    {
        if (timer is < 0 or > 600)
            return;
        var conf = await gss.GetGuildConfig(guildId);

        conf.AutoDeleteByeMessagesTimer = timer;
        await gss.UpdateGuildConfig(guildId, conf);
    }

    #region Get Enabled Status

    /// <summary>
    ///     Determines if the direct message greeting feature is enabled for a specified guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>A boolean indicating if the direct message greeting feature is enabled.</returns>
    public async Task<bool> GetGreetDmEnabled(ulong guildId)
    {
        var conf = await gss.GetGuildConfig(guildId);

        return conf.SendDmGreetMessage;
    }

    /// <summary>
    ///     Determines if the boost message feature is enabled for a specified guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>A boolean indicating if the boost message feature is enabled.</returns>
    public async Task<bool> GetBoostEnabled(ulong guildId)
    {
        var conf = await gss.GetGuildConfig(guildId);

        return conf.SendBoostMessage;
    }

    /// <summary>
    ///     Determines if the channel farewell message feature is enabled for a specified guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>A boolean indicating if the channel farewell message feature is enabled.</returns>
    public async Task<bool> GetByeEnabled(ulong guildId)
    {
        var conf = await gss.GetGuildConfig(guildId);

        return conf.SendChannelByeMessage;
    }

    #endregion

    #region Test Messages

    /// <summary>
    ///     Sends a test farewell message in the specified channel for a given user.
    /// </summary>
    /// <param name="channel">The text channel where the message should be sent.</param>
    /// <param name="user">The user for whom the farewell message is targeted.</param>
    public async Task ByeTest(ITextChannel channel, IGuildUser user)
    {
        var conf = await GetGreetSettings(user.GuildId);
        await ByeUsers(conf, channel, user);
    }

    /// <summary>
    ///     Sends a test boost message in the specified channel for a given user.
    /// </summary>
    /// <param name="channel">The text channel where the message should be sent.</param>
    /// <param name="user">The user for whom the boost message is targeted.</param>
    public async Task BoostTest(ITextChannel channel, IGuildUser user)
    {
        var conf = await GetGreetSettings(user.GuildId);
        conf.BoostMessageChannelId = channel.Id;
        await TriggerBoostMessage(conf, user as SocketGuildUser).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sends a test direct message greeting to the specified user.
    /// </summary>
    /// <param name="channel">The direct message channel to use for sending the message.</param>
    /// <param name="user">The user to receive the greeting message.</param>
    /// <returns>A boolean indicating if the direct message was successfully sent.</returns>
    public async Task<bool> GreetDmTest(IDMChannel channel, IGuildUser user)
    {
        var conf = await GetGreetSettings(user.GuildId);
        return await GreetDmUser(conf, user);
    }

    #endregion
}

/// <summary>
///     Represents the greeting settings for a guild, including configurations for greeting and farewell messages, both in
///     channels and via direct messages (DM), as well as settings for boost messages.
/// </summary>
public class GreetSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether boost messages are enabled for the guild.
    /// </summary>
    public bool SendBoostMessage { get; set; }

    /// <summary>
    ///     Gets or sets the message text to send when a user boosts the guild.
    /// </summary>
    public string? BoostMessage { get; set; }

    /// <summary>
    ///     Gets or sets the time in seconds after which the boost message should be automatically deleted. A value of 0 means
    ///     the message will not be deleted automatically.
    /// </summary>
    public int BoostMessageDeleteAfter { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID where boost messages should be sent.
    /// </summary>
    public ulong BoostMessageChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the time in seconds after which farewell messages should be automatically deleted.
    /// </summary>
    public int AutoDeleteByeMessagesTimer { get; set; }


    /// <summary>
    ///     Gets or sets the channel ID where farewell messages should be sent.
    /// </summary>
    public ulong ByeMessageChannelId { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether direct message greetings are enabled for the guild.
    /// </summary>
    public bool SendDmGreetMessage { get; set; }

    /// <summary>
    ///     Gets or sets the direct message greeting text.
    /// </summary>
    public string? DmGreetMessageText { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether channel farewell messages are enabled for the guild.
    /// </summary>
    public bool SendChannelByeMessage { get; set; }

    /// <summary>
    ///     Gets or sets the channel farewell message text.
    /// </summary>
    public string? ChannelByeMessageText { get; set; }

    /// <summary>
    ///     Creates an instance of <see cref="GreetSettings" /> from a given <see cref="GuildConfig" />.
    /// </summary>
    /// <param name="g">The <see cref="GuildConfig" /> from which to populate the <see cref="GreetSettings" />.</param>
    /// <returns>
    ///     A new instance of <see cref="GreetSettings" /> populated with the settings from the given
    ///     <see cref="GuildConfig" />.
    /// </returns>
    public static GreetSettings Create(GuildConfig g)
    {
        return new GreetSettings
        {
            AutoDeleteByeMessagesTimer = g.AutoDeleteByeMessagesTimer,
            ByeMessageChannelId = g.ByeMessageChannelId,
            SendDmGreetMessage = g.SendDmGreetMessage,
            DmGreetMessageText = g.DmGreetMessageText,
            SendChannelByeMessage = g.SendChannelByeMessage,
            ChannelByeMessageText = g.ChannelByeMessageText,
            BoostMessage = g.BoostMessage,
            BoostMessageChannelId = g.BoostMessageChannelId,
            BoostMessageDeleteAfter = g.BoostMessageDeleteAfter,
            SendBoostMessage = g.SendBoostMessage
        };
    }
}