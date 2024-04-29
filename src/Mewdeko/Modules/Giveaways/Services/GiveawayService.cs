using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.ModuleBehaviors;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Swan;

namespace Mewdeko.Modules.Giveaways.Services;

public class GiveawayService(DiscordSocketClient client, DbService db, IBotCredentials creds,
        GuildSettingsService guildSettings)
    : INService, IReadyExecutor
{
    public async Task OnReadyAsync()
    {
        Log.Information("Giveaway Loop Started");
        while (true)
        {
            await Task.Delay(2000).ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var giveawaysEnumerable = GetGiveawaysBeforeAsync(now);
                if (!giveawaysEnumerable.Any())
                    continue;

                Log.Information("Executing {Count} giveaways", giveawaysEnumerable.Count());

                // make groups of 5, with 1.5 second inbetween each one to ensure against ratelimits
                var i = 0;
                foreach (var group in giveawaysEnumerable
                             .GroupBy(_ => ++i / ((giveawaysEnumerable.Count() / 5) + 1)))
                {
                    var executedGiveaways = group.ToList();
                    await Task.WhenAll(executedGiveaways.Select(x => GiveawayTimerAction(x))).ConfigureAwait(false);
                    await UpdateGiveaways(executedGiveaways).ConfigureAwait(false);
                    await Task.Delay(1500).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Error in Giveaway loop: {ExMessage}", ex.Message);
                Log.Warning(ex.ToString());
            }
        }
    }

    public async Task SetGiveawayEmote(IGuild guild, string emote)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.GiveawayEmote = emote;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task<string> GetGiveawayEmote(ulong id)
        => (await guildSettings.GetGuildConfig(id)).GiveawayEmote;

    private async Task UpdateGiveaways(List<Database.Models.Giveaways> g)
    {
        await using var uow = db.GetDbContext();
        foreach (var i in g)
        {
            var toupdate = new Database.Models.Giveaways
            {
                When = i.When,
                BlacklistRoles = i.BlacklistRoles,
                BlacklistUsers = i.BlacklistUsers,
                ChannelId = i.ChannelId,
                Ended = 1,
                MessageId = i.MessageId,
                RestrictTo = i.RestrictTo,
                Item = i.Item,
                ServerId = i.ServerId,
                UserId = i.UserId,
                Winners = i.Winners,
                Emote = i.Emote
            };
            uow.Giveaways.Remove(i);
            uow.Giveaways.Add(toupdate);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    private IEnumerable<Database.Models.Giveaways> GetGiveawaysBeforeAsync(DateTime now)
    {
        using var uow = db.GetDbContext();
        IEnumerable<Database.Models.Giveaways> giveaways;

        if (uow.Database.IsNpgsql())
        {
            giveaways = uow.Giveaways
                .ToLinqToDB()
                .Where(x => (int)(x.ServerId / (ulong)Math.Pow(2, 22) % (ulong)creds.TotalShards) == client.ShardId &&
                            x.Ended != 1 && x.When < now).ToList();
        }

        else
        {
            giveaways = uow.Giveaways
                .FromSqlInterpolated(
                    $"select * from Giveaways where ((ServerId >> 22) % {creds.TotalShards}) = {client.ShardId} and ended = 0 and \"when\" < {now};")
                .ToList();
        }

        return giveaways;

        //todo cleanup with ef model port
        //return uow.Giveaways
        //    .FromSqlInterpolated(
        //        $"select * from giveaways where ((serverid >> 22) % {creds.TotalShards}) == {client.ShardId} and \"when\" < {now} and \"Ended\" == 0;")
        //    .ToListAsync();

    }

    public async Task GiveawaysInternal(ITextChannel chan, TimeSpan ts, string item, int winners, ulong host,
        ulong serverId, ITextChannel currentChannel, IGuild guild, string? reqroles = null,
        string? blacklistusers = null,
        string? blacklistroles = null, IDiscordInteraction? interaction = null, string banner = null,
        IRole pingROle = null)
    {
        var gconfig = await guildSettings.GetGuildConfig(serverId).ConfigureAwait(false);
        IRole role = null;
        if (gconfig.GiveawayPingRole != 0)
        {
            role = guild.GetRole(gconfig.GiveawayPingRole);
        }

        if (pingROle is not null)
        {
            role = pingROle;
        }

        var hostuser = await guild.GetUserAsync(host).ConfigureAwait(false);
        var emote = (await GetGiveawayEmote(guild.Id)).ToIEmote();
        var eb = new EmbedBuilder
        {
            Color = Mewdeko.OkColor,
            Title = item,
            Description =
                $"React with {emote} to enter!\nHosted by {hostuser.Mention}\nEnd Time: <t:{DateTime.UtcNow.Add(ts).ToUnixEpochDate()}:R> (<t:{DateTime.UtcNow.Add(ts).ToUnixEpochDate()}>)\n",
            Footer = new EmbedFooterBuilder()
                .WithText($"{winners} Winners | {guild} Giveaways | Ends on {DateTime.UtcNow.Add(ts):dd.MM.yyyy}")
        };
        if (!string.IsNullOrEmpty(reqroles))
        {
            var splitreqs = reqroles.Split(" ");
            var reqrolesparsed = new List<IRole>();
            foreach (var i in splitreqs)
            {
                if (!ulong.TryParse(i, out var parsed))
                    continue;
                try
                {
                    reqrolesparsed.Add(guild.GetRole(parsed));
                }
                catch
                {
                    //ignored
                }
            }

            if (reqrolesparsed.Count > 0)
            {
                eb.WithDescription(
                    $"React with {emote} to enter!\nHosted by {hostuser.Mention}\nRequired Roles: {string.Join("\n", reqrolesparsed.Select(x => x.Mention))}\nEnd Time: <t:{DateTime.UtcNow.Add(ts).ToUnixEpochDate()}:R> (<t:{DateTime.UtcNow.Add(ts).ToUnixEpochDate()}>)\n");
            }
        }

        if (!string.IsNullOrEmpty(gconfig.GiveawayEmbedColor))
        {
            if (gconfig.GiveawayEmbedColor.StartsWith("#"))
                eb.WithColor(new Color(Convert.ToUInt32(gconfig.GiveawayEmbedColor.Replace("#", ""), 16)));
            else if (gconfig.GiveawayWinEmbedColor.StartsWith("0x") && gconfig.GiveawayEmbedColor.Length == 8)
                eb.WithColor(new Color(Convert.ToUInt32(gconfig.GiveawayEmbedColor.Replace("0x", ""), 16)));
            else if (uint.TryParse(gconfig.GiveawayEmbedColor, out var colorNumber))
                eb.WithColor(new Color(colorNumber));
        }

        if (!string.IsNullOrEmpty(gconfig.GiveawayBanner))
        {
            if (Uri.IsWellFormedUriString(gconfig.GiveawayBanner, UriKind.Absolute))
                eb.WithImageUrl(gconfig.GiveawayBanner);
        }

        if (!string.IsNullOrEmpty(banner))
        {
            if (Uri.IsWellFormedUriString(banner, UriKind.Absolute))
                eb.WithImageUrl(banner);
        }

        var msg = await chan.SendMessageAsync(role is not null ? role.Mention : "", embed: eb.Build())
            .ConfigureAwait(false);
        await msg.AddReactionAsync(emote).ConfigureAwait(false);
        var time = DateTime.UtcNow + ts;
        var rem = new Database.Models.Giveaways
        {
            ChannelId = chan.Id,
            UserId = host,
            ServerId = serverId,
            Ended = 0,
            When = time,
            Item = item,
            MessageId = msg.Id,
            Winners = winners,
            Emote = emote.ToString()
        };
        if (!string.IsNullOrWhiteSpace(reqroles))
            rem.RestrictTo = reqroles;

        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            uow.Giveaways.Add(rem);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        if (interaction is not null)
            await interaction.SendConfirmFollowupAsync($"Giveaway started in {chan.Mention}").ConfigureAwait(false);
        else
            await currentChannel.SendConfirmAsync($"Giveaway started in {chan.Mention}").ConfigureAwait(false);
    }

    public async Task GiveawayTimerAction(Database.Models.Giveaways r, IGuild? inputguild = null,
        ITextChannel? inputchannel = null)
    {
        var dclient = client as IDiscordClient;
        var guild = inputguild ?? await dclient.GetGuildAsync(r.ServerId);
        if (guild is null)
            return;

        var channel = inputchannel ?? await guild.GetTextChannelAsync(r.ChannelId);
        if (channel is null)
            return;

        //todo: cleanup with ef model port
        //if (client.GetGuild(r.ServerId) is not { } guild)
        //    return;
        //if (client.GetGuild(r.ServerId).GetTextChannel(r.ChannelId) is not { } channel)
        //    return;
        IUserMessage ch;
        try
        {
            if (await channel.GetMessageAsync(r.MessageId) is not
                IUserMessage ch1)
            {
                return;
            }

            ch = ch1;
        }
        catch
        {
            return;
        }

        var prefix = await guildSettings.GetPrefix(guild.Id).ConfigureAwait(false);

        await using var uow = db.GetDbContext();
        var emote = r.Emote.ToIEmote();
        if (emote.Name == null)
        {
            await ch.Channel
                .SendErrorAsync($"[This Giveaway]({ch.GetJumpUrl()}) failed because the emote used for it is invalid!")
                .ConfigureAwait(false);
            return;
        }

        var reacts = await ch.GetReactionUsersAsync(emote, 999999).FlattenAsync().ConfigureAwait(false);
        if (!reacts.Any())
        {
            var emoteTest = await GetGiveawayEmote(guild.Id);
            var emoteTest2 = emoteTest.ToIEmote();
            if (emoteTest2.Name == null)
            {
                await ch.Channel
                    .SendErrorAsync(
                        $"[This Giveaway]({ch.GetJumpUrl()}) failed because the emote used for it is invalid!")
                    .ConfigureAwait(false);
                return;
            }

            reacts = await ch.GetReactionUsersAsync(emoteTest.ToIEmote(), 999999).FlattenAsync().ConfigureAwait(false);
        }

        if (reacts.Count(x => !x.IsBot) - 1 < r.Winners)
        {
            var eb = new EmbedBuilder
            {
                Color = Mewdeko.ErrorColor,
                Description = "There were not enough participants!"
            };
            await ch.ModifyAsync(x =>
            {
                x.Embed = eb.Build();
                x.Content = null;
            }).ConfigureAwait(false);
            r.Ended = 1;
            uow.Giveaways.Update(r);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            if (r.Winners == 1)
            {
                var users = reacts.Where(x => !x.IsBot).Select(x => guild.GetUserAsync(x.Id).GetAwaiter().GetResult())
                    .Where(x => x is not null).ToList();
                if (r.RestrictTo is not null)
                {
                    var parsedreqs = new List<ulong>();
                    foreach (var i in r.RestrictTo.Split(" "))
                    {
                        if (ulong.TryParse(i, out var parsed))
                        {
                            parsedreqs.Add(parsed);
                        }
                    }

                    try
                    {
                        if (parsedreqs.Count > 0)
                        {
                            users = users.Where(x =>
                                    x.GetRoles().Any() &&
                                    x.GetRoles().Select(i => i.Id).Intersect(parsedreqs).Count() == parsedreqs.Count)
                                .ToList();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e.ToString());
                        return;
                    }
                }

                if (users.Count == 0)
                {
                    var eb1 = new EmbedBuilder().WithErrorColor()
                        .WithDescription(
                            "Looks like nobody that actually met the role requirements joined..")
                        .Build();
                    await ch.ModifyAsync(x =>
                    {
                        x.Embed = eb1;
                        x.Content = null;
                    }).ConfigureAwait(false);
                    return;
                }

                var rand = new Random();
                var index = rand.Next(users.Count);
                var user = users.ToList()[index];
                var gset = await guildSettings.GetGuildConfig(guild.Id);
                if (gset.DmOnGiveawayWin == 1)
                {
                    if (!string.IsNullOrEmpty(gset.GiveawayEndMessage))
                    {
                        var rep = new ReplacementBuilder()
                            .WithChannel(channel)
                            .WithClient(client)
                            .WithServer(client, guild as SocketGuild)
                            .WithUser(user);

                        rep.WithOverride("%messagelink%",
                            () => $"https://discord.com/channels/{guild.Id}/{channel.Id}/{ch.Id}");
                        rep.WithOverride("%giveawayitem%", () => r.Item);
                        rep.WithOverride("%giveawaywinners%", () => r.Winners.ToString());

                        var replacer = rep.Build();

                        if (SmartEmbed.TryParse(replacer.Replace(gset.GiveawayEndMessage), guild.Id, out var embeds,
                                out var plaintext, out var components))
                        {
                            try
                            {
                                await user.SendMessageAsync(plaintext, embeds: embeds ?? null,
                                    components: components?.Build());
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                        else
                        {
                            var ebdm = new EmbedBuilder()
                                .WithOkColor()
                                .WithDescription(
                                    $"Congratulations! You won a giveaway for [{r.Item}](https://discord.com/channels/{r.ServerId}/{r.ChannelId}/{r.MessageId})!");
                            ebdm.AddField("Message from Host", replacer.Replace(gset.GiveawayEndMessage));
                            try
                            {
                                await user.SendMessageAsync(embed: ebdm.Build());
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            var ebdm = new EmbedBuilder()
                                .WithOkColor()
                                .WithDescription(
                                    $"Congratulations! You won a giveaway for [{r.Item}](https://discord.com/channels/{r.ServerId}/{r.ChannelId}/{r.MessageId})!");
                            await user.SendMessageAsync(embed: ebdm.Build());
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                var winbed = ch.Embeds.FirstOrDefault().ToEmbedBuilder()
                    .WithErrorColor()
                    .WithDescription($"Winner: {user.Mention}!\nHosted by: <@{r.UserId}>")
                    .WithFooter($"Ended at {DateTime.UtcNow:dd.MM.yyyy HH:mm:ss}");

                //todo: cleanup with ef model port
                //var eb = new EmbedBuilder
                //{
                //    Color = Mewdeko.OkColor, Description = $"{user.Mention} won the giveaway for {r.Item}!"
                //};

                await ch.ModifyAsync(x =>
                {
                    x.Embed = winbed.Build();
                    x.Content = $"{r.Emote} **Giveaway Ended!** {r.Emote}";
                }).ConfigureAwait(false);
                await ch.Channel.SendMessageAsync($"Congratulations to {user.Mention}! {r.Emote}",
                    embed: new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(
                            $"{user.Mention} won the giveaway for [{r.Item}](https://discord.com/channels/{r.ServerId}/{r.ChannelId}/{r.MessageId})! \n\n- (Hosted by: <@{r.UserId}>)\n- Reroll: `{prefix}reroll {r.MessageId}`")
                        .Build()).ConfigureAwait(false);
                r.Ended = 1;
                uow.Giveaways.Update(r);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
            else
            {
                var rand = new Random();
                var users = (await Task.WhenAll(reacts.Where(x => !x.IsBot)
                    .Select(async x => await guild.GetUserAsync(x.Id)).Where(x => x is not null))).ToHashSet();
                if (r.RestrictTo is not null)
                {
                    var parsedreqs = new List<ulong>();
                    var split = r.RestrictTo.Split(" ");
                    foreach (var i in split)
                    {
                        if (ulong.TryParse(i, out var parsed))
                        {
                            parsedreqs.Add(parsed);
                        }
                    }

                    try
                    {
                        if (parsedreqs.Count > 0)
                        {
                            users = users.Where(x =>
                                    x.GetRoles().Select(i => i.Id).Intersect(parsedreqs).Count() == parsedreqs.Count)
                                .ToHashSet();
                        }
                    }
                    catch
                    {
                        return;
                    }
                }

                if (!users.Any())
                {
                    var eb1 = new EmbedBuilder().WithErrorColor()
                        .WithDescription(
                            "Looks like nobody that actually met the role requirements joined..")
                        .Build();
                    await ch.ModifyAsync(x =>
                    {
                        x.Embed = eb1;
                        x.Content = null;
                    }).ConfigureAwait(false);
                }

                var winners = users.ToList().OrderBy(_ => rand.Next()).Take(r.Winners);

                var winbed = ch.Embeds.FirstOrDefault().ToEmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(
                        $"Winner: {string.Join(", ", winners.Take(5).Select(x => x.Mention))}!\nHosted by: <@{r.UserId}>")
                    .WithFooter($"Ended at {DateTime.UtcNow:dd.MM.yyyy HH:mm:ss}");

                /*
                 * todo: cleanup with ef model port
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.OkColor, Description =
                        $"{string.Join("", winners.Select(x => x.Mention))} won the giveaway for {r.Item}!"
                };
                */

                await ch.ModifyAsync(x =>
                {
                    x.Embed = winbed.Build();
                    x.Content = $"{r.Emote} **Giveaway Ended!** {r.Emote}";
                }).ConfigureAwait(false);

                foreach (var winners2 in winners.Chunk(50))
                {
                    await ch.Channel.SendMessageAsync(
                        $"Congratulations to {string.Join(", ", winners2.Select(x => x.Mention))}! {r.Emote}",
                        embed: new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription(
                                $"{string.Join(", ", winners2.Select(x => x.Mention))} won the giveaway for [{r.Item}](https://discord.com/channels/{r.ServerId}/{r.ChannelId}/{r.MessageId})! \n\n- (Hosted by: <@{r.UserId}>)\n- Reroll: `{prefix}reroll {r.MessageId}`")
                            .Build()).ConfigureAwait(false);
                }

                r.Ended = 1;
                uow.Giveaways.Update(r);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}