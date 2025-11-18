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
            var ors = Newtonsoft.Json.JsonConvert.DeserializeObject<ORSresulte>(orsJson);

            // Vérifier que ORS a bien retourné des features
            if (ors?.Features == null || ors.Features.Count == 0)
            {
                Console.WriteLine("[RoutingService ERROR] No features in ORS response");
                throw new Exception("ORS did not return features");
            }

            var feature = ors.Features[0];

            // Vérifier les segments
            if (feature.Properties.Segments == null || feature.Properties.Segments.Count == 0)
            {
                Console.WriteLine("[RoutingService ERROR] No segments in feature");
                throw new Exception("No segments in ORS feature");
            }

            var segment = feature.Properties.Segments[0];

            // Résumé
            var summary = feature.Properties.Summary;

            // Steps bruts ORS
            var rawSteps = segment.Steps;

            // Convertir les steps dans ton modèle
            var convertedSteps = rawSteps
                .Select(s => new Step
                {
                    Type = stepType,
                    Instructions = s.Instruction,
                    Distance = s.Distance,
                    Duration = s.Duration
                })
                .ToArray();

            // Géométrie : coordonnées GeoJSON déjà prêtes
            double[][] geometryArray = Array.Empty<double[]>();

            if (feature.Geometry?.Coordinates != null && feature.Geometry.Coordinates.Count > 0)
            {
                geometryArray = feature.Geometry.Coordinates
                    .Select(c => c.ToArray())
                    .ToArray();
            }

            return new ItineraryData
            {
                TotalDistance = summary.Distance,
                TotalDuration = summary.Duration,
                Steps = convertedSteps,
                Geometry = new Geometry { Coordinates = geometryArray }
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
            // Configurer le binding client avec des quotas augmentés
            var binding = new BasicHttpBinding
            {
                MaxReceivedMessageSize = 2147483647,
                MaxBufferSize = 2147483647,
                ReaderQuotas = new System.Xml.XmlDictionaryReaderQuotas
                {
                    MaxDepth = 32,
                    MaxStringContentLength = 2147483647,
                    MaxArrayLength = 2147483647,
                    MaxBytesPerRead = 2147483647,
                    MaxNameTableCharCount = 2147483647
                }
            };

            var factory = new ChannelFactory<IProxyCacheService>(
                    binding,
                    new EndpointAddress("http://localhost:8080/ProxyCacheService"));

            var proxy = factory.CreateChannel();

            try
            {
                double oLat = double.Parse(originLat, CultureInfo.InvariantCulture);
                double oLon = double.Parse(originLon, CultureInfo.InvariantCulture);
                double dLat = double.Parse(destLat, CultureInfo.InvariantCulture);
                double dLon = double.Parse(destLon, CultureInfo.InvariantCulture);

                Console.WriteLine($"[RoutingService] Origine: {originCity} ({oLat}, {oLon})");
                Console.WriteLine($"[RoutingService] Destination: {destCity} ({dLat}, {dLon})");

                string contract;
        
                if (originCity.ToLower() != destCity.ToLower())
                {
                    Console.WriteLine("[RoutingService] Villes différentes détectées");

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
                    Console.WriteLine($"[RoutingService] Même contrat '{originContract}' pour les deux villes");
                    contract = originContract;
                }
                else
                {
                    // Même ville
                    try
                    {
                        contract = FindContractForCity(proxy, originCity);
                        Console.WriteLine($"[RoutingService] Contrat trouvé: {contract}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RoutingService] Ville '{originCity}' sans contrat direct. Recherche du contrat le plus proche...");
                        contract = ContractResolver.ResolveContractForCoordinate(proxy, oLat, oLon).Result;
                        Console.WriteLine($"[RoutingService] Contrat de fallback: {contract}");
                        Console.WriteLine("[RoutingService] Exception: " + ex.Message);
                    }
                }
        
                var stations = proxy.GetStationsByContract(contract);
                return ComputeItinerary(proxy, oLat, oLon, dLat, dLon, stations);
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

            var realStartWalks = ComputeRealWalkOriginToStations(proxy, oLat, oLon, startCandidates);
            var realEndWalks = ComputeRealWalkStationsToDestination(proxy, dLat, dLon, endCandidates);

            var bestStart = realStartWalks.OrderBy(x => x.walkingDistance).First();
            var bestEnd = realEndWalks.OrderBy(x => x.walkingDistance).First();

            var startStation = bestStart.station;
            var endStation = bestEnd.station;
            var walk1 = bestStart.walkData;
            var walk2 = bestEnd.walkData;

            Console.WriteLine($"[RoutingService] Station de départ: {startStation.Name} ({walk1.TotalDistance:F0}m)");
            Console.WriteLine($"[RoutingService] Station d'arrivée: {endStation.Name} ({walk2.TotalDistance:F0}m)");

            var bike = GetBikingSegment(proxy,
                startStation.Position.Latitude,
                startStation.Position.Longitude,
                endStation.Position.Latitude,
                endStation.Position.Longitude);

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

            // Combiner les géométries de walk1, bike et walk2
            var combinedCoordinates = walk1.Geometry.Coordinates
                .Concat(bike.Geometry.Coordinates)
                .Concat(walk2.Geometry.Coordinates)
                .ToArray();

            Console.WriteLine($"[RoutingService] Géométrie totale: {combinedCoordinates.Length} points (walk1={walk1.Geometry.Coordinates.Length}, bike={bike.Geometry.Coordinates.Length}, walk2={walk2.Geometry.Coordinates.Length})");

            return new ItineraryResult
            {
                Success = true,
                Message = recommendation,
                Data = new ItineraryData
                {
                    TotalDistance = walk1.TotalDistance + bike.TotalDistance + walk2.TotalDistance,
                    TotalDuration = totalBike,
                    Steps = allSteps,
                    Geometry = new Geometry { Coordinates = combinedCoordinates }
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
