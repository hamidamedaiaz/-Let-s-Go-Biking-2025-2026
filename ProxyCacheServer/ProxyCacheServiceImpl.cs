using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ProxyCacheServer.ProxyModels;
using ProxyCacheService.ProxyModels;
using SharedModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.ServiceModel;
using System.Text;

namespace ProxyCacheService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ProxyCacheServiceImpl : IProxyCacheService
    {
        private readonly HttpClient _httpClient;

        private readonly GenericProxyCache<Contracts> _contractsCache;
        private readonly GenericProxyCache<Stations> _stationsCache;
        private readonly GenericProxyCache<OpenRouteResult> _routeCache;

        private readonly string _JCDapiKey;
        private readonly string _ORSapiKey;

        public ProxyCacheServiceImpl(IConfiguration c)
        {
            _httpClient = new HttpClient();
            _JCDapiKey = c["JCDApiKey"];
            _ORSapiKey = c["ORSApiKey"];

            Contracts.ApiKey = _JCDapiKey;
            Stations.ApiKey = _JCDapiKey;
            OpenRouteResult.ApiKey = _ORSapiKey;

            _routeCache = new GenericProxyCache<OpenRouteResult>();
            _contractsCache = new GenericProxyCache<Contracts>();
            _stationsCache = new GenericProxyCache<Stations>();

            Console.WriteLine("[ProxyCache] Préchargement des contrats...");
            _ = GetAvailableContracts();
            
            Console.WriteLine("[ProxyCache] Préchargement des contrats populaires...");
            ContractResolver.LoadPopularContractsAsync(this).Wait();
        }

        public string ComputeRoute(double startLatitude, double startLongitude, double endLatitude, double endLongitude, bool isBike)
        {
            string profile = isBike ? "cycling-regular" : "foot-walking";
            string cacheKey = $"route_{profile}_{startLatitude:F4}_{startLongitude:F4}_{endLatitude:F4}_{endLongitude:F4}";

            Console.WriteLine($"[ProxyCache] → ComputeRoute({profile})");
            Console.WriteLine($"Origine: ({startLatitude}, {startLongitude})");
            Console.WriteLine($"Destination: ({endLatitude} ,  {endLongitude})");

            try
            {
                var cachedObj = _routeCache.GetOrAdd(cacheKey, 600, () =>
                {
                    Console.WriteLine("[Cache MISS] Appel API OpenRouteService...");

                    string url = $"https://api.openrouteservice.org/v2/directions/{profile}/json?" +
                        $"api_key={_ORSapiKey}&" +
                        $"start={startLongitude.ToString(CultureInfo.InvariantCulture)},{startLatitude.ToString(CultureInfo.InvariantCulture)}&" +
                        $"end={endLongitude.ToString(CultureInfo.InvariantCulture)},{endLatitude.ToString(CultureInfo.InvariantCulture)}";

                    var response = _httpClient.GetAsync(url).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = response.Content.ReadAsStringAsync().Result;
                        throw new Exception($"OpenRouteService error {response.StatusCode}: {errorBody}");
                    }

                    string json = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine($"Route calculée et mise en cache");

                    return new OpenRouteResult { Value = json };
                });

                return cachedObj.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERREUR: {ex.Message}");
                throw new FaultException($"Erreur calcul itinéraire: {ex.Message}");
            }
        }

        public List<BikeContract> GetAvailableContracts()
        {
            Console.WriteLine("[INFO] Récupération des contrats via cache");

            string cacheKey = "jcdecaux_contracts";
            
            var obj = _contractsCache.GetOrAdd(cacheKey, 86400, () => new Contracts(_httpClient));

            return obj.Items;
        }

        public List<BikeStation> GetStationsByContract(string contractName)
        {
            Console.WriteLine($"[INFO] Récupération des stations pour le contrat '{contractName}' via cache");
            
            var obj = _stationsCache.GetOrAdd(contractName, 600, () => new Stations(_httpClient, contractName));
            
            return obj.BikeStations;
        }

        public string CallORS(string profile, string start, string end)
        {
            try
            {
                string cacheKey = $"ORS_{profile}_{start}_{end}";
                
                var cached = _routeCache.GetOrAdd(cacheKey, 600, () =>
                {
                    Console.WriteLine("[ProxyCache] ORS Cache MISS → Appel API ORS");

                    var startParts = start.Split(',');
                    var endParts = end.Split(',');
                    
                    double startLon = double.Parse(startParts[0], CultureInfo.InvariantCulture);
                    double startLat = double.Parse(startParts[1], CultureInfo.InvariantCulture);
                    double endLon = double.Parse(endParts[0], CultureInfo.InvariantCulture);
                    double endLat = double.Parse(endParts[1], CultureInfo.InvariantCulture);

                    Console.WriteLine($"[ProxyCache] Coordonnées parsées: ({startLon}, {startLat}) → ({endLon}, {endLat})");

                    string url = $"https://api.openrouteservice.org/v2/directions/{profile}/geojson";

                    var payload = new
                    {
                        coordinates = new double[][]
                        {
                            new double[] { startLon, startLat },
                            new double[] { endLon, endLat }
                        },
                        instructions = true,
                        language = "fr",
                        instructions_format = "text"
                    };

                    var jsonBody = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", _ORSapiKey);

                    var response = _httpClient.PostAsync(url, content).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        string error = response.Content.ReadAsStringAsync().Result;
                        throw new Exception($"ORS error {response.StatusCode}: {error}");
                    }

                    string resultJson = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine("[ProxyCache] ORS response cached.");

                    return new OpenRouteResult { Value = resultJson };
                });
                
                return cached.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERREUR CallORS: {ex.Message}");
                throw new FaultException($"Erreur CallORS: {ex.Message}");
            }
        }
    }
}
