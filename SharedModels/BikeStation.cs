
namespace SharedModels
{
    public class BikeStation
    {
        public int number { get; set; }
        public string name { get; set; } = "";
        public string address { get; set; } = "";
        public GpsPosition position { get; set; }
        public int available_bikes { get; set; }
        public int bike_stands { get; set; }
        public int available_bike_stands { get; set; }
        public string status { get; set; } = "";
    }
}
