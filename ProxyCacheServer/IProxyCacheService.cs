using SharedModels;
using System.Collections.Generic;
using System.ServiceModel;

namespace ProxyCacheService
{
    /// <summary>
    /// Service contract defining proxy cache operations.
    /// Provides cached access to JCDecaux bike stations and OpenRouteService routing data.
    /// </summary>
    [ServiceContract]
    public interface IProxyCacheService
    {
        /// <summary>
        /// Retrieves all available JCDecaux contracts.
        /// </summary>
        /// <returns>List of bike-sharing contracts</returns>
        [OperationContract]
        List<BikeContract> GetAvailableContracts();

        /// <summary>
        /// Retrieves bike stations for a specific contract.
        /// </summary>
        /// <param name="contractName">Name of the contract</param>
        /// <returns>List of bike stations</returns>
        [OperationContract]
        List<BikeStation> GetStationsByContract(string contractName);

        /// <summary>
        /// Computes a route between two coordinates.
        /// </summary>
        /// <param name="startLatitude">Starting latitude</param>
        /// <param name="startLongitude">Starting longitude</param>
        /// <param name="endLatitude">Ending latitude</param>
        /// <param name="endLongitude">Ending longitude</param>
        /// <param name="isBike">True for bike route, false for walking</param>
        /// <returns>Route data in JSON format</returns>
        [OperationContract]
        string ComputeRoute(double startLatitude, double startLongitude, double endLatitude, double endLongitude, bool isBike);

        /// <summary>
        /// Calls OpenRouteService API with specified profile and coordinates.
        /// </summary>
        /// <param name="profile">Route profile (e.g., "foot-walking", "cycling-regular")</param>
        /// <param name="start">Start coordinates as "longitude,latitude"</param>
        /// <param name="end">End coordinates as "longitude,latitude"</param>
        /// <returns>Route data in JSON format</returns>
        [OperationContract]
        string CallORS(string profile, string start, string end);
    }
}
