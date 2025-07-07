﻿using System.IO;
using System.Net;
using System.Net.Http;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Google.Cloud.Vision.V1;
using Grpc.Auth;
using Grpc.Core;
using Newtonsoft.Json.Linq;
using Image = Google.Cloud.Vision.V1.Image;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Google API service.
/// </summary>
public class GoogleApiService : IGoogleApiService
{
    private readonly IBotCredentials creds;
    private readonly IHttpClientFactory httpFactory;


    private readonly Dictionary<string?, string> languageDictionary = new()
    {
        {
            "afrikaans", "af"
        },
        {
            "albanian", "sq"
        },
        {
            "arabic", "ar"
        },
        {
            "armenian", "hy"
        },
        {
            "azerbaijani", "az"
        },
        {
            "basque", "eu"
        },
        {
            "belarusian", "be"
        },
        {
            "bengali", "bn"
        },
        {
            "bulgarian", "bg"
        },
        {
            "catalan", "ca"
        },
        {
            "chinese-traditional", "zh-TW"
        },
        {
            "chinese-simplified", "zh-CN"
        },
        {
            "chinese", "zh-CN"
        },
        {
            "croatian", "hr"
        },
        {
            "czech", "cs"
        },
        {
            "danish", "da"
        },
        {
            "dutch", "nl"
        },
        {
            "english", "en"
        },
        {
            "esperanto", "eo"
        },
        {
            "estonian", "et"
        },
        {
            "filipino", "tl"
        },
        {
            "finnish", "fi"
        },
        {
            "french", "fr"
        },
        {
            "galician", "gl"
        },
        {
            "german", "de"
        },
        {
            "georgian", "ka"
        },
        {
            "greek", "el"
        },
        {
            "haitian Creole", "ht"
        },
        {
            "hebrew", "iw"
        },
        {
            "hindi", "hi"
        },
        {
            "hungarian", "hu"
        },
        {
            "icelandic", "is"
        },
        {
            "indonesian", "id"
        },
        {
            "irish", "ga"
        },
        {
            "italian", "it"
        },
        {
            "japanese", "ja"
        },
        {
            "korean", "ko"
        },
        {
            "lao", "lo"
        },
        {
            "latin", "la"
        },
        {
            "latvian", "lv"
        },
        {
            "lithuanian", "lt"
        },
        {
            "macedonian", "mk"
        },
        {
            "malay", "ms"
        },
        {
            "maltese", "mt"
        },
        {
            "norwegian", "no"
        },
        {
            "persian", "fa"
        },
        {
            "polish", "pl"
        },
        {
            "portuguese", "pt"
        },
        {
            "romanian", "ro"
        },
        {
            "russian", "ru"
        },
        {
            "serbian", "sr"
        },
        {
            "slovak", "sk"
        },
        {
            "slovenian", "sl"
        },
        {
            "spanish", "es"
        },
        {
            "swahili", "sw"
        },
        {
            "swedish", "sv"
        },
        {
            "tamil", "ta"
        },
        {
            "telugu", "te"
        },
        {
            "thai", "th"
        },
        {
            "turkish", "tr"
        },
        {
            "ukrainian", "uk"
        },
        {
            "urdu", "ur"
        },
        {
            "vietnamese", "vi"
        },
        {
            "welsh", "cy"
        },
        {
            "yiddish", "yi"
        },
        {
            "af", "af"
        },
        {
            "sq", "sq"
        },
        {
            "ar", "ar"
        },
        {
            "hy", "hy"
        },
        {
            "az", "az"
        },
        {
            "eu", "eu"
        },
        {
            "be", "be"
        },
        {
            "bn", "bn"
        },
        {
            "bg", "bg"
        },
        {
            "ca", "ca"
        },
        {
            "zh-tw", "zh-TW"
        },
        {
            "zh-cn", "zh-CN"
        },
        {
            "hr", "hr"
        },
        {
            "cs", "cs"
        },
        {
            "da", "da"
        },
        {
            "nl", "nl"
        },
        {
            "en", "en"
        },
        {
            "eo", "eo"
        },
        {
            "et", "et"
        },
        {
            "tl", "tl"
        },
        {
            "fi", "fi"
        },
        {
            "fr", "fr"
        },
        {
            "gl", "gl"
        },
        {
            "de", "de"
        },
        {
            "ka", "ka"
        },
        {
            "el", "el"
        },
        {
            "ht", "ht"
        },
        {
            "iw", "iw"
        },
        {
            "hi", "hi"
        },
        {
            "hu", "hu"
        },
        {
            "is", "is"
        },
        {
            "id", "id"
        },
        {
            "ga", "ga"
        },
        {
            "it", "it"
        },
        {
            "ja", "ja"
        },
        {
            "ko", "ko"
        },
        {
            "lo", "lo"
        },
        {
            "la", "la"
        },
        {
            "lv", "lv"
        },
        {
            "lt", "lt"
        },
        {
            "mk", "mk"
        },
        {
            "ms", "ms"
        },
        {
            "mt", "mt"
        },
        {
            "no", "no"
        },
        {
            "fa", "fa"
        },
        {
            "pl", "pl"
        },
        {
            "pt", "pt"
        },
        {
            "ro", "ro"
        },
        {
            "ru", "ru"
        },
        {
            "sr", "sr"
        },
        {
            "sk", "sk"
        },
        {
            "sl", "sl"
        },
        {
            "es", "es"
        },
        {
            "sw", "sw"
        },
        {
            "sv", "sv"
        },
        {
            "ta", "ta"
        },
        {
            "te", "te"
        },
        {
            "th", "th"
        },
        {
            "tr", "tr"
        },
        {
            "uk", "uk"
        },
        {
            "ur", "ur"
        },
        {
            "vi", "vi"
        },
        {
            "cy", "cy"
        },
        {
            "yi", "yi"
        }
    };

    private readonly ILogger<GoogleApiService> logger;

    private readonly ImageAnnotatorClient? visionClient;

    private readonly YouTubeService yt;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GoogleApiService" /> class.
    /// </summary>
    /// <param name="creds">Bot credentials.</param>
    /// <param name="factory">HTTP client factory.</param>
    public GoogleApiService(IBotCredentials creds, IHttpClientFactory factory, ILogger<GoogleApiService> logger)
    {
        this.creds = creds;
        httpFactory = factory;
        this.logger = logger;

        var bcs = new BaseClientService.Initializer
        {
            ApplicationName = "Mewdeko Bot", ApiKey = this.creds.GoogleApiKey
        };

        try
        {
            var credential = GoogleCredential.FromFile(Path.Combine(Directory.GetCurrentDirectory(), "gcreds.json"))
                .CreateScoped(ImageAnnotatorClient.DefaultScopes);

            visionClient = new ImageAnnotatorClientBuilder
            {
                ChannelCredentials = credential.ToChannelCredentials()
            }.Build();
        }
        catch (Exception)
        {
            logger.LogError("Google Cloud Credentials not found. Image command will be unfiltered.");
            visionClient = null;
        }

        yt = new YouTubeService(bcs);
    }


    /// <inheritdoc />
    public bool IsImageSafe(SafeSearchAnnotation annotation)
    {
        // Adjust thresholds as needed based on your application's requirements
        return annotation.Adult != Likelihood.Likely && annotation.Adult != Likelihood.VeryLikely &&
               annotation.Violence != Likelihood.Likely && annotation.Violence != Likelihood.VeryLikely &&
               annotation.Racy != Likelihood.Likely && annotation.Racy != Likelihood.VeryLikely;
    }


    /// <summary>
    ///     Performs Safe Search detection on the specified image URL using the Google Cloud Vision API.
    /// </summary>
    /// <param name="imageUrl">The URL of the image to analyze.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains a <see cref="SafeSearchAnnotation" />
    ///     object
    ///     with the likelihoods of various types of inappropriate content.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="imageUrl" /> is null or empty.</exception>
    /// <exception cref="RpcException">Thrown when there is an error in the Vision API call.</exception>
    public async Task<SafeSearchAnnotation> DetectSafeSearchAsync(string imageUrl)
    {
        if (visionClient is null)
            return new SafeSearchAnnotation();
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ArgumentNullException(nameof(imageUrl), "Image URL cannot be null or empty.");

        // Create an Image object with the image URL
        var image = Image.FromUri(imageUrl);

        // Perform Safe Search detection
        var response = await visionClient.DetectSafeSearchAsync(image);

        return response;
    }


    /// <summary>
    ///     Gets video links by keyword.
    /// </summary>
    /// <param name="keywords">The keywords.</param>
    /// <returns>Array of search results.</returns>
    /// <exception cref="ArgumentNullException">keywords</exception>
    public async Task<SearchResult[]> GetVideoLinksByKeywordAsync(string keywords)
    {
        await Task.Yield();
        if (string.IsNullOrWhiteSpace(keywords))
            throw new ArgumentNullException(nameof(keywords));

        var query = yt.Search.List("snippet");
        query.MaxResults = 10;
        query.Q = keywords;
        query.Type = "video";
        query.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.Strict;

        return (await query.ExecuteAsync().ConfigureAwait(false)).Items.ToArray();
    }


    /// <summary>
    ///     Gets the list of supported languages.
    /// </summary>
    public IEnumerable<string?> Languages
    {
        get
        {
            return languageDictionary.Keys.OrderBy(x => x);
        }
    }

    /// <summary>
    ///     Translates the given text.
    /// </summary>
    /// <param name="sourceText">The source text.</param>
    /// <param name="sourceLanguage">The source language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <returns>The translated text.</returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<string> Translate(string sourceText, string sourceLanguage, string targetLanguage)
    {
        await Task.Yield();
        string text;

        if (!languageDictionary.ContainsKey(sourceLanguage) ||
            !languageDictionary.ContainsKey(targetLanguage))
        {
            throw new ArgumentException($"{nameof(sourceLanguage)}/{nameof(targetLanguage)}");
        }

        var url = new Uri(
            $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={ConvertToLanguageCode(sourceLanguage)}&tl={ConvertToLanguageCode(targetLanguage)}&dt=t&q={WebUtility.UrlEncode(sourceText)}");
        using (var http = httpFactory.CreateClient())
        {
            http.DefaultRequestHeaders.Add("user-agent",
                "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36");
            text = await http.GetStringAsync(url).ConfigureAwait(false);
        }

        return string.Concat(JArray.Parse(text)[0].Select(x => x[0]));
    }

    private string? ConvertToLanguageCode(string language)
    {
        languageDictionary.TryGetValue(language, out var mode);
        return mode;
    }
}