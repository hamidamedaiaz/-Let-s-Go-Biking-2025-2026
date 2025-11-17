using System.ServiceModel;
using System.ServiceModel.Web;

namespace RoutingServer
{
    [ServiceContract]
    public interface IRoutingService
    {
        [OperationContract]
        [WebGet(
            UriTemplate = "itinerary?originLat={originLat}&originLon={originLon}&originCity={originCity}&destLat={destLat}&destLon={destLon}&destCity={destCity}",
            ResponseFormat = WebMessageFormat.Json,
            BodyStyle = WebMessageBodyStyle.Wrapped
        )]
        ItineraryResult GetItinerary(
            string originLat,
            string originLon,
            string originCity,
            string destLat,
            string destLon,
            string destCity
        );

    }
}
