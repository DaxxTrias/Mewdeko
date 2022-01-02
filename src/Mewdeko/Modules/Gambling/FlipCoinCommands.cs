using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Mewdeko.Modules.Gambling;

public partial class Gambling
{
    [Group]
    public class FlipCoinCommands : GamblingSubmodule<GamblingService>
    {
        public enum BetFlipGuess
        {
            H = 1,
            Head = 1,
            Heads = 1,
            T = 2,
            Tail = 2,
            Tails = 2
        }

        private static readonly MewdekoRandom rng = new();
        private readonly ICurrencyService _cs;
        private readonly DbService _db;
        private readonly IImageCache _images;

        public FlipCoinCommands(IDataCache data, ICurrencyService cs, DbService db,
            GamblingConfigService gss) : base(gss)
        {
            _images = data.LocalImages;
            _cs = cs;
            _db = db;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Flip(int count = 1)
        {
            if (count > 10 || count < 1)
            {
                await ReplyErrorLocalizedAsync("flip_invalid", 10).ConfigureAwait(false);
                return;
            }

            var headCount = 0;
            var tailCount = 0;
            var imgs = new Image<Rgba32>[count];
            for (var i = 0; i < count; i++)
            {
                var headsArr = _images.Heads[rng.Next(0, _images.Heads.Count)];
                var tailsArr = _images.Tails[rng.Next(0, _images.Tails.Count)];
                if (rng.Next(0, 10) < 5)
                {
                    imgs[i] = Image.Load(headsArr);
                    headCount++;
                }
                else
                {
                    imgs[i] = Image.Load(tailsArr);
                    tailCount++;
                }
            }

            using var img = imgs.Merge(out var format);
            await using var stream = img.ToStream(format);
            foreach (var i in imgs) i.Dispose();
            var msg = count != 1
                ? Format.Bold(ctx.User.ToString()) + " " + GetText("flip_results", count, headCount, tailCount)
                : Format.Bold(ctx.User.ToString()) + " " + GetText("flipped", headCount > 0
                    ? Format.Bold(GetText("heads"))
                    : Format.Bold(GetText("tails")));
            await ctx.Channel.SendFileAsync(stream, $"{count} coins.{format.FileExtensions.First()}", msg)
                .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Betflip(ShmartNumber amount, BetFlipGuess guess)
        {
            if (!await CheckBetMandatory(amount).ConfigureAwait(false) || amount == 1)
                return;

            var removed = await _cs.RemoveAsync(ctx.User, "Betflip Gamble", amount, false, true)
                .ConfigureAwait(false);
            if (!removed)
            {
                await ReplyErrorLocalizedAsync("not_enough", CurrencySign).ConfigureAwait(false);
                return;
            }

            BetFlipGuess result;
            Uri imageToSend;
            var coins = _images.ImageUrls.Coins;
            if (rng.Next(0, 1000) <= 499)
            {
                imageToSend = coins.Heads[rng.Next(0, coins.Heads.Length)];
                result = BetFlipGuess.Heads;
            }
            else
            {
                imageToSend = coins.Tails[rng.Next(0, coins.Tails.Length)];
                result = BetFlipGuess.Tails;
            }

            string str;
            if (guess == result)
            {
                var toWin = (long) (amount * _config.BetFlip.Multiplier);
                str = Format.Bold(ctx.User.ToString()) + " " + GetText("flip_guess", toWin + CurrencySign);
                await _cs.AddAsync(ctx.User, "Betflip Gamble", toWin, false, true).ConfigureAwait(false);
            }
            else
            {
                str = ctx.User + " " + GetText("better_luck");
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithDescription(str)
                .WithOkColor()
                .WithImageUrl(imageToSend.ToString())).ConfigureAwait(false);
        }
    }
}