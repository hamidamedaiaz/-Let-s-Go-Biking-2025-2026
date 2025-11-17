using Newtonsoft.Json.Serialization;
using ProxyCacheService;
using SharedModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;


namespace RoutingServer
{
    public class RoutingService : IRoutingService
    {
        private ItineraryData GetWalkingSegment(

            IProxyCacheService proxy,
            double lat1, double lon1,
            double lat2, double lon2)
        {
            string orsJson = proxy.CallORS(
    "foot-walking",
    $"{lon1.ToString(CultureInfo.InvariantCulture)},{lat1.ToString(CultureInfo.InvariantCulture)}",
    $"{lon2.ToString(CultureInfo.InvariantCulture)},{lat2.ToString(CultureInfo.InvariantCulture)}");

            return ParseORSJson(orsJson, "walk");
        }
        private ItineraryData GetBikingSegment(
            IProxyCacheService proxy,
            double lat1, double lon1,
            double lat2, double lon2)
        {
            string orsJson = proxy.CallORS(
                "cycling-regular",
                $"{lon1.ToString(CultureInfo.InvariantCulture)},{lat1.ToString(CultureInfo.InvariantCulture)}",
                 $"{lon2.ToString(CultureInfo.InvariantCulture)},{lat2.ToString(CultureInfo.InvariantCulture)}");

             return ParseORSJson(orsJson, "bike");
        }

        private ItineraryData ParseORSJson(string orsJson, string stepType)
        {
            dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(orsJson);
            if (data?.features == null || data.features.Count == 0)
            {
                throw new Exception("Invalid ORS response: no features found");
            }

            var props = data.features[0].properties;

            double totalDistance = props.summary.distance;
            double totalDuration = props.summary.duration;

            List<Step> steps = new List<Step>();
            Geometry geometry = new Geometry();

            foreach (var segment in props.segments)
            {
                foreach (var s in segment.steps)
                {
                    steps.Add(new Step
                    {
                        Type = stepType,                        // "walk" ou "bike"
                        Instructions = (string)s.instruction,   // instruction complète
                        Distance = (double)s.distance,          // mètres
                        Duration = (double)s.duration           // secondes
                    });
                }
            }

            return new ItineraryData
            {
                TotalDistance = totalDistance,
                TotalDuration = totalDuration,
                Steps = steps.ToArray(),
                Geometry = new Geometry
                {
                    Coordinates = data.features[0].geometry.coordinates.ToObject<double[][]>()
                }
            };
        }

        public ItineraryResult GetItinerary(
            string originLat,
            string originLon,
            string originCity,
            string destLat,
            string destLon,
            string destCity)
        {
            double oLat = double.Parse(originLat, CultureInfo.InvariantCulture);
            double oLon = double.Parse(originLon, CultureInfo.InvariantCulture);
            double dLat = double.Parse(destLat, CultureInfo.InvariantCulture);
            double dLon = double.Parse(destLon, CultureInfo.InvariantCulture);

            var binding = new BasicHttpBinding
            {
                MaxReceivedMessageSize = 1024 * 1024 * 10, // 10 MB
                MaxBufferSize = 1024 * 1024 * 10,
                MaxBufferPoolSize = 1024 * 1024 * 10,
            };

            binding.ReaderQuotas.MaxStringContentLength = 1024 * 1024 * 10;
            binding.ReaderQuotas.MaxArrayLength = 1024 * 1024 * 10;
            binding.ReaderQuotas.MaxBytesPerRead = 4096;
            binding.ReaderQuotas.MaxDepth = 32;

            var factory = new ChannelFactory<IProxyCacheService>(
                    binding,
                    new EndpointAddress("http://localhost:8080/ProxyCacheService"));

            var proxy = factory.CreateChannel();

            try
            {
                Console.WriteLine($"PARAMS: {originLat}, {originLon}, {destLat}, {destLon}, {originCity}, {destCity}");


                Console.WriteLine($"[RoutingService] Origine: {originCity} ({oLat}, {oLon})");
                Console.WriteLine($"[RoutingService] Destination: {destCity} ({dLat}, {dLon})");

                if (originCity.ToLower() != destCity.ToLower())
                {
                    Console.WriteLine("[RoutingService] Villes différentes détectées");

                    //  Résolution intelligente avec ContractResolver
                    string originContract = ContractResolver.ResolveContractForCoordinate(proxy, oLat, oLon).Result;
                    string destContract = ContractResolver.ResolveContractForCoordinate(proxy, dLat, dLon).Result;

                    Console.WriteLine($"[RoutingService] Contrat origine: {originContract}");
                    Console.WriteLine($"[RoutingService] Contrat destination: {destContract}");

                    if (originContract != destContract)
                    {
                        return new ItineraryResult
                        {
                            Success = false,
                            Message = $"Origin ({originCity}/{originContract}) and destination ({destCity}/{destContract}) use different bike-sharing contracts.",
                            Data = null
                        };
                    }
                    // ✅ Même contrat malgré des villes différentes (ex: Lyon + Villeurbanne)
                    Console.WriteLine($"[RoutingService] Même contrat '{originContract}' pour les deux villes");
                    var stations = proxy.GetStationsByContract(originContract);

                    

                    return ComputeItinerary(proxy, oLat, oLon, dLat, dLon, stations);

                }
                string contract;
                try
                {
                    contract = FindContractForCity(proxy, originCity);
                    Console.WriteLine($"[RoutingService] Contrat trouvé: {contract}");
                }
                catch (Exception ex)
                {
                    // ✅ Fallback si la ville n'a pas de contrat direct
                    Console.WriteLine($"[RoutingService] Ville '{originCity}' sans contrat direct. Recherche du contrat le plus proche...");
                    contract = ContractResolver.ResolveContractForCoordinate(proxy, oLat, oLon).Result;
                    Console.WriteLine($"[RoutingService] Contrat de fallback: {contract}");
                    Console.WriteLine("[RoutingService] Exception: " + ex.Message);
                }
                var stationsForContract = proxy.GetStationsByContract(contract);
                return ComputeItinerary(proxy, oLat, oLon, dLat, dLon, stationsForContract);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoutingService ERROR] {ex.Message}");

                try { ((IClientChannel)proxy).Abort(); } catch { }

                return new ItineraryResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    Data = null
                };
            }
            finally
            {
                try
                {
                    if (((IClientChannel)proxy).State == CommunicationState.Opened)
                    {
                        ((IClientChannel)proxy).Close();
                    }
                }
                catch
                {
                    ((IClientChannel)proxy).Abort();
                }
                try { factory.Close(); } catch { }
            }
        }

        private ItineraryResult ComputeItinerary(
            IProxyCacheService proxy,
            double oLat,
            double oLon,
            double dLat,
            double dLon,
            List<BikeStation> stations)
        {
            // 1. Sélection des 3 meilleures stations candidates
            var startCandidates = GetThreeClosestStartStations(oLat, oLon, stations);
            var endCandidates = GetThreeClosestEndStations(dLat, dLon, stations);

            Console.WriteLine($"[RoutingService] Stations de départ candidates: {startCandidates.Count}");
            Console.WriteLine($"[RoutingService] Stations d'arrivée candidates: {endCandidates.Count}");

            if (!startCandidates.Any() || !endCandidates.Any())
            {
                return new ItineraryResult
                {
                    Success = false,
                    Message = "No valid stations nearby with bikes/stands available."
                };
            }

            // 2. Calcul des vraies distances de marche
            var realStartWalks = ComputeRealWalkOriginToStations(proxy, oLat, oLon, startCandidates);
            var realEndWalks = ComputeRealWalkStationsToDestination(proxy, dLat, dLon, endCandidates);

            // 3. Sélection des meilleures stations
            var bestStart = realStartWalks.OrderBy(x => x.walkingDistance).First();
            var bestEnd = realEndWalks.OrderBy(x => x.walkingDistance).First();

            var startStation = bestStart.station;
            var endStation = bestEnd.station;
            var walk1 = bestStart.walkData;
            var walk2 = bestEnd.walkData;

            Console.WriteLine($"[RoutingService] Station de départ: {startStation.Name} ({walk1.TotalDistance:F0}m)");
            Console.WriteLine($"[RoutingService] Station d'arrivée: {endStation.Name} ({walk2.TotalDistance:F0}m)");

            // 4. Calcul de l'itinéraire vélo
            var bike = GetBikingSegment(proxy,
                startStation.Position.Latitude,
                startStation.Position.Longitude,
                endStation.Position.Latitude,
                endStation.Position.Longitude);

            // 5. Comparaison avec marche directe
            var walkDirect = ParseORSJson(proxy.CallORS("foot-walking",
      $"{oLon.ToString(CultureInfo.InvariantCulture)},{oLat.ToString(CultureInfo.InvariantCulture)}",
      $"{dLon.ToString(CultureInfo.InvariantCulture)},{dLat.ToString(CultureInfo.InvariantCulture)}"), "walk");

            double totalBike = walk1.TotalDuration + bike.TotalDuration + walk2.TotalDuration;
            double walkTotalTime = walkDirect.TotalDuration;

            Console.WriteLine($"[RoutingService] Durée vélo: {totalBike / 60:F1}min vs marche: {walkTotalTime / 60:F1}min");

            string recommendation = totalBike < walkDirect.TotalDuration * 0.9
                ? "bike"
                : "walk";

            Console.WriteLine($"[RoutingService] Recommandation: {recommendation}");

            var allSteps = walk1.Steps.Concat(bike.Steps).Concat(walk2.Steps).ToArray();

            return new ItineraryResult
            {
                Success = true,
                Message = recommendation,
                Data = new ItineraryData
                {
                    TotalDistance = walk1.TotalDistance + bike.TotalDistance + walk2.TotalDistance,
                    TotalDuration = totalBike,
                    Steps = allSteps,
                    Geometry = bike.Geometry
                }
            };
        }

        private double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371000; // meters
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;

            lat1 *= Math.PI / 180;
            lat2 *= Math.PI / 180;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2) *
                       Math.Cos(lat1) * Math.Cos(lat2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private List<BikeStation> GetThreeClosestStartStations(
            double lat,
            double lon,
            IEnumerable<BikeStation> stations)
        {
            return stations
                .Where(s => s.Status == "OPEN")
                .Where(s => s.TotalStands.Availabilities.Bikes > 0)
                .OrderBy(s => Haversine(lat, lon, s.Position.Latitude, s.Position.Longitude))
                .Take(3)
                .ToList();
        }

        private List<BikeStation> GetThreeClosestEndStations(
            double lat,
            double lon,
            IEnumerable<BikeStation> stations)
        {
            return stations
                .Where(s => s.Status == "OPEN")
                .Where(s => s.TotalStands.Availabilities.Stands > 0)
                .OrderBy(s => Haversine(lat, lon, s.Position.Latitude, s.Position.Longitude))
                .Take(3)
                .ToList();
        }

        private string FindContractForCity(IProxyCacheService proxy, string city)
        {
            city = city.ToLower();
            var contracts = proxy.GetAvailableContracts();

            foreach (var c in contracts)
            {
                if (c.Cities != null && c.Cities.Any(x => x.ToLower() == city))
                    return c.Name;
            }
            throw new Exception($"No JCDecaux contract for city {city}");
        }

        private List<(BikeStation station, double walkingDistance, ItineraryData walkData)>
            ComputeRealWalkOriginToStations(
                IProxyCacheService proxy,
                double oLat,
                double oLon,
                List<BikeStation> candidates)
        {
            var result = new List<(BikeStation, double, ItineraryData)>();

            foreach (var st in candidates)
            {
                var walk = GetWalkingSegment(
                    proxy,
                    oLat, oLon,
                    st.Position.Latitude,
                    st.Position.Longitude);

                result.Add((st, walk.TotalDistance, walk));
            }

            return result;
        }

        private List<(BikeStation station, double walkingDistance, ItineraryData walkData)>
            ComputeRealWalkStationsToDestination(
                IProxyCacheService proxy,
                double dLat,
                double dLon,
                List<BikeStation> candidates)
        {
            var result = new List<(BikeStation, double, ItineraryData)>();

            foreach (var st in candidates)
            {
                var walk = GetWalkingSegment(
                    proxy,
                    st.Position.Latitude,
                    st.Position.Longitude,
                    dLat, dLon);

                result.Add((st, walk.TotalDistance, walk));
            }
            return result;
        }
    }
}
