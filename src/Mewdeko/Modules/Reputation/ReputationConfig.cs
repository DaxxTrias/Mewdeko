using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Reputation.Services;

namespace Mewdeko.Modules.Reputation;

public partial class Reputation
{
    /// <summary>
    ///     Interactive admin configuration interface for the reputation system.
    /// </summary>
    public class ReputationConfig : MewdekoSubmodule<RepService>
    {
        private readonly RepConfigService configService;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ReputationConfig" /> class.
        /// </summary>
        /// <param name="configService">The reputation configuration service.</param>
        public ReputationConfig(RepConfigService configService)
        {
            this.configService = configService;
        }

        /// <summary>
        ///     Opens the interactive reputation configuration interface.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepConfig()
        {
            await configService.ShowConfigurationMenuAsync(ctx).ConfigureAwait(false);
        }

        /// <summary>
        ///     Exports the current reputation configuration to JSON.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepExport()
        {
            await configService.ExportConfigurationAsync(ctx).ConfigureAwait(false);
        }

        /// <summary>
        ///     Imports reputation configuration from uploaded JSON file.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepImport()
        {
            await configService.ImportConfigurationAsync(ctx).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows the current reputation configuration in a detailed embed.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepStatus()
        {
            await configService.ShowConfigurationStatusAsync(ctx).ConfigureAwait(false);
        }
    }
}