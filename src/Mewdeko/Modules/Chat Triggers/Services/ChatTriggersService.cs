﻿using AngleSharp.Text;
using Discord;
using Discord.WebSocket;
using LinqToDB.Common;
using Mewdeko.Common;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Common.Yml;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Chat_Triggers.Common;
using Mewdeko.Modules.Chat_Triggers.Extensions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CTModel = Mewdeko.Database.Models.ChatTriggers;

namespace Mewdeko.Modules.Chat_Triggers.Services;

public sealed class ChatTriggersService : IEarlyBehavior, INService, IReadyExecutor
{
    public enum CtField
    {
        AutoDelete,
        DmResponse,
        AllowTarget,
        ContainsAnywhere,
        Message,
        ReactToTrigger,
        NoRespond,
        PermsEnabledByDefault,
        ChannelsEnabledByDefault
    }

    private const string MENTION_PH = "%bot.mention%";

    private const string PREPEND_EXPORT =
        @"# Keys are triggers, Each key has a LIST of custom reactions in the following format:
# - res: Response string
#   react: 
#     - <List
#     -  of
#     - reactions>
#   at: Whether custom reaction allows targets (see .h .crat) 
#   ca: Whether custom reaction expects trigger anywhere (see .h .crca) 
#   dm: Whether custom reaction DMs the response (see .h .crdm) 
#   ad: Whether custom reaction automatically deletes triggering message (see .h .crad) 
#   rtt: Whether custom reaction emotes are added to the response or trigger

";

    private static readonly ISerializer _exportSerializer = new SerializerBuilder()
        .WithEventEmitter(args => new MultilineScalarFlowStyleEmitter(args))
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithIndentedSequences()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .DisableAliases()
        .Build();

    private readonly Mewdeko _bot;
    private readonly DiscordSocketClient _client;
    private readonly CommandHandler _cmd;
    private readonly CmdCdService _cmdCds;
    private readonly TypedKey<bool> _crsReloadedKey = new("crs.reloaded");

    private readonly DbService _db;

    private readonly TypedKey<CTModel> _gcrAddedKey = new("gcr.added");
    private readonly TypedKey<int> _gcrDeletedkey = new("gcr.deleted");
    private readonly TypedKey<CTModel> _gcrEditedKey = new("gcr.edited");

    private readonly object _gcrWriteLock = new();
    private readonly GlobalPermissionService _gperm;
    private readonly DiscordPermOverrideService _discordPermOverride;
    private readonly PermissionService _perms;
    private readonly IPubSub _pubSub;
    private readonly Random _rng;
    private readonly IBotStrings _strings;

    // it is perfectly fine to have global customreactions as an array
    // 1. custom reactions are almost never added (compared to how many times they are being looped through)
    // 2. only need write locks for this as we'll rebuild+replace the array on every edit
    // 3. there's never many of them (at most a thousand, usually < 100)
    private CTModel[] globalReactions;
    private ConcurrentDictionary<ulong, CTModel[]> newGuildReactions;

    private bool ready;

    public ChatTriggersService(PermissionService perms, DbService db, IBotStrings strings,
        Mewdeko bot,
        DiscordSocketClient client, CommandHandler cmd, GlobalPermissionService gperm, CmdCdService cmdCds,
        IPubSub pubSub,
        DiscordPermOverrideService discordPermOverride)
    {
        _db = db;
        _client = client;
        _perms = perms;
        _cmd = cmd;
        _strings = strings;
        _bot = bot;
        _cmdCds = cmdCds;
        _gperm = gperm;
        _pubSub = pubSub;
        _discordPermOverride = discordPermOverride;
        _rng = new MewdekoRandom();

        _pubSub.Sub(_crsReloadedKey, OnCrsShouldReload);
        pubSub.Sub(_gcrAddedKey, OnGcrAdded);
        pubSub.Sub(_gcrDeletedkey, OnGcrDeleted);
        pubSub.Sub(_gcrEditedKey, OnGcrEdited);

        bot.JoinedGuild += OnJoinedGuild;
        _client.LeftGuild += OnLeftGuild;
    }

    public int Priority => -1;
    public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;

    public async Task<bool> RunBehavior(DiscordSocketClient client, IGuild guild, IUserMessage msg)
    {
        // maybe this message is a custom reaction
        var ct = TryGetChatTriggers(msg);

        if (ct is null)
            return false;
        if (await _cmdCds.TryBlock(guild, msg.Author, ct.Trigger))
            return false;
        if (!ct.ValidTriggerTypes.HasFlag(ChatTriggerType.Message))
            return false;

        try
        {
            if (_gperm.BlockedModules.Contains("ActualChatTriggers")) return true;

            if (guild is SocketGuild sg)
            {
                var pc = _perms.GetCacheFor(guild.Id);
                if (!pc.Permissions.CheckPermissions(msg, ct.Trigger, "ActualChatTriggers",
                        out var index))
                {
                    if (pc.Verbose)
                    {
                        var returnMsg = _strings.GetText("perm_prevent", sg.Id,
                            index + 1,
                            Format.Bold(pc.Permissions[index].GetCommand(_cmd.GetPrefix(guild), sg)));
                        try
                        {
                            await msg.Channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        Log.Information(returnMsg);
                    }

                    return true;
                }

                if (_discordPermOverride.TryGetOverrides(guild.Id, ct.Trigger, out var perms))
                {
                    var user = msg.Author as IGuildUser;
                    if (!user.GuildPermissions.Has(perms.Value))
                    {
                        Log.Information($"Chat Trigger {ct.Trigger} Blocked for {msg.Author} in {guild} due to them missing {perms}.");
                        return false;
                    }
                }
            }

            IUserMessage sentMsg = null;
            if (!ct.NoRespond)
                sentMsg = await ct.Send(msg, _client, false).ConfigureAwait(false);

            foreach (var reaction in ct.GetReactions())
            {
                try
                {
                    if (!ct.ReactToTrigger && !ct.NoRespond)
                        await sentMsg.AddReactionAsync(reaction.ToIEmote());
                    else
                        await msg.AddReactionAsync(reaction.ToIEmote());
                }
                catch
                {
                    Log.Warning("Unable to add reactions to message {Message} in server {GuildId}", sentMsg.Id,
                        ct.GuildId);
                    break;
                }

                await Task.Delay(1000);
            }

            try
            {
                if (ct.AutoDeleteTrigger)
                    await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            if (ct.GuildId is not null && msg?.Author is IGuildUser guildUser)
            {
                var effectedUsers = ct.RoleGrantType switch
                {
                    CTRoleGrantType.Mentioned => msg.Content.GetUserMentions().Take(5),
                    CTRoleGrantType.Sender => new List<ulong> { msg.Author.Id },
                    CTRoleGrantType.Both => Enumerable.Append(msg.Content.GetUserMentions().Take(4), msg.Author.Id),
                    _ => new List<ulong>()
                };

                foreach (var userId in effectedUsers)
                {
                    var user = await guildUser.Guild.GetUserAsync(userId);
                    try
                    {
                        var baseRoles = user.RoleIds.Where(x => x != guild?.EveryoneRole.Id).ToList();
                        var roles = baseRoles.Where(x => !ct.RemovedRoles?.Contains(x.ToString()) ?? true).ToList();
                        roles.AddRange(ct.GetGrantedRoles().Where(x => !user.RoleIds.Contains(x)));
                        // difference is caused by @everyone
                        if (baseRoles.Any(x => !roles.Contains(x)) || roles.Any(x => !baseRoles.Contains(x)))
                            await user.ModifyAsync(x => x.RoleIds = new(roles));
                    }
                    catch
                    {
                        Log.Warning("Unable to modify the roles of {User} in {GuildId}", guildUser.Id, ct.GuildId);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex.Message);
        }

        return false;
    }

    public async Task<bool> RunInteractionTrigger(DiscordSocketClient client, SocketInteraction inter, CTModel ct)
    {
        if (!ct.ValidTriggerTypes.HasFlag(ChatTriggerType.Interaction))
            return false;
        try
        {
            var fakeMsg = new MewdekoUserMessage()
            {
                    Author = inter.User, Content = ct.Trigger, Channel = inter.Channel,
            };
            
            if (_gperm.BlockedModules.Contains("ActualChatTriggers")) return true;
            
            if (inter.Channel is IGuildChannel {Guild: SocketGuild guild})
            {
                var pc = _perms.GetCacheFor(guild.Id);
                if (!pc.Permissions.CheckPermissions(fakeMsg, ct.Trigger, "ActualChatTriggers",
                        out var index))
                {
                    if (pc.Verbose)
                    {
                        var returnMsg = _strings.GetText("perm_prevent", guild.Id,
                            index + 1,
                            Format.Bold(pc.Permissions[index].GetCommand(_cmd.GetPrefix(guild), guild)));
                        try
                        {
                            await fakeMsg.Channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        Log.Information(returnMsg);
                    }

                    return true;
                }

                if (_discordPermOverride.TryGetOverrides(guild.Id, ct.Trigger, out var perms))
                {
                    var user = inter.User as IGuildUser;
                    if (!user.GuildPermissions.Has(perms.Value))
                    {
                        Log.Information($"Chat Trigger {ct.Trigger} Blocked for {inter.User} in {guild} due to them missing {perms}.");
                        return false;
                    }
                }
            }

            IUserMessage sentMsg = null;
            if (!ct.NoRespond)
                sentMsg = await ct.SendInteraction(inter, _client, false, fakeMsg, ct.EphemeralResponse).ConfigureAwait(false);

            foreach (var reaction in ct.GetReactions())
            {
                try
                {
                    if (!ct.ReactToTrigger && !ct.NoRespond)
                        await sentMsg.AddReactionAsync(reaction.ToIEmote());
                    else
                        await sentMsg.AddReactionAsync(reaction.ToIEmote());
                }
                catch
                {
                    Log.Warning("Unable to add reactions to message {Message} in server {GuildId}", sentMsg.Id,
                        ct.GuildId);
                    break;
                }

                await Task.Delay(1000);
            }

            if (ct.GuildId is not null && inter.User is IGuildUser guildUser)
            {
                List<ulong> effectedUsers = new();
                effectedUsers = inter is SocketUserCommand uCMD
                    ? ct.RoleGrantType switch
                    {
                        CTRoleGrantType.Mentioned => new List<ulong> {uCMD.Data.Member.Id},
                        CTRoleGrantType.Sender => new List<ulong> {uCMD.User.Id},
                        CTRoleGrantType.Both => new List<ulong> {uCMD.User.Id, uCMD.Data.Member.Id},
                        _ => new List<ulong>()
                    }
                    : ct.RoleGrantType switch
                    {
                        CTRoleGrantType.Mentioned => new(),
                        CTRoleGrantType.Sender => new List<ulong> {inter.User.Id},
                        CTRoleGrantType.Both => new List<ulong> {inter.User.Id},
                        _ => new List<ulong>()
                    };

                foreach (var userId in effectedUsers)
                {
                    var user = await guildUser.Guild.GetUserAsync(userId);
                    try
                    {
                        var baseRoles = user.RoleIds.Where(x => x != guildUser.Guild?.EveryoneRole.Id).ToList();
                        var roles = baseRoles.Where(x => !ct.RemovedRoles?.Contains(x.ToString()) ?? true).ToList();
                        roles.AddRange(ct.GetGrantedRoles().Where(x => !user.RoleIds.Contains(x)));
                        
                        // difference is caused by @everyone
                        if (baseRoles.Any(x => !roles.Contains(x)) || roles.Any(x => !baseRoles.Contains(x)))
                            await user.ModifyAsync(x => x.RoleIds = new(roles));
                    }
                    catch
                    {
                        Log.Warning("Unable to modify the roles of {User} in {GuildId}", guildUser.Id, ct.GuildId);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex.Message);
        }

        return false;
    }
    
    public string ExportCrs(ulong? guildId)
    {
        var crs = GetChatTriggersFor(guildId);

        var crsDict = crs
            .GroupBy(x => x.Trigger)
            .ToDictionary(x => x.Key, x => x.Select(ExportedTriggers.FromModel));

        return PREPEND_EXPORT + _exportSerializer
            .Serialize(crsDict)
            .UnescapeUnicodeCodePoints();
    }

    public async Task<bool> ImportCrsAsync(IGuildUser user, string input)
    {
        Dictionary<string, List<ExportedTriggers>> data;
        try
        {
            data = Yaml.Deserializer.Deserialize<Dictionary<string, List<ExportedTriggers>>>(input);
            if (data.Sum(x => x.Value.Count) == 0)
                return false;
        }
        catch
        {
            return false;
        }

        await using var uow = _db.GetDbContext();
        List<CTModel> triggers = new();
        foreach (var (trigger, value) in data)
        {
            triggers.AddRange(value
                                    .Where(ct => !string.IsNullOrWhiteSpace(ct.Res))
                                    .Select(ct => new CTModel
                                    {
                                        GuildId = user.Guild.Id,
                                        Response = ct.Res,
                                        Reactions = ct.React?.JoinWith("@@@"),
                                        Trigger = trigger,
                                        AllowTarget = ct.At,
                                        ContainsAnywhere = ct.Ca,
                                        DmResponse = ct.Dm,
                                        AutoDeleteTrigger = ct.Ad,
                                        NoRespond = ct.Nr,
                                        IsRegex = ct.Rgx,
                                        GrantedRoles = string.Join("@@@", ct.aRole.Select(x => x.ToString())),
                                        RemovedRoles = string.Join("@@@", ct.rRole.Select(x => x.ToString()))
                                    }));
        }

        List<ulong> roles = new();
        triggers.ForEach(x => roles.AddRange(x.GetGrantedRoles()));
        triggers.ForEach(x => roles.AddRange(x.GetRemovedRoles()));

        if (roles.Count > 0 && !roles.Any(y => !user.Guild.GetRole(y).CanManageRole(user)))
            return false;

        await uow.ChatTriggers.AddRangeAsync(triggers);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await TriggerReloadChatTriggers();
        return true;
    }

    private async Task ReloadInternal(IReadOnlyList<ulong> allGuildIds)
    {
        await using var uow = _db.GetDbContext();
        var guildItems = await uow.ChatTriggers
            .AsNoTracking()
            .Where(x => allGuildIds.Contains(x.GuildId.Value))
            .ToListAsync();

        newGuildReactions = guildItems
            .GroupBy(k => k.GuildId!.Value)
            .ToDictionary(g => g.Key,
                g => g.Select(x =>
                {
                    x.Trigger = x.Trigger.Replace(MENTION_PH, _client.CurrentUser.Mention);
                    return x;
                }).ToArray())
            .ToConcurrent();

        lock (_gcrWriteLock)
        {
            globalReactions = uow.ChatTriggers
                .AsNoTracking()
                .Where(x => x.GuildId == null || x.GuildId == 0)
                .AsEnumerable()
                .Select(x =>
                {
                    x.Trigger = x.Trigger.Replace(MENTION_PH, _client.CurrentUser.Mention);
                    return x;
                })
                .ToArray();
        }

        ready = true;
    }

    private CTModel? TryGetChatTriggers(IUserMessage umsg)
    {
        if (!ready)
            return null;

        if (umsg.Channel is not SocketTextChannel channel)
            return null;

        var content = umsg.Content.Trim().ToLowerInvariant();

        if (newGuildReactions.TryGetValue(channel.Guild.Id, out var reactions) && reactions.Length > 0)
        {
            var cr = MatchChatTriggers(content, reactions);
            if (cr is not null)
                return cr;
        }

        var localGrs = globalReactions;

        return MatchChatTriggers(content, localGrs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CTModel? MatchChatTriggers(string content, CTModel[] crs)
    {
        var result = new List<CTModel>(1);
        for (var i = 0; i < crs.Length; i++)
        {
            var ct = crs[i];
            var trigger = ct.Trigger;

            // regex triggers are only checked on regex content
            if (ct.IsRegex)
            {
                if (Regex.IsMatch(new string(content), trigger, RegexOptions.None, TimeSpan.FromMilliseconds(1)))
                    result.Add(ct);
                continue;
            }

            // if the trigger depends on user mentions to grant roles
            // those should be removed
            if (ct.RoleGrantType is CTRoleGrantType.Mentioned or CTRoleGrantType.Both)
            {
                content = content.RemoveUserMentions().Trim();
            }

            if (content.Length > trigger.Length)
            {
                // if input is greater than the trigger, it can only work if:
                // it has CA enabled
                if (ct.ContainsAnywhere)
                {
                    // if ca is enabled, we have to check if it is a word within the content
                    var wp = Extensions.Extensions.GetWordPosition(content, trigger);

                    // if it is, then that's valid
                    if (wp != WordPosition.None) result.Add(ct);

                    // if it's not, then it cant' work under any circumstance,
                    // because content is greater than the trigger length
                    // so it can't be equal, and it's not contained as a word
                    continue;
                }

                // if CA is disabled, and CR has AllowTarget, then the
                // content has to start with the trigger followed by a space
                if (ct.AllowTarget && content.StartsWith(trigger, StringComparison.OrdinalIgnoreCase)
                                   && content[trigger.Length] == ' ')
                {
                    result.Add(ct);
                }
            }
            else if (content.Length < ct.Trigger.Length)
            {
                // if input length is less than trigger length, it means
                // that the reaction can never be triggered
            }
            else
            {
                // if input length is the same as trigger length
                // reaction can only trigger if the strings are equal
                if (content.SequenceEqual(ct.Trigger)) result.Add(ct);
            }
        }

        if (result.Count == 0)
            return null;

        return result[_rng.Next(0, result.Count)];
    }

    public async Task ResetCrReactions(ulong? maybeGuildId, int id)
    {
        CTModel ct;
        await using var uow = _db.GetDbContext();
        ct = uow.ChatTriggers.GetById(id);
        if (ct is null)
            return;

        ct.Reactions = string.Empty;

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task UpdateInternalAsync(ulong? maybeGuildId, CTModel ct)
    {
        if (maybeGuildId is { } guildId)
            UpdateInternal(guildId, ct);
        else
        {
            _ = _pubSub.Pub(_gcrEditedKey, ct);
            return;
        }

        // handle interaction updates
        if (ct.ApplicationCommandType == CTApplicationCommandType.None) return;
        
        var guild = _client.GetGuild(guildId);
        await RegisterTriggersToGuildAsync(guild).ConfigureAwait(false);
    }

    private void UpdateInternal(ulong? maybeGuildId, CTModel ct)
    {
        if (maybeGuildId is { } guildId)
        {
            newGuildReactions.AddOrUpdate(guildId, new[] { ct },
                        (_, old) =>
                        {
                            var newArray = old.ToArray();
                            for (var i = 0; i < newArray.Length; i++)
                            {
                                if (newArray[i].Id == ct.Id)
                                    newArray[i] = ct;
                            }

                            return newArray;
                        });
        }
        else
        {
            lock (_gcrWriteLock)
            {
                var crs = globalReactions;
                for (var i = 0; i < crs.Length; i++)
                {
                    if (crs[i].Id == ct.Id)
                        crs[i] = ct;
                }
            }
        }
    }

    private Task AddInternalAsync(ulong? maybeGuildId, CTModel ct)
    {
        // only do this for perf purposes
        ct.Trigger = ct.Trigger.Replace(MENTION_PH, _client.CurrentUser.Mention);

        if (maybeGuildId is { } guildId)
        {
            newGuildReactions.AddOrUpdate(guildId,
                        new[] { ct },
                        (_, old) => old.With(ct));
        }
        else
        {
            return _pubSub.Pub(_gcrAddedKey, ct);
        }

        return Task.CompletedTask;
    }

    private Task DeleteInternalAsync(ulong? maybeGuildId, int id)
    {
        if (maybeGuildId is { } guildId)
        {
            newGuildReactions.AddOrUpdate(guildId,
                Array.Empty<CTModel>(),
                (_, old) => DeleteInternal(old, id));

            return Task.CompletedTask;
        }

        lock (_gcrWriteLock)
        {
            var cr = Array.Find(globalReactions, item => item.Id == id);
            if (cr is not null) return _pubSub.Pub(_gcrDeletedkey, cr.Id);
        }

        return Task.CompletedTask;
    }

    private static CTModel[] DeleteInternal(IReadOnlyList<CTModel>? cts, int id)
    {
        if (cts is null || cts.Count == 0)
            return cts as CTModel[] ?? cts?.ToArray();

        var newCrs = new CTModel[cts.Count - 1];
        for (int i = 0, k = 0; i < cts.Count; i++, k++)
        {
            if (cts[i].Id == id)
            {
                k--;
                continue;
            }

            newCrs[k] = cts[i];
        }

        return newCrs;
    }

    public async Task SetCrReactions(ulong? guildId, int id, IEnumerable<string> emojis)
    {
        CTModel ct;
        await using (var uow = _db.GetDbContext())
        {
            ct = uow.ChatTriggers.GetById(id);
            if (ct is null)
                return;

            ct.Reactions = string.Join("@@@", emojis);

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        await UpdateInternalAsync(guildId, ct);
    }

    public async Task<(bool Sucess, bool NewValue)> ToggleCrOptionAsync(CTModel ct, CtField field)
    {
        var newVal = false;
        await using (var uow = _db.GetDbContext())
        {
            if (ct is null)
                return (false, false);
            newVal = field switch
            {
                CtField.AutoDelete => ct.AutoDeleteTrigger = !ct.AutoDeleteTrigger,
                CtField.ContainsAnywhere => ct.ContainsAnywhere = !ct.ContainsAnywhere,
                CtField.DmResponse => ct.DmResponse = !ct.DmResponse,
                CtField.AllowTarget => ct.AllowTarget = !ct.AllowTarget,
                CtField.ReactToTrigger => ct.ReactToTrigger = !ct.ReactToTrigger,
                CtField.NoRespond => ct.NoRespond = !ct.NoRespond,
                _ => newVal
            };

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        await UpdateInternalAsync(ct.GuildId, ct);

        return (true, newVal);
    }

    public CTModel? GetChatTriggers(ulong? guildId, int id)
    {
        using var uow = _db.GetDbContext();
        var ct = uow.ChatTriggers.GetById(id);
        if (ct == null || ct.GuildId != guildId)
            return null;

        return ct;
    }

    public CTModel? GetGuildOrGlobalTriggers(ulong? guildId, int id)
    {
        using var uow = _db.GetDbContext();
        var ct = uow.ChatTriggers.GetById(id);
        if (ct == null || (ct.GuildId != guildId && ct.GuildId is not 0 or null))
            return null;

        return ct;
    }

    public int DeleteAllChatTriggers(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var count = uow.ChatTriggers.ClearFromGuild(guildId);
        uow.SaveChanges();

        newGuildReactions.TryRemove(guildId, out _);

        return count;
    }

    public bool ReactionExists(ulong? guildId, string input)
    {
        using var uow = _db.GetDbContext();
        var ct = uow.ChatTriggers.GetByGuildIdAndInput(guildId, input);
        return ct != null;
    }

    public Task OnReadyAsync() => ReloadInternal(_bot.GetCurrentGuildIds());

    private ValueTask OnCrsShouldReload(bool _) => new(ReloadInternal(_bot.GetCurrentGuildIds()));

    private ValueTask OnGcrAdded(CTModel c)
    {
        lock (_gcrWriteLock)
        {
            var newGlobalReactions = new CTModel[globalReactions.Length + 1];
            Array.Copy(globalReactions, newGlobalReactions, globalReactions.Length);
            newGlobalReactions[globalReactions.Length] = c;
            globalReactions = newGlobalReactions;
        }

        return default;
    }

    private ValueTask OnGcrEdited(CTModel c)
    {
        lock (_gcrWriteLock)
        {
            for (var i = 0; i < globalReactions.Length; i++)
            {
                if (globalReactions[i].Id == c.Id)
                {
                    globalReactions[i] = c;
                    return default;
                }
            }

            // if edited ct is not found?!
            // add it
            OnGcrAdded(c);
        }

        return default;
    }

    private ValueTask OnGcrDeleted(int id)
    {
        lock (_gcrWriteLock)
        {
            globalReactions = DeleteInternal(globalReactions, id);
        }

        return default;
    }

    public Task TriggerReloadChatTriggers() => _pubSub.Pub(_crsReloadedKey, true);

    private Task OnLeftGuild(SocketGuild arg)
    {
        newGuildReactions.TryRemove(arg.Id, out _);

        return Task.CompletedTask;
    }

    private async Task OnJoinedGuild(GuildConfig gc)
    {
        await using var uow = _db.GetDbContext();
        newGuildReactions[gc.GuildId] = await
            uow
            .ChatTriggers
            .AsNoTracking()
            .Where(x => x.GuildId == gc.GuildId)
            .ToArrayAsync();
    }
    
    public async Task<CTModel?> AddAsync(ulong? guildId, string key, string message, bool regex)
    {
        key = key.ToLowerInvariant();
        var cr = new CTModel
        {
            GuildId = guildId,
            Trigger = key,
            Response = message,
            IsRegex = regex
        };

        if (cr.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
            cr.AllowTarget = true;

        await using (var uow = _db.GetDbContext())
        {
            uow.ChatTriggers.Add(cr);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        await AddInternalAsync(guildId, cr).ConfigureAwait(false);

        return cr;
    }

    public async Task<CTModel?> EditAsync(ulong? guildId, int id, string message, bool? regex, string? trigger = null)
    {
        await using var uow = _db.GetDbContext();
        var ct = uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.IsRegex = regex ?? ct.IsRegex;

        // disable allowtarget if message had target, but it was removed from it
        if (!message.Contains("%target%", StringComparison.OrdinalIgnoreCase)
            && ct.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
        {
            ct.AllowTarget = false;
        }

        ct.Response = message;
        ct.Trigger = trigger ?? ct.Trigger;

        // enable allow target if message is edited to contain target
        if (ct.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
            ct.AllowTarget = true;

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct);

        return ct;
    }

    
    public async Task<CTModel?> DeleteAsync(ulong? guildId, int id)
    {
        await using var uow = _db.GetDbContext();
        var toDelete = uow.ChatTriggers.GetById(id);

        if (toDelete is null)
            return null;

        if ((toDelete.IsGlobal() && guildId == null) || guildId == toDelete.GuildId)
        {
            uow.ChatTriggers.Remove(toDelete);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await DeleteInternalAsync(guildId, id);
            return toDelete;
        }

        return null;
    }

    public async Task<CTModel?> SetRoleGrantType(ulong? guildId, int id, CTRoleGrantType type)
    {
        await using var uow = _db.GetDbContext();
        var ct = uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.RoleGrantType = type;

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct);

        return ct;
    }

    public async Task<CTModel?> SetInteractionType(ulong? guildId, int id, CTApplicationCommandType type)
    {
        await using var uow = _db.GetDbContext();
        var ct = uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        ct.ApplicationCommandType = type;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }
    
    public async Task<CTModel?> SetInteractionName(ulong? guildID, int id, string name)
    {
        await using var uow = _db.GetDbContext();
        var ct = uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildID)
            return null;

        ct.ApplicationCommandName = name;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildID, ct).ConfigureAwait(false);

        return ct;
    }
    
    public async Task<CTModel?> SetInteractionDescription(ulong? guildID, int id, string description)
    {
        await using var uow = _db.GetDbContext();
        var ct = uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildID)
            return null;

        ct.ApplicationCommandDescription = description;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildID, ct).ConfigureAwait(false);

        return ct;
    }
    
    public async Task<CTModel?> SetInteractionEphemeral(ulong? guildID, int id, bool ephemeral)
    {
        await using var uow = _db.GetDbContext();
        var ct = uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildID)
            return null;

        ct.EphemeralResponse = ephemeral;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildID, ct).ConfigureAwait(false);

        return ct;
    }
    
    public async Task<CTModel?> SetValidTriggerType(ulong? guildID, int id, ChatTriggerType type, bool enabled)
    {
        await using var uow = _db.GetDbContext();
        var ct = uow.ChatTriggers.GetById(id);

        if (ct == null || ct.GuildId != guildID)
            return null;

        switch (enabled)
        {
            case true when !ct.ValidTriggerTypes.HasFlag(type):
                ct.ValidTriggerTypes |= type;
                break;
            case false when ct.ValidTriggerTypes.HasFlag(type):
                ct.ValidTriggerTypes ^= type;
                break;
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(guildID, ct).ConfigureAwait(false);

        return ct;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CTModel[] GetChatTriggersFor(ulong? maybeGuildId)
    {
        if (maybeGuildId is { } guildId)
        {
            return newGuildReactions.TryGetValue(guildId, out var cts)
                ? cts
                : Array.Empty<CTModel>();
        }

        lock (_gcrWriteLock)
        {
            return globalReactions;
        }
    }

    public async Task ToggleGrantedRole(CTModel ct, ulong rId)
    {
        await using var uow = _db.GetDbContext();
        var roles = ct.GetGrantedRoles();
        if (!roles.Contains(rId))
            roles.Add(rId);
        else
            roles.RemoveAll(x => x == rId);

        ct.GrantedRoles = string.Join("@@@", roles.Select(x => x.ToString()));
        uow.ChatTriggers.Update(ct);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(ct.GuildId, ct).ConfigureAwait(false);
    }

    public async Task ToggleRemovedRole(CTModel ct, ulong rId)
    {
        await using var uow = _db.GetDbContext();
        var roles = ct.GetRemovedRoles();
        if (!roles.Contains(rId))
            roles.Add(rId);
        else
            roles.RemoveAll(x => x == rId);

        ct.RemovedRoles = string.Join("@@@", roles.Select(x => x.ToString()));
        uow.ChatTriggers.Update(ct);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await UpdateInternalAsync(ct.GuildId, ct).ConfigureAwait(false);
    }

    public EmbedBuilder GetEmbed(CTModel ct, ulong? gId = null, string? title = null)
    {
        var eb = new EmbedBuilder().WithOkColor()
            .WithTitle(title)
            .WithDescription($"#{ct.Id}")
            .AddField(_strings.GetText("ct_interaction_type_title", gId), _strings.GetText("ct_interaction_type_body", gId, ct.ApplicationCommandType.ToString()))
            .AddField(_strings.GetText("ct_realname", gId), ct.RealName)
            .AddField(efb => efb.WithName(_strings.GetText("trigger", gId)).WithValue(ct.Trigger.TrimTo(1024)))
            .AddField(efb =>
                efb.WithName(_strings.GetText("response", gId))
                    .WithValue($"{(ct.Response + "\n```css\n" + ct.Response).TrimTo(1024)}```"));
        var reactions = ct.GetReactions();
        if (reactions.Length >= 1)
            eb.AddField(_strings.GetText("trigger_reactions", gId), string.Concat(reactions));
        var addedRoles = ct.GetGrantedRoles();
        if (addedRoles.Count >= 1)
            eb.AddField(_strings.GetText("added_roles", gId), addedRoles.Select(x => $"<@&{x}>").Aggregate((x, y) => $"{x}, {y}"));
        var removedRoles = ct.GetRemovedRoles();
        if (removedRoles.Count >= 1)
            eb.AddField(_strings.GetText("removed_roles", gId), removedRoles.Select(x => $"<@&{x}>").Aggregate((x, y) => $"{x}, {y}"));
        if (addedRoles.Count >= 1 || removedRoles.Count >= 1)
            eb.AddField(_strings.GetText("role_grant_type", gId), ct.RoleGrantType);
        if (!ct.ApplicationCommandDescription.IsNullOrWhiteSpace())
            eb.AddField(_strings.GetText("ct_interaction_description", gId), ct.ApplicationCommandDescription);
        if (ct.ApplicationCommandId != 0)
            eb.AddField(_strings.GetText("ct_interaction_id", gId), ct.ApplicationCommandId.ToString());
        if (ct.ValidTriggerTypes != ChatTriggerType.All)
            eb.AddField(_strings.GetText("ct_valid_fields", gId), ct.ValidTriggerTypes.ToString());
        return eb;
    }

    private record TriggerChildGrouping(string Name, CTModel? Triggers, List<TriggerChildGrouping>? Children);

    public List<ApplicationCommandProperties> GetApplicationCommandProperties(ulong guildID)
    {

        var props = new List<ApplicationCommandProperties>();
        var groups = new List<TriggerChildGrouping>();

        var triggers = GetChatTriggersFor(guildID);

        if (GetACCTErrors(triggers)?.Any() ?? false)
        {
            throw new InvalidOperationException("ACCTs cannot be build when ACCT errors are detected.");
        }

        if (triggers.Length == 0)
            return props;
        groups = triggers.Where(x => x.ApplicationCommandType == CTApplicationCommandType.Slash
                                     && x.ValidTriggerTypes.HasFlag(ChatTriggerType.Interaction)
                                     && x.RealName.Split(' ').Length == 1)
                         .Select(x => new TriggerChildGrouping(x.RealName, x, null)).ToList();
        triggers.Where(x =>
            x.ApplicationCommandType == CTApplicationCommandType.Slash && x.RealName.Split(' ').Length == 2).ForEach(
            x =>
            {
                if (groups.Any(y => y.Name == x.RealName.Split(' ').First()))
                    groups.First(y => y.Name == x.RealName.Split(' ').First()).Children
                          .Add(new(x.RealName.Split(' ').Last(), x, null));
                else
                    groups.Add(new(x.RealName.Split(' ').First(), null,
                        new List<TriggerChildGrouping> {new(x.RealName.Split(' ').Last(), x, null)}));
            });

        triggers.Where(x =>
            x.ApplicationCommandType == CTApplicationCommandType.Slash
            && x.ValidTriggerTypes.HasFlag(ChatTriggerType.Interaction)
            && x.RealName.Split(' ').Length == 3).Select(x =>
        {
            TriggerChildGrouping group = null;
            if (groups.Any(y => y.Name == x.RealName.Split(' ').First()))
                group = groups.First(y => y.Name == x.RealName.Split(' ').First());
            else
            {
                groups.Add(new(x.RealName.Split(' ').First(), null, new List<TriggerChildGrouping>()));
                group = groups.First(y => y.Name == x.RealName.Split(' ').First());
            }

            return (Triggers: x, Group: group);
        }).Select(x =>
        {
            TriggerChildGrouping group = null;
            var groups = x.Group.Children;
            if (groups.Any(y => y.Name == x.Triggers.RealName.Split(' ')[1]))
                group = groups.First(y => y.Name == x.Triggers.RealName.Split(' ')[1]);
            else
            {
                groups.Add(new(x.Triggers.RealName.Split(' ')[1], null, new()));
                group = groups.First(y => y.Name == x.Triggers.RealName.Split(' ')[1]);
            }

            return (Triggers: x.Triggers, Group: group);
        }).ForEach(x => x.Group.Children.Add(new(x.Triggers.RealName, x.Triggers, null)));

        props = groups.Select(x => new SlashCommandBuilder()
            .WithName(x.Name)
            .WithDescription((x.Triggers?.ApplicationCommandDescription.IsNullOrWhiteSpace() ?? true) ? "description" : x.Triggers!.ApplicationCommandDescription)
            .AddOptions(x.Triggers is not null ? Array.Empty<SlashCommandOptionBuilder>() : (x.Children.Select(y => new SlashCommandOptionBuilder()
                .WithName(y.Name)
                .WithDescription((y.Triggers?.ApplicationCommandDescription.IsNullOrWhiteSpace() ?? true) ? "description" : y.Triggers!.ApplicationCommandDescription)
                .WithType(y.Triggers is null ? ApplicationCommandOptionType.SubCommandGroup : ApplicationCommandOptionType.SubCommand)
                .AddOptions(y.Triggers is not null ? Array.Empty<SlashCommandOptionBuilder>() : y.Children.Select(z => new SlashCommandOptionBuilder()
                    .WithName(z.Name.Split(' ')[2])
                    .WithDescription((z.Triggers?.ApplicationCommandDescription.IsNullOrWhiteSpace() ?? true) ? "description" : z.Triggers!.ApplicationCommandDescription)
                    .WithType(ApplicationCommandOptionType.SubCommand)).ToArray()))).ToArray())).Select(x => x.Build() as ApplicationCommandProperties).ToList();

        triggers.Where(x => x.ApplicationCommandType == CTApplicationCommandType.Message).ForEach(x =>
            props.Add(new MessageCommandBuilder().WithName(x.RealName).WithDMPermission(false).Build()));

        triggers.Where(x => x.ApplicationCommandType == CTApplicationCommandType.User).ForEach(x =>
            props.Add(new UserCommandBuilder().WithName(x.RealName).WithDMPermission(false).Build()));
        return props;
    }

    public bool TryGetApplicationCommandProperties(ulong guildId, out List<ApplicationCommandProperties>? props)
    {
        try
        {
            props = GetApplicationCommandProperties(guildId);
            return true;
        }
        catch
        {
            props = null;
            return false;
        }
    }

    public async Task<bool> RegisterTriggersToGuildAsync(IGuild guild)
    {
        if (!TryGetApplicationCommandProperties(guild.Id, out var props) || props is null)
            return false;
        #if DEBUG
        var cmd = new List<IApplicationCommand>();
        foreach (var prop in props)
            cmd.Add(await guild.CreateApplicationCommandAsync(prop));
        #else
        var cmd = await guild.BulkOverwriteApplicationCommandsAsync(props.ToArray()).ConfigureAwait(false);
        #endif
        if (cmd is null)
            return false;
        await using var uow = _db.GetDbContext();
        var cts = uow.ChatTriggers.Where(x => x.GuildId == guild.Id).ToList();
        cmd.SelectMany(cmd => cmd.GetCtNames().Select(name => (cmd, name))).ToList().ForEach(x =>
            cts.First(y => y.RealName == x.name).ApplicationCommandId = x.cmd.Id);
        await uow.SaveChangesAsync();
        return true;
    }

    public static readonly Regex ValidCommandRegex = new(@"^(?:[\w-]{1,32} {0,1}){1,3}$", RegexOptions.Compiled);
    
    public static bool IsValidName(CTApplicationCommandType type, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length is > 32 or < 1)
            return false;
        
        return type is not CTApplicationCommandType.Slash || ValidCommandRegex.IsMatch(name);
    }

    public List<ChatTriggersInteractionError>? GetACCTErrors(ulong? guildId) =>
        GetACCTErrors(GetChatTriggersFor(guildId));

    public static List<ChatTriggersInteractionError>? GetACCTErrors(IEnumerable<CTModel> triggers)
    {
        triggers = triggers.Where(x => x.ApplicationCommandType != CTApplicationCommandType.None);
        var errors = new List<ChatTriggersInteractionError>();
        Dictionary<string, List<(string Name, int Id)>> totalChildren = new();
        foreach (var trigger in triggers )
        {
            var triggerDepth = trigger.RealName.Split(' ').Length;
            var parent = triggerDepth > 1 ? trigger.RealName.Split(' ')[..^1].Join(' ') : "";
            if (!parent.IsNullOrWhiteSpace())
            {
                var value = totalChildren.GetValueOrDefault(parent, new());
                totalChildren[parent] = value.Append((trigger.RealName, trigger.Id)).ToList();
            }

            if (!IsValidName(trigger.ApplicationCommandType, trigger.RealName))
                errors.Add(new("invalid_name", new[] {trigger.Id}, new[] {trigger.RealName}));

            foreach (var newTrigger in triggers.Where(x => x.Id != trigger.Id))
            {
                var newTriggerDepth = trigger.RealName.Split(' ').Length;
                if (trigger.RealName == newTrigger.RealName)
                    errors.Add(new("duplicate", new[] {trigger.Id, newTrigger.Id},
                        new[] {trigger.RealName, newTrigger.RealName}));
                switch (triggerDepth)
                {
                    case 1 when newTriggerDepth == 2 && newTrigger.RealName.Split(' ')[0] == trigger.RealName:
                        errors.Add(new("subcommand_match_parent", new[] {trigger.Id, newTrigger.Id},
                            new[] {trigger.RealName, newTrigger.RealName}));
                        break;
                    case 2 when newTriggerDepth == 3 && newTrigger.RealName.Split(' ')[..1].Join(' ') == trigger.RealName:
                        errors.Add(new("subcommand_match_parent", new[] {trigger.Id, newTrigger.Id},
                            new[] {trigger.RealName, newTrigger.RealName}));
                        break;
                }
            }
        }

        totalChildren.Where(x => x.Value.Count > 25).ForEach(x => errors.Add(new("too_many_children",
            x.Value.Select(y => y.Id).ToArray(), x.Value.Select(y => y.Name).ToArray())));
        return errors.Any() ? errors : null;
    }
}
