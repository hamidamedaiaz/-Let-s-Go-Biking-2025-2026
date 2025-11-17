using SharedModels;
using System.Collections.Generic;
using System.ServiceModel;

namespace ProxyCacheService
{
    [ServiceContract]
    public interface IProxyCacheService
    {
        [OperationContract]
        List<BikeContract> GetAvailableContracts();

        [OperationContract]
        List<BikeStation> GetStationsByContract(string contractName);

        [OperationContract]
        string ComputeRoute(double startLatitude, double startLongitude, double endLatitude, double endLongitude, bool isBike);

        [OperationContract]
        string CallORS(string profile, string start, string end);
    }
}
