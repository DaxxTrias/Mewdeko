using Google.Apis.YouTube.v3.Data;
using Google.Cloud.Vision.V1;

namespace Mewdeko.Services;

/// <summary>
///     Represents a service for interacting with the Google API.
/// </summary>
public interface IGoogleApiService : INService
{
    /// <summary>
    ///     Gets the list of supported languages.
    /// </summary>
    public IEnumerable<string?> Languages { get; }

    /// <summary>
    ///     Translates the given text from the source language to the target language.
    /// </summary>
    /// <param name="sourceText">The text to translate.</param>
    /// <param name="sourceLanguage">The source language of the text (optional).</param>
    /// <param name="targetLanguage">The target language of the translation.</param>
    /// <returns>The translated text.</returns>
    public Task<string> Translate(string sourceText, string? sourceLanguage, string? targetLanguage);

    /// <summary>
    ///     Searches for videos on YouTube based on the given keywords.
    /// </summary>
    /// <param name="keywords">The keywords to search for.</param>
    /// <returns>An array of search results.</returns>
    public Task<SearchResult[]> GetVideoLinksByKeywordAsync(string keywords);

    /// <summary>
    ///     Gets an images safesearch param
    /// </summary>
    /// <param name="imageUrl">The image to check</param>
    /// <returns></returns>
    public Task<SafeSearchAnnotation> DetectSafeSearchAsync(string imageUrl);

    /// <summary>
    ///     Determines whether an image is considered safe based on the likelihoods in the <see cref="SafeSearchAnnotation" />.
    /// </summary>
    /// <param name="annotation">The <see cref="SafeSearchAnnotation" /> containing likelihoods of inappropriate content.</param>
    /// <returns>
    ///     <c>true</c> if the image is considered safe; otherwise, <c>false</c>.
    ///     An image is considered unsafe if any of the specified content types are likely or very likely.
    /// </returns>
    public bool IsImageSafe(SafeSearchAnnotation annotation);
}