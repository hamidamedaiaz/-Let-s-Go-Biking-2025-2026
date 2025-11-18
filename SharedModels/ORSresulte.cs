using Newtonsoft.Json;
using System.Collections.Generic;

namespace SharedModels
{
    public class ORSresulte
    {
        [JsonProperty("features")]
        public List<ORSFeature> Features { get; set; }

        [JsonProperty("routes")]
        public List<ORSRoute> Routes { get; set; }

        [JsonProperty("bbox")]
        public List<double> BBox { get; set; }
    }

    public class ORSRoute
    {
        [JsonProperty("summary")]
        public ORSSummary Summary { get; set; }

        [JsonProperty("segments")]
        public List<ORSSegment> Segments { get; set; }

        [JsonProperty("geometry")]
        public string Geometry { get; set; } 

        [JsonProperty("way_points")]
        public List<int> WayPoints { get; set; }
    }

    public class ORSFeature
    {
        [JsonProperty("properties")]
        public ORSProperties Properties { get; set; }

        [JsonProperty("geometry")]
        public ORSGeometry Geometry { get; set; }
    }

    public class ORSProperties
    {
        [JsonProperty("segments")]
        public List<ORSSegment> Segments { get; set; }

        [JsonProperty("summary")]
        public ORSSummary Summary { get; set; }
    }

    public class ORSSummary
    {
        [JsonProperty("distance")]
        public double Distance { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }
    }

    public class ORSSegment
    {
        [JsonProperty("distance")]
        public double Distance { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("steps")]
        public List<ORSStep> Steps { get; set; }
    }

    public class ORSStep
    {
        [JsonProperty("distance")]
        public double Distance { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("instruction")]
        public string Instruction { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("way_points")]
        public List<int> WayPoints { get; set; }
    }

    public class ORSGeometry
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("coordinates")]
        public List<List<double>> Coordinates { get; set; }
    }
}
