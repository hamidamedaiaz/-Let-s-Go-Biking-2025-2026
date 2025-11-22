using ProxyCacheService;
using RoutingServer.ServiceReference1;
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
        private readonly ProxyCacheServiceClient _proxyClient = new ProxyCacheServiceClient();

        private ItineraryData GetWalkingSegment(
            double lat1, double lon1,
            double lat2, double lon2)
        {
            string orsJson = _proxyClient.CallORS(
                "foot-walking",
                $"{lon1.ToString(CultureInfo.InvariantCulture)},{lat1.ToString(CultureInfo.InvariantCulture)}",
                $"{lon2.ToString(CultureInfo.InvariantCulture)},{lat2.ToString(CultureInfo.InvariantCulture)}");
            return ParseORSJson(orsJson, "walk");
        }

        private ItineraryData GetBikingSegment(
            double lat1, double lon1,
            double lat2, double lon2)
        {
            string orsJson = _proxyClient.CallORS(
                "cycling-regular",
                $"{lon1.ToString(CultureInfo.InvariantCulture)},{lat1.ToString(CultureInfo.InvariantCulture)}",
                $"{lon2.ToString(CultureInfo.InvariantCulture)},{lat2.ToString(CultureInfo.InvariantCulture)}");
            return ParseORSJson(orsJson, "bike");
        }

        private ItineraryData ParseORSJson(string orsJson, string stepType)
        {
            var ors = Newtonsoft.Json.JsonConvert.DeserializeObject<ORSResult>(orsJson);

            if (ors?.Features == null || ors.Features.Count == 0)
            {
                Console.WriteLine("[RoutingService ERROR] No features in ORS response");
                throw new Exception("ORS did not return features");
            }

            var feature = ors.Features[0];

            if (feature.Properties.Segments == null || feature.Properties.Segments.Count == 0)
            {
                Console.WriteLine("[RoutingService ERROR] No segments in feature");
                throw new Exception("No segments in ORS feature");
            }

            var segment = feature.Properties.Segments[0];
            var summary = feature.Properties.Summary;
            var rawSteps = segment.Steps;

            var convertedSteps = rawSteps
                .Select(s => new Step
                {
                    Type = stepType,
                    Instructions = s.Instruction,
                    Distance = s.Distance,
                    Duration = s.Duration
                })
                .ToArray();

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
            try
            {
                double oLat = double.Parse(originLat, CultureInfo.InvariantCulture);
                double oLon = double.Parse(originLon, CultureInfo.InvariantCulture);
                double dLat = double.Parse(destLat, CultureInfo.InvariantCulture);
                double dLon = double.Parse(destLon, CultureInfo.InvariantCulture);

                Console.WriteLine($"[RoutingService] Origine: {originCity} ({oLat}, {oLon})");
                Console.WriteLine($"[RoutingService] Destination: {destCity} ({dLat}, {dLon})");

                string originContract = null;
                string destContract = null;

                try
                {
                    originContract = FindContractForCity(originCity);
                    Console.WriteLine($"[RoutingService] Contrat origine: {originContract}");
                }
                catch (Exception)
                {
                    originContract = null;


                    // Console.WriteLine($"[RoutingService] Pas de contrat pour '{originCity}', recherche par coordonnées...");
                    // ✅ Créer un proxy IProxyCacheService pour ContractResolver
                    //originContract = ContractResolver.ResolveContractForCoordinate(proxy, oLat, oLon).Result;
                    //Console.WriteLine($"[RoutingService] Contrat origine (fallback): {originContract}");
                }

                try 
                {
                    destContract = FindContractForCity(destCity);
                    Console.WriteLine($"[RoutingService] Contrat destination: {destContract}");
                }
                catch (Exception)
                {

                    destContract = null;
                    // Console.WriteLine($"[RoutingService] Pas de contrat pour '{destCity}', recherche par coordonnées...");
                    //destContract = ContractResolver.ResolveContractForCoordinate(proxy, dLat, dLon).Result;
                    // Console.WriteLine($"[RoutingService] Contrat destination (fallback): {destContract}");

                }

                if (!originContract.Equals(destContract, StringComparison.OrdinalIgnoreCase) || (originCity == null || destCity == null))
                {
                    
                    Console.WriteLine($"[RoutingService] ❌ Contrats différents ({originContract} ≠ {destContract}) → Marche directe uniquement");

                    var walkOnly = GetWalkingSegment(oLat, oLon, dLat, dLon);

                    return new ItineraryResult
                    {
                        Success = true,
                        Message = "walk",
                        Data = walkOnly
                    };
                }
                else
                {
                    var stations = _proxyClient.GetStationsByContract(originContract).ToList();
                    return ComputeItinerary(oLat, oLon, dLat, dLon, stations);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoutingService ERROR] {ex.Message}");

                return new ItineraryResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    Data = null
                };
            }
            // ✅ PAS de finally qui ferme le proxy statique !
        }

        // ✅ Helper pour créer un proxy temporaire pour ContractResolver


        private ItineraryResult ComputeItinerary(
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
                    Success = true,
                    Message = "walk",
                    Data = GetWalkingSegment(oLat, oLon, dLat, dLon)
                };
            }

            var walkDirect = ParseORSJson(_proxyClient.CallORS("foot-walking",
                $"{oLon.ToString(CultureInfo.InvariantCulture)},{oLat.ToString(CultureInfo.InvariantCulture)}",
                $"{dLon.ToString(CultureInfo.InvariantCulture)},{dLat.ToString(CultureInfo.InvariantCulture)}"), "walk");

            var realStartWalks = ComputeRealWalkOriginToStations(oLat, oLon, startCandidates);
            var realEndWalks = ComputeRealWalkStationsToDestination(dLat, dLon, endCandidates);

            var bestStart = realStartWalks.OrderBy(x => x.walkingDistance).First();
            var bestEnd = realEndWalks.OrderBy(x => x.walkingDistance).First();

            var startStation = bestStart.station;
            var endStation = bestEnd.station;
            var walk1 = bestStart.walkData;
            var walk2 = bestEnd.walkData;

            Console.WriteLine($"[RoutingService] Station de départ: {startStation.Name} ({walk1.TotalDistance:F0}m)");
            Console.WriteLine($"[RoutingService] Station d'arrivée: {endStation.Name} ({walk2.TotalDistance:F0}m)");

            if ((walk1.TotalDuration + walk2.TotalDuration) > walkDirect.TotalDuration * 0.5)
            {
                Console.WriteLine($"[RoutingService] ⚠️ Marches trop longues, retour marche directe");
                return new ItineraryResult
                {
                    Success = true,
                    Message = "walk",
                    Data = walkDirect
                };
            }

            var bike = GetBikingSegment(
                startStation.Position.Latitude,
                startStation.Position.Longitude,
                endStation.Position.Latitude,
                endStation.Position.Longitude);

            double totalBike = walk1.TotalDuration + bike.TotalDuration + walk2.TotalDuration;
            double walkTotalTime = walkDirect.TotalDuration;

            Console.WriteLine($"[RoutingService] Durée vélo: {totalBike / 60:F1}min vs marche: {walkTotalTime / 60:F1}min");

            string recommendation = totalBike < walkDirect.TotalDuration * 0.9
                ? "bike"
                : "walk";

            Console.WriteLine($"[RoutingService] Recommandation: {recommendation}");

            var allSteps = walk1.Steps.Concat(bike.Steps).Concat(walk2.Steps).ToArray();

            var combinedCoordinates = walk1.Geometry.Coordinates
                .Concat(bike.Geometry.Coordinates)
                .Concat(walk2.Geometry.Coordinates)
                .ToArray();

            Console.WriteLine($"[RoutingService] Géométrie totale: {combinedCoordinates.Length} points");

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
            double R = 6371000;
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

        private string FindContractForCity(string city)
        {
            string nc = Normalize(city);

            var contracts = _proxyClient.GetAvailableContracts();

            foreach (var contract in contracts)
            {
                if (contract.Cities == null || contract.Cities.Count == 0)
                    continue;

                foreach (var city2 in contract.Cities)
                {
                    string nc2 = Normalize(city2);

                    if (nc == nc2)
                        return contract.Name;

                    if (nc.StartsWith(nc2))
                        return contract.Name;

                    if (nc2.StartsWith(nc))
                        return contract.Name;
                }
            }

            throw new Exception($"No JCDecaux contract matches the city '{city}'");
        }


        private List<(BikeStation station, double walkingDistance, ItineraryData walkData)>
            ComputeRealWalkOriginToStations(
                double oLat,
                double oLon,
                List<BikeStation> candidates)
        {
            var tasks = candidates.Select(st =>
            {
                return System.Threading.Tasks.Task.Run(() =>
                {
                    var walk = GetWalkingSegment(oLat, oLon, st.Position.Latitude, st.Position.Longitude);
                    return (st, walk.TotalDistance, walk);
                });
            }).ToArray();

            var results = System.Threading.Tasks.Task.WhenAll(tasks).Result;
            return results.ToList();
        }

        private List<(BikeStation station, double walkingDistance, ItineraryData walkData)>
            ComputeRealWalkStationsToDestination(
                double dLat,
                double dLon,
                List<BikeStation> candidates)
        {
            var tasks = candidates.Select(st =>
            {
                return System.Threading.Tasks.Task.Run(() =>
                {
                    var walk = GetWalkingSegment(st.Position.Latitude, st.Position.Longitude, dLat, dLon);
                    return (st, walk.TotalDistance, walk);
                });
            }).ToArray();

            var results = System.Threading.Tasks.Task.WhenAll(tasks).Result;
            return results.ToList();
        }
    
    private string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";

            return s
                .ToLower()
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("'", "")
                .Replace("’", "")
                .Replace("é", "e")
                .Replace("è", "e")
                .Replace("ê", "e")
                .Replace("à", "a")
                .Replace("â", "a")
                .Replace("ô", "o")
                .Replace("î", "i");
        }

    } }