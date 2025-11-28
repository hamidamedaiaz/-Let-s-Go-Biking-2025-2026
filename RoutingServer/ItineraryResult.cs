using System.Runtime.Serialization;

namespace RoutingServer
{
    /// <summary>
    /// Represents the result of an itinerary calculation.
    /// </summary>
    [DataContract]
    public class ItineraryResult
    {
        /// <summary>
        /// Indicates whether the itinerary calculation was successful.
        /// </summary>
        [DataMember]
        public bool Success { get; set; }

        /// <summary>
        /// Recommendation message (e.g., "walk" or "bike") or error description.
        /// </summary>
        [DataMember]
        public string Message { get; set; }

        /// <summary>
        /// Detailed itinerary data including steps and geometry.
        /// </summary>
        [DataMember]
        public ItineraryData Data { get; set; }
    }

    /// <summary>
    /// Contains detailed information about an itinerary.
    /// </summary>
    [DataContract]
    public class ItineraryData
    {
        /// <summary>
        /// Total distance in meters.
        /// </summary>
        [DataMember]
        public double TotalDistance { get; set; }

        /// <summary>
        /// Total duration in seconds.
        /// </summary>
        [DataMember]
        public double TotalDuration { get; set; }

        /// <summary>
        /// Array of individual steps composing the itinerary.
        /// </summary>
        [DataMember]
        public Step[] Steps { get; set; }

        /// <summary>
        /// Complete geometry of the route.
        /// </summary>
        [DataMember]
        public Geometry Geometry { get; set; }
    }

    /// <summary>
    /// Represents a single step in an itinerary.
    /// </summary>
    [DataContract]
    public class Step
    {
        /// <summary>
        /// Type of movement (e.g., "walk" or "bike").
        /// </summary>
        [DataMember]
        public string Type { get; set; }

        /// <summary>
        /// Human-readable instructions for this step.
        /// </summary>
        [DataMember]
        public string Instructions { get; set; }

        /// <summary>
        /// Distance of this step in meters.
        /// </summary>
        [DataMember]
        public double Distance { get; set; }

        /// <summary>
        /// Duration of this step in seconds.
        /// </summary>
        [DataMember]
        public double Duration { get; set; }

        /// <summary>
        /// Array of coordinates [longitude, latitude] for this step.
        /// </summary>
        [DataMember]
        public double[][] Coordinates { get; set; }
    }

    /// <summary>
    /// Represents the geographical geometry of a route.
    /// </summary>
    [DataContract]
    public class Geometry
    {
        /// <summary>
        /// Array of coordinates [longitude, latitude] forming the complete route.
        /// </summary>
        [DataMember]
        public double[][] Coordinates { get; set; }
    }
}