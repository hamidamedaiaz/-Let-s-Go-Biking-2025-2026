using Newtonsoft.Json;
using SharedModels;
using System.Collections.Generic;
using System.Net.Http;

namespace ProxyCacheService.ProxyModels
{
    public class Contracts
    {
        private readonly HttpClient _httpClient;
        public static string ApiKey;
        public List<BikeContract> Items { get; set; }

        public Contracts()
        {
            Items = new List<BikeContract>();
        }

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
