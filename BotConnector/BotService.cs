// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.PowerVirtualAgents.Samples.RelayBotSample
{
    /// <summary>
    /// Bot Service class to interact with bot
    /// </summary>
    public class BotService : IBotService
    {
        private static readonly HttpClient s_httpClient = new HttpClient();

        public string BotName { get; set; }

        public string BotId { get; set; }

        public string TenantId { get; set; }

        public string TokenEndPoint { get; set; }

        public string GetBotName()
        {
            return BotName;
        }

        /// <summary>
        /// Get directline token for connecting bot
        /// </summary>
        /// <returns>directline token as string</returns>
        public async Task<string> GetTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(TokenEndPoint))
                throw new InvalidOperationException("BotService:TokenEndPoint is missing (check appsettings.json).");
            if (string.IsNullOrWhiteSpace(BotId))
                throw new InvalidOperationException("BotService:BotId is missing (you must set it from Copilot Studio).");
            if (string.IsNullOrWhiteSpace(TenantId))
                throw new InvalidOperationException("BotService:TenantId is missing (use your AAD tenant GUID).");

            string token;
            using (var httpRequest = new HttpRequestMessage())
            {
                httpRequest.Method = HttpMethod.Get;
                UriBuilder uriBuilder = new UriBuilder(TokenEndPoint);
                var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                query["botId"] = BotId;
                query["tenantId"] = TenantId;
                uriBuilder.Query = query.ToString() ?? string.Empty;
                httpRequest.RequestUri = uriBuilder.Uri;
                using (var response = await s_httpClient.SendAsync(httpRequest))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException($"BotService:TokenEndPoint returned {response.StatusCode}: {response.Content.ReadAsStringAsync()}");
                    }

                    var responseString = await response.Content.ReadAsStringAsync();

                    token = Rest.Serialization.SafeJsonConvert.DeserializeObject<DirectLineToken>(responseString).Token;
                }
            }

            return token;
        }
    }
}
