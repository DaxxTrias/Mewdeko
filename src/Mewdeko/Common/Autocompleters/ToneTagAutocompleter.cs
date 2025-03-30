using Discord.Interactions;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for tone tags.
/// </summary>
public class ToneTagAutocompleter : AutocompleteHandler
{
    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="inter">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction inter,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        await Task.CompletedTask;
        if (inter.Data.Current.Value is not string currentValue)
            return AutocompletionResult.FromSuccess();
        return AutocompletionResult.FromSuccess(
            (services.GetService(typeof(ToneTagService)) as ToneTagService)
            .Tags.SelectMany(x => x.GetAllValues()).Select(x => '/' + x)
            .Where(x => x.Contains(currentValue,
                StringComparison.InvariantCultureIgnoreCase))
            .OrderByDescending(x =>
                x.StartsWith(currentValue,
                    StringComparison.InvariantCultureIgnoreCase)).Take(25)
            .Select(x => new AutocompleteResult(x, x)));
    }
}