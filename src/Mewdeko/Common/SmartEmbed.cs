using System.Text.Json;
using Serilog;

namespace Mewdeko.Common;

/// <summary>
///     Provides methods for parsing and creating Discord embeds using embed json found at https://eb.mewdeko.tech
/// </summary>
public static class SmartEmbed
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    ///     Tries to parse the input string into Discord embeds, plain text, and components.
    /// </summary>
    /// <param name="input">The input string containing the embed data.</param>
    /// <param name="guildId">The ID of the guild where the embed is parsed.</param>
    /// <param name="embeds">
    ///     When this method returns, contains the parsed Discord embeds, if parsing succeeds; otherwise,
    ///     null.
    /// </param>
    /// <param name="plainText">When this method returns, contains the parsed plain text, if parsing succeeds; otherwise, null.</param>
    /// <param name="components">
    ///     When this method returns, contains the parsed components, if parsing succeeds; otherwise,
    ///     null.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the input string is successfully parsed into Discord embeds, plain text, and components;
    ///     otherwise, <c>false</c>.
    /// </returns>
    public static bool TryParse(
        string? input,
        ulong? guildId,
        out Discord.Embed[]? embeds,
        out string? plainText,
        out ComponentBuilder? components)
    {
        try
        {
            components = null;
            plainText = null;
            embeds = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            foreach (var jsonCandidate in ExtractJsonCandidates(input))
            {
                if (!TryDeserialize(jsonCandidate, out var newEmbed))
                {
                    continue;
                }

                if (newEmbed.Embed?.Fields is { Count: > 0 })
                {
                    foreach (var f in newEmbed.Embed.Fields)
                    {
                        f.Name = f.Name.TrimTo(256);
                        f.Value = f.Value.TrimTo(1024);
                    }
                }

                if (newEmbed.Embeds is not null && newEmbed.Embeds.Any(x => x.Fields is not null))
                {
                    foreach (var f in newEmbed.Embeds.Select(x => x.Fields).Where(y => y is not null))
                    {
                        foreach (var ff in f)
                        {
                            ff.Name = ff.Name.TrimTo(256);
                            ff.Value = ff.Value.TrimTo(1024);
                        }
                    }
                }

                if (newEmbed.Embed is not null)
                {
                    embeds = NewEmbed.ToEmbedArray([
                        newEmbed.Embed
                    ]);
                }
                else if (newEmbed.Embeds is not null && newEmbed.Embeds.Any())
                {
                    embeds = NewEmbed.ToEmbedArray(newEmbed.Embeds);
                }
                else
                {
                    embeds = null;
                }

                plainText = newEmbed.Content;
                components = newEmbed.GetComponents(guildId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to parse embed");
            embeds = null;
            plainText = null;
            components = null;
            return false;
        }
    }

    private static bool TryDeserialize(string json, out NewEmbed? newEmbed)
    {
        try
        {
            newEmbed = JsonSerializer.Deserialize<NewEmbed>(json, JsonOptions);
            return newEmbed is { IsValid: true };
        }
        catch (Exception ex)
        {
            // AI responses may contain json-like snippets that are not valid SmartEmbed payloads.
            // Keep this at debug level to avoid noisy logs while we keep trying other candidates.
            Log.Debug(ex, "Failed to parse SmartEmbed candidate JSON");
            newEmbed = null;
            return false;
        }
    }

    private static IEnumerable<string> ExtractJsonCandidates(string input)
    {
        var seenCandidates = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in ExtractBalancedJsonObjects(input))
        {
            if (seenCandidates.Add(candidate))
                yield return candidate;
        }

        foreach (var fencedContent in ExtractCodeFenceBodies(input))
        {
            foreach (var candidate in ExtractBalancedJsonObjects(fencedContent))
            {
                if (seenCandidates.Add(candidate))
                    yield return candidate;
            }
        }
    }

    private static IEnumerable<string> ExtractCodeFenceBodies(string input)
    {
        var searchStart = 0;
        while (searchStart < input.Length)
        {
            var openingFence = input.IndexOf("```", searchStart, StringComparison.Ordinal);
            if (openingFence < 0)
                yield break;

            var firstLineEnd = input.IndexOf('\n', openingFence + 3);
            if (firstLineEnd < 0)
                yield break;

            var closingFence = input.IndexOf("```", firstLineEnd + 1, StringComparison.Ordinal);
            if (closingFence < 0)
                yield break;

            var fenceBody = input.Substring(firstLineEnd + 1, closingFence - firstLineEnd - 1).Trim();
            if (!string.IsNullOrWhiteSpace(fenceBody))
                yield return fenceBody;

            searchStart = closingFence + 3;
        }
    }

    private static IEnumerable<string> ExtractBalancedJsonObjects(string input)
    {
        var depth = 0;
        var startIndex = -1;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < input.Length; i++)
        {
            var current = input[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '{')
            {
                if (depth == 0)
                    startIndex = i;

                depth++;
                continue;
            }

            if (current == '}' && depth > 0)
            {
                depth--;

                if (depth == 0 && startIndex >= 0)
                    yield return input.Substring(startIndex, i - startIndex + 1);
            }
        }
    }
}