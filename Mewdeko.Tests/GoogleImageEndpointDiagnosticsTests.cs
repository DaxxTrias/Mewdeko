using System.Net;
using GScraper;
using GScraper.Google;

namespace Mewdeko.Tests;

[TestFixture]
[Explicit("Manual diagnostic test. Run explicitly when investigating Google endpoint behavior.")]
public class GoogleImageEndpointDiagnosticsTests
{
    private const string LiveTestEnvVar = "MEWDEKO_RUN_LIVE_GOOGLE_IMAGE_TEST";
    private const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";

    [Test]
    [Category("Integration")]
    [CancelAfter(30000)]
    public async Task GoogleImageEndpoint_ForApple_ShouldReturnSuccess()
    {
        EnsureLiveTestEnabled();

        using var client = new HttpClient(new HttpClientHandler
        {
            UseCookies = true, AllowAutoRedirect = true
        });

        client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Cookie",
            "CONSENT=YES+; SOCS=CAESEwgDEgk0OTA3Nzk3MjMaAmVuIAEaBgiA_LysBg");

        var requestUri = BuildGoogleImageRequestUri("apple");
        using var response = await client.GetAsync(requestUri).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            () =>
                $"Google endpoint returned {(int)response.StatusCode} {response.StatusCode} for '{requestUri}'.\n" +
                $"Body preview:\n{Truncate(body, 1200)}");

        // Google's async response generally starts with an anti-XSSI prefix.
        Assert.That(
            body.StartsWith(")]}'", StringComparison.Ordinal),
            Is.True,
            () => $"Unexpected response prefix. Body preview:\n{Truncate(body, 400)}");
    }

    [Test]
    [Category("Integration")]
    [CancelAfter(30000)]
    public async Task GoogleScraper_ForApple_ShouldReturnImageResults()
    {
        EnsureLiveTestEnabled();

        using var scraper = new GoogleScraper();

        try
        {
            var results = (await scraper.GetImagesAsync("apple", SafeSearchLevel.Strict).ConfigureAwait(false)).ToList();
            Assert.That(results, Is.Not.Empty, "GoogleScraper returned no image results for query 'apple'.");
        }
        catch (HttpRequestException ex)
        {
            var status = ex.StatusCode is null ? "n/a" : ((int)ex.StatusCode).ToString();
            Assert.Fail($"GoogleScraper failed with HTTP status {status}: {ex.Message}");
        }
    }

    private static void EnsureLiveTestEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(LiveTestEnvVar), "1", StringComparison.Ordinal))
        {
            Assert.Ignore(
                $"Live endpoint test skipped. Set environment variable {LiveTestEnvVar}=1 to run.");
        }
    }

    private static Uri BuildGoogleImageRequestUri(string query)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var path = $"?q={encodedQuery}&asearch=isch&async=_fmt:json,p:1&tbs=,,,,,&safe=active";
        return new Uri("https://www.google.com/search" + path, UriKind.Absolute);
    }

    private static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            return value;

        return value[..maxChars] + "...";
    }
}
