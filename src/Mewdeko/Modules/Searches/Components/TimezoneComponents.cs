using Discord.Interactions;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches.Components;

/// <summary>
///     Handles interaction components for timezone selection.
/// </summary>
public class TimezoneComponents : MewdekoSlashModuleBase<SearchesService>
{
    /// <summary>
    ///     Handles the select menu for timezone selection.
    /// </summary>
    /// <param name="values">The selected values from the select menu.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [ComponentInteraction("timezone_select_menu:*")]
    public async Task HandleTimezoneSelect(string originalQuery, string[] values)
    {
        if (values.Length == 0 || string.IsNullOrEmpty(values[0]))
        {
            await ctx.Interaction.RespondAsync(Strings.NoTimezoneSelected(ctx.Guild.Id), ephemeral: true);
            return;
        }

        // Extract timezone ID from the value format "timezone_select:timezoneId"
        var selectedValue = values[0];
        if (!selectedValue.StartsWith("timezone_select:"))
        {
            await ctx.Interaction.RespondAsync(Strings.InvalidSelection(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var timezoneId = selectedValue["timezone_select:".Length..];

        try
        {
            // Get timezone info and calculate current time
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);

            var timeString = currentTime.ToString("h:mm:ss tt");
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.TimeNew(ctx.Guild.Id))
                .WithDescription(Format.Code(timeString))
                .AddField(Strings.Location(ctx.Guild.Id), timezone.DisplayName, true)
                .AddField(Strings.Timezone(ctx.Guild.Id), timezone.Id, true);

            var comp = ctx.Interaction as IComponentInteraction;

            // Update the original message with the selected timezone
            await comp.UpdateAsync(x =>
            {
                x.Embed = eb.Build();
                x.Components = new ComponentBuilder().Build(); // Remove the select menu
            });
        }
        catch (TimeZoneNotFoundException)
        {
            await ctx.Interaction.RespondAsync(Strings.TimezoneNotFound(ctx.Guild.Id), ephemeral: true);
        }
        catch (Exception)
        {
            await ctx.Interaction.RespondAsync(Strings.ErrorOccured(ctx.Guild.Id), ephemeral: true);
        }
    }
}