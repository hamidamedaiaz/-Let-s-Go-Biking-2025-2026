using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProxyCacheService
{
    public static class ContractResolver
    {
        private static Dictionary<string, ContractInfo> _contractsCache = new Dictionary<string, ContractInfo>();
        private static bool _loaded = false;

        public class ContractInfo
        {
            public string Name { get; set; }
            public double MinLat { get; set; }
            public double MaxLat { get; set; }
            public double MinLon { get; set; }
            public double MaxLon { get; set; }
        }

        public static Task LoadContractsAsync(IProxyCacheService proxy)
        {
            if (_loaded) return Task.CompletedTask;

            Console.WriteLine("[ContractResolver] Chargement des contrats et bounding boxes...");

            var contracts = proxy.GetAvailableContracts();

            foreach (var contract in contracts)
            {
                var stations = proxy.GetStationsByContract(contract.Name);

                if (stations == null || stations.Count == 0)
                    continue;

                var info = new ContractInfo
                {
                    Name = contract.Name.ToLower(),
                    MinLat = stations.Min(s => s.Position.Latitude),
                    MaxLat = stations.Max(s => s.Position.Latitude),
                    MinLon = stations.Min(s => s.Position.Longitude),
                    MaxLon = stations.Max(s => s.Position.Longitude)
                };

                _contractsCache[info.Name] = info;

                Console.WriteLine($"[ContractResolver] {contract.Name}: bbox=[{info.MinLat:F3},{info.MinLon:F3}] -> [{info.MaxLat:F3},{info.MaxLon:F3}]");
            }

            _loaded = true;
            return Task.CompletedTask;
        }

        public static async Task<string> ResolveContractForCoordinate(IProxyCacheService proxy, double lat, double lon)
        {
            await LoadContractsAsync(proxy);

            // 1️⃣ Vérification bounding box (rapide)
            foreach (var c in _contractsCache.Values)
            {
                if (lat >= c.MinLat && lat <= c.MaxLat &&
                    lon >= c.MinLon && lon <= c.MaxLon)
                {
                    Console.WriteLine($"[ContractResolver] Coordonnée ({lat:F4}, {lon:F4}) dans bbox de '{c.Name}'");
                    return c.Name;
                }
            }

            // 2️⃣ Contrat le plus proche
            Console.WriteLine($"[ContractResolver] Coordonnée hors bbox. Recherche du contrat le plus proche...");
            return FindNearestContract(lat, lon);
        }

        private static string FindNearestContract(double lat, double lon)
        {
            double bestDist = double.MaxValue;
            string bestContract = null;

            foreach (var c in _contractsCache.Values)
            {
                // Centre géographique du contrat
                double centerLat = (c.MinLat + c.MaxLat) / 2;
                double centerLon = (c.MinLon + c.MaxLon) / 2;

                double d = Haversine(lat, lon, centerLat, centerLon);

                if (d < bestDist)
                {
                    bestDist = d;
                    bestContract = c.Name;
                }
            }

            Console.WriteLine($"[ContractResolver] Contrat le plus proche: '{bestContract}' à {bestDist:F2} km");
            return bestContract;
        }

        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371; // km
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }
    }
}