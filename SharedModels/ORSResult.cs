using Newtonsoft.Json;
using System.Collections.Generic;

namespace SharedModels
{
    /// <summary>
    /// Represents a complete routing response from OpenRouteService.
    /// </summary>
    public class ORSResult
    {
        /// <summary>
        /// List of GeoJSON features containing route information.
        /// </summary>
        [JsonProperty("features")]
        public List<ORSFeature> Features { get; set; }

        /// <summary>
        /// List of computed routes.
        /// </summary>
        [JsonProperty("routes")]
        public List<ORSRoute> Routes { get; set; }

        /// <summary>
        /// Bounding box coordinates [minLon, minLat, maxLon, maxLat].
        /// </summary>
        [JsonProperty("bbox")]
        public List<double> BBox { get; set; }
    }

    /// <summary>
    /// Represents a single route with summary and detailed segments.
    /// </summary>
    public class ORSRoute
    {
        /// <summary>
        /// Summary information about the route.
        /// </summary>
        [JsonProperty("summary")]
        public ORSSummary Summary { get; set; }

        /// <summary>
        /// List of route segments with turn-by-turn instructions.
        /// </summary>
        [JsonProperty("segments")]
        public List<ORSSegment> Segments { get; set; }

        /// <summary>
        /// Encoded geometry string (polyline format).
        /// </summary>
        [JsonProperty("geometry")]
        public string Geometry { get; set; }

        /// <summary>
        /// Indices of waypoints along the route.
        /// </summary>
        [JsonProperty("way_points")]
        public List<int> WayPoints { get; set; }
    }

    /// <summary>
    /// Represents a GeoJSON feature containing route properties and geometry.
    /// </summary>
    public class ORSFeature
    {
        /// <summary>
        /// Route properties including segments and summary.
        /// </summary>
        [JsonProperty("properties")]
        public ORSProperties Properties { get; set; }

        /// <summary>
        /// GeoJSON geometry of the route.
        /// </summary>
        [JsonProperty("geometry")]
        public ORSGeometry Geometry { get; set; }
    }

    /// <summary>
    /// Contains route properties including segments and summary data.
    /// </summary>
    public class ORSProperties
    {
        /// <summary>
        /// List of route segments with turn-by-turn instructions.
        /// </summary>
        [JsonProperty("segments")]
        public List<ORSSegment> Segments { get; set; }

        /// <summary>
        /// Summary information about the entire route.
        /// </summary>
        [JsonProperty("summary")]
        public ORSSummary Summary { get; set; }
    }

    /// <summary>
    /// Contains summary information about a route.
    /// </summary>
    public class ORSSummary
    {
        /// <summary>
        /// Total distance in meters.
        /// </summary>
        [JsonProperty("distance")]
        public double Distance { get; set; }

        /// <summary>
        /// Total duration in seconds.
        /// </summary>
        [JsonProperty("duration")]
        public double Duration { get; set; }
    }

    /// <summary>
    /// Represents a segment of a route with detailed steps.
    /// </summary>
    public class ORSSegment
    {
        /// <summary>
        /// Distance of this segment in meters.
        /// </summary>
        [JsonProperty("distance")]
        public double Distance { get; set; }

        /// <summary>
        /// Duration of this segment in seconds.
        /// </summary>
        [JsonProperty("duration")]
        public double Duration { get; set; }

        /// <summary>
        /// List of individual steps within this segment.
        /// </summary>
        [JsonProperty("steps")]
        public List<ORSStep> Steps { get; set; }
    }

    /// <summary>
    /// Represents a single navigation step with instructions.
    /// </summary>
    public class ORSStep
    {
        /// <summary>
        /// Distance of this step in meters.
        /// </summary>
        [JsonProperty("distance")]
        public double Distance { get; set; }

        /// <summary>
        /// Duration of this step in seconds.
        /// </summary>
        [JsonProperty("duration")]
        public double Duration { get; set; }

        /// <summary>
        /// Type code of the maneuver.
        /// </summary>
        [JsonProperty("type")]
        public int Type { get; set; }

        /// <summary>
        /// Human-readable navigation instruction.
        /// </summary>
        [JsonProperty("instruction")]
        public string Instruction { get; set; }

        /// <summary>
        /// Name of the street or path for this step.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Waypoint indices for this step.
        /// </summary>
        [JsonProperty("way_points")]
        public List<int> WayPoints { get; set; }
    }

    /// <summary>
    /// Represents GeoJSON geometry for a route.
    /// </summary>
    public class ORSGeometry
    {
        /// <summary>
        /// Geometry type (typically "LineString").
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// List of coordinate pairs [longitude, latitude].
        /// </summary>
        [JsonProperty("coordinates")]
        public List<List<double>> Coordinates { get; set; }
    }
}
