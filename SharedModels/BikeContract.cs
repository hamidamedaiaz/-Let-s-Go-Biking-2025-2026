using System.Runtime.Serialization;

namespace SharedModels
{
        [DataContract]
        public class BikeContract
        {
            [DataMember]
            public string name { get; set; } = "";

            [DataMember]
            public string commercial_name { get; set; } = "";

            [DataMember]
            public string country_code { get; set; } = "";

            [DataMember]
            public string[] cities { get; set; } = new string[0];
        }
}
