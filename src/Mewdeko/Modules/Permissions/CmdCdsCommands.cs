using Discord.Commands;
using Humanizer;
using LinqToDB;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Collections;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Permissions.Services;
using DataModel;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    /// <summary>
    ///     Represents commands for managing command cooldowns.
    /// </summary>
    /// <param name="service">The command cooldown service</param>
    /// <param name="dbFactory">The database service</param>
    [Group]
    public class CmdCdsCommands(
        CmdCdService service,
        IDataConnectionFactory dbFactory)
        : MewdekoSubmodule
    {
        private ConcurrentDictionary<ulong, ConcurrentHashSet<ActiveCooldown>> ActiveCooldowns
        {
            get
            {
                return service.ActiveCooldowns;
            }
        }

        /// <summary>
        ///     Sets or clears the cooldown for a specified command in the guild.
        /// </summary>
        /// <param name="command">The command to set the cooldown for.</param>
        /// <param name="time">The duration of the cooldown. Defaults to 0s, clearing the cooldown.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        ///     Command cooldowns can be set between 0 seconds (effectively clearing the cooldown) and 90,000 seconds.
        ///     Setting a cooldown affects all instances of the command within the guild.
        /// </remarks>
        /// <example>
        ///     .cmdcd "command name" 30s - Sets a 30-second cooldown for the specified command.
        ///     .cmdcd "command name" - Clears the cooldown for the specified command.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task CmdCooldown(CommandOrCrInfo command, StoopidTime? time = null)
        {
            time ??= StoopidTime.FromInput("0s");
            var channel = (ITextChannel)ctx.Channel;
            var guildId = channel.Guild.Id;

            if (time.Time.TotalSeconds is < 0 or > 90000)
            {
                await ReplyErrorAsync(Strings.InvalidSecondParamBetween(guildId, 0, 90000)).ConfigureAwait(false);
                return;
            }

            var name = command.Name.ToLowerInvariant();

            await using var db = await dbFactory.CreateConnectionAsync();

            // Get existing cooldown with direct query
            var existingCooldown = await db.CommandCooldowns
                .FirstOrDefaultAsync(cc => cc.GuildId == guildId && cc.CommandName == name);

            if (existingCooldown != null)
                await db.CommandCooldowns
                    .Where(cc => cc.Id == existingCooldown.Id)
                    .DeleteAsync();

            if (time.Time.TotalSeconds != 0)
            {
                // Add new cooldown
                await db.InsertAsync(new CommandCooldown
                {
                    GuildId = guildId,
                    CommandName = name,
                    Seconds = Convert.ToInt32(time.Time.TotalSeconds)
                });
            }

            if (time.Time.TotalSeconds == 0)
            {
                var activeCds = ActiveCooldowns.GetOrAdd(guildId, []);
                activeCds.RemoveWhere(ac => ac.Command == name);
                await ReplyConfirmAsync(Strings.CmdcdCleared(guildId,
                    Format.Bold(name))).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.CmdcdAdd(guildId,
                    Format.Bold(name),
                    Format.Bold(time.Time.Humanize()))).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Displays all commands with active cooldowns in the guild.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        ///     This method lists all commands that currently have a cooldown set, along with the duration of each cooldown.
        ///     If no commands have cooldowns set, a message indicating this will be sent.
        /// </remarks>
        /// <example>
        ///     .allcmdcds - Lists all commands with their respective cooldowns.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task AllCmdCooldowns()
        {
            var channel = (ITextChannel)ctx.Channel;
            var guildId = channel.Guild.Id;

            await using var db = await dbFactory.CreateConnectionAsync();

            // Get cooldowns for this guild
            var commandCooldowns = await db.CommandCooldowns
                .Where(cc => cc.GuildId == guildId)
                .ToListAsync();

            if (commandCooldowns.Count == 0)
            {
                await ReplyConfirmAsync(Strings.CmdcdNone(guildId)).ConfigureAwait(false);
            }
            else
            {
                await channel.SendTableAsync("",
                        commandCooldowns.Select(c => $"{c.CommandName}: {c.Seconds}{Strings.Sec(guildId)}"),
                        s => $"{s,-30}", 2)
                    .ConfigureAwait(false);
            }
        }
    }
}