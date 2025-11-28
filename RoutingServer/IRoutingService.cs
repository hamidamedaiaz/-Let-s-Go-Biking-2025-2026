using System.ServiceModel;
using System.ServiceModel.Web;

namespace RoutingServer
{
    /// <summary>
    /// Service contract defining the routing service operations.
    /// Provides REST endpoints for itinerary calculation.
    /// </summary>
    [ServiceContract]
    public interface IRoutingService
    {
        /// <summary>
        /// Calculates an itinerary between two points.
        /// </summary>
        /// <param name="originLat">Origin latitude</param>
        /// <param name="originLon">Origin longitude</param>
        /// <param name="originCity">Origin city name</param>
        /// <param name="destLat">Destination latitude</param>
        /// <param name="destLon">Destination longitude</param>
        /// <param name="destCity">Destination city name</param>
        /// <returns>Itinerary result with route details and recommendation</returns>
        [OperationContract]
        [WebGet(
            UriTemplate = "itinerary?originLat={originLat}&originLon={originLon}&originCity={originCity}&destLat={destLat}&destLon={destLon}&destCity={destCity}",
            ResponseFormat = WebMessageFormat.Json,
            BodyStyle = WebMessageBodyStyle.Bare
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
