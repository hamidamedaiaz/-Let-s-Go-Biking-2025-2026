using Newtonsoft.Json;

namespace SharedModels
{
    public class Availabilities
    {
        [JsonProperty("bikes")]
        public int Bikes { get; set; }

        [JsonProperty("stands")]
        public int Stands { get; set; }

        [JsonProperty("mechanicalBikes")]
        public int MechanicalBikes { get; set; }

        [JsonProperty("electricalBikes")]
        public int ElectricalBikes { get; set; }

        [JsonProperty("electricalInternalBatteryBikes")]
        public int ElectricalInternalBatteryBikes { get; set; }

        [JsonProperty("electricalRemovableBatteryBikes")]
        public int ElectricalRemovableBatteryBikes { get; set; }
    }
}
