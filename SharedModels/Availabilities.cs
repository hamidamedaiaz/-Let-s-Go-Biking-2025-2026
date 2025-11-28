using Newtonsoft.Json;

namespace SharedModels
{
    /// <summary>
    /// Represents real-time availability data for bikes and stands at a station.
    /// </summary>
    public class Availabilities
    {
        /// <summary>
        /// Total number of bikes available (mechanical + electrical).
        /// </summary>
        [JsonProperty("bikes")]
        public int Bikes { get; set; }

        /// <summary>
        /// Number of empty stands available for parking.
        /// </summary>
        [JsonProperty("stands")]
        public int Stands { get; set; }

        /// <summary>
        /// Number of mechanical (traditional) bikes available.
        /// </summary>
        [JsonProperty("mechanicalBikes")]
        public int MechanicalBikes { get; set; }

        /// <summary>
        /// Number of electrical bikes available.
        /// </summary>
        [JsonProperty("electricalBikes")]
        public int ElectricalBikes { get; set; }

        /// <summary>
        /// Number of electrical bikes with internal (non-removable) batteries.
        /// </summary>
        [JsonProperty("electricalInternalBatteryBikes")]
        public int ElectricalInternalBatteryBikes { get; set; }

        /// <summary>
        /// Number of electrical bikes with removable batteries.
        /// </summary>
        [JsonProperty("electricalRemovableBatteryBikes")]
        public int ElectricalRemovableBatteryBikes { get; set; }
    }
}
