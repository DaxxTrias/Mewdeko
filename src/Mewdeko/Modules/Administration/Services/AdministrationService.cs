﻿using DataModel;
using Discord.Commands;
using LinqToDB;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service responsible for providing data to the admin module.
/// </summary>
public class AdministrationService : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AdministrationService" /> class with the specified dependencies.
    /// </summary>
    /// <param name="cmdHandler">The command handler.</param>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="bot">The bot instance.</param>
    public AdministrationService(CommandHandler cmdHandler,
        GuildSettingsService guildSettings, Mewdeko bot, IDataConnectionFactory dbFactory)
    {
        // Assign the database service and guild settings service
        this.guildSettings = guildSettings;
        this.dbFactory = dbFactory;

        // Subscribe to the CommandExecuted event of the command handler
        cmdHandler.CommandExecuted += DelMsgOnCmd_Handler;
    }


    /// <summary>
    ///     Sets the staff role for a given guild.
    /// </summary>
    /// <param name="guild">The guild to set the staff role for.</param>
    /// <param name="role">The role to set as the staff role.</param>
    public async Task StaffRoleSet(IGuild guild, ulong role)
    {
        // Create a new database context
        await using var db = await dbFactory.CreateConnectionAsync();
        // Get the guild configuration for the given guild ID
        var gc = await guildSettings.GetGuildConfig(guild.Id);

        // Set the staff role
        gc.StaffRole = role;

        // Update the guild configuration in the cache
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Toggles the opt-out status for a given guild.
    /// </summary>
    /// <param name="guild">The guild to toggle the opt-out status for.</param>
    /// <returns>A boolean indicating the new opt-out status.</returns>
    public async Task<bool> ToggleOptOut(IGuild guild)
    {
        // Create a new database context
        var gc = await guildSettings.GetGuildConfig(guild.Id);

        // Toggle the opt-out status
        gc.StatsOptOut = !gc.StatsOptOut;

        // Update the guild configuration in the cache
        await guildSettings.UpdateGuildConfig(guild.Id, gc);

        // Return the boolean equivalent of the new opt-out status
        return gc.StatsOptOut;
    }


    /// <summary>
    ///     Deletes the statistics data for a given guild.
    /// </summary>
    /// <param name="guild">The guild to delete the statistics data for.</param>
    /// <returns>A boolean indicating whether any data was deleted.</returns>
    public async Task<bool> DeleteStatsData(IGuild guild)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var toRemove = dbContext.CommandStats.Where(x => x.GuildId == guild.Id);
        if (!toRemove.Any())
            return false;
        await toRemove.DeleteAsync();
        return true;
    }

    /// <summary>
    ///     Sets the member role for a given guild.
    /// </summary>
    /// <param name="guild">The guild to set the member role for.</param>
    /// <param name="role">The role to set as the member role.</param>
    public async Task MemberRoleSet(IGuild guild, ulong role)
    {
        var gc = await guildSettings.GetGuildConfig(guild.Id);
        gc.MemberRole = role;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Gets the staff role for a given guild.
    /// </summary>
    /// <param name="id">The ID of the guild to get the staff role for.</param>
    /// <returns>The ID of the staff role.</returns>
    public async Task<ulong> GetStaffRole(ulong id)
    {
        return (await guildSettings.GetGuildConfig(id)).StaffRole;
    }

    /// <summary>
    ///     Gets the member role for a given guild.
    /// </summary>
    /// <param name="id">The ID of the guild to get the member role for.</param>
    /// <returns>The ID of the member role.</returns>
    public async Task<ulong> GetMemberRole(ulong id)
    {
        return (await guildSettings.GetGuildConfig(id)).MemberRole;
    }

    /// <summary>
    ///     Gets the DeleteMessageOnCommand data for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to get the data for.</param>
    /// <returns>A tuple containing a boolean indicating the DeleteMessageOnCommand status and a collection of channels.</returns>
    public async Task<(bool DelMsgOnCmd, IEnumerable<DelMsgOnCmdChannel> channels)> GetDelMsgOnCmdData(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var conf = await guildSettings.GetGuildConfig(guildId);
        var dconf = dbContext.DelMsgOnCmdChannels.Where(x => x.GuildId == guildId);

        return (conf.DeleteMessageOnCommand, dconf);
    }

    /// <summary>
    ///     Handles the deletion of messages on command execution.
    /// </summary>
    /// <param name="msg">The user message that triggered the command.</param>
    /// <param name="cmd">The executed command.</param>
    /// <returns>A completed task.</returns>
    private async Task DelMsgOnCmd_Handler(IUserMessage msg, CommandInfo cmd)
    {
        if (msg.Channel is not ITextChannel channel)
            return;
        await using var db = await dbFactory.CreateConnectionAsync();
        var conf = await guildSettings.GetGuildConfig(channel.GuildId);

        var exists = await db.DelMsgOnCmdChannels.FirstOrDefaultAsync(x => x.ChannelId == channel.Id);
        if (exists is not null)
        {
            // If the state is true and the command is not 'Purge' or 'pick', delete the message
            if (exists.State && cmd.Name != "Purge" && cmd.Name != "pick")
            {
                try
                {
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
        }
        else if (conf.DeleteMessageOnCommand && cmd.Name != "Purge")
        {
            try
            {
                await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }
    }

    /// <summary>
    ///     Toggles the DeleteMessageOnCommand setting for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the setting for.</param>
    /// <returns>A boolean indicating the new state of the DeleteMessageOnCommand setting.</returns>
    public async Task<bool> ToggleDeleteMessageOnCommand(ulong guildId)
    {
        var conf = await guildSettings.GetGuildConfig(guildId);

        // Toggle the value using a ternary operator
        conf.DeleteMessageOnCommand = !conf.DeleteMessageOnCommand;

        await guildSettings.UpdateGuildConfig(guildId, conf);

        return conf.DeleteMessageOnCommand;
    }


    /// <summary>
    ///     Sets the DeleteMessageOnCommand state for a specific channel in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the state for.</param>
    /// <param name="chId">The ID of the channel to set the state for.</param>
    /// <param name="newState">The new state to set.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetDelMsgOnCmdState(ulong guildId, ulong chId, Administration.State newState)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var settingsTable = db.DelMsgOnCmdChannels;

        if (newState == Administration.State.Inherit)
        {
            await settingsTable
                .Where(x => x.GuildId == guildId && x.ChannelId == chId)
                .DeleteAsync();
        }
        else
        {
            var targetState = newState == Administration.State.Enable;

            var updatedRows = await settingsTable
                .Where(x => x.GuildId == guildId && x.ChannelId == chId)
                .Set(x => x.State, targetState)
                .UpdateAsync();

            if (updatedRows == 0)
            {
                await db.InsertAsync(new DelMsgOnCmdChannel
                {
                    GuildId = guildId, ChannelId = chId, State = targetState
                });
            }
        }
    }


    /// <summary>
    ///     Sets the deafen status for a list of users in a guild.
    /// </summary>
    /// <param name="value">
    ///     The deafen status to set. If true, the users will be deafened. If false, the users will be
    ///     undeafened.
    /// </param>
    /// <param name="users">The users to set the deafen status for.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task DeafenUsers(bool value, params IGuildUser[] users)
    {
        if (users.Length == 0)
            return;
        foreach (var u in users)
        {
            try
            {
                await u.ModifyAsync(usr => usr.Deaf = value).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }
    }

    /// <summary>
    ///     Edits a message in a text channel.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="chanl">The text channel where the message is located.</param>
    /// <param name="messageId">The ID of the message to edit.</param>
    /// <param name="text">The new text for the message. If null, the message content will be removed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task EditMessage(ICommandContext context, ITextChannel chanl, ulong messageId, string? text)
    {
        var msg = await chanl.GetMessageAsync(messageId).ConfigureAwait(false);

        if (msg is not IUserMessage umsg || msg.Author.Id != context.Client.CurrentUser.Id)
            return;

        var rep = new ReplacementBuilder()
            .WithDefault(context)
            .Build();

        if (SmartEmbed.TryParse(rep.Replace(text), context.Guild?.Id, out var embed, out var plainText,
                out var components))
        {
            await umsg.ModifyAsync(x =>
            {
                x.Embeds = embed;
                x.Content = plainText?.SanitizeMentions();
                x.Components = components.Build();
            }).ConfigureAwait(false);
        }
        else
        {
            await umsg.ModifyAsync(x =>
            {
                x.Content = text.SanitizeMentions();
                x.Embed = null;
                x.Components = null;
            }).ConfigureAwait(false);
        }
    }
}