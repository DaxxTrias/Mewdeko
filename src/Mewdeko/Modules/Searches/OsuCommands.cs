﻿using System.Net.Http;
using System.Text.Json;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Common;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    /// <summary>
    ///     Module for interacting with osu! APIs and retrieving user data.
    /// </summary>
    [Group]
    public class OsuCommands(IBotCredentials creds, IHttpClientFactory factory, ILogger<OsuCommands> logger)
        : MewdekoSubmodule
    {
        /// <summary>
        ///     Retrieves osu! user profile information.
        /// </summary>
        /// <remarks>
        ///     This command retrieves osu! user profile information from the osu! API and displays it in an embed.
        /// </remarks>
        /// <param name="user">The osu! username to retrieve information for.</param>
        /// <param name="mode">The game mode (standard, taiko, catch, mania) to retrieve information for (optional).</param>
        /// <example>
        ///     <code>.osu username</code>
        ///     <code>.osu username mode</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task Osu(string user, [Remainder] string? mode = null)
        {
            if (string.IsNullOrWhiteSpace(user))
                return;

            using var http = factory.CreateClient();
            var modeNumber = string.IsNullOrWhiteSpace(mode)
                ? 0
                : ResolveGameMode(mode);

            try
            {
                if (string.IsNullOrWhiteSpace(creds.OsuApiKey))
                {
                    await ReplyErrorAsync(Strings.OsuApiKey(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var smode = ResolveGameMode(modeNumber);
                var userReq = $"https://osu.ppy.sh/api/get_user?k={creds.OsuApiKey}&u={user}&m={modeNumber}";
                var userResString = await http.GetStringAsync(userReq)
                    .ConfigureAwait(false);
                var objs = JsonSerializer.Deserialize<List<OsuUserData>>(userResString);

                if (objs.Count == 0)
                {
                    await ReplyErrorAsync(Strings.OsuUserNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var obj = objs[0];
                var userId = obj.UserId;

                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.OsuProfileTitle(ctx.Guild.Id, smode, user))
                    .WithThumbnailUrl($"https://a.ppy.sh/{userId}")
                    .WithDescription($"https://osu.ppy.sh/u/{userId}")
                    .AddField(Strings.OsuOfficialRank(ctx.Guild.Id), $"#{obj.PpRank}", true)
                    .AddField(Strings.OsuCountryRank(ctx.Guild.Id),
                        $"#{obj.PpCountryRank} :flag_{obj.Country.ToLower()}:", true)
                    .AddField(Strings.OsuTotalPp(ctx.Guild.Id), Math.Round(obj.PpRaw, 2), true)
                    .AddField(Strings.OsuAccuracy(ctx.Guild.Id), $"{Math.Round(obj.Accuracy, 2)}%", true)
                    .AddField(Strings.OsuPlaycount(ctx.Guild.Id), obj.Playcount, true)
                    .AddField(Strings.OsuLevel(ctx.Guild.Id), Math.Round(obj.Level), true)
                ).ConfigureAwait(false);
            }
            catch (ArgumentOutOfRangeException)
            {
                await ReplyErrorAsync(Strings.OsuUserNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ReplyErrorAsync(Strings.OsuFailed(ctx.Guild.Id)).ConfigureAwait(false);
                logger.LogWarning(ex, "Osu command failed");
            }
        }

        /// <summary>
        ///     Retrieves osu!Gatari user profile information.
        /// </summary>
        /// <remarks>
        ///     This command retrieves osu!Gatari user profile information from the Gatari API and displays it in an embed.
        /// </remarks>
        /// <param name="user">The osu!Gatari username to retrieve information for.</param>
        /// <param name="mode">The game mode (standard, taiko, catch, mania) to retrieve information for (optional).</param>
        /// <example>
        ///     <code>.gatari username</code>
        ///     <code>.gatari username mode</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task Gatari(string user, [Remainder] string? mode = null)
        {
            using var http = factory.CreateClient();
            var modeNumber = string.IsNullOrWhiteSpace(mode)
                ? 0
                : ResolveGameMode(mode);

            var modeStr = ResolveGameMode(modeNumber);
            var resString = await http
                .GetStringAsync($"https://api.gatari.pw/user/stats?u={user}&mode={modeNumber}")
                .ConfigureAwait(false);

            var statsResponse = JsonSerializer.Deserialize<GatariUserStatsResponse>(resString);
            if (statsResponse.Code != 200 || statsResponse.Stats.Id == 0)
            {
                await ReplyErrorAsync(Strings.OsuUserNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var usrResString = await http.GetStringAsync($"https://api.gatari.pw/users/get?u={user}")
                .ConfigureAwait(false);

            var userData = JsonSerializer.Deserialize<GatariUserResponse>(usrResString).Users[0];
            var userStats = statsResponse.Stats;

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.OsuProfileTitle(ctx.Guild.Id, $"Gatari {modeStr}", user))
                .WithThumbnailUrl($"https://a.gatari.pw/{userStats.Id}")
                .WithDescription($"https://osu.gatari.pw/u/{userStats.Id}")
                .AddField(Strings.OsuOfficialRank(ctx.Guild.Id), $"#{userStats.Rank}", true)
                .AddField(Strings.OsuCountryRank(ctx.Guild.Id),
                    $"#{userStats.CountryRank} :flag_{userData.Country.ToLower()}:", true)
                .AddField(Strings.OsuTotalPp(ctx.Guild.Id), userStats.Pp, true)
                .AddField(Strings.OsuAccuracy(ctx.Guild.Id), $"{Math.Round(userStats.AvgAccuracy, 2)}%", true)
                .AddField(Strings.OsuPlaycount(ctx.Guild.Id), userStats.Playcount, true)
                .AddField(Strings.OsuLevel(ctx.Guild.Id), userStats.Level, true);

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves the top 5 osu! plays for a user.
        /// </summary>
        /// <remarks>
        ///     This command retrieves the top 5 osu! plays for a user from the osu! API and displays them in an embed.
        /// </remarks>
        /// <param name="user">The osu! username to retrieve plays for.</param>
        /// <param name="mode">The game mode (standard, taiko, catch, mania) to retrieve plays for (optional).</param>
        /// <example>
        ///     <code>.osu5 username</code>
        ///     <code>.osu5 username mode</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task Osu5(string user, [Remainder] string? mode = null)
        {
            var channel = (ITextChannel)ctx.Channel;
            if (string.IsNullOrWhiteSpace(creds.OsuApiKey))
            {
                await channel.SendErrorAsync(Strings.OsuApiKeyRequired(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(user))
            {
                await channel.SendErrorAsync(Strings.OsuUsernameRequired(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            using var http = factory.CreateClient();
            var m = 0;
            if (!string.IsNullOrWhiteSpace(mode)) m = ResolveGameMode(mode);

            var reqString =
                $"https://osu.ppy.sh/api/get_user_best?k={creds.OsuApiKey}&u={Uri.EscapeDataString(user)}&type=string&limit=5&m={m}";

            var resString = await http.GetStringAsync(reqString).ConfigureAwait(false);
            var obj = JsonSerializer.Deserialize<List<OsuUserBests>>(resString);

            var mapTasks = obj.Select(async item =>
            {
                var mapReqString = $"https://osu.ppy.sh/api/get_beatmaps?k={creds.OsuApiKey}&b={item.BeatmapId}";

                var mapResString = await http.GetStringAsync(mapReqString).ConfigureAwait(false);
                var map = JsonSerializer.Deserialize<List<OsuMapData>>(mapResString).FirstOrDefault();
                if (map is null)
                    return default;
                var pp = Math.Round(item.Pp, 2);
                var acc = CalculateAcc(item, m);
                var mods = ResolveMods(item.EnabledMods);

                var title = $"{map.Artist}-{map.Title} ({map.Version})";
                var desc = $"""
                            [/b/{item.BeatmapId}](https://osu.ppy.sh/b/{item.BeatmapId})
                            {$"{pp}pp",-7} | {$"{acc}%",-7}

                            """;
                if (mods != "+") desc += Format.Bold(mods);

                return (title, desc);
            });

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.OsuTopPlays(ctx.Guild.Id, user));

            var mapData = await Task.WhenAll(mapTasks).ConfigureAwait(false);
            foreach (var (title, desc) in mapData.Where(x => x != default)) eb.AddField(title, desc);

            await channel.EmbedAsync(eb).ConfigureAwait(false);
        }

        //https://osu.ppy.sh/wiki/Accuracy
        private static double CalculateAcc(OsuUserBests play, int mode)
        {
            double hitPoints;
            double totalHits;
            switch (mode)
            {
                case 0:
                    hitPoints = play.Count50 * 50 +
                                play.Count100 * 100 +
                                play.Count300 * 300;
                    totalHits = play.Count50 + play.Count100 +
                                play.Count300 + play.Countmiss;
                    totalHits *= 300;
                    break;
                case 1:
                    hitPoints = play.Countmiss * 0 + play.Count100 * 0.5 + play.Count300;
                    totalHits = (play.Countmiss + play.Count100 + play.Count300) * 300;
                    hitPoints *= 300;
                    break;
                case 2:
                    hitPoints = play.Count50 + play.Count100 + play.Count300;
                    totalHits = play.Countmiss + play.Count50 + play.Count100 + play.Count300 +
                                play.Countkatu;
                    break;
                default:
                    hitPoints = play.Count50 * 50 +
                                play.Count100 * 100 +
                                play.Countkatu * 200 +
                                (play.Count300 + play.Countgeki) * 300;

                    totalHits = (play.Countmiss + play.Count50 + play.Count100 +
                                 play.Countkatu + play.Count300 + play.Countgeki) * 300;
                    break;
            }

            return Math.Round(hitPoints / totalHits * 100, 2);
        }

        private static int ResolveGameMode(string mode)
        {
            return mode.ToUpperInvariant() switch
            {
                "STD" => 0,
                "STANDARD" => 0,
                "TAIKO" => 1,
                "CTB" => 2,
                "CATCHTHEBEAT" => 2,
                "MANIA" => 3,
                "OSU!MANIA" => 3,
                _ => 0
            };
        }

        private static string ResolveGameMode(int mode)
        {
            return mode switch
            {
                0 => "Standard",
                1 => "Taiko",
                2 => "Catch",
                3 => "Mania",
                _ => "Standard"
            };
        }

        //https://github.com/ppy/osu-api/wiki#mods
        private static string ResolveMods(int mods)
        {
            var modString = "+";

            if (IsBitSet(mods, 0))
                modString += "NF";
            if (IsBitSet(mods, 1))
                modString += "EZ";
            if (IsBitSet(mods, 8))
                modString += "HT";

            if (IsBitSet(mods, 3))
                modString += "HD";
            if (IsBitSet(mods, 4))
                modString += "HR";
            if (IsBitSet(mods, 6) && !IsBitSet(mods, 9))
                modString += "DT";
            if (IsBitSet(mods, 9))
                modString += "NC";
            if (IsBitSet(mods, 10))
                modString += "FL";

            if (IsBitSet(mods, 5))
                modString += "SD";
            if (IsBitSet(mods, 14))
                modString += "PF";

            if (IsBitSet(mods, 7))
                modString += "RX";
            if (IsBitSet(mods, 11))
                modString += "AT";
            if (IsBitSet(mods, 12))
                modString += "SO";
            return modString;
        }

        private static bool IsBitSet(int mods, int pos)
        {
            return (mods & (1 << pos)) != 0;
        }
    }
}