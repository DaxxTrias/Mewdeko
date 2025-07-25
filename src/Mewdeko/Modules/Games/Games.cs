﻿using Discord.Commands;
using LinqToDB;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Configs;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games : MewdekoModuleBase<GamesService>
{
    private readonly BotConfig config;
    private readonly IDataConnectionFactory dbFactory;
    private readonly MewdekoRandom rng = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="Games" /> class.
    /// </summary>
    /// <param name="data">The data cache service.</param>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="config">Bot config service.</param>
    public Games(IDataCache data, IDataConnectionFactory dbFactory, BotConfig config)
    {
        this.config = config;
        (_, this.dbFactory) = (data.LocalImages, dbFactory);
    }

    /// <summary>
    ///     Command to choose randomly from a list of options.
    /// </summary>
    /// <param name="list">The list of options separated by semicolons.</param>
    /// <example>.choose option1; option2; option3</example>
    [Cmd]
    [Aliases]
    public async Task Choose([Remainder] string? list = null)
    {
        if (string.IsNullOrWhiteSpace(list))
            return;
        var listArr = list.Split(';');
        if (listArr.Length < 2)
            return;
        await ctx.Channel.SendConfirmAsync(Strings.ChoiceMade(ctx.Guild.Id, listArr[rng.Next(0, listArr.Length)]))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Command to consult the magic 8-ball for an answer.
    /// </summary>
    /// <param name="question">The question to ask.</param>
    /// <example>.8ball Will I win the lottery?</example>
    [Cmd]
    [Aliases]
    public async Task EightBall([Remainder] string? question = null)
    {
        if (string.IsNullOrWhiteSpace(question))
            return;

        var res = Service.GetEightballResponse(question);
        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithColor(Mewdeko.OkColor)
            .WithDescription(ctx.User.ToString())
            .AddField(efb =>
                efb.WithName($"❓ {Strings.Question(ctx.Guild.Id)}").WithValue(question).WithIsInline(false))
            .AddField($"🎱 {Strings.Eightball(ctx.Guild.Id)}", res));
    }

    /// <summary>
    ///     Command that used to exist. Called trans people a slur in NadekoBot. Here as a memory and a fuck you to NadekoBot.
    /// </summary>
    /// <example>Terrible command. Dont use it.</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task RateGirl()
    {
        await ctx.Channel.SendErrorAsync(
            Strings.InappropriateContentBlocked(ctx.Guild.Id),
            config);
    }

    /// <summary>
    ///     Funni interjecting linux command
    /// </summary>
    /// <param name="guhnoo">The name to replace "GNU".</param>
    /// <param name="loonix">The name to replace "Linux".</param>
    /// <example>.linux guhnoo loonix</example>
    [Cmd]
    [Aliases]
    public async Task Linux(string guhnoo, string loonix)
    {
        await ctx.Channel.SendConfirmAsync(
            Strings.LinuxCopypasta(ctx.Guild.Id, guhnoo, loonix)
        ).ConfigureAwait(false);
    }

    /// <summary>
    ///     Command to toggle the user's dragon status. Usually used for beta commands.
    /// </summary>
    /// <example>.dragon</example>
    [Cmd]
    [Aliases]
    [HelpDisabled]
    public async Task Dragon()
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var user = await dbContext.GetOrCreateUser(ctx.User);
        user.IsDragon = !user.IsDragon;
        await dbContext.UpdateAsync(user);
        await ReplyConfirmAsync(user.IsDragon ? Strings.DragonSet(ctx.Guild.Id) : Strings.DragonUnset(ctx.Guild.Id))
            .ConfigureAwait(false);
    }
}