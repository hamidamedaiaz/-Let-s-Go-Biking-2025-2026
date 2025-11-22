using Newtonsoft.Json;
using System;

namespace SharedModels
{
    public class BikeStation
    {
        [JsonProperty("number")]
        public int Number { get; set; }

        [JsonProperty("contractName")]
        public string ContractName { get; set; } = "";

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("address")]
        public string Address { get; set; } = "";

        [JsonProperty("position")]
        public Position Position { get; set; }

        [JsonProperty("banking")]
        public bool Banking { get; set; }

        [JsonProperty("bonus")]
        public bool Bonus { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = "";

        [JsonProperty("lastUpdate")]
        public DateTime? LastUpdate { get; set; }

        [JsonProperty("connected")]
        public bool Connected { get; set; }

        [JsonProperty("overflow")]
        public bool Overflow { get; set; }

        [JsonProperty("shape")]
        public object Shape { get; set; }

        [JsonProperty("totalStands")]
        public Stands TotalStands { get; set; }

        [JsonProperty("mainStands")]
        public Stands MainStands { get; set; }

        [JsonProperty("overflowStands")]
        public Stands OverflowStands { get; set; }
    }
    public class Stands
    {
        [JsonProperty("availabilities")]
        public Availabilities Availabilities { get; set; }

        [JsonProperty("capacity")]
        public int Capacity { get; set; }
    }
    public class Position
    {
        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        [JsonProperty("longitude")]
        public double Longitude { get; set; }
    }
}
