using Discord.Interactions;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches.Components;

/// <summary>
///     Handles Components V2 interactions for weather displays.
/// </summary>
public class WeatherComponents : MewdekoSlashModuleBase<SearchesService>
{
    /// <summary>
    ///     Handles main weather selection.
    /// </summary>
    /// <param name="location">The location query.</param>
    /// <param name="values">The selected option.</param>
    [ComponentInteraction("weather_main_select:*")]
    public async Task HandleMainSelect(string location, string[] values)
    {
        if (values.Length == 0)
        {
            await ctx.Interaction.RespondAsync(Strings.InvalidSelection(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var parts = values[0].Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var index))
        {
            await ctx.Interaction.RespondAsync(Strings.InvalidSelection(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var viewType = parts[0];

        try
        {
            var weatherData = await Service.GetWeatherDataAsync(location);
            if (weatherData == null)
            {
                await ctx.Interaction.RespondAsync(Strings.CityNotFound(ctx.Guild.Id), ephemeral: true);
                return;
            }

            var component = BuildWeatherComponent(weatherData, viewType, index);
            var comp = ctx.Interaction as IComponentInteraction;

            await comp.UpdateAsync(x =>
            {
                x.Components = component.Build();
                x.Flags = MessageFlags.ComponentsV2;
            });
        }
        catch (Exception)
        {
            await ctx.Interaction.RespondAsync(Strings.ErrorOccured(ctx.Guild.Id), ephemeral: true);
        }
    }

    /// <summary>
    ///     Builds the weather component with Components V2.
    /// </summary>
    /// <param name="weatherData">The weather data to display.</param>
    /// <param name="viewType">The view type (current, daily, hourly).</param>
    /// <param name="index">The selected index for the view type.</param>
    private ComponentBuilderV2 BuildWeatherComponent(OpenMeteoWeatherResponse weatherData, string viewType = "current",
        int index = 0)
    {
        var current = weatherData.Current;
        var emoji = WeatherCodeInterpreter.GetEmoji(current.WeatherCode);
        var condition = WeatherCodeInterpreter.GetDescription(current.WeatherCode);

        var builder = new ComponentBuilderV2();

        var locationDisplay = current.Country == current.LocationName
            ? current.LocationName
            : $"{current.LocationName}, {current.Country}";

        // Header container
        builder.WithContainer([
            new TextDisplayBuilder($"# {emoji} Weather for {locationDisplay}"),
            new TextDisplayBuilder($"-# {condition} • Updated at {DateTime.Parse(current.Time):HH:mm}")
        ], Mewdeko.OkColor);

        // Single select menu with all options
        var weatherOptions = new List<SelectMenuOptionBuilder>
        {
            new(Strings.CurrentConditions(ctx.Guild.Id), "current:0",
                Strings.LiveWeatherData(ctx.Guild.Id), isDefault: viewType == "current")
        };

        // Add daily options
        for (var i = 0; i < Math.Min(weatherData.Daily.Time.Count, 7); i++)
        {
            var date = DateTime.Parse(weatherData.Daily.Time[i]);
            var label = i == 0 ? $"{Strings.Today(ctx.Guild.Id)} ({date:MMM dd})" :
                i == 1 ? $"{Strings.Tomorrow(ctx.Guild.Id)} ({date:MMM dd})" : date.ToString("ddd, MMM dd");
            var tempMax = weatherData.Daily.TemperatureMax[i];
            var tempMin = weatherData.Daily.TemperatureMin[i];

            weatherOptions.Add(new SelectMenuOptionBuilder()
                .WithLabel(label)
                .WithDescription(
                    $"{Strings.High(ctx.Guild.Id)}: {tempMax:F1}°C, {Strings.Low(ctx.Guild.Id)}: {tempMin:F1}°C")
                .WithValue($"daily:{i}")
                .WithDefault(viewType == "daily" && index == i));
        }

        weatherOptions.Add(new SelectMenuOptionBuilder(Strings.HourlyForecast(ctx.Guild.Id), "hourly:0",
            Strings.DetailedHourlyConditions(ctx.Guild.Id), isDefault: viewType == "hourly"));

        builder.WithActionRow([
            new SelectMenuBuilder($"weather_main_select:{current.LocationName}", weatherOptions,
                Strings.SelectForecastPeriod(ctx.Guild.Id))
        ]);

        // Build content based on view type
        switch (viewType)
        {
            case "current":
                BuildCurrentWeatherContainer(builder, current);
                BuildWeeklySummaryContainer(builder, weatherData);
                break;
            case "hourly":
                BuildHourlyWeatherContainer(builder, weatherData, index);
                break;
            case "daily":
                BuildDailyWeatherContainer(builder, weatherData, index);
                break;
        }

        builder.WithSeparator()
            .WithTextDisplay($"-# {Strings.WeatherAttribution(ctx.Guild.Id)}");

        return builder;
    }

    /// <summary>
    ///     Builds the current weather container.
    /// </summary>
    private void BuildCurrentWeatherContainer(ComponentBuilderV2 builder, CurrentWeather current)
    {
        var f = (double c) => c * 1.8 + 32;

        var tempDisplay =
            $"🌡️ **{current.Temperature:F1}°C** ({f(current.Temperature):F1}°F) • {Strings.FeelsLike(ctx.Guild.Id)} {current.ApparentTemperature:F1}°C";
        var humidityDisplay = $"💧 {Strings.Humidity(ctx.Guild.Id)}: {current.RelativeHumidity}%";
        var windDisplay = current.WindGusts.HasValue
            ? $"💨 {Strings.WindSpeed(ctx.Guild.Id)}: {current.WindSpeed:F1} km/h, {current.WindDirection}° ({Strings.WindGusts(ctx.Guild.Id)} {current.WindGusts:F1} km/h)"
            : $"💨 {Strings.WindSpeed(ctx.Guild.Id)}: {current.WindSpeed:F1} km/h, {current.WindDirection}°";

        builder.WithTextDisplay(tempDisplay)
            .WithTextDisplay(humidityDisplay)
            .WithTextDisplay(windDisplay);

        builder.WithSeparator();

        var pressureDisplay = $"📊 {Strings.Pressure(ctx.Guild.Id)}: {current.SurfacePressure:F1} hPa";
        var cloudsDisplay = $"☁️ {Strings.CloudCover(ctx.Guild.Id)}: {current.CloudCover}%";
        var dayNightDisplay = current.IsDay == 1 ? "🌞 Day" : "🌙 Night";

        builder.WithTextDisplay(pressureDisplay)
            .WithTextDisplay(cloudsDisplay)
            .WithTextDisplay(dayNightDisplay);

        if (current.Visibility.HasValue)
            builder.WithTextDisplay($"👁️ {Strings.Visibility(ctx.Guild.Id)}: {current.Visibility / 1000:F1} km");
    }

    /// <summary>
    ///     Builds the hourly weather container.
    /// </summary>
    private void BuildHourlyWeatherContainer(ComponentBuilderV2 builder, OpenMeteoWeatherResponse weatherData,
        int dayIndex)
    {
        var startHour = dayIndex * 24;
        var endHour = Math.Min(startHour + 24, weatherData.Hourly.Time.Count);

        var hourlyData = new List<string>();

        for (var i = startHour; i < endHour; i += 3)
        {
            if (i >= weatherData.Hourly.Time.Count) break;

            var time = DateTime.Parse(weatherData.Hourly.Time[i]);
            var temp = weatherData.Hourly.Temperature[i];
            var humidity = weatherData.Hourly.RelativeHumidity[i];
            var precipitation = weatherData.Hourly.PrecipitationProbability[i];
            var emoji = WeatherCodeInterpreter.GetEmoji(weatherData.Hourly.WeatherCode[i]);

            hourlyData.Add($"`{time:HH:mm}` {emoji} {temp:F1}°C • {humidity}% • {precipitation}% rain");
        }

        builder.WithTextDisplay($"## {Strings.HourlyForecast(ctx.Guild.Id)}")
            .WithTextDisplay(string.Join("\n", hourlyData));
    }

    /// <summary>
    ///     Builds the daily weather container for a specific day.
    /// </summary>
    private void BuildDailyWeatherContainer(ComponentBuilderV2 builder, OpenMeteoWeatherResponse weatherData, int index)
    {
        if (index >= weatherData.Daily.Time.Count)
            return;

        var date = DateTime.Parse(weatherData.Daily.Time[index]);
        var tempMax = weatherData.Daily.TemperatureMax[index];
        var tempMin = weatherData.Daily.TemperatureMin[index];
        var precipitation = weatherData.Daily.PrecipitationSum[index];
        var uv = weatherData.Daily.UvIndexMax[index];
        var sunrise = DateTime.Parse(weatherData.Daily.Sunrise[index]);
        var sunset = DateTime.Parse(weatherData.Daily.Sunset[index]);

        var dayLabel = index == 0 ? Strings.Today(ctx.Guild.Id) :
            index == 1 ? Strings.Tomorrow(ctx.Guild.Id) : date.ToString("dddd, MMMM dd");

        builder.WithTextDisplay($"## {dayLabel}")
            .WithTextDisplay(
                $"🌡️ **{Strings.High(ctx.Guild.Id)}**: {tempMax:F1}°C • **{Strings.Low(ctx.Guild.Id)}**: {tempMin:F1}°C")
            .WithTextDisplay($"🌧️ **{Strings.Precipitation(ctx.Guild.Id)}**: {precipitation:F1}mm")
            .WithTextDisplay($"☀️ **{Strings.UvIndex(ctx.Guild.Id)}**: {uv:F1}")
            .WithTextDisplay(
                $"🌅 **{Strings.Sunrise(ctx.Guild.Id)}**: {sunrise:HH:mm} • 🌇 **{Strings.Sunset(ctx.Guild.Id)}**: {sunset:HH:mm}");
    }

    /// <summary>
    ///     Builds the weekly summary container.
    /// </summary>
    private void BuildWeeklySummaryContainer(ComponentBuilderV2 builder, OpenMeteoWeatherResponse weatherData)
    {
        var weeklyComponents = new List<TextDisplayBuilder>();

        for (var i = 0; i < Math.Min(weatherData.Daily.Time.Count, 7); i++)
        {
            var date = DateTime.Parse(weatherData.Daily.Time[i]);
            var tempMax = weatherData.Daily.TemperatureMax[i];
            var tempMin = weatherData.Daily.TemperatureMin[i];
            var precipitation = weatherData.Daily.PrecipitationSum[i];
            var uv = weatherData.Daily.UvIndexMax[i];

            var dayLabel = i == 0 ? Strings.Today(ctx.Guild.Id) :
                i == 1 ? Strings.Tomorrow(ctx.Guild.Id) : date.ToString("ddd");
            var precipText = precipitation > 0 ? $" • {precipitation:F1}mm" : "";
            var uvText = uv > 5 ? $" • UV: {uv:F1}" : "";

            weeklyComponents.Add(
                new TextDisplayBuilder($"`{dayLabel,-9}` {tempMax:F1}°/{tempMin:F1}°C{precipText}{uvText}"));
        }

        builder.WithContainer([
            new TextDisplayBuilder($"## {Strings.ForecastOutlook(ctx.Guild.Id)}"),
            ..weeklyComponents
        ], new Color(46, 204, 113));
    }
}