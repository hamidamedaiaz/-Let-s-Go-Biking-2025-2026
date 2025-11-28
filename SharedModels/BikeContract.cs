using Newtonsoft.Json;
using System.Collections.Generic;

namespace SharedModels
{
    /// <summary>
    /// Represents a JCDecaux bike-sharing contract.
    /// A contract corresponds to a bike-sharing system in one or more cities.
    /// </summary>
    public class BikeContract
    {
        /// <summary>
        /// Internal name of the contract.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Commercial name of the bike-sharing service.
        /// </summary>
        [JsonProperty("commercial_name")]
        public string CommercialName { get; set; } = "";

        /// <summary>
        /// ISO country code where the contract operates.
        /// </summary>
        [JsonProperty("country_code")]
        public string Country { get; set; } = "";

        /// <summary>
        /// List of cities covered by this contract.
        /// </summary>
        [JsonProperty("cities")]
        public List<string> Cities { get; set; } = new List<string>();
    }
}
