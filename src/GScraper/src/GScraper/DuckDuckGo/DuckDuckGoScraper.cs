using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GScraper.DuckDuckGo;

// TODO: Add support for cancellation tokens and regular search method

/// <summary>
/// Represents a DuckDuckGo scraper.
/// </summary>
public class DuckDuckGoScraper : IDisposable
{
    /// <summary>
    /// Returns the default API endpoint.
    /// </summary>
    public const string DefaultApiEndpoint = "https://duckduckgo.com";

    /// <summary>
    /// Returns the maximum query length.
    /// </summary>
    public const int MaxQueryLength = 500;

    private static ReadOnlySpan<byte> TokenStart => "vqd=\""u8;

    private const string _defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";
    private static  readonly Uri _defaultBaseAddress = new(DefaultApiEndpoint);

    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckDuckGoScraper"/> class.
    /// </summary>
    public DuckDuckGoScraper()
        : this(new HttpClient())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckDuckGoScraper"/> class using the provided <see cref="HttpClient"/>.
    /// </summary>
    public DuckDuckGoScraper(HttpClient client)
    {
        _httpClient = client;
        Init(_httpClient, _defaultBaseAddress);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckDuckGoScraper"/> class using the provided <see cref="HttpClient"/> and API endpoint.
    /// </summary>
    [Obsolete("This constructor is deprecated and it will be removed in a future version. Use DuckDuckGoScraper(HttpClient) instead.")]
    public DuckDuckGoScraper(HttpClient client, string apiEndpoint)
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
            client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://duckduckgo.com/");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/javascript, */*; q=0.01");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        }
        catch (InvalidOperationException)
        {
            // HttpClient already started a request, can't modify headers anymore.
        }
    }

    /// <summary>
    /// Gets images from DuckDuckGo.
    /// </summary>
    /// <remarks>This method returns at most 100 image results.</remarks>
    /// <param name="query">The search query.</param>
    /// <param name="safeSearch">The safe search level.</param>
    /// <param name="time">The image time.</param>
    /// <param name="size">The image size.</param>
    /// <param name="color">The image color.</param>
    /// <param name="type">The image type.</param>
    /// <param name="layout">The image layout.</param>
    /// <param name="license">The image license.</param>
    /// <param name="region">The region. <see cref="DuckDuckGoRegions"/> contains the regions that can be used here.</param>
    /// <returns>A task representing the asynchronous operation. The result contains an <see cref="IEnumerable{T}"/> of <see cref="DuckDuckGoImageResult"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="query"/> is larger than <see cref="MaxQueryLength"/>.</exception>
    /// <exception cref="GScraperException">An error occurred during the scraping process.</exception>
    public async Task<IEnumerable<DuckDuckGoImageResult>> GetImagesAsync(string query, SafeSearchLevel safeSearch = SafeSearchLevel.Moderate,
        DuckDuckGoImageTime time = DuckDuckGoImageTime.Any, DuckDuckGoImageSize size = DuckDuckGoImageSize.All, DuckDuckGoImageColor color = DuckDuckGoImageColor.All,
        DuckDuckGoImageType type = DuckDuckGoImageType.All, DuckDuckGoImageLayout layout = DuckDuckGoImageLayout.All, DuckDuckGoImageLicense license = DuckDuckGoImageLicense.All,
        string region = DuckDuckGoRegions.UsEnglish)
    {
        GScraperGuards.NotNull(query, nameof(query));
        GScraperGuards.NotNullOrEmpty(region, nameof(region));
        GScraperGuards.ArgumentInRange(query.Length, MaxQueryLength, nameof(query), $"The query cannot be larger than {MaxQueryLength}.");

        string token = await GetTokenAsync(query).ConfigureAwait(false);
        var uri = new Uri(_httpClient.BaseAddress ?? _defaultBaseAddress, BuildImageQuery(token, query, safeSearch, time, size, color, type, layout, license, region));

        using var stream = await _httpClient.GetStreamAsync(uri).ConfigureAwait(false);

        var response = (await JsonSerializer.DeserializeAsync(stream, DuckDuckGoImageSearchResponseContext.Default.DuckDuckGoImageSearchResponse).ConfigureAwait(false))!;

        return Array.AsReadOnly(response.Results);
    }

    private static string BuildImageQuery(string token, string query, SafeSearchLevel safeSearch, DuckDuckGoImageTime time, DuckDuckGoImageSize size,
        DuckDuckGoImageColor color, DuckDuckGoImageType type, DuckDuckGoImageLayout layout, DuckDuckGoImageLicense license, string region)
    {
        string url = $"i.js?l={region}" +
                     "&o=json" +
                     $"&q={Uri.EscapeDataString(query)}" +
                     $"&vqd={token}" +
                     "&f=";

        url += time == DuckDuckGoImageTime.Any ? ',' : $"time:{time},";
        url += size == DuckDuckGoImageSize.All ? ',' : $"size:{size},";
        url += color == DuckDuckGoImageColor.All ? ',' : $"color:{color.ToString().ToLowerInvariant()},";
        url += type == DuckDuckGoImageType.All ? ',' : $"type:{type},";
        url += layout == DuckDuckGoImageLayout.All ? ',' : $"layout:{layout},";
        url += license == DuckDuckGoImageLicense.All ? "" : $"license:{license}";
        url += $"&p={(safeSearch == SafeSearchLevel.Off ? "-1" : "1")}";

        return url;
    }
    
    private async Task<string> GetTokenAsync(string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_httpClient.BaseAddress ?? _defaultBaseAddress, $"?q={Uri.EscapeDataString(query)}"));
        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
    
        // Grab those cookies
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Remove("Cookie");
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", string.Join("; ", cookies));
            }
            catch (InvalidOperationException)
            {
                // HttpClient already started a request, can't modify headers anymore.
            }
        }
    
        byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        return GetToken(bytes);
    }
    
    private static string GetToken(ReadOnlySpan<byte> rawHtml)
    {
        int startIndex = rawHtml.IndexOf(TokenStart);

        if (startIndex == -1)
        {
            throw new GScraperException("Failed to get the DuckDuckGo token.", "DuckDuckGo");
        }

        var sliced = rawHtml[(startIndex + TokenStart.Length)..];
        int endIndex = sliced.IndexOf((byte)'"');

        if (endIndex == -1)
        {
            throw new GScraperException("Failed to get the DuckDuckGo token.", "DuckDuckGo");
        }

#if NETSTANDARD2_1_OR_GREATER
        return Encoding.UTF8.GetString(sliced[..endIndex]);
#else
        return Encoding.UTF8.GetString(sliced[..endIndex].ToArray());
#endif
    }
    public async Task<string> DebugGetImages(string query)
    {
        // Step 1: get token
        byte[] bytes = await _httpClient.GetByteArrayAsync(new Uri($"?q={Uri.EscapeDataString(query)}", UriKind.Relative));
        string token = GetToken(bytes);
        Console.WriteLine($"Token: {token}");

        // Step 2: try the image request
        var uri = $"i.js?l=us-en&o=json&q={Uri.EscapeDataString(query)}&vqd={token}&f=,,,,,&p=-1";
        Console.WriteLine($"Image URL: {_httpClient.BaseAddress}{uri}");
    
        var response = await _httpClient.GetAsync(new Uri(uri, UriKind.Relative));
        Console.WriteLine($"Image step status: {response.StatusCode}");
        return await response.Content.ReadAsStringAsync();
    }
    public async Task<string> DebugGetRawHtml(string query)
    {
        var response = await _httpClient.GetAsync(new Uri($"?q={Uri.EscapeDataString(query)}", UriKind.Relative));
        Console.WriteLine($"Token step status: {response.StatusCode}");
        var html = await response.Content.ReadAsStringAsync();
    
        // Search for vqd in whatever format it might be now
        var vqdIndex = html.IndexOf("vqd");
        if (vqdIndex != -1)
            Console.WriteLine($"VQD context: {html.Substring(vqdIndex, Math.Min(100, html.Length - vqdIndex))}");
        else
            Console.WriteLine("VQD not found in response");
        
        return html;
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