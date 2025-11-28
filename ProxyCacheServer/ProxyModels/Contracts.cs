using Newtonsoft.Json;
using SharedModels;
using System.Collections.Generic;
using System.Net.Http;

namespace ProxyCacheService.ProxyModels
{
    /// <summary>
    /// Represents a collection of JCDecaux bike-sharing contracts.
    /// Provides caching and API access to contract data.
    /// </summary>
    public class Contracts
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// JCDecaux API key shared across instances.
        /// </summary>
        public static string ApiKey;

        /// <summary>
        /// List of bike-sharing contracts.
        /// </summary>
        public List<BikeContract> Items { get; set; }

        /// <summary>
        /// Default constructor for cache initialization.
        /// </summary>
        public Contracts()
        {
            Items = new List<BikeContract>();
        }

        /// <summary>
        /// Constructor that fetches contracts from JCDecaux API.
        /// </summary>
        /// <param name="httpClient">HTTP client for API calls</param>
        public Contracts(HttpClient httpClient)
        {
            _httpClient = httpClient;
            var json = httpClient.GetStringAsync(
                $"https://api.jcdecaux.com/vls/v3/contracts?apiKey={ApiKey}"
            ).GetAwaiter().GetResult();

            Items = JsonConvert.DeserializeObject<List<BikeContract>>(json);
        }
    }
}
