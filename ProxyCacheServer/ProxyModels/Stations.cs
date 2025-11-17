using Newtonsoft.Json;
using SharedModels;
using System.Collections.Generic;
using System.Net.Http;

namespace ProxyCacheService.ProxyModels
{
    public class Stations
    {
        private static readonly HttpClient httpClient = new HttpClient();
        public static string ApiKey;
        public List<BikeStation> BikeStations { get; set; }

        public Stations()
        {
            BikeStations = new List<BikeStation>();
        }

        public Stations(HttpClient httpClient, string contractName)
        {
            var json = httpClient.GetStringAsync(
                $"https://api.jcdecaux.com/vls/v3/stations?contract={contractName}&apiKey={ApiKey}"
            ).Result;

            BikeStations = JsonConvert.DeserializeObject<List<BikeStation>>(json);
        }
    }
}
