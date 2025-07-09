using Discord.Interactions;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Provides autocomplete suggestions for AI model selection.
/// </summary>
public class AiModelAutoCompleter : AutocompleteHandler
{
    private readonly AiService aiService;
    private readonly ILogger<AiModelAutoCompleter> logger;


    /// <summary>
    ///     Initializes a new instance of the AiModelAutoCompleter class.
    /// </summary>
    public AiModelAutoCompleter(AiService aiService, ILogger<AiModelAutoCompleter> logger)
    {
        this.aiService = aiService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var guildId = context.Guild?.Id ?? 0;
        if (guildId == 0)
            return AutocompletionResult.FromError(new Exception("Must be used in a guild"));

        var firstOption = autocompleteInteraction.Data.Options.FirstOrDefault(x => x.Name == "provider");

        var provider = (string)firstOption?.Value switch
        {
            "Claude" => AiService.AiProvider.Claude,
            "OpenAI" => AiService.AiProvider.OpenAi,
            "Groq" => AiService.AiProvider.Groq,
            "Grok" => AiService.AiProvider.Grok,
            _ => AiService.AiProvider.OpenAi
        };
        logger.LogInformation(provider.GetType().Name);

        var config = await aiService.GetOrCreateConfig(guildId);

        if (string.IsNullOrEmpty(config.ApiKey))
            return AutocompletionResult.FromSuccess(
                [new AutocompleteResult("No API key set for this provider. Set a key before trying to query models", "none")]);

        try
        {
            var models = await aiService.GetSupportedModels(provider, config.ApiKey);
            var searchTerm = (string)autocompleteInteraction.Data.Current.Value;

            return AutocompletionResult.FromSuccess(
                models.Where(m => m.Id.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                  m.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(m => m.Id.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .Take(25)
                    .Select(m => new AutocompleteResult(m.Name, m.Id)));
        }
        catch
        {
            return AutocompletionResult.FromSuccess(
                [new AutocompleteResult("Invalid API key for this provider", "none")]);
        }
    }
}