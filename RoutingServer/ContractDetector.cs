// ========================================
// ContractDetector.cs
// ========================================

using RoutingServer.ServiceReference2;
using SharedModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RoutingServer
{
    /// <summary>
    /// Detects available JCDecaux contracts along a given route.
    /// Used for multi-contract itinerary planning over long distances.
    /// </summary>
    public class ContractDetector
    {
        private readonly ProxyCacheServiceClient _proxyClient;

        private const double DefaultMaxDistanceFromRoute = 100000; // 100 km in meters

        public ContractDetector(ProxyCacheServiceClient proxyClient)
        {
            _proxyClient = proxyClient;
        }

        /// <summary>
        /// Detects all contracts available along a route between origin and destination.
        /// </summary>
        /// <param name="originLat">Origin latitude</param>
        /// <param name="originLon">Origin longitude</param>
        /// <param name="destLat">Destination latitude</param>
        /// <param name="destLon">Destination longitude</param>
        /// <param name="maxDistanceFromRoute">Maximum perpendicular distance from route to consider a contract (in meters)</param>
        /// <returns>List of contracts found on the route, ordered by distance from origin</returns>
        public List<ContractOnRoute> DetectContractsOnRoute(
            double originLat,
            double originLon,
            double destLat,
            double destLon,
            double maxDistanceFromRoute = DefaultMaxDistanceFromRoute)
        {
            Console.WriteLine("[ContractDetector] ========================================");
            Console.WriteLine("[ContractDetector] Detecting contracts on route");

            var allContracts = _proxyClient.GetAvailableContracts().ToList();
            Console.WriteLine($"[ContractDetector] Total available contracts: {allContracts.Count}");

            var contractsOnRoute = new List<ContractOnRoute>();

            foreach (var contract in allContracts)
            {
                try
                {
                    var stations = _proxyClient.GetStationsByContract(contract.Name).ToList();

                    if (stations.Count == 0) continue;

                    double avgLat = stations.Average(s => s.Position.Latitude);
                    double avgLon = stations.Average(s => s.Position.Longitude);

                    bool isOnRoute = IsContractOnRoute(
                        originLat, originLon,
                        destLat, destLon,
                        avgLat, avgLon,
                        maxDistanceFromRoute);

                    if (isOnRoute)
                    {
                        double distFromOrigin = Haversine(originLat, originLon, avgLat, avgLon);

                        contractsOnRoute.Add(new ContractOnRoute
                        {
                            ContractName = contract.Name,
                            CenterLat = avgLat,
                            CenterLon = avgLon,
                            DistanceFromOrigin = distFromOrigin,
                            Stations = stations
                        });

                        Console.WriteLine($"[ContractDetector] Contract found: {contract.Name} at {distFromOrigin / 1000:F1} km from origin");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ContractDetector] Error loading contract {contract.Name}: {ex.Message}");
                }
            }

            contractsOnRoute = contractsOnRoute.OrderBy(c => c.DistanceFromOrigin).ToList();

            Console.WriteLine($"[ContractDetector] {contractsOnRoute.Count} contracts available on route");
            foreach (var c in contractsOnRoute)
            {
                Console.WriteLine($"[ContractDetector]   - {c.ContractName} ({c.DistanceFromOrigin / 1000:F1} km from origin)");
            }

            return contractsOnRoute;
        }

        /// <summary>
        /// Determines if a contract center point is close enough to the route line.
        /// </summary>
        /// <param name="originLat">Route origin latitude</param>
        /// <param name="originLon">Route origin longitude</param>
        /// <param name="destLat">Route destination latitude</param>
        /// <param name="destLon">Route destination longitude</param>
        /// <param name="pointLat">Contract center latitude</param>
        /// <param name="pointLon">Contract center longitude</param>
        /// <param name="maxDistance">Maximum acceptable distance from route (in meters)</param>
        /// <returns>True if the contract is within acceptable distance from route</returns>
        private bool IsContractOnRoute(
            double originLat, double originLon,
            double destLat, double destLon,
            double pointLat, double pointLon,
            double maxDistance)
        {
            double distance = DistancePointToLine(
                originLat, originLon,
                destLat, destLon,
                pointLat, pointLon);

            return distance <= maxDistance;
        }

        /// <summary>
        /// Calculates the perpendicular distance from a point to a line segment.
        /// Uses projection to find the closest point on the segment.
        /// </summary>
        /// <param name="x1">Line point A latitude (origin)</param>
        /// <param name="y1">Line point A longitude (origin)</param>
        /// <param name="x2">Line point B latitude (destination)</param>
        /// <param name="y2">Line point B longitude (destination)</param>
        /// <param name="px">Point latitude</param>
        /// <param name="py">Point longitude</param>
        /// <returns>Distance from point to line segment in meters</returns>
        private double DistancePointToLine(
            double x1, double y1,
            double x2, double y2,
            double px, double py)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;

            if (dx == 0 && dy == 0)
            {
                return Haversine(x1, y1, px, py);
            }

            double t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));

            double closestX = x1 + t * dx;
            double closestY = y1 + t * dy;

            return Haversine(px, py, closestX, closestY);
        }

        /// <summary>
        /// Calculates the Haversine distance between two geographical coordinates.
        /// </summary>
        /// <param name="lat1">First point latitude</param>
        /// <param name="lon1">First point longitude</param>
        /// <param name="lat2">Second point latitude</param>
        /// <param name="lon2">Second point longitude</param>
        /// <returns>Distance in meters</returns>
        private double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadiusMeters = 6371000;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;

            lat1 *= Math.PI / 180;
            lat2 *= Math.PI / 180;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2) *
                       Math.Cos(lat1) * Math.Cos(lat2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusMeters * c;
        }
    }

    /// <summary>
    /// Represents a bike-sharing contract found along a route.
    /// </summary>
    public class ContractOnRoute
    {
        /// <summary>
        /// Name of the JCDecaux contract.
        /// </summary>
        public string ContractName { get; set; }

        /// <summary>
        /// Latitude of the contract's geographical center.
        /// </summary>
        public double CenterLat { get; set; }

        /// <summary>
        /// Longitude of the contract's geographical center.
        /// </summary>
        public double CenterLon { get; set; }

        /// <summary>
        /// Distance from route origin to this contract (in meters).
        /// </summary>
        public double DistanceFromOrigin { get; set; }

        /// <summary>
        /// List of bike stations available in this contract.
        /// </summary>
        public List<BikeStation> Stations { get; set; }
    }
}