using System.Collections.Generic;
using System.ServiceModel;
namespace SharedModels
{

    public interface IProxyCacheService
    {


        /// <summary>
        /// Calcule un itinéraire via OpenRouteService (avec cache)
        /// </summary>
        [OperationContract]
        string ComputeRoute(double startLat, double startLon, double endLat, double endLon, bool isBike);


        [OperationContract]
        List<BikeStation> GetStations(string  contraName);


    }
}
