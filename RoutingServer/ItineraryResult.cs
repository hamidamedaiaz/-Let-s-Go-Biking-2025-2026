using System.Runtime.Serialization;

namespace RoutingServer
{
    [DataContract]
    public class ItineraryResult
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public ItineraryData Data { get; set; }
    }

    [DataContract]
    public class ItineraryData
    {
        [DataMember]
        public double TotalDistance { get; set; }

        [DataMember]
        public double TotalDuration { get; set; }

        [DataMember]
        public Step[] Steps { get; set; }

        [DataMember]
        public Geometry Geometry { get; set; }
    }

    [DataContract]
    public class Step
    {
        [DataMember]
        public string Type { get; set; }

        [DataMember]
        public string Instructions { get; set; }

        [DataMember]
        public double Distance { get; set; }

        [DataMember]
        public double Duration { get; set; }

        [DataMember]
        public double[][] Coordinates { get; set; }
    }

    [DataContract]
    public class Geometry
    {
        [DataMember]
        public double[][] Coordinates { get; set; }
    }
}