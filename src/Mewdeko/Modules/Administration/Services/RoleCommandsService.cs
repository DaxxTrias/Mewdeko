using LinqToDB;
using Serilog;
using DataModel;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// Service for managing reaction-based role assignments and removals.
/// </summary>
public class RoleCommandsService : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    /// Initializes a new instance of the RoleCommandsService.
    /// </summary>
    /// <param name="dbFactory">Provider for database context access.</param>
    /// <param name="eventHandler">Event handler for Discord events.</param>
    /// <param name="guildSettings">Service for accessing guild configurations.</param>
    public RoleCommandsService(
        IDataConnectionFactory dbFactory,
        EventHandler eventHandler,
        GuildSettingsService guildSettings)
    {
        this.dbFactory = dbFactory;
        this.guildSettings = guildSettings;

        eventHandler.ReactionAdded += HandleReactionAdded;
        eventHandler.ReactionRemoved += HandleReactionRemoved;
    }

    /// <summary>
    /// Handles when a reaction is added to a message.
    /// </summary>
    private async Task HandleReactionAdded(
        Cacheable<IUserMessage, ulong> msg,
        Cacheable<IMessageChannel, ulong> chan,
        SocketReaction reaction)
    {
        try
        {
            if (!reaction.User.IsSpecified || reaction.User.Value.IsBot || reaction.User.Value is not SocketGuildUser gusr)
                return;

            if (chan.Value is not SocketGuildChannel gch)
                return;

            var reactRoles = await guildSettings.GetReactionRoles(gch.Guild.Id);
            if (reactRoles == null || reactRoles.Count == 0)
                return;

            var message = msg.HasValue ? msg.Value : await msg.GetOrDownloadAsync().ConfigureAwait(false);
            var conf = reactRoles.FirstOrDefault(x => x.MessageId == message.Id);

            var reactionRole = conf?.ReactionRoles.FirstOrDefault(x => x.EmoteName == reaction.Emote.Name || x.EmoteName == reaction.Emote.ToString());
            if (reactionRole == null)
                return;

            if (conf.Exclusive)
            {
                await HandleExclusiveRole(gusr, msg, conf, reactionRole, reaction).ConfigureAwait(false);
            }

            var toAdd = gusr.Guild.GetRole(reactionRole.RoleId);
            if (toAdd != null && gusr.Roles.All(r => r.Id != toAdd.Id))
                await gusr.AddRolesAsync([toAdd]).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var gch = chan.Value as IGuildChannel;
            Log.Error(ex, "Reaction Role Add failed in Guild {GuildId}", gch?.GuildId);
        }
    }

    private static async Task HandleExclusiveRole(
        SocketGuildUser user,
        Cacheable<IUserMessage, ulong> msg,
        ReactionRoleMessage conf,
        ReactionRole currentRole,
        SocketReaction reaction)
    {
        var roleIdsToRemove = conf.ReactionRoles
            .Where(x => x.RoleId != currentRole.RoleId)
            .Select(x => user.Guild.GetRole(x.RoleId))
            .Where(x => x != null && user.Roles.Any(ur => ur.Id == x.Id))
            .ToList();

        if (roleIdsToRemove.Any())
        {
            try
            {
                await user.RemoveRolesAsync(roleIdsToRemove).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                 Log.Warning(ex, "Error removing exclusive roles from user {UserId}", user.Id);
            }
        }

        try
        {
            var message = await msg.GetOrDownloadAsync().ConfigureAwait(false);
            foreach (var (key, _) in message.Reactions)
            {
                if (key.Name == reaction.Emote.Name)
                    continue;

                var reactedUsers = await message.GetReactionUsersAsync(key, 10).FlattenAsync();
                if (reactedUsers.Any(u => u.Id == user.Id))
                {
                    try
                    {
                        await message.RemoveReactionAsync(key, user).ConfigureAwait(false);
                        await Task.Delay(100);
                    }
                    catch { /* ignored */ }
                }
            }
        }
        catch (Exception ex)
        {
             Log.Warning(ex, "Error removing other reactions for exclusive role for user {UserId}", user.Id);
        }
    }

    /// <summary>
    /// Handles when a reaction is removed from a message.
    /// </summary>
    private async Task HandleReactionRemoved(
        Cacheable<IUserMessage, ulong> msg,
        Cacheable<IMessageChannel, ulong> chan,
        SocketReaction reaction)
    {
        try
        {
            if (!reaction.User.IsSpecified || reaction.User.Value.IsBot || reaction.User.Value is not SocketGuildUser gusr)
                return;

            if (chan.Value is not SocketGuildChannel gch)
                return;

            var reactRoles = await guildSettings.GetReactionRoles(gch.Guild.Id);
            if (reactRoles == null || reactRoles.Count == 0)
                return;

            var message = msg.HasValue ? msg.Value : await msg.GetOrDownloadAsync().ConfigureAwait(false);
            var conf = reactRoles.FirstOrDefault(x => x.MessageId == message.Id);
             if (conf == null) return;

            if (conf.Exclusive)
                 return;

            var reactionRole = conf.ReactionRoles.FirstOrDefault(x => x.EmoteName == reaction.Emote.Name || x.EmoteName == reaction.Emote.ToString());
            if (reactionRole == null)
                return;

            var toRemove = gusr.Guild.GetRole(reactionRole.RoleId);
            if (toRemove != null && gusr.Roles.Any(r => r.Id == toRemove.Id))
                await gusr.RemoveRolesAsync(new[] { toRemove }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var gch = chan.Value as IGuildChannel;
            Log.Error(ex, "Reaction Role Remove failed in Guild {GuildId}", gch?.GuildId);
        }
    }

    /// <summary>
    /// Gets all reaction role messages for a guild.
    /// </summary>
    /// <param name="guildId">ID of the guild.</param>
    /// <returns>A tuple containing success status and collection of reaction role messages.</returns>
    public async Task<(bool Success, HashSet<ReactionRoleMessage>? Messages)> Get(ulong guildId)
    {
        var reactRoles = await guildSettings.GetReactionRoles(guildId);
        return (reactRoles != null && reactRoles.Count > 0, reactRoles);
    }

    /// <summary>
    /// Adds a new reaction role message to a guild.
    /// </summary>
    /// <param name="guildId">ID of the guild.</param>
    /// <param name="reactionRoleMessage">The reaction role message to add.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> Add(ulong guildId, ReactionRoleMessage reactionRoleMessage)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        reactionRoleMessage.GuildId = guildId;
        try
        {
             // Insert main message
             await db.InsertAsync(reactionRoleMessage).ConfigureAwait(false);
             if (reactionRoleMessage.ReactionRoles != null && reactionRoleMessage.ReactionRoles.Any())
             {
                 foreach(var rr in reactionRoleMessage.ReactionRoles)
                 {
                     rr.ReactionRoleMessageId = reactionRoleMessage.Id; // Assign FK
                     await db.InsertAsync(rr).ConfigureAwait(false);
                 }
             }

             guildSettings.ClearCacheForGuild(guildId);
             return true;
        }
        catch(Exception ex)
        {
             Log.Error(ex, "Failed to add ReactionRoleMessage for Guild {GuildId}", guildId);
             return false;
        }
    }

    /// <summary>
    /// Removes a reaction role message and its associated reaction roles from a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the reaction role message to remove.</param>
    public async Task Remove(ulong guildId, int index)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var messageToRemove = await db.GetTable<ReactionRoleMessage>()
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Index)
            .Skip(index)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (messageToRemove == null)
            return;

        try
        {
            await db.GetTable<ReactionRole>()
                .Where(rr => rr.ReactionRoleMessageId == messageToRemove.Id)
                .DeleteAsync().ConfigureAwait(false);

            await db.DeleteAsync(messageToRemove).ConfigureAwait(false);

            guildSettings.ClearCacheForGuild(guildId);
        }
        catch(Exception ex)
        {
             Log.Error(ex, "Failed to remove ReactionRoleMessage at index {Index} for Guild {GuildId}", index, guildId);
        }
    }
}