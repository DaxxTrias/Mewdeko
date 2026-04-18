using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace GScraper.Google;

// TODO: Add support for cancellation tokens and regular search method

/// <summary>
/// Represents a Google Search scraper.
/// </summary>
public class GoogleScraper : IDisposable
{
    /// <summary>
    /// Returns the default API endpoint.
    /// </summary>
    public const string DefaultApiEndpoint = "https://www.google.com/search";

    //private const string _defaultUserAgent = "NSTN/3.62.475170463.release Dalvik/2.1.0 (Linux; U; Android 12) Mobile";
    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";
    private static readonly Uri _defaultBaseAddress = new(DefaultApiEndpoint);

    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleScraper"/> class.
    /// </summary>
    public GoogleScraper()
        : this(new HttpClient(new HttpClientHandler { UseCookies = true, AllowAutoRedirect = true }))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleScraper"/> class using the provided <see cref="HttpClient"/>.
    /// </summary>
    public GoogleScraper(HttpClient client)
    {
        _httpClient = client;
        Init(_httpClient, _defaultBaseAddress);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleScraper"/> class using the provided <see cref="HttpClient"/> and API endpoint.
    /// </summary>
    [Obsolete("This constructor is deprecated and it will be removed in a future version. Use GoogleScraper(HttpClient) instead.")]
    public GoogleScraper(HttpClient client, string apiEndpoint)
    {
        _httpClient = client;
        Init(_httpClient, new Uri(apiEndpoint));
    }

    private void Init(HttpClient client, Uri apiEndpoint)
    {
        GScraperGuards.NotNull(client, nameof(client));
        GScraperGuards.NotNull(apiEndpoint, nameof(apiEndpoint));

        if (client.BaseAddress is null)
        {
            try
            {
                client.BaseAddress = apiEndpoint;
            }
            catch (InvalidOperationException)
            {
                // HttpClient already started a request, can't set BaseAddress anymore.
                // We'll use absolute URLs instead.
            }
        }

        if (client.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            try
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(_defaultUserAgent);
            }
            catch (InvalidOperationException)
            {
                // HttpClient already started a request, can't modify headers anymore.
            }
        }

        try
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", "CONSENT=YES+; SOCS=CAESEwgDEgk0OTA3Nzk3MjMaAmVuIAEaBgiA_LysBg");
        }
        catch (InvalidOperationException)
        {
            // HttpClient already started a request, can't modify headers anymore.
        }
    }

    /// <summary>
    /// Gets images from Google Images.
    /// </summary>
    /// <remarks>This method returns at most 100 image results.</remarks>
    /// <param name="query">The search query.</param>
    /// <param name="safeSearch">The safe search level.</param>
    /// <param name="size">The image size.</param>
    /// <param name="color">The image color. <see cref="GoogleImageColors"/> contains the colors that can be used here.</param>
    /// <param name="type">The image type.</param>
    /// <param name="time">The image time.</param>
    /// <param name="license">The image license. <see cref="GoogleImageLicenses"/> contains the licenses that can be used here.</param>
    /// <param name="language">The language code to use. <see cref="GoogleLanguages"/> contains the language codes that can be used here.</param>
    /// <returns>A task representing the asynchronous operation. The result contains an <see cref="IEnumerable{T}"/> of <see cref="GScraper.GoogleImageResult"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null or empty.</exception>
    /// <exception cref="GScraperException">An error occurred during the scraping process.</exception>
    public async Task<IEnumerable<GoogleImageResult>> GetImagesAsync(string query, SafeSearchLevel safeSearch = SafeSearchLevel.Off, GoogleImageSize size = GoogleImageSize.Any,
        string? color = null, GoogleImageType type = GoogleImageType.Any, GoogleImageTime time = GoogleImageTime.Any,
        string? license = null, string? language = null)
    {
        // TODO: Use pagination
        GScraperGuards.NotNull(query, nameof(query));

        var uri = new Uri(_httpClient.BaseAddress ?? _defaultBaseAddress, BuildImageQuery(query, safeSearch, size, color, type, time, license, language));

        byte[] bytes = await _httpClient.GetByteArrayAsync(uri).ConfigureAwait(false);
        var images = JsonSerializer.Deserialize(bytes.AsSpan(5, bytes.Length - 5), GoogleImageSearchResponseContext.Default.GoogleImageSearchResponse)!.Ischj.Metadata;
        images?.RemoveAll(static x => !x.Url.StartsWith("http"));

        return images is null ? Array.Empty<GoogleImageResultModel>() : images.AsReadOnly();
    }

    private static string BuildImageQuery(string query, SafeSearchLevel safeSearch, GoogleImageSize size, string? color,
        GoogleImageType type, GoogleImageTime time, string? license, string? language)
    {
        //string url = $"?q={Uri.EscapeDataString(query)}&tbm=isch&asearch=isch&async=_fmt:json,p:1&tbs=";
        string url = $"?q={Uri.EscapeDataString(query)}&asearch=isch&async=_fmt:json,p:1&tbs=";
        
        url += size == GoogleImageSize.Any ? ',' : $"isz:{(char)size},";
        url += string.IsNullOrEmpty(color) ? ',' : $"ic:{color},";
        url += type == GoogleImageType.Any ? ',' : $"itp:{type.ToString().ToLowerInvariant()},";
        url += time == GoogleImageTime.Any ? ',' : $"qdr:{(char)time},";
        url += string.IsNullOrEmpty(license) ? "" : $"il:{license}";

        url += "&safe=" + safeSearch switch
        {
            SafeSearchLevel.Off => "off",
            _ => "active"
        };

        if (!string.IsNullOrEmpty(language))
            url += $"&lr=lang_{language}&hl={language}";

        return url;
    }
    
    public async Task<string> DebugGetRawResponse(string query)
    {
        var path = BuildImageQuery(query, SafeSearchLevel.Off, GoogleImageSize.Any, null, GoogleImageType.Any, GoogleImageTime.Any, null, null);
        var fullUrl = new Uri(_httpClient.BaseAddress!, path);
        Console.WriteLine($"Full URL: {fullUrl}");
    
        var response = await _httpClient.GetAsync(fullUrl);
        Console.WriteLine($"Status: {response.StatusCode}");
        return await response.Content.ReadAsStringAsync();
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc cref="Dispose()"/>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
            _httpClient.Dispose();

        _disposed = true;
    }
}