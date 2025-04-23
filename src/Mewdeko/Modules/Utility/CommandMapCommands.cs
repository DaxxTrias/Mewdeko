using DataModel;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;


namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands for managing command aliases.
    /// </summary>
    /// <param name="db">The database service.</param>
    /// <param name="serv">The interactive service.</param>
    [Group]
    public class CommandMapCommands(IDataConnectionFactory dbFactory, InteractiveService serv, GuildSettingsService service)
        : MewdekoSubmodule<CommandMapService>
    {
        /// <summary>
        ///     Clears all command aliases for the guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AliasesClear()
        {
            var count = Service.ClearAliases(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.AliasesCleared(ctx.Guild.Id, count)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a new alias for a command, or removes an existing alias if no mapping is provided.
        /// </summary>
        /// <param name="trigger">The trigger word for the alias.</param>
        /// <param name="mapping">The command to map to the alias. If null, the alias will be removed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task Alias(string trigger, [Remainder] string? mapping = null)
        {
            if (string.IsNullOrWhiteSpace(trigger))
                return;

            trigger = trigger.Trim().ToLowerInvariant();
            var guildId = ctx.Guild.Id;

            await using var dbContext = await dbFactory.CreateConnectionAsync();

            if (string.IsNullOrWhiteSpace(mapping))
            {
                var gottenMaps = await Service.GetCommandMap(guildId);
                if (gottenMaps != null && (gottenMaps.Count != 0 ||
                                           !gottenMaps.Remove(trigger, out _)))
                {
                    await ReplyErrorAsync(Strings.AliasRemoveFail(guildId, Format.Code(trigger))).ConfigureAwait(false);
                    return;
                }

                // Direct query with GuildId and trigger filter
                var tr = await dbContext.CommandAliases
                    .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Trigger == trigger);

                if (tr != null)
                    await dbContext.DeleteAsync(tr);

                await ReplyConfirmAsync(Strings.AliasRemoved(guildId, Format.Code(trigger))).ConfigureAwait(false);
                return;
            }

            // Add new command alias with GuildId
            await dbContext.InsertAsync(new CommandAlias
            {
                GuildId = guildId,
                Mapping = mapping,
                Trigger = trigger
            });


            await ReplyConfirmAsync(Strings.AliasAdded(guildId, Format.Code(trigger), Format.Code(mapping)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all command aliases currently set for the guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task AliasList()
        {
            var aliases = await Service.GetCommandMap(ctx.Guild.Id);
            if (aliases is null || aliases.Count == 0)
            {
                await ReplyErrorAsync(Strings.AliasesNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex((aliases.Count - 1) / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithOkColor()
                    .WithTitle(Strings.AliasList(ctx.Guild.Id))
                    .WithDescription(string.Join("\n",
                        aliases.Skip(page * 10).Take(10).Select(x => $"`{x.Key}` => `{x.Value}`")));
            }
        }
    }
}