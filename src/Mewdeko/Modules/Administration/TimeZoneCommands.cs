﻿using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Handles commands related to time zones.
    /// </summary>
    /// <param name="serv"></param>
    [Group]
    public class TimeZoneCommands(InteractiveService serv) : MewdekoSubmodule<GuildTimezoneService>
    {
        /// <summary>
        ///     List all available time zones.
        /// </summary>
        /// <example>.timezones</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Timezones()
        {
            var timezones = TimeZoneInfo.GetSystemTimeZones()
                .OrderBy(x => x.BaseUtcOffset)
                .ToArray();
            const int timezonesPerPage = 20;

            var curTime = DateTimeOffset.UtcNow;

            var i = 0;
            var timezoneStrings = timezones
                .Select(x => (x, ++i % 2 == 0))
                .Select(data =>
                {
                    var (tzInfo, flip) = data;
                    var nameStr = $"{tzInfo.Id,-30}";
                    var offset = curTime.ToOffset(tzInfo.GetUtcOffset(curTime)).ToString("zzz");
                    if (flip)
                        return $"{offset} {Format.Code(nameStr)}";
                    return $"{Format.Code(offset)} {nameStr}";
                });

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(timezones.Length / 20)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithColor(Mewdeko.OkColor)
                    .WithTitle(Strings.TimezonesAvailable(ctx.Guild.Id))
                    .WithDescription(string.Join("\n",
                        timezoneStrings.Skip(page * timezonesPerPage)
                            .Take(timezonesPerPage)));
            }
        }

        /// <summary>
        ///     Shows the time zone of the guild if set.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Timezone()
        {
            await ReplyConfirmAsync(Strings.TimezoneGuild(ctx.Guild.Id, Service.GetTimeZoneOrUtc(ctx.Guild.Id)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Gets info about a time zone.
        /// </summary>
        /// <param name="id">The timezone ID.</param>
        /// <example>.timezone UTC</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task Timezone([Remainder] string id)
        {
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch
            {
                tz = null;
            }

            if (tz == null)
            {
                await ReplyErrorAsync(Strings.TimezoneNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetTimeZone(ctx.Guild.Id, tz);

            await ctx.Channel.SendConfirmAsync(tz.ToString()).ConfigureAwait(false);
        }
    }
}