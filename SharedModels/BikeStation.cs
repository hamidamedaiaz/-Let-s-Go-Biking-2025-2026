using Newtonsoft.Json;
using System;

namespace SharedModels
{
    /// <summary>
    /// Represents a bike-sharing station with real-time availability data.
    /// </summary>
    public class BikeStation
    {
        /// <summary>
        /// Unique station number within the contract.
        /// </summary>
        [JsonProperty("number")]
        public int Number { get; set; }

        /// <summary>
        /// Name of the contract this station belongs to.
        /// </summary>
        [JsonProperty("contractName")]
        public string ContractName { get; set; } = "";

        /// <summary>
        /// Station name.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Street address of the station.
        /// </summary>
        [JsonProperty("address")]
        public string Address { get; set; } = "";

        /// <summary>
        /// Geographical position of the station.
        /// </summary>
        [JsonProperty("position")]
        public Position Position { get; set; }

        /// <summary>
        /// Indicates if the station has a payment terminal.
        /// </summary>
        [JsonProperty("banking")]
        public bool Banking { get; set; }

        /// <summary>
        /// Indicates if the station offers bonus points.
        /// </summary>
        [JsonProperty("bonus")]
        public bool Bonus { get; set; }

        /// <summary>
        /// Current operational status (OPEN or CLOSED).
        /// </summary>
        [JsonProperty("status")]
        public string Status { get; set; } = "";

        /// <summary>
        /// Timestamp of last data update.
        /// </summary>
        [JsonProperty("lastUpdate")]
        public DateTime? LastUpdate { get; set; }

        /// <summary>
        /// Indicates if the station is connected to the network.
        /// </summary>
        [JsonProperty("connected")]
        public bool Connected { get; set; }

        /// <summary>
        /// Indicates if the station has overflow parking.
        /// </summary>
        [JsonProperty("overflow")]
        public bool Overflow { get; set; }

        /// <summary>
        /// Geographical shape of the station area (if applicable).
        /// </summary>
        [JsonProperty("shape")]
        public object Shape { get; set; }

        /// <summary>
        /// Total stands information (main + overflow).
        /// </summary>
        [JsonProperty("totalStands")]
        public Stands TotalStands { get; set; }

        /// <summary>
        /// Main parking area stands information.
        /// </summary>
        [JsonProperty("mainStands")]
        public Stands MainStands { get; set; }

        /// <summary>
        /// Overflow parking area stands information.
        /// </summary>
        [JsonProperty("overflowStands")]
        public Stands OverflowStands { get; set; }
    }

    /// <summary>
    /// Represents stand information for a bike station area.
    /// </summary>
    public class Stands
    {
        /// <summary>
        /// Current availability data for bikes and stands.
        /// </summary>
        [JsonProperty("availabilities")]
        public Availabilities Availabilities { get; set; }

        /// <summary>
        /// Total capacity of the stand area.
        /// </summary>
        [JsonProperty("capacity")]
        public int Capacity { get; set; }
    }

    /// <summary>
    /// Represents geographical coordinates.
    /// </summary>
    public class Position
    {
        /// <summary>
        /// Latitude in decimal degrees.
        /// </summary>
        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude in decimal degrees.
        /// </summary>
        [JsonProperty("longitude")]
        public double Longitude { get; set; }
    }
}
