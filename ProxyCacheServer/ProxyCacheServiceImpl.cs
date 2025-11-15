using Newtonsoft.Json;
using SharedModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.ServiceModel;
using System.Text;

namespace ProxyCacheService
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ProxyCacheServiceImpl : IProxyCacheService
    {

        private string _jcdecauxApiKey ;
        private readonly HttpClient _httpClient;
        private readonly string _openRouteApiKey;
        private readonly GenericProxyCache<OpenRouteResulte> _routeCache;
        private readonly GenericProxyCache<List<BikeStation>> _stationsCache;


        public ProxyCacheServiceImpl()
        {
            _httpClient = new HttpClient();
            _routeCache = new GenericProxyCache<OpenRouteResulte>();
            _stationsCache = new GenericProxyCache<List<BikeStation>>();
            
            _openRouteApiKey = ConfigurationManager.AppSettings["OpenRouteApiKey"] 
                ?? throw new InvalidOperationException("Clé OpenRouteService manquante");
            
            _jcdecauxApiKey = ConfigurationManager.AppSettings["JCDecauxApiKey"]
                ?? throw new InvalidOperationException("Clé JCDecaux manquante");
        }

        public string ComputeRoute(double startLat, double startLon, double endLat, double endLon, bool isBike)
        {
            string profile = isBike ? "cycling-regular" : "foot-walking";
            string cacheKey = $"route_{profile}_{startLat:F4}_{startLon:F4}_{endLat:F4}_{endLon:F4}";





            Console.WriteLine($"[ProxyCache] → ComputeRoute({profile})");
            Console.WriteLine($"   Origine: ({startLat}, {startLon})");
            Console.WriteLine($"   Destination: ({endLat}, {endLon})");




            try
            {
                // ✅ Get retourne directement ProxyCacheRouteString
                var cachedObj = _routeCache.Get(cacheKey, 600);
                
                // ✅ cachedObj.Value est déjà le string JSON
                if (!string.IsNullOrEmpty(cachedObj?.Value))
                {
                    Console.WriteLine("   [Cache HIT]");
                    return cachedObj.Value; // ✅ Un seul .Value
                }

                Console.WriteLine("   [Cache MISS] Appel API OpenRouteService...");

                string url = $"https://api.openrouteservice.org/v2/directions/{profile}";
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

                var content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", _openRouteApiKey);

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

                Console.WriteLine($"   ✅ Route calculée: {distance}m en {duration / 60:F1}min");

                // ✅ Stocker dans le cache : cachedObj est déjà l'objet ProxyCacheRouteString
                cachedObj.Value = json; // ✅ Un seul .Value
                Console.WriteLine($"   💾 Route mise en cache");

                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ ERREUR: {ex.Message}");
                throw new FaultException($"Erreur calcul itinéraire: {ex.Message}");
            }
        }






        public void Dispose()
        {
            _httpClient?.Dispose();
            Console.WriteLine("[ProxyCache] Service disposé.");
        }



       
        // on contacte ici jcdcaux
        public List<BikeStation> GetStations(string contractName)
        {
            Console.WriteLine($"\n[ProxyCache] → GetStations({contractName})");

            try
            {
                // 🔑 Clé de cache unique par contrat
                string cacheKey = $"stations_{contractName}";

                // 🗃️ Vérifier le cache (5 minutes)
                var cached = _stationsCache.Get(cacheKey, 300);

                // ✅ Vérifier si la liste contient des données
                if (cached != null && cached.Count > 0)
                {
                    Console.WriteLine($"   [Cache HIT] {cached.Count} stations");
                    
                    // 📊 Stats du cache
                    int cachedOpenStations = cached.Count(s => s.status == "OPEN");
                    int cachedTotalBikes = cached.Sum(s => s.available_bikes);
                    Console.WriteLine($"   📊 Ouvertes: {cachedOpenStations}, Vélos: {cachedTotalBikes}");
                    
                    return cached;
                }

                // 🌐 Cache MISS → Appel API
                Console.WriteLine("   [Cache MISS] Appel API JCDecaux...");
        
                string url = $"https://api.jcdecaux.com/vls/v1/stations?contract={contractName}&apiKey={_jcdecauxApiKey}";
                Console.WriteLine($"   🌐 URL: {url}");

                // 📥 Appel HTTP
                var response = _httpClient.GetStringAsync(url).Result;

                // 🧩 Parser le JSON
                var stations = JsonConvert.DeserializeObject<List<BikeStation>>(response);

                Console.WriteLine($"   ✅ {stations.Count} stations récupérées");

                // 📊 Statistiques
                int openStations = stations.Count(s => s.status == "OPEN");
                int totalBikes = stations.Sum(s => s.available_bikes);
                int totalStands = stations.Sum(s => s.bike_stands);

                Console.WriteLine($"   📊 Statistiques:");
                Console.WriteLine($"      Stations ouvertes: {openStations}/{stations.Count}");
                Console.WriteLine($"      Vélos disponibles: {totalBikes}");
                Console.WriteLine($"      Places totales: {totalStands}");

                // ✅ STOCKER DANS LE CACHE (c'était manquant !)
                cached.Clear();
                cached.AddRange(stations);
                Console.WriteLine($"   💾 Stations mises en cache pour 5 minutes");

                return stations;
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"   ❌ ERREUR HTTP: {httpEx.Message}");

                if (httpEx.Message.Contains("404"))
                {
                    Console.WriteLine($"   ⚠️ Contrat '{contractName}' introuvable");
                    Console.WriteLine($"   💡 Contrats valides: Paris, Lyon, Marseille, Toulouse, Nantes, Strasbourg...");
                }

                return new List<BikeStation>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ ERREUR: {ex.Message}");
                return new List<BikeStation>();
            }
        }

    }
}