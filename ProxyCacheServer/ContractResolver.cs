using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProxyCacheService
{
    public static class ContractResolver
    {
        private static Dictionary<string, ContractInfo> _contractsCache = new Dictionary<string, ContractInfo>();
        private static readonly object _lock = new object();

        public class ContractInfo
        {
            public string Name { get; set; }
            public double MinLat { get; set; }
            public double MaxLat { get; set; }
            public double MinLon { get; set; }
            public double MaxLon { get; set; }
            
            public double Area => (MaxLat - MinLat) * (MaxLon - MinLon);
        }

        // ✅ NOUVEAU : Charger un contrat spécifique à la demande
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
                    Console.WriteLine($"[ContractResolver] Chargement bbox pour '{contractName}'...");
                    
                    var stations = proxy.GetStationsByContract(contractName);

                    if (stations == null || stations.Count == 0)
                    {
                        Console.WriteLine($"[ContractResolver] ⚠️ Aucune station pour '{contractName}'");
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

                    Console.WriteLine($"[ContractResolver] ✓ {contractName}: bbox=[{info.MinLat:F3},{info.MinLon:F3}] -> [{info.MaxLat:F3},{info.MaxLon:F3}]");
                    
                    return info;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ContractResolver] ❌ Erreur pour '{contractName}': {ex.Message}");
                    return null;
                }
            }
        }

        // ✅ MODIFIÉ : Charger seulement les contrats populaires au démarrage
        public static Task LoadPopularContractsAsync(IProxyCacheService proxy)
        {
            lock (_lock)
            {
                Console.WriteLine("[ContractResolver] Préchargement des contrats populaires...");

                var popularContracts = new[] { "Lyon", "Paris", "Marseille", "Toulouse", "Nantes" };

                foreach (var contractName in popularContracts)
                {
                    LoadContractBBox(proxy, contractName);
                }

                Console.WriteLine($"[ContractResolver] ✓ {_contractsCache.Count} contrats populaires préchargés");
                return Task.CompletedTask;
            }
        }

        // ✅ MODIFIÉ : Résoudre avec lazy loading
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
                    Console.WriteLine($"[ContractResolver] ✓ Coordonnée ({lat:F4}, {lon:F4}) dans '{bestMatch.Name}'");
                    return bestMatch.Name;
                }
            }

            Console.WriteLine($"[ContractResolver] Coordonnée hors bbox connus, chargement de tous les contrats...");
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
                    Console.WriteLine($"[ContractResolver] ✓ Coordonnée ({lat:F4}, {lon:F4}) dans '{bestMatch.Name}'");
                    return bestMatch.Name;
                }

                return FindNearestContract(lat, lon);
            }
        }

        // ✅ NOUVEAU : Charger tous les contrats (fallback)
        private static async Task LoadAllContractsAsync(IProxyCacheService proxy)
        {
            var contracts = proxy.GetAvailableContracts();

            foreach (var contract in contracts)
            {
                LoadContractBBox(proxy, contract.Name);
            }
        }

        private static string FindNearestContract(double lat, double lon)
        {
            if (_contractsCache.Count == 0)
            {
                Console.WriteLine("[ContractResolver] ❌ Aucun contrat chargé");
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

            Console.WriteLine($"[ContractResolver] ✓ Contrat le plus proche: '{bestContract}' à {bestDist:F2} km");
            return bestContract;
        }

        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371;
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