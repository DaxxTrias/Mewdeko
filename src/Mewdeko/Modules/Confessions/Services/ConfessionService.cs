using Discord;

namespace Mewdeko.Modules.Confessions.Services;

/*
 * todo: when we do the ef model port. will need to come back to this
public class ConfessionService2(DbService db, DiscordSocketClient client,
    GuildSettingsService guildSettings)
    : INService
{
*/

public class ConfessionService : INService
{
    private readonly DbService db;
    private readonly DiscordSocketClient client;
    private readonly GuildSettingsService guildSettings;

    public ConfessionService(DbService db, DiscordSocketClient client,
        GuildSettingsService guildSettings)
    {
        this.db = db;
        this.client = client;
        this.guildSettings = guildSettings;
    }

    public async Task SendConfession(
    ulong serverId,
    IUser user,
    string confession,
    IMessageChannel currentChannel, IInteractionContext? ctx = null, string? imageUrl = null)
    {
        var uow = db.GetDbContext();
        var confessions = uow.Confessions.ForGuild(serverId);
        if (confessions.Count > 0)
        {
            var guild = client.GetGuild(serverId);
            var current = confession.LastOrDefault();
            var currentUser = guild.GetUser(client.CurrentUser.Id);
            var confessionChannel =
                guild.GetTextChannel((await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionChannel);
            //var confessNumber = current?.ConfessNumber + 1 ?? 1;

            if (confessionChannel is null)
            {
                if (ctx?.Interaction is not null)
                {
                    await ctx.Interaction.SendEphemeralErrorAsync(
                        "The confession channel is invalid! Please tell the server staff about this!")
                            .ConfigureAwait(false);
                    return;
                }

                await currentChannel.SendErrorAsync(
                    "The confession channel is invalid! Please tell the server staff about this!")
                    .ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder().WithOkColor()
                .WithAuthor($"Anonymous confession #{current.ConfessNumber + 1}", guild.IconUrl)
                .WithDescription(confession)
                .WithFooter(
                    $"Do /confess or dm me .confess {guild.Id} your confession to send a confession!")
                .WithCurrentTimestamp();
            if (imageUrl != null)
                eb.WithImageUrl(imageUrl);
            var perms = currentUser.GetPermissions(confessionChannel);
            if (!perms.EmbedLinks || !perms.SendMessages)
            {
                if (ctx?.Interaction is not null)
                {
                    await ctx.Interaction.SendEphemeralConfirmAsync(
                        "Seems I dont have permission to post in the confession channel! Please tell server staff.")
                        .ConfigureAwait(false);
                    return;
                }

                await currentChannel.SendErrorAsync(
                    "Seems I dont have permission to post in the confession channel! Please tell the server staff.")
                    .ConfigureAwait(false);
                return;
            }

            /*
             * todo: old probably safe to delete (100% post ef model migration)
            var eb = CreateEmbed(guild, confession, confessNumber, imageUrl);
            if (!await HasPermissions(guild, confessionChannel))
            {
                SendMessage(ctx, currentChannel, "Seems I don't have permission to post in the confession channel! Please tell the server staff.");
                return;
            }
            */

            var msg = await confessionChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            if (ctx?.Interaction is not null)
            {
                await ctx.Interaction
                .SendEphemeralConfirmAsync(
                    "Your confession has been sent! Please keep in mind if the server is abusing confessions you can send in a report using `/confessions report`")
                .ConfigureAwait(false);

            }
            else
            {
                await currentChannel
                .SendConfirmAsync(
                    "Your confession has been sent! Please keep in mind if the server is abusing confessions you can send in a report using `/confessions report`")
                .ConfigureAwait(false);
            }

            var toadd = new Database.Models.Confessions
            {
                ChannelId = current.ChannelId,
                Confession = confession,
                ConfessNumber = ConfessNumber + 1,
                GuildId = current.GuildId,
                MessageId = msg.Id,
                UserId = user.Id
            };

            uow.Confessions.Add(toadd);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            if (await GetConfessionLogChannel(serverId) != 0)
            {
                var logChannel = guild.GetTextChannel(await GetConfessionLogChannel(serverId));
                if (logChannel is null)
                    return;
                var eb2 = new EmbedBuilder().WithErrorColor()
                    .AddField("user", $"{user} | {user.Id}")
                    .AddField($"Confession {current.ConfessNumber + 1}", confession)
                    .AddField("Message Link", msg.GetJumpUrl())
                        .AddField("***WARNING***",
                        "***Misuses of this function will be found out. Blacking you from its functionality.");
                await logChannel.SendMessageAsync(embed: eb2.Build()).ConfigureAwait(false);
            }
            //todo: old
            //await LogConfession(serverId, user, confession, msg, confessNumber);
        }
    }

    public async Task SetConfessionChannel(IGuild guild, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ConfessionChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    private async Task<ulong> GetConfessionChannel(ulong id)
    => (await guildSettings.GetGuildConfig(id)).ConfessionChannel;

    public async Task ToggleUserBlacklistAsync(ulong guildId, ulong roleId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        var blacklists = gc.GetConfessionBlacklists();
        if (!blacklists.Remove(roleId))
            blacklists.Add(roleId);

        gc.SetConfessionBlacklists(blacklists);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildSettings.UpdateGuildConfig(guildId, gc);
    }

    public async Task SetConfessionLogChannel(IGuild guild, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.ConfessionLogChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildSettings.UpdateGuildConfig(guild.Id, gc);
    }
    private async Task<ulong> GetConfessionLogChannel(ulong id)
    => (await guildSettings.GetGuildConfig(id)).ConfessionLogChannel;

    private static void SendMessage(IInteractionContext? ctx, IMessageChannel currentChannel, string message)
{
    if (ctx?.Interaction is not null)
    {
        ctx.Interaction.SendEphemeralFollowupErrorAsync(message).ConfigureAwait(false);
    }
    else
    {
        currentChannel.SendErrorAsync(message).ConfigureAwait(false);
    }
}

    private static EmbedBuilder CreateEmbed(IGuild guild, string confession, ulong confessNumber, string? imageUrl)
{
    var eb = new EmbedBuilder().WithOkColor()
        .WithAuthor($"Anonymous confession #{confessNumber}", guild.IconUrl)
        .WithDescription(confession)
        .WithFooter($"Do /confess or dm me .confess {guild.Id} yourconfession to send a confession!")
        .WithCurrentTimestamp();

    if (!string.IsNullOrEmpty(imageUrl))
    {
        eb.WithImageUrl(imageUrl);
    }
    return eb;
}
    
    private async Task<bool> HasPermissions(IGuild guild, ITextChannel channel)
{
    var currentUser = await guild.GetUserAsync(client.CurrentUser.Id);
    var perms = currentUser.GetPermissions(channel);
    return perms is { EmbedLinks: true, SendMessages: true };
}
    
    private async Task LogConfession(ulong serverId, IUser user, string confession, IMessage msg, ulong confessNumber)
{
    var guild = client.GetGuild(serverId);
    var logChannelId = await GetConfessionLogChannel(serverId);
    if (logChannelId == 0) return;

    var logChannel = guild.GetTextChannel(logChannelId);
    if (logChannel is null) return;

    var eb = new EmbedBuilder().WithErrorColor()
        .AddField("User", $"{user} | {user.Id}")
        .AddField($"Confession {confessNumber}", confession)
        .AddField("Message Link", msg.GetJumpUrl())
        .AddField("***WARNING***", "***Misuse of this function will lead me to finding out, blacklisting this server, and tearing out your reproductive organs.***");

    await logChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
}
    
}

public static class ConfessionExtensions
{
    public static List<ulong> GetConfessionBlacklists(this GuildConfig gc)
        => string.IsNullOrWhiteSpace(gc.ConfessionBlacklist)
        ? new List<ulong>()
        : gc.ConfessionBlacklist.Split(' ').Select(ulong.Parse).ToList();

    public static void SetConfessionBlacklists(this GuildConfig gc, IEnumerable<ulong> blacklists) =>
        gc.ConfessionBlacklist = blacklists.JoinWith(' ');
}