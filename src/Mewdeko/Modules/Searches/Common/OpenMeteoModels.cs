using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents the response from Open-Meteo Geocoding API
/// </summary>
public class OpenMeteoGeocodingResponse
{
    /// <summary>
    ///     Gets or sets the list of geocoding results.
    /// </summary>
    [JsonPropertyName("results")]
    public List<GeocodingResult> Results { get; set; } = new();
}

/// <summary>
///     Represents a single geocoding result
/// </summary>
public class GeocodingResult
{
    /// <summary>
    ///     Gets or sets the location name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the latitude coordinate.
    /// </summary>
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    /// <summary>
    ///     Gets or sets the longitude coordinate.
    /// </summary>
    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    /// <summary>
    ///     Gets or sets the country name.
    /// </summary>
    [JsonPropertyName("country")]
    public string Country { get; set; }

    /// <summary>
    ///     Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; }

    /// <summary>
    ///     Gets or sets the first-level administrative division (state, province, etc).
    /// </summary>
    [JsonPropertyName("admin1")]
    public string Admin1 { get; set; }

    /// <summary>
    ///     Gets or sets the timezone identifier.
    /// </summary>
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; }
}

/// <summary>
///     Represents the response from Open-Meteo Weather API
/// </summary>
public class OpenMeteoWeatherResponse
{
    /// <summary>
    ///     Gets or sets the latitude of the weather location.
    /// </summary>
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    /// <summary>
    ///     Gets or sets the longitude of the weather location.
    /// </summary>
    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    /// <summary>
    ///     Gets or sets the timezone identifier.
    /// </summary>
    [JsonPropertyName("timezone")]
    public string Timezone { get; set; }

    /// <summary>
    ///     Gets or sets the timezone abbreviation.
    /// </summary>
    [JsonPropertyName("timezone_abbreviation")]
    public string TimezoneAbbreviation { get; set; }

    /// <summary>
    ///     Gets or sets the current weather conditions.
    /// </summary>
    [JsonPropertyName("current")]
    public CurrentWeather Current { get; set; }

    /// <summary>
    ///     Gets or sets the units for current weather data.
    /// </summary>
    [JsonPropertyName("current_units")]
    public CurrentUnits CurrentUnits { get; set; }

    /// <summary>
    ///     Gets or sets the daily weather data.
    /// </summary>
    [JsonPropertyName("daily")]
    public DailyWeather Daily { get; set; }

    /// <summary>
    ///     Gets or sets the units for daily weather data.
    /// </summary>
    [JsonPropertyName("daily_units")]
    public DailyUnits DailyUnits { get; set; }
}

/// <summary>
///     Represents current weather conditions
/// </summary>
public class CurrentWeather
{
    /// <summary>
    ///     Gets or sets the time of the weather observation.
    /// </summary>
    [JsonPropertyName("time")]
    public string Time { get; set; }

    /// <summary>
    ///     Gets or sets the temperature at 2 meters above ground in Celsius.
    /// </summary>
    [JsonPropertyName("temperature_2m")]
    public double Temperature { get; set; }

    /// <summary>
    ///     Gets or sets the relative humidity at 2 meters above ground in percent.
    /// </summary>
    [JsonPropertyName("relative_humidity_2m")]
    public int RelativeHumidity { get; set; }

    /// <summary>
    ///     Gets or sets the apparent temperature (feels like) in Celsius.
    /// </summary>
    [JsonPropertyName("apparent_temperature")]
    public double ApparentTemperature { get; set; }

    /// <summary>
    ///     Gets or sets the WMO weather interpretation code.
    /// </summary>
    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }

    /// <summary>
    ///     Gets or sets the wind speed at 10 meters above ground in km/h.
    /// </summary>
    [JsonPropertyName("wind_speed_10m")]
    public double WindSpeed { get; set; }

    /// <summary>
    ///     Gets or sets the wind direction at 10 meters above ground in degrees.
    /// </summary>
    [JsonPropertyName("wind_direction_10m")]
    public int WindDirection { get; set; }

    /// <summary>
    ///     Gets or sets the atmospheric pressure at surface level in hPa.
    /// </summary>
    [JsonPropertyName("surface_pressure")]
    public double SurfacePressure { get; set; }

    /// <summary>
    ///     Gets or sets the total cloud cover as an area fraction in percent.
    /// </summary>
    [JsonPropertyName("cloud_cover")]
    public int CloudCover { get; set; }

    /// <summary>
    ///     Gets or sets whether it's day (1) or night (0).
    /// </summary>
    [JsonPropertyName("is_day")]
    public int IsDay { get; set; }

    /// <summary>
    ///     Gets or sets the wind gusts at 10 meters above ground in km/h.
    /// </summary>
    [JsonPropertyName("wind_gusts_10m")]
    public double? WindGusts { get; set; }

    /// <summary>
    ///     Gets or sets the visibility in meters.
    /// </summary>
    [JsonPropertyName("visibility")]
    public double? Visibility { get; set; }

    // Additional properties not from API but added for convenience
    /// <summary>
    ///     Gets or sets the location name (added from geocoding).
    /// </summary>
    [JsonIgnore]
    public string LocationName { get; set; }

    /// <summary>
    ///     Gets or sets the country name (added from geocoding).
    /// </summary>
    [JsonIgnore]
    public string Country { get; set; }

    /// <summary>
    ///     Gets or sets the country code (added from geocoding).
    /// </summary>
    [JsonIgnore]
    public string CountryCode { get; set; }
}

/// <summary>
///     Represents units for current weather data
/// </summary>
public class CurrentUnits
{
    /// <summary>
    ///     Gets or sets the temperature unit (e.g., "°C").
    /// </summary>
    [JsonPropertyName("temperature_2m")]
    public string Temperature { get; set; }

    /// <summary>
    ///     Gets or sets the relative humidity unit (e.g., "%").
    /// </summary>
    [JsonPropertyName("relative_humidity_2m")]
    public string RelativeHumidity { get; set; }

    /// <summary>
    ///     Gets or sets the wind speed unit (e.g., "km/h").
    /// </summary>
    [JsonPropertyName("wind_speed_10m")]
    public string WindSpeed { get; set; }

    /// <summary>
    ///     Gets or sets the surface pressure unit (e.g., "hPa").
    /// </summary>
    [JsonPropertyName("surface_pressure")]
    public string SurfacePressure { get; set; }
}

/// <summary>
///     Helper class to interpret WMO weather codes
/// </summary>
public static class WeatherCodeInterpreter
{
    /// <summary>
    ///     Gets a human-readable description for a WMO weather code.
    /// </summary>
    /// <param name="code">The WMO weather code.</param>
    /// <returns>A string description of the weather condition.</returns>
    public static string GetDescription(int code)
    {
        return code switch
        {
            0 => "Clear sky",
            1 => "Mainly clear",
            2 => "Partly cloudy",
            3 => "Overcast",
            45 => "Foggy",
            48 => "Depositing rime fog",
            51 => "Light drizzle",
            53 => "Moderate drizzle",
            55 => "Dense drizzle",
            56 => "Light freezing drizzle",
            57 => "Dense freezing drizzle",
            61 => "Slight rain",
            63 => "Moderate rain",
            65 => "Heavy rain",
            66 => "Light freezing rain",
            67 => "Heavy freezing rain",
            71 => "Slight snow fall",
            73 => "Moderate snow fall",
            75 => "Heavy snow fall",
            77 => "Snow grains",
            80 => "Slight rain showers",
            81 => "Moderate rain showers",
            82 => "Violent rain showers",
            85 => "Slight snow showers",
            86 => "Heavy snow showers",
            95 => "Thunderstorm",
            96 => "Thunderstorm with slight hail",
            99 => "Thunderstorm with heavy hail",
            _ => "Unknown"
        };
    }

    /// <summary>
    ///     Gets an emoji representation for a WMO weather code.
    /// </summary>
    /// <param name="code">The WMO weather code.</param>
    /// <returns>An emoji representing the weather condition.</returns>
    public static string GetEmoji(int code)
    {
        return code switch
        {
            0 => "☀️",
            1 or 2 => "🌤️",
            3 => "☁️",
            45 or 48 => "🌫️",
            51 or 53 or 55 or 56 or 57 => "🌦️",
            61 or 63 or 65 or 66 or 67 => "🌧️",
            71 or 73 or 75 or 77 => "🌨️",
            80 or 81 or 82 => "🌦️",
            85 or 86 => "🌨️",
            95 or 96 or 99 => "⛈️",
            _ => "❓"
        };
    }
}

/// <summary>
///     Represents daily weather data
/// </summary>
public class DailyWeather
{
    /// <summary>
    ///     Gets or sets the time array for daily data.
    /// </summary>
    [JsonPropertyName("time")]
    public List<string> Time { get; set; }

    /// <summary>
    ///     Gets or sets the maximum daily temperature at 2 meters above ground.
    /// </summary>
    [JsonPropertyName("temperature_2m_max")]
    public List<double> TemperatureMax { get; set; }

    /// <summary>
    ///     Gets or sets the minimum daily temperature at 2 meters above ground.
    /// </summary>
    [JsonPropertyName("temperature_2m_min")]
    public List<double> TemperatureMin { get; set; }

    /// <summary>
    ///     Gets or sets the sunrise times.
    /// </summary>
    [JsonPropertyName("sunrise")]
    public List<string> Sunrise { get; set; }

    /// <summary>
    ///     Gets or sets the sunset times.
    /// </summary>
    [JsonPropertyName("sunset")]
    public List<string> Sunset { get; set; }

    /// <summary>
    ///     Gets or sets the maximum UV index for the day.
    /// </summary>
    [JsonPropertyName("uv_index_max")]
    public List<double> UvIndexMax { get; set; }

    /// <summary>
    ///     Gets or sets the total precipitation sum for the day.
    /// </summary>
    [JsonPropertyName("precipitation_sum")]
    public List<double> PrecipitationSum { get; set; }
}

/// <summary>
///     Represents units for daily weather data
/// </summary>
public class DailyUnits
{
    /// <summary>
    ///     Gets or sets the temperature unit (e.g., "°C").
    /// </summary>
    [JsonPropertyName("temperature_2m_max")]
    public string TemperatureMax { get; set; }

    /// <summary>
    ///     Gets or sets the temperature unit (e.g., "°C").
    /// </summary>
    [JsonPropertyName("temperature_2m_min")]
    public string TemperatureMin { get; set; }

    /// <summary>
    ///     Gets or sets the UV index unit.
    /// </summary>
    [JsonPropertyName("uv_index_max")]
    public string UvIndexMax { get; set; }

    /// <summary>
    ///     Gets or sets the precipitation unit (e.g., "mm").
    /// </summary>
    [JsonPropertyName("precipitation_sum")]
    public string PrecipitationSum { get; set; }
}