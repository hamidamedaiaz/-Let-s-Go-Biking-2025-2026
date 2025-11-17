using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ProxyCacheServer.ProxyModels;
using ProxyCacheService.ProxyModels;
using SharedModels;
using System;
using System.Collections.Generic;
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
                var cachedObj = _routeCache.Get(cacheKey, 600);

                if (!string.IsNullOrEmpty(cachedObj?.Value))
                {
                    Console.WriteLine("[Cache HIT]");
                    return cachedObj.Value;
                }

                Console.WriteLine("[Cache MISS] Appel API OpenRouteService...");

                string url = $"https://api.openrouteservice.org/v2/directions/{profile}";
                var payload = new
                {
                    coordinates = new double[][]
                    {
                        new double[] { startLongitude, startLatitude },
                        new double[] { endLongitude, endLatitude }
                    },
                    instructions = true,
                    language = "fr",
                    instructions_format = "text"
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", _ORSapiKey);

                var response = _httpClient.PostAsync(url, content).Result;

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = response.Content.ReadAsStringAsync().Result;
                    throw new Exception($"OpenRouteService error {response.StatusCode}: {errorBody}");
                }

                string json = response.Content.ReadAsStringAsync().Result;
                dynamic routeData = JsonConvert.DeserializeObject(json);

                double distance = routeData.routes[0].summary.distance;
                double duration = routeData.routes[0].summary.duration;

                Console.WriteLine($"Route calculée: {distance}m en {duration / 60:F1}min");

                cachedObj.Value = json;
                Console.WriteLine($"Route mise en cache");

                return json;
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
            var obj = _contractsCache.Get(cacheKey, 86400);

            if (obj.Items.Count == 0)
            {
                Console.WriteLine("[CACHE MISS] Chargement depuis JCDecaux");

                obj = new Contracts(_httpClient);

                _contractsCache.Set(cacheKey, obj, 86400);
            }
            return obj.Items;
        }

        public List<BikeStation> GetStationsByContract(string contractName)
        {
            Console.WriteLine($"[INFO] Récupération des stations pour le contrat '{contractName}' via cache");
            var obj = _stationsCache.Get(contractName, 600);

            if (obj.BikeStations.Count == 0)
            {
                Console.WriteLine("[CACHE MISS] Chargement depuis JCDecaux");
                obj = new Stations(_httpClient, contractName);
                _stationsCache.Set(contractName, obj, 600);
            }
            return obj.BikeStations;
        }

        public string CallORS(string profile, string start, string end)
        {
            try
            {
                string cacheKey = $"ORS_{profile}_{start}_{end}";
                var cached = _routeCache.Get(cacheKey, 600);
                if (!string.IsNullOrEmpty(cached?.Value))
                {
                    Console.WriteLine("[ProxyCache] ORS Cache HIT");
                    return cached.Value;
                }
                Console.WriteLine("[ProxyCache] ORS Cache MISS → Appel API ORS");

                string url = $"https://api.openrouteservice.org/v2/directions/{profile}";

                var payload = new
                {
                    coordinates = new double[][]
                    {
                new double[] { double.Parse(start.Split(',')[0]), double.Parse(start.Split(',')[1]) },
                new double[] { double.Parse(end.Split(',')[0]), double.Parse(end.Split(',')[1]) }
                    },
                    instructions = true,
                    language = "fr",
                    instructions_format = "text"
                };

                var jsonBody = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", _ORSapiKey);

                var response = _httpClient.PostAsync(url, content).Result;

                if (!response.IsSuccessStatusCode)
                {
                    string error = response.Content.ReadAsStringAsync().Result;
                    throw new Exception($"ORS error {response.StatusCode}: {error}");
                }

                string resultJson = response.Content.ReadAsStringAsync().Result;
                cached.Value = resultJson;
                Console.WriteLine("[ProxyCache] ORS response cached.");

                return resultJson;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERREUR CallORS: {ex.Message}");
                throw new FaultException($"Erreur CallORS: {ex.Message}");
            }
        }
    }
}
