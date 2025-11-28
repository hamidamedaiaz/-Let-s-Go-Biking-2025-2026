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
    /// <summary>
    /// Implementation of the proxy cache service.
    /// Provides caching layer for JCDecaux bike stations and OpenRouteService routing data.
    /// </summary>
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

            Console.WriteLine("[ProxyCacheService] Preloading contracts data...");
            _ = GetAvailableContracts();

            Console.WriteLine("[ProxyCacheService] Preloading popular contracts...");
            ContractResolver.LoadPopularContractsAsync(this).Wait();
        }

        /// <summary>
        /// Computes a route between two coordinates.
        /// </summary>
        /// <param name="startLatitude">Starting point latitude</param>
        /// <param name="startLongitude">Starting point longitude</param>
        /// <param name="endLatitude">Ending point latitude</param>
        /// <param name="endLongitude">Ending point longitude</param>
        /// <param name="isBike">True for cycling route, false for walking route</param>
        /// <returns>JSON string containing route data</returns>
        public string ComputeRoute(double startLatitude, double startLongitude, double endLatitude, double endLongitude, bool isBike)
        {
            string profile = isBike ? "cycling-regular" : "foot-walking";
            string cacheKey = $"route_{profile}_{startLatitude:F4}_{startLongitude:F4}_{endLatitude:F4}_{endLongitude:F4}";

            Console.WriteLine($"[ProxyCacheService] Route computation requested: {profile}");
            Console.WriteLine($"[ProxyCacheService] Origin: ({startLatitude}, {startLongitude})");
            Console.WriteLine($"[ProxyCacheService] Destination: ({endLatitude}, {endLongitude})");

            try
            {
                var cachedObj = _routeCache.GetOrAdd(cacheKey, 600, () =>
                {
                    Console.WriteLine("[ProxyCacheService] Cache miss, calling OpenRouteService API");

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
                    Console.WriteLine("[ProxyCacheService] Route calculated and cached");

                    return new OpenRouteResult { Value = json };
                });

                return cachedObj.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProxyCacheService] ERROR: {ex.Message}");
                throw new FaultException($"Route computation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves all available JCDecaux contracts.
        /// </summary>
        /// <returns>List of bike-sharing contracts</returns>
        public List<BikeContract> GetAvailableContracts()
        {
            Console.WriteLine("[ProxyCacheService] Retrieving contracts via cache");

            string cacheKey = "jcdecaux_contracts";

            var obj = _contractsCache.GetOrAdd(cacheKey, 86400, () => new Contracts(_httpClient));

            return obj.Items;
        }

        /// <summary>
        /// Retrieves all bike stations for a specific contract.
        /// </summary>
        /// <param name="contractName">Name of the JCDecaux contract</param>
        /// <returns>List of bike stations in the contract</returns>
        public List<BikeStation> GetStationsByContract(string contractName)
        {
            Console.WriteLine($"[ProxyCacheService] Retrieving stations for contract '{contractName}' via cache");

            var obj = _stationsCache.GetOrAdd(contractName, 600, () => new Stations(_httpClient, contractName));

            return obj.BikeStations;
        }

        /// <summary>
        /// Calls OpenRouteService API to compute a route.
        /// </summary>
        /// <param name="profile">Route profile (foot-walking or cycling-regular)</param>
        /// <param name="start">Start coordinates as "lon,lat"</param>
        /// <param name="end">End coordinates as "lon,lat"</param>
        /// <returns>JSON string containing route data</returns>
        public string CallORS(string profile, string start, string end)
        {
            try
            {
                string cacheKey = $"ORS_{profile}_{start}_{end}";

                var cached = _routeCache.GetOrAdd(cacheKey, 600, () =>
                {
                    Console.WriteLine("[ProxyCacheService] ORS cache miss, calling API");

                    var startParts = start.Split(',');
                    var endParts = end.Split(',');

                    double startLon = double.Parse(startParts[0], CultureInfo.InvariantCulture);
                    double startLat = double.Parse(startParts[1], CultureInfo.InvariantCulture);
                    double endLon = double.Parse(endParts[0], CultureInfo.InvariantCulture);
                    double endLat = double.Parse(endParts[1], CultureInfo.InvariantCulture);

                    Console.WriteLine($"[ProxyCacheService] Parsed coordinates: ({startLon}, {startLat}) to ({endLon}, {endLat})");

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
                    Console.WriteLine("[ProxyCacheService] ORS response cached");

                    return new OpenRouteResult { Value = resultJson };
                });

                return cached.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProxyCacheService] ERROR in CallORS: {ex.Message}");
                throw new FaultException($"ORS call error: {ex.Message}");
            }
        }
    }
}
