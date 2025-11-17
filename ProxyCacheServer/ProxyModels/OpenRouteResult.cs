using System.Net.Http;

namespace ProxyCacheServer.ProxyModels
{
    public class OpenRouteResult
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static string ApiKey;
        public string Value { get; set; }
        public OpenRouteResult() { }

        public OpenRouteResult(HttpClient httpClient, string coordinates)
        {
            var json = httpClient.GetStringAsync(
                $"https://api.openrouteservice.org/v2/directions/cycling-regular?api_key={ApiKey}&start={coordinates}"
            ).Result;
            Value = json;
        }
    }
}
