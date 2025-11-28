using Newtonsoft.Json;
using SharedModels;
using System.Collections.Generic;
using System.Net.Http;

namespace ProxyCacheService.ProxyModels
{
    /// <summary>
    /// Represents a collection of bike stations for a specific contract.
    /// Provides caching and API access to station data.
    /// </summary>
    public class Stations
    {
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// JCDecaux API key shared across instances.
        /// </summary>
        public static string ApiKey;

        /// <summary>
        /// List of bike stations.
        /// </summary>
        public List<BikeStation> BikeStations { get; set; }

        /// <summary>
        /// Default constructor for cache initialization.
        /// </summary>
        public Stations()
        {
            BikeStations = new List<BikeStation>();
        }

        /// <summary>
        /// Constructor that fetches stations from JCDecaux API for a specific contract.
        /// </summary>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="contractName">Name of the JCDecaux contract</param>
        public Stations(HttpClient httpClient, string contractName)
        {
            var json = httpClient.GetStringAsync(
                $"https://api.jcdecaux.com/vls/v3/stations?contract={contractName}&apiKey={ApiKey}"
            ).Result;

            BikeStations = JsonConvert.DeserializeObject<List<BikeStation>>(json);
        }
    }
}
