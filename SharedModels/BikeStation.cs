
using System.Runtime.Serialization;

namespace SharedModels
{
    [DataContract]
    public class BikeStation
    {
        [DataMember]
        public int number { get; set; }
        [DataMember]
        public string name { get; set; } = "";
        [DataMember]
        public string address { get; set; } = "";
        [DataMember]
        public GpsPosition position { get; set; }
        [DataMember]
        public int available_bikes { get; set; }
        [DataMember]
        public int bike_stands { get; set; }
        [DataMember]
        public int available_bike_stands { get; set; }
        [DataMember]
        public string status { get; set; } = "";
    }
}
