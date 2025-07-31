using Discord.Commands;
using LinqToDB;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Reputation.Services;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Reputation;

public partial class Reputation
{
    /// <summary>
    ///     Administrative commands for reputation system configuration.
    /// </summary>
    public class ReputationAdminCommands : MewdekoSubmodule<RepCommandRequirementsService>
    {
        private readonly IDataConnectionFactory dbFactory;
        private readonly RepService repService;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ReputationAdminCommands" /> class.
        /// </summary>
        /// <param name="repService">The reputation service.</param>
        /// <param name="dbFactory">The database connection factory.</param>
        public ReputationAdminCommands(RepService repService, IDataConnectionFactory dbFactory)
        {
            this.repService = repService;
            this.dbFactory = dbFactory;
        }

        /// <summary>
        ///     Sets reputation requirements for a command.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <param name="minReputation">The minimum reputation required.</param>
        /// <param name="repType">The specific reputation type required (optional).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepCommandReq(string commandName, int minReputation, string? repType = null)
        {
            if (minReputation < 0)
            {
                await ReplyErrorAsync(Strings.RepAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            // Validate reputation type if provided
            if (!string.IsNullOrEmpty(repType) && !await repService.IsValidReputationTypeAsync(ctx.Guild.Id, repType))
            {
                await ReplyErrorAsync(Strings.RepInvalidType(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.AddCommandRequirementAsync(ctx.Guild.Id, commandName.ToLowerInvariant(),
                minReputation, repType).ConfigureAwait(false);

            await SuccessAsync(Strings.RepCommandRequirementAdded(ctx.Guild.Id,
                commandName, minReputation, repType ?? "total")).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets reputation requirements for a command in specific channels.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <param name="minReputation">The minimum reputation required.</param>
        /// <param name="repType">The specific reputation type required.</param>
        /// <param name="channels">The channels where this requirement applies.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepCommandReqChannels(string commandName, int minReputation, string repType,
            params ITextChannel[] channels)
        {
            if (minReputation < 0)
            {
                await ReplyErrorAsync(Strings.RepAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (channels.Length == 0)
            {
                await ReplyErrorAsync(Strings.RepNoChannelsSpecified(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            // Validate reputation type if not "total"
            if (repType != "total" && !await repService.IsValidReputationTypeAsync(ctx.Guild.Id, repType))
            {
                await ReplyErrorAsync(Strings.RepInvalidType(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var channelIds = channels.Select(c => c.Id).ToList();
            var channelIdJson = JsonConvert.SerializeObject(channelIds);
            var actualRepType = repType == "total" ? null : repType;

            await Service.AddCommandRequirementAsync(ctx.Guild.Id, commandName.ToLowerInvariant(),
                minReputation, actualRepType, channelIdJson).ConfigureAwait(false);

            var channelNames = string.Join(", ", channels.Select(c => $"#{c.Name}"));
            await SuccessAsync(Strings.RepCommandRequirementAddedChannels(ctx.Guild.Id,
                commandName, minReputation, repType, channelNames)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets bypass roles for a command requirement.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <param name="roles">The roles that can bypass the requirement.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepCommandBypass(string commandName, params IRole[] roles)
        {
            if (roles.Length == 0)
            {
                await ReplyErrorAsync(Strings.RepNoRolesSpecified(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var roleIds = roles.Select(r => r.Id).ToList();
            var roleIdJson = JsonConvert.SerializeObject(roleIds);

            // Get existing requirement
            await using var db = await dbFactory.CreateConnectionAsync();
            var requirement = await db.RepCommandRequirements
                .FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id && x.CommandName == commandName.ToLowerInvariant());

            if (requirement == null)
            {
                await ReplyErrorAsync(Strings.RepCommandRequirementNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            // Update with bypass roles
            await Service.AddCommandRequirementAsync(ctx.Guild.Id, commandName.ToLowerInvariant(),
                requirement.MinReputation, requirement.RequiredRepType, requirement.RestrictedChannels,
                requirement.DenialMessage, roleIdJson, requirement.ShowInHelp);

            var roleNames = string.Join(", ", roles.Select(r => r.Name));
            await SuccessAsync(Strings.RepCommandBypassAdded(ctx.Guild.Id, commandName, roleNames))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes reputation requirements from a command.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepCommandReqRemove(string commandName)
        {
            var deleted = await Service.RemoveCommandRequirementAsync(ctx.Guild.Id, commandName.ToLowerInvariant())
                .ConfigureAwait(false);

            if (deleted > 0)
                await SuccessAsync(Strings.RepCommandRequirementRemoved(ctx.Guild.Id, commandName))
                    .ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.RepCommandRequirementNotFound(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all command requirements for the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RepCommandReqList()
        {
            var requirements = await Service.GetCommandRequirementsAsync(ctx.Guild.Id).ConfigureAwait(false);

            if (!requirements.Any())
            {
                await ReplyConfirmAsync(Strings.RepNoCommandRequirements(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepCommandRequirementsList(ctx.Guild.Id))
                .WithDescription(Strings.RepCommandRequirementsDesc(ctx.Guild.Id));

            foreach (var req in requirements.Take(15))
            {
                var value = $"**Min Rep:** {req.MinReputation} {req.RequiredRepType ?? "total"}";

                if (!string.IsNullOrEmpty(req.RestrictedChannels))
                {
                    try
                    {
                        var channelIds = JsonConvert.DeserializeObject<List<ulong>>(req.RestrictedChannels);
                        if (channelIds?.Any() == true)
                        {
                            var channelNames = new List<string>();
                            foreach (var channelId in channelIds)
                            {
                                var channel = await ctx.Guild.GetChannelAsync(channelId);
                                if (channel != null) channelNames.Add($"#{channel.Name}");
                            }

                            if (channelNames.Any())
                                value += $"\n**Channels:** {string.Join(", ", channelNames)}";
                        }
                    }
                    catch
                    {
                        // Ignore JSON parsing errors
                    }
                }

                embed.AddField($".{req.CommandName}", value, true);
            }

            if (requirements.Count > 15)
                embed.WithFooter($"Showing first 15 of {requirements.Count} requirements");

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows reputation requirements for a specific command.
        /// </summary>
        /// <param name="commandName">The command to check.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task RepCommandInfo(string commandName)
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var requirement = await db.RepCommandRequirements
                .FirstOrDefaultAsync(x =>
                    x.GuildId == ctx.Guild.Id && x.CommandName == commandName.ToLowerInvariant() && x.IsActive);

            if (requirement == null)
            {
                await ReplyConfirmAsync(Strings.RepCommandNoRequirements(ctx.Guild.Id, commandName))
                    .ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepCommandRequirementInfo(ctx.Guild.Id, commandName))
                .AddField(Strings.RepMinReputation(ctx.Guild.Id),
                    $"{requirement.MinReputation} {requirement.RequiredRepType ?? "total"}", true)
                .AddField(Strings.RepShowInHelp(ctx.Guild.Id), requirement.ShowInHelp ? "✅" : "❌", true);

            if (!string.IsNullOrEmpty(requirement.RestrictedChannels))
            {
                try
                {
                    var channelIds = JsonConvert.DeserializeObject<List<ulong>>(requirement.RestrictedChannels);
                    var channelNames = new List<string>();

                    foreach (var channelId in channelIds ?? [])
                    {
                        var channel = await ctx.Guild.GetChannelAsync(channelId);
                        if (channel != null) channelNames.Add($"#{channel.Name}");
                    }

                    if (channelNames.Any())
                        embed.AddField(Strings.RepRestrictedChannels(ctx.Guild.Id), string.Join(", ", channelNames));
                }
                catch
                {
                    // Ignore JSON parsing errors
                }
            }

            if (!string.IsNullOrEmpty(requirement.DenialMessage))
                embed.AddField(Strings.RepDenialMessage(ctx.Guild.Id), requirement.DenialMessage);

            if (!string.IsNullOrEmpty(requirement.BypassRoles))
            {
                try
                {
                    var roleIds = JsonConvert.DeserializeObject<List<ulong>>(requirement.BypassRoles);
                    var roleNames = new List<string>();

                    foreach (var roleId in roleIds ?? [])
                    {
                        var role = ctx.Guild.GetRole(roleId);
                        if (role != null) roleNames.Add(role.Name);
                    }

                    if (roleNames.Any())
                        embed.AddField(Strings.RepBypassRoles(ctx.Guild.Id), string.Join(", ", roleNames));
                }
                catch
                {
                    // Ignore JSON parsing errors
                }
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}