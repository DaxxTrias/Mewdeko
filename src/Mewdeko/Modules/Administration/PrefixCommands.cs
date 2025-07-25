﻿using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Module for managing the bot's prefix.
    /// </summary>
    /// <param name="guildSettings"></param>
    [Group]
    public class PrefixCommands(GuildSettingsService guildSettings) : MewdekoSubmodule
    {
        /// <summary>
        ///     Enumerates different variations of the term "set". Used for idiots that try to do .prefix set !.
        /// </summary>
        public enum Set
        {
            /// <summary>
            ///     Represents the term "set".
            /// </summary>
            Set
        }


        /// <summary>
        ///     Displays the current command prefix for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is available to all users.
        /// </remarks>
        [Cmd]
        [Aliases]
        [Priority(1)]
        public async Task PrefixCommand()
        {
            await ReplyConfirmAsync(Strings.PrefixCurrent(ctx.Guild.Id,
                    Format.Code(await guildSettings.GetPrefix(ctx.Guild))))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the command prefix for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <param name="_">Used for idiots that try to do .prefix set !</param>
        /// <param name="prefix">The new command prefix to set.</param>
        /// <example>.prefix set !</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(0)]
        public Task PrefixCommand(Set _, [Remainder] string prefix)
        {
            return PrefixCommand(prefix);
        }

        /// <summary>
        ///     Sets the command prefix for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        /// <param name="prefix">The new command prefix to set.</param>
        /// <example>.prefix !</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task PrefixCommand([Remainder] string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return;

            var oldPrefix = await guildSettings.GetPrefix(ctx.Guild);
            var newPrefix = await guildSettings.SetPrefix(ctx.Guild, prefix);

            await ReplyConfirmAsync(Strings.PrefixNew(ctx.Guild.Id, Format.Code(oldPrefix), Format.Code(newPrefix))
            ).ConfigureAwait(false);
        }
    }
}