using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DataModel;
using Discord.Commands;
using LinqToDB;
using LinqToDB.Data;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Data model for quote export/import operations.
    /// </summary>
    public class QuoteExportData
    {
        /// <summary>
        ///     The keyword associated with the quote.
        /// </summary>
        public string Keyword { get; set; } = null!;

        /// <summary>
        ///     The text content of the quote.
        /// </summary>
        public string Text { get; set; } = null!;

        /// <summary>
        ///     The name of the user who created the quote.
        /// </summary>
        public string AuthorName { get; set; } = null!;

        /// <summary>
        ///     The Discord ID of the user who created the quote.
        /// </summary>
        public ulong AuthorId { get; set; }

        /// <summary>
        ///     The date and time when the quote was added.
        /// </summary>
        public DateTime? DateAdded { get; set; }

        /// <summary>
        ///     The number of times the quote has been used.
        /// </summary>
        public ulong UseCount { get; set; }
    }

    /// <summary>
    ///     Provides commands for managing and displaying quotes within a guild. I dont know why you would use this when chat
    ///     triggers exist.
    /// </summary>
    [Group]
    public class QuoteCommands(IDataConnectionFactory dbFactory, HttpClient httpClient) : MewdekoSubmodule
    {
        /// <summary>
        ///     Lists quotes in the guild. Quotes can be ordered by keyword or date added.
        /// </summary>
        /// <param name="order">Determines the order in which quotes are listed.</param>
        /// <returns>A task that represents the asynchronous operation of listing quotes.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public Task ListQuotes(OrderType order = OrderType.Keyword)
        {
            return ListQuotes(1, order);
        }

        /// <summary>
        ///     Lists quotes in the guild on a specific page. Quotes can be ordered by keyword or date added.
        /// </summary>
        /// <param name="page">The page number of quotes to display.</param>
        /// <param name="order">Determines the order in which quotes are listed.</param>
        /// <returns>A task that represents the asynchronous operation of listing quotes.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        public async Task ListQuotes(int page = 1, OrderType order = OrderType.Keyword)
        {
            page--;
            if (page < 0)
                return;

            IEnumerable<Quote> quotes;

            await using var db = await dbFactory.CreateConnectionAsync();
            {
                var query = db.Quotes.Where(x => x.GuildId == ctx.Guild.Id);

                if (order == OrderType.Keyword)
                    query = query.OrderBy(x => x.Keyword);
                else
                    query = query.OrderBy(x => x.Id);

                quotes = await query.Skip(15 * page).Take(15).ToListAsync();
            }

            var enumerable = quotes as Quote[] ?? quotes.ToArray();
            if (enumerable.Length > 0)
            {
                await ctx.Channel.SendConfirmAsync(Strings.QuotesPage(ctx.Guild.Id, page + 1),
                        string.Join("\n",
                            enumerable.Select(q =>
                                $"`#{q.Id}` {Format.Bold(q.Keyword.SanitizeAllMentions()),-20} by {q.AuthorName.SanitizeAllMentions()}")))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.QuotesPageNone(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Displays a random quote matching the specified keyword.
        /// </summary>
        /// <param name="keyword">The keyword to search for in quotes.</param>
        /// <returns>A task that represents the asynchronous operation of displaying a quote.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QuotePrint([Remainder] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return;

            keyword = keyword.ToUpperInvariant();

            await using var db = await dbFactory.CreateConnectionAsync();

            var matchingQuotes = await db.Quotes
                .Where(q => q.GuildId == ctx.Guild.Id && q.Keyword == keyword)
                .ToListAsync();

            var quote = matchingQuotes.Any()
                ? matchingQuotes.MinBy(_ => new Random().Next())
                : null;

            if (quote == null)
                return;

            var rep = new ReplacementBuilder()
                .WithDefault(Context)
                .Build();

            if (SmartEmbed.TryParse(rep.Replace(quote.Text), ctx.Guild?.Id, out var embed, out var plainText,
                    out var components))
            {
                await ctx.Channel.SendMessageAsync($"`#{quote.Id}` 📣 {plainText?.SanitizeAllMentions()}",
                    embeds: embed, components: components?.Build()).ConfigureAwait(false);
                return;
            }

            await ctx.Channel
                .SendMessageAsync($"`#{quote.Id}` 📣 {rep.Replace(quote.Text)?.SanitizeAllMentions()}")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Displays the quote with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the quote to display.</param>
        /// <returns>A task that represents the asynchronous operation of displaying a quote.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QuoteShow(int id)
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id);

            if (quote?.GuildId != Context.Guild.Id)
                quote = null;

            if (quote is null)
            {
                await ReplyErrorAsync(Strings.QuotesNotfound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ShowQuoteData(quote).ConfigureAwait(false);
        }

        private async Task ShowQuoteData(Quote data)
        {
            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.QuoteId(ctx.Guild.Id, $"#{data.Id}"))
                .AddField(efb => efb.WithName(Strings.Trigger(ctx.Guild.Id)).WithValue(data.Keyword))
                .AddField(efb => efb.WithName(Strings.Response(ctx.Guild.Id)).WithValue(data.Text.Length > 1000
                    ? Strings.RedactedTooLong(ctx.Guild.Id)
                    : Format.Sanitize(data.Text)))
                .WithFooter(Strings.CreatedBy(ctx.Guild.Id, $"{data.AuthorName} ({data.AuthorId})"))
            ).ConfigureAwait(false);
        }

        /// <summary>
        ///     Searches for and displays a quote that matches both a keyword and a text query.
        /// </summary>
        /// <param name="keyword">The keyword to match in the quotes.</param>
        /// <param name="text">The text to match in the quotes.</param>
        /// <returns>A task that represents the asynchronous operation of searching for and displaying a quote.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QuoteSearch(string keyword, [Remainder] string text)
        {
            if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                return;

            keyword = keyword.ToUpperInvariant();

            await using var db = await dbFactory.CreateConnectionAsync();

            var matchingQuotes = await db.Quotes
                .Where(q => q.GuildId == ctx.Guild.Id &&
                            q.Keyword == keyword &&
                            q.Text.ToUpper().Contains(text.ToUpper()))
                .ToListAsync();

            var keywordquote = matchingQuotes.Any()
                ? matchingQuotes.MinBy(_ => new Random().Next())
                : null;

            if (keywordquote == null)
                return;

            await ctx.Channel.SendMessageAsync(
                    $"`#{keywordquote.Id}` 💬 {keyword.ToLowerInvariant()}:  {keywordquote.Text.SanitizeAllMentions()}")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Displays who added a quote with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the quote to display.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QuoteId(int id)
        {
            if (id < 0)
                return;

            var rep = new ReplacementBuilder()
                .WithDefault(Context)
                .Build();

            await using var db = await dbFactory.CreateConnectionAsync();
            var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id);

            if (quote is null || quote.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync(Strings.QuotesNotfound(ctx.Guild.Id), Config).ConfigureAwait(false);
                return;
            }

            var infoText =
                $"`#{quote.Id} added by {quote.AuthorName.SanitizeAllMentions()}` 🗯️ {quote.Keyword.ToLowerInvariant().SanitizeAllMentions()}:\n";

            if (SmartEmbed.TryParse(rep.Replace(quote.Text), ctx.Guild?.Id, out var embed, out var plainText,
                    out var components))
            {
                await ctx.Channel.SendMessageAsync(infoText + plainText.SanitizeMentions(), embeds: embed,
                        components: components?.Build())
                    .ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.SendMessageAsync(infoText + rep.Replace(quote.Text)?.SanitizeAllMentions())
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Adds a new quote with the specified keyword and text.
        /// </summary>
        /// <param name="keyword">The keyword associated with the quote.</param>
        /// <param name="text">The text of the quote.</param>
        /// <returns>A task that represents the asynchronous operation of adding a new quote.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QuoteAdd(string keyword, [Remainder] string text)
        {
            if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                return;

            keyword = keyword.ToUpperInvariant();

            var q = new Quote
            {
                AuthorId = ctx.Message.Author.Id,
                AuthorName = ctx.Message.Author.Username,
                GuildId = ctx.Guild.Id,
                Keyword = keyword,
                Text = text
            };

            await using var db = await dbFactory.CreateConnectionAsync();
            q.Id = await db.InsertWithInt32IdentityAsync(q);

            await ReplyConfirmAsync(Strings.QuoteAddedNew(ctx.Guild.Id, Format.Code(q.Id.ToString())))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes a quote with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the quote to delete.</param>
        /// <returns>A task that represents the asynchronous operation of deleting a quote.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QuoteDelete(int id)
        {
            var isAdmin = ((IGuildUser)ctx.Message.Author).GuildPermissions.Administrator;

            var success = false;
            string? response;

            await using var db = await dbFactory.CreateConnectionAsync();
            var q = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id);

            if (q?.GuildId != ctx.Guild.Id || !isAdmin && q.AuthorId != ctx.Message.Author.Id)
            {
                response = Strings.QuotesRemoveNone(ctx.Guild.Id);
            }
            else
            {
                await db.DeleteAsync(q);
                success = true;
                response = Strings.QuoteDeleted(ctx.Guild.Id, id);
            }

            if (success)
                await ctx.Channel.SendConfirmAsync(response).ConfigureAwait(false);
            else
                await ctx.Channel.SendErrorAsync(response, Config).ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes all quotes associated with the specified keyword.
        /// </summary>
        /// <param name="keyword">The keyword whose associated quotes will be deleted.</param>
        /// <returns>A task that represents the asynchronous operation of deleting quotes.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task DelAllQuotes([Remainder] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return;

            keyword = keyword.ToUpperInvariant();

            await using var db = await dbFactory.CreateConnectionAsync();

            await db.Quotes
                .Where(x => x.GuildId == ctx.Guild.Id && x.Keyword.ToUpper() == keyword)
                .DeleteAsync();

            await ReplyConfirmAsync(Strings.QuotesDeleted(ctx.Guild.Id, Format.Bold(keyword.SanitizeAllMentions())))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Exports all quotes from the guild in YAML format.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation of exporting quotes.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task QuoteExport()
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var quotes = await db.Quotes
                .Where(q => q.GuildId == ctx.Guild.Id)
                .OrderBy(q => q.Keyword)
                .ThenBy(q => q.Id)
                .ToListAsync();

            if (!quotes.Any())
            {
                await ReplyErrorAsync(Strings.QuotesPageNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var exportData = quotes.Select(q => new QuoteExportData
            {
                Keyword = q.Keyword,
                Text = q.Text,
                AuthorName = q.AuthorName,
                AuthorId = q.AuthorId,
                DateAdded = q.DateAdded,
                UseCount = q.UseCount
            }).ToList();

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonContent = JsonSerializer.Serialize(exportData, jsonOptions);
            var fileName = $"quotes-{ctx.Guild.Name.Replace(" ", "-")}-{DateTime.UtcNow:yyyy-MM-dd}.json";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
            var fileAttachment = new FileAttachment(stream, fileName);

            await ctx.Channel.SendFileAsync(fileAttachment,
                    Strings.QuoteExportSuccess(ctx.Guild.Id, quotes.Count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Imports quotes from a JSON file attachment. Duplicate quotes are allowed.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation of importing quotes.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task QuoteImport()
        {
            var attachment = ctx.Message.Attachments.FirstOrDefault();
            if (attachment == null)
            {
                await ReplyErrorAsync(Strings.QuoteImportNoFile(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!attachment.Filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyErrorAsync(Strings.QuoteImportInvalidFormat(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            try
            {
                var content = await httpClient.GetStringAsync(attachment.Url);
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true
                };

                var importData = JsonSerializer.Deserialize<List<QuoteExportData>>(content, jsonOptions);

                if (importData == null || !importData.Any())
                {
                    await ReplyErrorAsync(Strings.QuoteImportEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await using var db = await dbFactory.CreateConnectionAsync();

                var validQuotes = importData
                    .Where(data => !string.IsNullOrWhiteSpace(data.Keyword) && !string.IsNullOrWhiteSpace(data.Text))
                    .Select(data => new Quote
                    {
                        GuildId = ctx.Guild.Id,
                        Keyword = data.Keyword.ToUpperInvariant(),
                        Text = data.Text,
                        AuthorName = string.IsNullOrWhiteSpace(data.AuthorName) ? ctx.User.Username : data.AuthorName,
                        AuthorId = data.AuthorId == 0 ? ctx.User.Id : data.AuthorId,
                        UseCount = data.UseCount,
                        DateAdded = data.DateAdded ?? DateTime.UtcNow
                    })
                    .ToList();

                var errorCount = importData.Count - validQuotes.Count;

                if (validQuotes.Any())
                {
                    await db.BulkCopyAsync(validQuotes);
                    var successCount = validQuotes.Count;

                    await ReplyConfirmAsync(Strings.QuoteImportSuccess(ctx.Guild.Id, successCount, errorCount))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorAsync(Strings.QuoteImportFailed(ctx.Guild.Id)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await ReplyErrorAsync(Strings.QuoteImportError(ctx.Guild.Id, ex.Message)).ConfigureAwait(false);
            }
        }
    }
}