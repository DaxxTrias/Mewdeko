using System.Net.Http;
using System.Text.Json;
using LinqToDB;
using Mewdeko.Modules.UserProfile.Common;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service for fetching and managing user pronouns from an external API.
/// </summary>
public class PronounsService : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly HttpClient http;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PronounsService"/> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="http">The HTTP client for API requests.</param>
    public PronounsService(IDataConnectionFactory dbFactory, HttpClient http)
    {
        this.dbFactory = dbFactory;
        this.http = http;
    }

    /// <summary>
    ///     Asynchronously gets a user's pronouns from the local database or queries an external API if not found.
    /// </summary>
    /// <param name="discordId">The Discord user ID to fetch pronouns for.</param>
    /// <returns>A PronounSearchResult object containing the pronouns or indicating if unspecified.</returns>
    public async Task<PronounSearchResult> GetPronounsOrUnspecifiedAsync(ulong discordId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Query using LinqToDB
        var user = await db.DiscordUsers
            .FirstOrDefaultAsync(x => x.UserId == discordId)
            .ConfigureAwait(false);

        // If we have pronouns stored locally, return them
        if (user != null && !string.IsNullOrWhiteSpace(user.Pronouns))
            return new PronounSearchResult(user.Pronouns, false);

        // Otherwise, fetch from the PronounDB API
        try
        {
            var result = await http.GetStringAsync($"https://pronoundb.org/api/v1/lookup?platform=discord&id={discordId}")
                .ConfigureAwait(false);

            var pronouns = JsonSerializer.Deserialize<PronounDbResult>(result);

            return new PronounSearchResult(MapPronounCode(pronouns?.Pronouns ?? "unspecified"), true);
        }
        catch (Exception)
        {
            // Log the exception (you might want to use a proper logging framework)
            return new PronounSearchResult("Failed to retrieve pronouns", true);
        }
    }

    /// <summary>
    ///     Maps pronouns codes from PronounDB to human-readable formats.
    /// </summary>
    /// <param name="code">The pronoun code from PronounDB.</param>
    /// <returns>Human-readable pronouns.</returns>
    private static string MapPronounCode(string code)
    {
        return code switch
        {
            "unspecified" => "Unspecified",
            "hh" => "he/him",
            "hi" => "he/it",
            "hs" => "he/she",
            "ht" => "he/they",
            "ih" => "it/him",
            "ii" => "it/its",
            "is" => "it/she",
            "it" => "it/they",
            "shh" => "she/he",
            "sh" => "she/her",
            "si" => "she/it",
            "st" => "she/they",
            "th" => "they/he",
            "ti" => "they/it",
            "ts" => "they/she",
            "tt" => "they/them",
            "any" => "Any pronouns",
            "other" => "Pronouns not on PronounDB",
            "ask" => "Pronouns you should ask them about",
            "avoid" => "A name instead of pronouns",
            _ => "Failed to resolve pronouns."
        };
    }
}