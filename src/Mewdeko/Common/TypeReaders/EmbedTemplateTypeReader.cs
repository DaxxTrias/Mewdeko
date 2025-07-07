using System.Text.RegularExpressions;
using Discord.Commands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Utility.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.TypeReaders;

/// <summary>
///     Type reader for parsing embed templates and raw embed JSON into EmbedTemplateResult objects.
/// </summary>
public partial class EmbedTemplateTypeReader : MewdekoTypeReader<EmbedTemplateResult>
{
    private static readonly Regex TemplateRegex = MyRegex();

    /// <summary>
    ///     Initializes a new instance of the <see cref="EmbedTemplateTypeReader" /> class.
    /// </summary>
    /// <param name="client">The DiscordShardedClient instance.</param>
    /// <param name="cmds">The CommandService instance.</param>
    public EmbedTemplateTypeReader(DiscordShardedClient client, CommandService cmds) : base(client, cmds)
    {
    }

    /// <inheritdoc />
    public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input,
        IServiceProvider services)
    {
        if (string.IsNullOrWhiteSpace(input))
            return TypeReaderResult.FromError(CommandError.ParseFailed, "Input cannot be empty.");

        var originalInput = input; // Store original input for storage purposes
        var match = TemplateRegex.Match(input.Trim());

        if (match.Success)
        {
            // This is a template reference
            var templateName = match.Groups[1].Value;
            var embedService = services.GetRequiredService<EmbedService>();

            try
            {
                var embed = await embedService.GetEmbedTemplateAsync(
                    context.User.Id,
                    context.Guild?.Id,
                    templateName);

                if (embed == null)
                {
                    return TypeReaderResult.FromError(CommandError.ObjectNotFound,
                        $"Embed template '{templateName}' not found.");
                }

                // Apply replacements to the template JSON
                var replacer = new ReplacementBuilder()
                    .WithDefault(context)
                    .Build();

                var processedJson = replacer.Replace(embed.JsonCode);

                return ParseUsingSmartEmbed(processedJson, context.Guild?.Id ?? 0, embed.JsonCode);
            }
            catch (Exception ex)
            {
                return TypeReaderResult.FromError(CommandError.Exception,
                    $"Error retrieving embed template '{templateName}': {ex.Message}");
            }
        }

        // This is raw embed JSON/text - use SmartEmbed.TryParse
        return ParseUsingSmartEmbed(input, context.Guild?.Id ?? 0, originalInput);
    }

    private static TypeReaderResult ParseUsingSmartEmbed(string input, ulong guildId, string contentToStore)
    {
        try
        {
            // Always create a result, regardless of whether SmartEmbed parsing succeeds
            var result = new EmbedTemplateResult
            {
                ContentToStore = contentToStore
            };

            if (SmartEmbed.TryParse(input, guildId, out var embedData, out var plainText, out var components))
            {
                // Successfully parsed as embed/JSON
                result.Embeds = embedData;
                result.PlainText = plainText;
                result.Components = components;
            }
            else
            {
                // Failed to parse as JSON, treat as plain text
                result.Embeds = null;
                result.PlainText = input;
                result.Components = null;
            }

            return TypeReaderResult.FromSuccess(result);
        }
        catch (Exception ex)
        {
            return TypeReaderResult.FromError(CommandError.Exception,
                $"Error parsing embed: {ex.Message}");
        }
    }

    [GeneratedRegex(@"^{(?:template|t):([^}]+)}$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}