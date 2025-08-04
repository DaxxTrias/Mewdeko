using Discord.Commands;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;
using Embed = DataModel.Embed;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Provides commands for managing and using embed templates within a guild and per-user.
    /// </summary>
    [Group]
    public class EmbedCommands(IDataConnectionFactory dbFactory) : MewdekoSubmodule<EmbedService>
    {
        /// <summary>
        ///     Saves an embed template for personal use.
        /// </summary>
        /// <param name="name">The name of the embed template</param>
        /// <param name="embedJson">The JSON representation of the embed</param>
        /// <returns>A task that represents the asynchronous operation of saving an embed template.</returns>
        [Cmd]
        [Aliases]
        public async Task EmbedSave(string name, [Remainder] string embedJson)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                await ReplyErrorAsync(Strings.EmbedSaveNameRequired(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(embedJson))
            {
                await ReplyErrorAsync(Strings.EmbedSaveJsonRequired(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
                return;
            }

            // Validate the embed JSON
            if (!SmartEmbed.TryParse(embedJson, ctx.Guild?.Id, out var embedData, out var plainText,
                    out var components))
            {
                await ReplyErrorAsync(Strings.EmbedSaveInvalidJson(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
                return;
            }

            await using var db = await dbFactory.CreateConnectionAsync();

            // Check if user already has a template with this name
            var existingEmbed = await db.GetTable<Embed>()
                .FirstOrDefaultAsync(e => e.UserId == ctx.User.Id &&
                                          e.EmbedName == name &&
                                          e.GuildId == null);

            if (existingEmbed != null)
            {
                await ReplyErrorAsync(Strings.EmbedSaveAlreadyExists(ctx.Guild?.Id ?? 0, name)).ConfigureAwait(false);
                return;
            }

            var embed = new Embed
            {
                UserId = ctx.User.Id,
                EmbedName = name,
                JsonCode = embedJson,
                DateAdded = DateTime.UtcNow,
                GuildId = null,
                IsGuildShared = false
            };

            await db.InsertAsync(embed);

            await ReplyConfirmAsync(Strings.EmbedSaveSuccess(ctx.Guild?.Id ?? 0, name)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Saves an embed template for guild use (requires ManageMessages permission).
        /// </summary>
        /// <param name="name">The name of the embed template</param>
        /// <param name="embedJson">The JSON representation of the embed</param>
        /// <returns>A task that represents the asynchronous operation of saving a guild embed template.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task GuildEmbedSave(string name, [Remainder] string embedJson)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                await ReplyErrorAsync(Strings.EmbedSaveNameRequired(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(embedJson))
            {
                await ReplyErrorAsync(Strings.EmbedSaveJsonRequired(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            // Validate the embed JSON
            if (!SmartEmbed.TryParse(embedJson, ctx.Guild.Id, out var embedData, out var plainText, out var components))
            {
                await ReplyErrorAsync(Strings.EmbedSaveInvalidJson(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await using var db = await dbFactory.CreateConnectionAsync();

            // Check if guild already has a template with this name
            var existingEmbed = await db.GetTable<Embed>()
                .FirstOrDefaultAsync(e => e.GuildId == ctx.Guild.Id &&
                                          e.EmbedName == name &&
                                          e.IsGuildShared == true);

            if (existingEmbed != null)
            {
                await ReplyErrorAsync(Strings.EmbedSaveAlreadyExists(ctx.Guild.Id, name)).ConfigureAwait(false);
                return;
            }

            var embed = new Embed
            {
                UserId = ctx.User.Id,
                EmbedName = name,
                JsonCode = embedJson,
                DateAdded = DateTime.UtcNow,
                GuildId = ctx.Guild.Id,
                IsGuildShared = true
            };

            await db.InsertAsync(embed);

            await ReplyConfirmAsync(Strings.GuildEmbedSaveSuccess(ctx.Guild.Id, name)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all personal embed templates.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation of listing embed templates.</returns>
        [Cmd]
        [Aliases]
        public async Task EmbedList()
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var userEmbeds = await db.GetTable<Embed>()
                .Where(e => e.UserId == ctx.User.Id && e.GuildId == null)
                .OrderBy(e => e.EmbedName)
                .ToListAsync();

            if (!userEmbeds.Any())
            {
                await ReplyErrorAsync(Strings.EmbedListNone(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
                return;
            }

            var embedNames = userEmbeds.Select(e => $"• {e.EmbedName}").ToList();
            await ReplyConfirmAsync(Strings.EmbedListPersonal(ctx.Guild?.Id ?? 0, string.Join("\n", embedNames)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all guild embed templates.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation of listing guild embed templates.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task GuildEmbedList()
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var guildEmbeds = await db.GetTable<Embed>()
                .Where(e => e.GuildId == ctx.Guild.Id && e.IsGuildShared == true)
                .OrderBy(e => e.EmbedName)
                .ToListAsync();

            if (!guildEmbeds.Any())
            {
                await ReplyErrorAsync(Strings.GuildEmbedListNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embedNames = guildEmbeds.Select(e => $"• {e.EmbedName}").ToList();
            await ReplyConfirmAsync(Strings.EmbedListGuild(ctx.Guild.Id, string.Join("\n", embedNames)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes a personal embed template.
        /// </summary>
        /// <param name="name">The name of the embed template to delete</param>
        /// <returns>A task that represents the asynchronous operation of deleting an embed template.</returns>
        [Cmd]
        [Aliases]
        public async Task EmbedDelete(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                await ReplyErrorAsync(Strings.EmbedDeleteNameRequired(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
                return;
            }

            await using var db = await dbFactory.CreateConnectionAsync();

            var embed = await db.GetTable<Embed>()
                .FirstOrDefaultAsync(e => e.UserId == ctx.User.Id &&
                                          e.EmbedName == name &&
                                          e.GuildId == null);

            if (embed == null)
            {
                await ReplyErrorAsync(Strings.EmbedDeleteNotFound(ctx.Guild?.Id ?? 0, name)).ConfigureAwait(false);
                return;
            }

            await db.DeleteAsync(embed);

            await ReplyConfirmAsync(Strings.EmbedDeleteSuccess(ctx.Guild?.Id ?? 0, name)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes a guild embed template (requires ManageMessages permission).
        /// </summary>
        /// <param name="name">The name of the embed template to delete</param>
        /// <returns>A task that represents the asynchronous operation of deleting a guild embed template.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task GuildEmbedDelete(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                await ReplyErrorAsync(Strings.EmbedDeleteNameRequired(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await using var db = await dbFactory.CreateConnectionAsync();

            var embed = await db.GetTable<Embed>()
                .FirstOrDefaultAsync(e => e.GuildId == ctx.Guild.Id &&
                                          e.EmbedName == name &&
                                          e.IsGuildShared == true);

            if (embed == null)
            {
                await ReplyErrorAsync(Strings.EmbedDeleteNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
                return;
            }

            await db.DeleteAsync(embed);

            await ReplyConfirmAsync(Strings.GuildEmbedDeleteSuccess(ctx.Guild.Id, name)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Previews an embed template.
        /// </summary>
        /// <param name="name">The name of the embed template to preview</param>
        /// <returns>A task that represents the asynchronous operation of previewing an embed template.</returns>
        [Cmd]
        [Aliases]
        public async Task EmbedPreview(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                await ReplyErrorAsync(Strings.EmbedPreviewNameRequired(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
                return;
            }

            await using var db = await dbFactory.CreateConnectionAsync();

            // Check user embeds first
            var embed = await db.GetTable<Embed>()
                .FirstOrDefaultAsync(e => e.UserId == ctx.User.Id &&
                                          e.EmbedName == name &&
                                          e.GuildId == null);

            // If not found and in guild context, check guild embeds
            if (embed == null && ctx.Guild != null)
            {
                embed = await db.GetTable<Embed>()
                    .FirstOrDefaultAsync(e => e.GuildId == ctx.Guild.Id &&
                                              e.EmbedName == name &&
                                              e.IsGuildShared == true);
            }

            if (embed == null)
            {
                await ReplyErrorAsync(Strings.EmbedPreviewNotFound(ctx.Guild?.Id ?? 0, name)).ConfigureAwait(false);
                return;
            }

            var replacer = new ReplacementBuilder()
                .WithDefault(Context)
                .Build();

            var content = replacer.Replace(embed.JsonCode);

            if (SmartEmbed.TryParse(content, ctx.Guild?.Id, out var embedData, out var plainText, out var components))
            {
                await ctx.Channel.SendMessageAsync(plainText ?? "", embeds: embedData, components: components?.Build())
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.EmbedPreviewInvalidJson(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
            }
        }
    }
}