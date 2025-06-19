using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Mewdeko.Modules.Patreon.Common;
using Serilog;

namespace Mewdeko.Modules.Patreon.Services;

/// <summary>
/// Client for interacting with the Patreon API v2
/// </summary>
public class PatreonApiClient : INService
{
    private const string BaseUrl = "https://www.patreon.com/api/oauth2/v2";
    private const string OAuthUrl = "https://www.patreon.com/oauth2";
    private const string OAuthTokenUrl = "https://www.patreon.com/api/oauth2/token";
    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the PatreonApiClient class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public PatreonApiClient(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Gets the OAuth authorization URL for Patreon
    /// </summary>
    /// <param name="clientId">Patreon client ID</param>
    /// <param name="redirectUri">OAuth redirect URI</param>
    /// <param name="state">State parameter for OAuth flow</param>
    /// <returns>Authorization URL</returns>
    public string GetAuthorizationUrl(string clientId, string redirectUri, string state)
    {
        var scope = "identity campaigns w:campaigns.webhook campaigns.members";

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["response_type"] = "code";
        query["client_id"] = clientId;
        query["redirect_uri"] = redirectUri;
        query["scope"] = scope;
        query["state"] = state;

        return $"{OAuthUrl}/authorize?{query}";
    }

    /// <summary>
    /// Exchanges authorization code for access token
    /// </summary>
    /// <param name="code">Authorization code from OAuth callback</param>
    /// <param name="clientId">Patreon client ID</param>
    /// <param name="clientSecret">Patreon client secret</param>
    /// <param name="redirectUri">OAuth redirect URI</param>
    /// <returns>Token response</returns>
    public async Task<PatreonTokenResponse?> ExchangeCodeForTokenAsync(string code, string clientId,
        string clientSecret, string redirectUri)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();

            var requestData = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri
            };

            var content = new FormUrlEncodedContent(requestData);
            var response = await client.PostAsync($"{OAuthTokenUrl}", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to exchange Patreon code for token: {StatusCode} - {Content}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PatreonTokenResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exchanging Patreon authorization code for token");
            return null;
        }
    }

    /// <summary>
    /// Refreshes an access token using refresh token
    /// </summary>
    /// <param name="refreshToken">The refresh token</param>
    /// <param name="clientId">Patreon client ID</param>
    /// <param name="clientSecret">Patreon client secret</param>
    /// <returns>New token response</returns>
    public async Task<PatreonTokenResponse?> RefreshTokenAsync(string refreshToken, string clientId,
        string clientSecret)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();

            var requestData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            };

            var content = new FormUrlEncodedContent(requestData);
            var response = await client.PostAsync($"{OAuthUrl}/token", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to refresh Patreon token: {StatusCode} - {Content}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PatreonTokenResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error refreshing Patreon token");
            return null;
        }
    }

    /// <summary>
    /// Gets the current user's campaigns
    /// </summary>
    /// <param name="accessToken">Access token</param>
    /// <returns>List of campaigns</returns>
    public async Task<List<PatreonCampaign>?> GetCampaignsAsync(string accessToken)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);

            var includes = "tiers,goals,creator";
            var fields =
                "campaign[created_at,creation_name,discord_server_id,image_small_url,image_url,is_charged_immediately,is_monthly,main_video_url,one_liner,patron_count,pay_per_name,pledge_sum,pledge_url,published_at,summary,thanks_embed,thanks_msg,thanks_video_url,url]";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["include"] = includes;
            query["fields"] = fields;

            var response = await client.GetAsync($"{BaseUrl}/campaigns?{query}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to get Patreon campaigns: {StatusCode} - {Content}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<PatreonApiResponse<List<PatreonCampaign>>>(responseContent,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon campaigns");
            return null;
        }
    }

    /// <summary>
    /// Gets members/supporters for a campaign
    /// </summary>
    /// <param name="accessToken">Access token</param>
    /// <param name="campaignId">Campaign ID</param>
    /// <param name="cursor">Pagination cursor</param>
    /// <returns>Campaign members</returns>
    public async Task<PatreonApiResponse<List<PatreonMember>>?> GetCampaignMembersAsync(string accessToken,
        string campaignId, string? cursor = null)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);

            var includes = "currently_entitled_tiers,user";
            var fields =
                "member[currently_entitled_amount_cents,email,full_name,is_follower,last_charge_date,last_charge_status,lifetime_support_cents,next_charge_date,note,patron_status,pledge_relationship_start,will_pay_amount_cents]";
            fields += ",user[email,first_name,full_name,image_url,last_name,social_connections,thumb_url,url,vanity]";
            fields +=
                ",tier[amount_cents,created_at,description,discord_role_ids,image_url,patron_count,post_count,published,title,url]";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["include"] = includes;
            query["fields"] = fields;
            query["page[count]"] = "1000"; // Max allowed

            if (!string.IsNullOrEmpty(cursor))
            {
                query["page[cursor]"] = cursor;
            }

            var response = await client.GetAsync($"{BaseUrl}/campaigns/{campaignId}/members?{query}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to get Patreon campaign members: {StatusCode} - {Content}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<PatreonApiResponse<List<PatreonMember>>>(responseContent,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return apiResponse;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon campaign members");
            return null;
        }
    }

    /// <summary>
    /// Gets tiers for a campaign
    /// </summary>
    /// <param name="accessToken">Access token</param>
    /// <param name="campaignId">Campaign ID</param>
    /// <returns>Campaign tiers</returns>
    public async Task<List<PatreonTierData>?> GetCampaignTiersAsync(string accessToken, string campaignId)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);

            var fields =
                "tier[amount_cents,created_at,description,discord_role_ids,image_url,patron_count,post_count,published,title,url]";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["fields"] = fields;

            var response = await client.GetAsync($"{BaseUrl}/campaigns/{campaignId}/tiers?{query}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to get Patreon campaign tiers: {StatusCode} - {Content}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<PatreonApiResponse<List<PatreonTierData>>>(responseContent,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon campaign tiers");
            return null;
        }
    }

    /// <summary>
    /// Gets goals for a campaign
    /// </summary>
    /// <param name="accessToken">Access token</param>
    /// <param name="campaignId">Campaign ID</param>
    /// <returns>Campaign goals</returns>
    public async Task<List<PatreonGoalData>?> GetCampaignGoalsAsync(string accessToken, string campaignId)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);

            var fields = "goal[amount_cents,completed_percentage,created_at,description,reached_at,title]";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["fields"] = fields;

            var response = await client.GetAsync($"{BaseUrl}/campaigns/{campaignId}/goals?{query}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to get Patreon campaign goals: {StatusCode} - {Content}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<PatreonApiResponse<List<PatreonGoalData>>>(responseContent,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon campaign goals");
            return null;
        }
    }

    /// <summary>
    /// Gets campaign details
    /// </summary>
    /// <param name="accessToken">Access token</param>
    /// <param name="campaignId">Campaign ID</param>
    /// <returns>Campaign details</returns>
    public async Task<PatreonCampaign?> GetCampaignAsync(string accessToken, string campaignId)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);

            var includes = "tiers,goals,creator";
            var fields =
                "campaign[created_at,creation_name,discord_server_id,image_small_url,image_url,is_charged_immediately,is_monthly,main_video_url,one_liner,patron_count,pay_per_name,pledge_sum,pledge_url,published_at,summary,thanks_embed,thanks_msg,thanks_video_url,url]";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["include"] = includes;
            query["fields"] = fields;

            var response = await client.GetAsync($"{BaseUrl}/campaigns/{campaignId}?{query}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to get Patreon campaign: {StatusCode} - {Content}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<PatreonApiResponse<PatreonCampaign>>(responseContent,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon campaign");
            return null;
        }
    }

    /// <summary>
    /// Gets the current user's identity
    /// </summary>
    /// <param name="accessToken">Access token</param>
    /// <returns>User identity</returns>
    public async Task<PatreonApiResponse<PatreonUser>?> GetUserIdentityAsync(string accessToken)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);

            var fields = "user[email,first_name,full_name,image_url,last_name,social_connections,url,vanity]";
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["fields"] = fields;

            var response = await client.GetAsync($"{BaseUrl}/identity?{query}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to get Patreon user identity: {StatusCode} - {Content}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PatreonApiResponse<PatreonUser>>(responseContent,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon user identity");
            return null;
        }
    }

    /// <summary>
    /// Gets the current user's campaigns
    /// </summary>
    /// <param name="accessToken">Access token</param>
    /// <returns>User's campaigns</returns>
    public async Task<PatreonApiResponse<List<PatreonCampaign>>?> GetUserCampaignsAsync(string accessToken)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);

            var includes = "tiers,goals";
            var fields =
                "campaign[created_at,creation_name,discord_server_id,image_small_url,image_url,is_charged_immediately,is_monthly,main_video_url,one_liner,patron_count,pay_per_name,pledge_sum,pledge_url,published_at,summary,url]";

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["include"] = includes;
            query["fields"] = fields;

            var response = await client.GetAsync($"{BaseUrl}/campaigns?{query}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to get Patreon user campaigns: {StatusCode} - {Content}",
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PatreonApiResponse<List<PatreonCampaign>>>(responseContent,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Patreon user campaigns");
            return null;
        }
    }

    /// <summary>
    /// Creates an authenticated HTTP client
    /// </summary>
    /// <param name="accessToken">Access token</param>
    /// <returns>Authenticated HTTP client</returns>
    private HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("User-Agent", "Mewdeko-Bot/1.0");
        return client;
    }
}