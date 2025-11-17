using Newtonsoft.Json;
using System.Collections.Generic;

namespace SharedModels
{
    public class BikeContract
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("commercial_name")]
        public string CommercialName { get; set; } = "";

        [JsonProperty("country_code")]
        public string Country { get; set; } = "";

        [JsonProperty("cities")]
        public List<string> Cities { get; set; } = new List<string>();
    }
}
