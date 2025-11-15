
using System.Runtime.Serialization;

namespace SharedModels
{
    [DataContract]
    public class GpsPosition
    {
        [DataMember]
        public double lat { get; set; }
        [DataMember]
        public double lng { get; set; }
    }
}
