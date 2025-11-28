using System.Net.Http;

namespace ProxyCacheServer.ProxyModels
{
    /// <summary>
    /// Represents a cached OpenRouteService routing result.
    /// </summary>
    public class OpenRouteResult
    {
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// OpenRouteService API key shared across instances.
        /// </summary>
        public static string ApiKey;

        /// <summary>
        /// JSON string containing the route data.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Default constructor for cache initialization.
        /// </summary>
        public OpenRouteResult() { }

        /// <summary>
        /// Constructor that fetches route data from OpenRouteService API.
        /// </summary>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="coordinates">Coordinates string for the route</param>
        public OpenRouteResult(HttpClient httpClient, string coordinates)
        {
            var json = httpClient.GetStringAsync(
                $"https://api.openrouteservice.org/v2/directions/cycling-regular?api_key={ApiKey}&start={coordinates}"
            ).Result;
            Value = json;
        }
    }
}
