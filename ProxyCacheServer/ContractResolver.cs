using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProxyCacheService
{
    /// <summary>
    /// Resolves bike-sharing contracts based on geographical coordinates.
    /// Maintains a cache of contract bounding boxes for efficient lookups.
    /// </summary>
    public static class ContractResolver
    {
        private static Dictionary<string, ContractInfo> _contractsCache = new Dictionary<string, ContractInfo>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Represents geographical information about a contract.
        /// </summary>
        public class ContractInfo
        {
            /// <summary>
            /// Contract name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Minimum latitude of the contract's coverage area.
            /// </summary>
            public double MinLat { get; set; }

            /// <summary>
            /// Maximum latitude of the contract's coverage area.
            /// </summary>
            public double MaxLat { get; set; }

            /// <summary>
            /// Minimum longitude of the contract's coverage area.
            /// </summary>
            public double MinLon { get; set; }

            /// <summary>
            /// Maximum longitude of the contract's coverage area.
            /// </summary>
            public double MaxLon { get; set; }

            /// <summary>
            /// Approximate area of the contract's bounding box.
            /// </summary>
            public double Area => (MaxLat - MinLat) * (MaxLon - MinLon);
        }

        /// <summary>
        /// Loads the bounding box for a specific contract.
        /// </summary>
        /// <param name="proxy">Proxy service instance</param>
        /// <param name="contractName">Contract name to load</param>
        /// <returns>Contract info or null if loading fails</returns>
        private static ContractInfo LoadContractBBox(IProxyCacheService proxy, string contractName)
        {
            lock (_lock)
            {
                var normalizedName = contractName.ToLower();
                if (_contractsCache.ContainsKey(normalizedName))
                {
                    return _contractsCache[normalizedName];
                }

                try
                {
                    Console.WriteLine($"[ContractResolver] Loading bounding box for '{contractName}'");

                    var stations = proxy.GetStationsByContract(contractName);

                    if (stations == null || stations.Count == 0)
                    {
                        Console.WriteLine($"[ContractResolver] No stations found for '{contractName}'");
                        return null;
                    }

                    var info = new ContractInfo
                    {
                        Name = normalizedName,
                        MinLat = stations.Min(s => s.Position.Latitude),
                        MaxLat = stations.Max(s => s.Position.Latitude),
                        MinLon = stations.Min(s => s.Position.Longitude),
                        MaxLon = stations.Max(s => s.Position.Longitude)
                    };

                    _contractsCache[normalizedName] = info;

                    Console.WriteLine($"[ContractResolver] Loaded {contractName}: bbox=[{info.MinLat:F3},{info.MinLon:F3}] to [{info.MaxLat:F3},{info.MaxLon:F3}]");

                    return info;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ContractResolver] Error loading contract '{contractName}': {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Preloads popular contracts at service startup.
        /// </summary>
        /// <param name="proxy">Proxy service instance</param>
        /// <returns>Completed task</returns>
        public static Task LoadPopularContractsAsync(IProxyCacheService proxy)
        {
            lock (_lock)
            {
                Console.WriteLine("[ContractResolver] Preloading popular contracts");

                var popularContracts = new[] { "Lyon", "Paris", "Marseille", "Toulouse", "Nantes" };

                foreach (var contractName in popularContracts)
                {
                    LoadContractBBox(proxy, contractName);
                }

                Console.WriteLine($"[ContractResolver] Preloaded {_contractsCache.Count} popular contracts");
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Resolves the contract for given coordinates using lazy loading.
        /// First checks preloaded contracts, then loads all contracts if necessary.
        /// </summary>
        /// <param name="proxy">Proxy service instance</param>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <returns>Contract name or null if no match found</returns>
        public static async Task<string> ResolveContractForCoordinate(IProxyCacheService proxy, double lat, double lon)
        {
            if (_contractsCache.Count == 0)
            {
                await LoadPopularContractsAsync(proxy);
            }

            lock (_lock)
            {
                var matchingContracts = _contractsCache.Values
                    .Where(c => lat >= c.MinLat && lat <= c.MaxLat &&
                               lon >= c.MinLon && lon <= c.MaxLon)
                    .ToList();

                if (matchingContracts.Any())
                {
                    var bestMatch = matchingContracts.OrderBy(c => c.Area).First();
                    Console.WriteLine($"[ContractResolver] Coordinate ({lat:F4}, {lon:F4}) matched to '{bestMatch.Name}'");
                    return bestMatch.Name;
                }
            }

            Console.WriteLine("[ContractResolver] Coordinate outside known bounding boxes, loading all contracts");
            await LoadAllContractsAsync(proxy);

            lock (_lock)
            {
                var matchingContracts = _contractsCache.Values
                    .Where(c => lat >= c.MinLat && lat <= c.MaxLat &&
                               lon >= c.MinLon && lon <= c.MaxLon)
                    .ToList();

                if (matchingContracts.Any())
                {
                    var bestMatch = matchingContracts.OrderBy(c => c.Area).First();
                    Console.WriteLine($"[ContractResolver] Coordinate ({lat:F4}, {lon:F4}) matched to '{bestMatch.Name}'");
                    return bestMatch.Name;
                }

                return FindNearestContract(lat, lon);
            }
        }

        /// <summary>
        /// Loads all available contracts (fallback mechanism).
        /// </summary>
        /// <param name="proxy">Proxy service instance</param>
        private static async Task LoadAllContractsAsync(IProxyCacheService proxy)
        {
            var contracts = proxy.GetAvailableContracts();

            foreach (var contract in contracts)
            {
                LoadContractBBox(proxy, contract.Name);
            }
        }

        /// <summary>
        /// Finds the nearest contract to given coordinates.
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <returns>Nearest contract name or null if none available</returns>
        private static string FindNearestContract(double lat, double lon)
        {
            if (_contractsCache.Count == 0)
            {
                Console.WriteLine("[ContractResolver] No contracts loaded");
                return null;
            }

            double bestDist = double.MaxValue;
            string bestContract = null;

            foreach (var c in _contractsCache.Values)
            {
                double centerLat = (c.MinLat + c.MaxLat) / 2;
                double centerLon = (c.MinLon + c.MaxLon) / 2;

                double d = Haversine(lat, lon, centerLat, centerLon);

                if (d < bestDist)
                {
                    bestDist = d;
                    bestContract = c.Name;
                }
            }

            Console.WriteLine($"[ContractResolver] Nearest contract: '{bestContract}' at {bestDist:F2} km");
            return bestContract;
        }

        /// <summary>
        /// Calculates Haversine distance between two coordinates.
        /// </summary>
        /// <returns>Distance in kilometers</returns>
        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadiusKm = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusKm * c;
        }
    }
}