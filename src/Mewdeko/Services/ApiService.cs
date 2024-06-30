using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Mewdeko.Database;

namespace Mewdeko.Services
{
    public class ApiService : INService
    {
        private readonly HttpClient _httpClient;
        private readonly ApiKeyRepository _apiKeyRepository;

        public ApiService(ApiKeyRepository apiKeyRepository)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://www.bitmex.com")
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _apiKeyRepository = apiKeyRepository;
        }

        private (string ApiKey, string ApiSecret) GetApiCredentials()
        {
            return _apiKeyRepository.GetLatestApiKey();
        }

        private async Task DumpApiResponseToFile(string responseContent, string fileName)
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            await File.WriteAllTextAsync(filePath, responseContent);
        }

        public async Task<List<UserAffiliate>> GetUserAffiliatesAsync()
        {
            string path = "/api/v1/useraffiliates";
            string verb = "GET";
            string data = "";
            string expires = GetExpires();
            var (apiKey, apiSecret) = GetApiCredentials();
            string signature = GetSignature(apiSecret, verb, path, expires, data);

            AddAuthenticationHeaders(apiKey, expires, signature);

            var response = await _httpClient.GetAsync(path);
            var content = await response.Content.ReadAsStringAsync();

            // Dump the raw API response to a file
            await DumpApiResponseToFile(content, "UserAffiliatesResponse.json");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<List<UserAffiliate>>(content);
                var userAffiliates = result ?? new List<UserAffiliate>();

                // Store the data in the database
                _apiKeyRepository.UpsertUserAffiliates(userAffiliates);

                return userAffiliates;
            }

            HandleErrorResponse(content);
            return new List<UserAffiliate>();
        }

        public async Task<GuildMemberResponse> GetGuildMembersAsync()
        {
            string path = "/api/v1/guild/me?populateMembers=true";
            string verb = "GET";
            string data = "";
            string expires = GetExpires();
            var (apiKey, apiSecret) = GetApiCredentials();
            string signature = GetSignature(apiSecret, verb, path, expires, data);

            AddAuthenticationHeaders(apiKey, expires, signature);

            var response = await _httpClient.GetAsync(path);
            var content = await response.Content.ReadAsStringAsync();

            // Dump the raw API response to a file
            await DumpApiResponseToFile(content, "GuildMembersResponse.json");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<GuildMemberResponse>(content);
                var guildMembers = result ?? new GuildMemberResponse { Members = new List<BMexMember>() };

                // Store the data in the database
                _apiKeyRepository.UpsertGuildMembers(guildMembers.Members);

                return guildMembers;
            }

            HandleErrorResponse(content);
            return new GuildMemberResponse { Members = new List<BMexMember>() };
        }

        private string GetExpires()
        {
            var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60; // Expires in 60 seconds
            return expires.ToString();
        }

        private string GetSignature(string apiSecret, string verb, string path, string expires, string data)
        {
            var message = $"{verb}{path}{expires}{data}";
            var encoding = new ASCIIEncoding();
            byte[] keyBytes = encoding.GetBytes(apiSecret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyBytes))
            {
                byte[] hashMessage = hmacsha256.ComputeHash(messageBytes);
                return BitConverter.ToString(hashMessage).Replace("-", "").ToLower();
            }
        }

        private void AddAuthenticationHeaders(string apiKey, string expires, string signature)
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("api-expires", expires);
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("api-signature", signature);
        }

        private void HandleErrorResponse(string content)
        {
            // Handle error responses from the API
            // You can log the error or throw an exception based on your requirements
        }
    }
}
