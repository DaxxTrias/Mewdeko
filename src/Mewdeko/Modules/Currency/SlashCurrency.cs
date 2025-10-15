using Discord.Interactions;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     Slash command module for currency game interactions.
/// </summary>
[Group("currency", "Currency and gambling games")]
public class SlashCurrency : MewdekoSlashCommandModule
{
    /// <summary>
    ///     The currency service for managing user balances.
    /// </summary>
    public ICurrencyService Service { get; set; }

    /// <summary>
    ///     The trivia chain service for managing trivia chain games.
    /// </summary>
    public ITriviaChainService TriviaChainService { get; set; }

    /// <summary>
    ///     Handle dice duel challenge acceptance.
    /// </summary>
    [ComponentInteraction("diceduel_accept_*_*", true)]
    public async Task DiceDuelAccept(ulong challengerId, long betAmount)
    {
        if (ctx.User.Id == challengerId)
        {
            await RespondAsync(Strings.DiceDuelCannotAcceptOwnChallenge(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var challengerBalance = await Service.GetUserBalanceAsync(challengerId, ctx.Guild.Id);
        var accepterBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);

        if (betAmount > challengerBalance || betAmount > accepterBalance)
        {
            await RespondAsync(
                Strings.DiceDuelInsufficientFundsAccept(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)),
                ephemeral: true);
            return;
        }

        // Roll dice for both players
        var rand = new Random();
        var challengerRoll = rand.Next(1, 7);
        var accepterRoll = rand.Next(1, 7);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.DiceDuelResultTitle(ctx.Guild.Id))
            .WithColor(challengerRoll == accepterRoll ? Color.Gold : Color.Green);

        if (challengerRoll > accepterRoll)
        {
            // Challenger wins
            await Service.AddUserBalanceAsync(challengerId, betAmount, ctx.Guild.Id);
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(challengerId, betAmount, Strings.DiceDuelTransactionWon(ctx.Guild.Id),
                ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, Strings.DiceDuelTransactionLost(ctx.Guild.Id),
                ctx.Guild.Id);

            var challenger = await ctx.Guild.GetUserAsync(challengerId);
            eb.WithDescription(Strings.DiceDuelChallengerWin(ctx.Guild.Id, challenger?.Mention ?? "Challenger",
                challengerRoll, ctx.User.Mention, accepterRoll));
        }
        else if (accepterRoll > challengerRoll)
        {
            // Accepter wins
            await Service.AddUserBalanceAsync(ctx.User.Id, betAmount, ctx.Guild.Id);
            await Service.AddUserBalanceAsync(challengerId, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, betAmount, Strings.DiceDuelTransactionWon(ctx.Guild.Id),
                ctx.Guild.Id);
            await Service.AddTransactionAsync(challengerId, -betAmount, Strings.DiceDuelTransactionLost(ctx.Guild.Id),
                ctx.Guild.Id);

            var challenger = await ctx.Guild.GetUserAsync(challengerId);
            eb.WithDescription(Strings.DiceDuelAccepterWin(ctx.Guild.Id, ctx.User.Mention, accepterRoll,
                challenger?.Mention ?? "Challenger", challengerRoll));
        }
        else
        {
            // Tie - no money changes hands
            eb.WithDescription(Strings.DiceDuelTie(ctx.Guild.Id, challengerRoll.ToString()));
        }

        await RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Handle dice duel challenge decline.
    /// </summary>
    [ComponentInteraction("diceduel_decline_*", true)]
    public async Task DiceDuelDecline(ulong challengerId)
    {
        if (ctx.User.Id == challengerId)
        {
            await RespondAsync(Strings.DiceDuelCannotDeclineOwnChallenge(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var challenger = await ctx.Guild.GetUserAsync(challengerId);
        await RespondAsync(Strings.DiceDuelDeclined(ctx.Guild.Id));
    }

    /// <summary>
    ///     Handle trivia chain cash out.
    /// </summary>
    [ComponentInteraction("triviachain_cashout_*_*", true)]
    public async Task TriviaChainCashOut(ulong userId, long winnings)
    {
        if (ctx.User.Id != userId)
        {
            await RespondAsync(Strings.TriviaChainNotYourButton(ctx.Guild.Id), ephemeral: true);
            return;
        }

        if (winnings > 0)
        {
            await Service.AddUserBalanceAsync(userId, winnings, ctx.Guild.Id);
            await Service.AddTransactionAsync(userId, winnings, Strings.TriviaChainTransactionCashedOut(ctx.Guild.Id),
                ctx.Guild.Id);

            await RespondAsync(Strings.TriviaChainCashedOut(ctx.Guild.Id, winnings,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await RespondAsync(Strings.TriviaChainNothingToCashOut(ctx.Guild.Id), ephemeral: true);
        }
    }

    /// <summary>
    ///     Handle trivia chain answer selection.
    /// </summary>
    [ComponentInteraction("triviachain_answer_*_*", true)]
    public async Task TriviaChainAnswer(ulong userId, string answerIndex)
    {
        if (ctx.User.Id != userId)
        {
            await RespondAsync(Strings.TriviaChainNotYourButton(ctx.Guild.Id), ephemeral: true);
            return;
        }

        // Get the trivia chain state
        var chainState = TriviaChainService.GetTriviaChainState(userId);
        if (chainState == null)
        {
            await RespondAsync(Strings.TriviaChainExpired(ctx.Guild.Id), ephemeral: true);
            return;
        }

        // Process the answer using the service
        var result = await TriviaChainService.ProcessTriviaAnswerAsync(ctx, answerIndex, chainState, Service);

        if (result.GameCompleted || result.GameFailed)
        {
            await RespondAsync(embed: result.ResultEmbed, components: result.NextComponents);
        }
        else if (result.UpdatedState != null)
        {
            await RespondAsync(embed: result.ResultEmbed, components: result.NextComponents);
        }
    }
}