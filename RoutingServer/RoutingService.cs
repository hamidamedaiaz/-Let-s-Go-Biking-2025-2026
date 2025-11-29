using RoutingServer.ServiceReference;
using SharedModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RoutingServer
{
    /// <summary>
    /// Provides routing services for bike-sharing itineraries.
    /// Supports multiple routing strategies: intra-contract, inter-contract, hybrid, and multi-contract.
    /// </summary>
    public class RoutingService : IRoutingService
    {
        private readonly ProxyCacheServiceClient _proxyClient = new ProxyCacheServiceClient();

        private const double MultiContractDistanceThreshold = 50000; // 50 km in meters
        private const double HybridTimeAdvantageRatio = 0.9; // Bike must be 10% faster than walking

        #region Segment Retrieval Methods

        /// <summary>
        /// Retrieves a walking segment between two coordinates.
        /// </summary>
        /// <param name="lat1">Starting latitude</param>
        /// <param name="lon1">Starting longitude</param>
        /// <param name="lat2">Ending latitude</param>
        /// <param name="lon2">Ending longitude</param>
        /// <returns>Itinerary data for the walking segment</returns>
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

        /// <summary>
        /// Retrieves a biking segment between two coordinates.
        /// </summary>
        /// <param name="lat1">Starting latitude</param>
        /// <param name="lon1">Starting longitude</param>
        /// <param name="lat2">Ending latitude</param>
        /// <param name="lon2">Ending longitude</param>
        /// <returns>Itinerary data for the biking segment</returns>
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

        /// <summary>
        /// Parses OpenRouteService JSON response and converts it to internal itinerary data format.
        /// </summary>
        /// <param name="orsJson">Raw JSON response from ORS API</param>
        /// <param name="stepType">Type of movement (walk or bike)</param>
        /// <returns>Parsed itinerary data with coordinates and steps</returns>
        private ItineraryData ParseORSJson(string orsJson, string stepType)
        {
            var ors = Newtonsoft.Json.JsonConvert.DeserializeObject<ORSResult>(orsJson);

            if (ors?.Features == null || ors.Features.Count == 0)
            {
                Console.WriteLine("[RoutingService] ERROR: No features in ORS response");
                throw new Exception("ORS did not return features");
            }

            var feature = ors.Features[0];

            if (feature.Properties.Segments == null || feature.Properties.Segments.Count == 0)
            {
                Console.WriteLine("[RoutingService] ERROR: No segments in feature");
                throw new Exception("No segments in ORS feature");
            }

            var segment = feature.Properties.Segments[0];
            var summary = feature.Properties.Summary;
            var rawSteps = segment.Steps;

            double[][] allCoordinates = Array.Empty<double[]>();

            if (feature.Geometry?.Coordinates != null && feature.Geometry.Coordinates.Count > 0)
            {
                allCoordinates = feature.Geometry.Coordinates
                    .Select(c => c.ToArray())
                    .ToArray();
            }

            Console.WriteLine($"[RoutingService] Parsing {stepType} segment: {rawSteps.Count} steps, {allCoordinates.Length} coordinates");

            var convertedSteps = new List<Step>();
            double totalDistance = rawSteps.Sum(s => s.Distance);
            int currentIndex = 0;

            for (int i = 0; i < rawSteps.Count; i++)
            {
                var orsStep = rawSteps[i];
                int startIdx = currentIndex;
                int endIdx = allCoordinates.Length;

                if (orsStep.WayPoints != null && orsStep.WayPoints.Count > 0)
                {
                    startIdx = orsStep.WayPoints[0];

                    if (i < rawSteps.Count - 1)
                    {
                        var nextStep = rawSteps[i + 1];
                        if (nextStep.WayPoints != null && nextStep.WayPoints.Count > 0)
                        {
                            endIdx = nextStep.WayPoints[0];
                        }
                    }

                    currentIndex = endIdx;
                }
                else
                {
                    double stepRatio = orsStep.Distance / totalDistance;
                    int pointsInStep = Math.Max(2, (int)(allCoordinates.Length * stepRatio));
                    endIdx = Math.Min(currentIndex + pointsInStep, allCoordinates.Length);

                    if (i == rawSteps.Count - 1)
                    {
                        endIdx = allCoordinates.Length;
                    }

                    currentIndex = endIdx;
                }

                var stepCoordinates = allCoordinates
                    .Skip(startIdx)
                    .Take(Math.Max(1, endIdx - startIdx))
                    .ToArray();

                Console.WriteLine($"[RoutingService] Step {i + 1}/{rawSteps.Count}: {stepType} - indices [{startIdx}, {endIdx}] = {stepCoordinates.Length} coords");

                convertedSteps.Add(new Step
                {
                    Type = stepType,
                    Instructions = orsStep.Instruction,
                    Distance = orsStep.Distance,
                    Duration = orsStep.Duration,
                    Coordinates = stepCoordinates
                });
            }

            Console.WriteLine($"[RoutingService] Successfully created {convertedSteps.Count} steps with coordinates");

            return new ItineraryData
            {
                TotalDistance = summary.Distance,
                TotalDuration = summary.Duration,
                Steps = convertedSteps.ToArray(),
                Geometry = new Geometry
                {
                    Coordinates = allCoordinates
                }
            };
        }

        #endregion

        #region Main Entry Point

        /// <summary>
        /// Main entry point for itinerary calculation.
        /// Determines the optimal routing strategy based on distance and contract availability.
        /// </summary>
        /// <param name="originLat">Origin latitude</param>
        /// <param name="originLon">Origin longitude</param>
        /// <param name="originCity">Origin city name</param>
        /// <param name="destLat">Destination latitude</param>
        /// <param name="destLon">Destination longitude</param>
        /// <param name="destCity">Destination city name</param>
        /// <returns>Complete itinerary result with recommendation</returns>
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

                LogRouteRequest(originCity, oLat, oLon, destCity, dLat, dLon);

                double totalDistance = Haversine(oLat, oLon, dLat, dLon);
                Console.WriteLine($"[RoutingService] Total distance (crow flight): {totalDistance / 1000:F1} km");

                string originContract = ResolveContract(originCity, "origin");
                string destContract = ResolveContract(destCity, "destination");

                return DetermineRoutingStrategy(oLat, oLon, dLat, dLon, totalDistance, originContract, destContract);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoutingService] ERROR: {ex.Message}");
                return new ItineraryResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    Data = null
                };
            }
        }

        /// <summary>
        /// Logs the initial route request details.
        /// </summary>
        private void LogRouteRequest(string originCity, double oLat, double oLon, string destCity, double dLat, double dLon)
        {
            Console.WriteLine("[RoutingService] ========================================");
            Console.WriteLine($"[RoutingService] Route request received");
            Console.WriteLine($"[RoutingService] Origin: {originCity} ({oLat}, {oLon})");
            Console.WriteLine($"[RoutingService] Destination: {destCity} ({dLat}, {dLon})");
        }

        /// <summary>
        /// Attempts to resolve a contract for a given city.
        /// </summary>
        /// <param name="city">City name</param>
        /// <param name="locationType">Type of location (origin/destination) for logging</param>
        /// <returns>Contract name if found, null otherwise</returns>
        private string ResolveContract(string city, string locationType)
        {
            try
            {
                string contract = FindContractForCity(city);
                Console.WriteLine($"[RoutingService] Contract for {locationType} resolved: {contract}");
                return contract;
            }
            catch
            {
                Console.WriteLine($"[RoutingService] No contract found for {locationType}: {city}");
                return null;
            }
        }

        /// <summary>
        /// Determines the optimal routing strategy based on distance and contract availability.
        /// </summary>
        /// <returns>Itinerary result with selected routing strategy</returns>
        private ItineraryResult DetermineRoutingStrategy(
            double oLat, double oLon, double dLat, double dLon,
            double totalDistance, string originContract, string destContract)
        {
            if (totalDistance > MultiContractDistanceThreshold)
            {
                Console.WriteLine("[RoutingService] Strategy: Multi-contract (long distance)");
                return ComputeMultiContractItinerary(oLat, oLon, dLat, dLon);
            }

            if (originContract != null && destContract != null &&
                originContract.Equals(destContract, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[RoutingService] Strategy: Intra-contract ({originContract})");
                var stations = _proxyClient.GetStationsByContract(originContract).ToList();
                return ComputeItinerary(oLat, oLon, dLat, dLon, stations);
            }

            if (originContract != null && destContract != null &&
                !originContract.Equals(destContract, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[RoutingService] Strategy: Inter-contract");
                return ComputeInterContractItinerary(oLat, oLon, dLat, dLon, originContract, destContract);
            }

            if (originContract == null && destContract != null)
            {
                Console.WriteLine($"[RoutingService] Strategy: Hybrid walk-to-bike (destination: {destContract})");
                return ComputeHybridWalkToBike(oLat, oLon, dLat, dLon, destContract);
            }

            if (originContract != null && destContract == null)
            {
                Console.WriteLine($"[RoutingService] Strategy: Hybrid bike-to-walk (origin: {originContract})");
                return ComputeHybridBikeToWalk(oLat, oLon, dLat, dLon, originContract);
            }

            Console.WriteLine("[RoutingService] Strategy: Walking only (no contracts available)");
            var walkOnly = GetWalkingSegment(oLat, oLon, dLat, dLon);
            return new ItineraryResult
            {
                Success = true,
                Message = "walk",
                Data = walkOnly
            };
        }

        #endregion

        #region Hybrid Cases

        /// <summary>
        /// Computes a hybrid itinerary starting without a contract and ending with one.
        /// Route: Walk to entry station → Bike within contract → Walk to destination.
        /// </summary>
        /// <param name="destContract">Destination contract name</param>
        /// <returns>Hybrid itinerary result</returns>
        private ItineraryResult ComputeHybridWalkToBike(
            double oLat,
            double oLon,
            double dLat,
            double dLon,
            string destContract)
        {
            Console.WriteLine("[RoutingService] ========================================");
            Console.WriteLine("[RoutingService] Computing hybrid walk-to-bike itinerary");
            Console.WriteLine($"[RoutingService] Destination contract: {destContract}");

            var destStations = _proxyClient.GetStationsByContract(destContract).ToList();
            Console.WriteLine($"[RoutingService] Available stations in {destContract}: {destStations.Count}");

            var entryCandidates = GetThreeClosestStartStations(oLat, oLon, destStations);

            if (!entryCandidates.Any())
            {
                Console.WriteLine("[RoutingService] No available stations, falling back to walking");
                return new ItineraryResult
                {
                    Success = true,
                    Message = "walk",
                    Data = GetWalkingSegment(oLat, oLon, dLat, dLon)
                };
            }

            var realEntryWalks = ComputeRealWalkOriginToStations(oLat, oLon, entryCandidates);
            var bestEntryStation = realEntryWalks.OrderBy(x => x.walkingDistance).First();
            var walk1 = bestEntryStation.walkData;

            Console.WriteLine($"[RoutingService] Entry station selected: {bestEntryStation.station.Name}");
            Console.WriteLine($"[RoutingService] Walk to entry: {walk1.TotalDistance:F0}m ({walk1.TotalDuration / 60:F1}min)");

            var finalCandidates = GetThreeClosestEndStations(dLat, dLon, destStations);

            if (!finalCandidates.Any())
            {
                Console.WriteLine("[RoutingService] No available end stations");
                var fallbackWalk = GetWalkingSegment(
                    bestEntryStation.station.Position.Latitude,
                    bestEntryStation.station.Position.Longitude,
                    dLat,
                    dLon);

                return CombineSegments(new[] { walk1, fallbackWalk }, "walk");
            }

            var realFinalWalks = ComputeRealWalkStationsToDestination(dLat, dLon, finalCandidates);
            var bestFinalStation = realFinalWalks.OrderBy(x => x.walkingDistance).First();

            Console.WriteLine($"[RoutingService] Final station selected: {bestFinalStation.station.Name}");

            var bike = GetBikingSegment(
                bestEntryStation.station.Position.Latitude,
                bestEntryStation.station.Position.Longitude,
                bestFinalStation.station.Position.Latitude,
                bestFinalStation.station.Position.Longitude);

            Console.WriteLine($"[RoutingService] Bike segment: {bike.TotalDistance:F0}m ({bike.TotalDuration / 60:F1}min)");

            var walk2 = bestFinalStation.walkData;
            Console.WriteLine($"[RoutingService] Walk to destination: {walk2.TotalDistance:F0}m ({walk2.TotalDuration / 60:F1}min)");

            var walkDirect = GetWalkingSegment(oLat, oLon, dLat, dLon);
            double hybridTime = walk1.TotalDuration + bike.TotalDuration + walk2.TotalDuration;

            Console.WriteLine($"[RoutingService] Hybrid duration: {hybridTime / 60:F1}min vs walk: {walkDirect.TotalDuration / 60:F1}min");

            string recommendation = hybridTime < walkDirect.TotalDuration * HybridTimeAdvantageRatio ? "bike" : "walk";
            Console.WriteLine($"[RoutingService] Recommendation: {recommendation}");

            return CombineSegments(new[] { walk1, bike, walk2 }, recommendation);
        }

        /// <summary>
        /// Computes a hybrid itinerary starting with a contract and ending without one.
        /// Route: Walk to start station → Bike within contract → Walk to destination.
        /// </summary>
        /// <param name="originContract">Origin contract name</param>
        /// <returns>Hybrid itinerary result</returns>
        private ItineraryResult ComputeHybridBikeToWalk(
            double oLat,
            double oLon,
            double dLat,
            double dLon,
            string originContract)
        {
            Console.WriteLine("[RoutingService] ========================================");
            Console.WriteLine("[RoutingService] Computing hybrid bike-to-walk itinerary");
            Console.WriteLine($"[RoutingService] Origin contract: {originContract}");

            var originStations = _proxyClient.GetStationsByContract(originContract).ToList();
            Console.WriteLine($"[RoutingService] Available stations in {originContract}: {originStations.Count}");

            var startCandidates = GetThreeClosestStartStations(oLat, oLon, originStations);

            if (!startCandidates.Any())
            {
                Console.WriteLine("[RoutingService] No available stations, falling back to walking");
                return new ItineraryResult
                {
                    Success = true,
                    Message = "walk",
                    Data = GetWalkingSegment(oLat, oLon, dLat, dLon)
                };
            }

            var realStartWalks = ComputeRealWalkOriginToStations(oLat, oLon, startCandidates);
            var bestStartStation = realStartWalks.OrderBy(x => x.walkingDistance).First();
            var walk1 = bestStartStation.walkData;

            Console.WriteLine($"[RoutingService] Start station selected: {bestStartStation.station.Name}");
            Console.WriteLine($"[RoutingService] Walk to start: {walk1.TotalDistance:F0}m ({walk1.TotalDuration / 60:F1}min)");

            var allExitCandidates = originStations
                .Where(s => s.Status == "OPEN")
                .Where(s => s.TotalStands.Availabilities.Stands > 0)
                .ToList();

            if (!allExitCandidates.Any())
            {
                Console.WriteLine("[RoutingService] No available exit stations");
                return new ItineraryResult
                {
                    Success = true,
                    Message = "walk",
                    Data = GetWalkingSegment(oLat, oLon, dLat, dLon)
                };
            }

            var bestExitStation = allExitCandidates
                .OrderBy(s => Haversine(s.Position.Latitude, s.Position.Longitude, dLat, dLon))
                .First();

            Console.WriteLine($"[RoutingService] Exit station selected: {bestExitStation.Name}");
            Console.WriteLine($"[RoutingService] Distance to destination: {Haversine(bestExitStation.Position.Latitude, bestExitStation.Position.Longitude, dLat, dLon):F0}m");

            var bike = GetBikingSegment(
                bestStartStation.station.Position.Latitude,
                bestStartStation.station.Position.Longitude,
                bestExitStation.Position.Latitude,
                bestExitStation.Position.Longitude);

            Console.WriteLine($"[RoutingService] Bike segment: {bike.TotalDistance:F0}m ({bike.TotalDuration / 60:F1}min)");

            var walk2 = GetWalkingSegment(
                bestExitStation.Position.Latitude,
                bestExitStation.Position.Longitude,
                dLat,
                dLon);

            Console.WriteLine($"[RoutingService] Walk to destination: {walk2.TotalDistance:F0}m ({walk2.TotalDuration / 60:F1}min)");

            var walkDirect = GetWalkingSegment(oLat, oLon, dLat, dLon);
            double hybridTime = walk1.TotalDuration + bike.TotalDuration + walk2.TotalDuration;

            Console.WriteLine($"[RoutingService] Hybrid duration: {hybridTime / 60:F1}min vs walk: {walkDirect.TotalDuration / 60:F1}min");

            string recommendation = hybridTime < walkDirect.TotalDuration * HybridTimeAdvantageRatio ? "bike" : "walk";
            Console.WriteLine($"[RoutingService] Recommendation: {recommendation}");

            return CombineSegments(new[] { walk1, bike, walk2 }, recommendation);
        }

        #endregion

        #region Intra-Contract Itinerary

        /// <summary>
        /// Computes an itinerary within a single contract.
        /// Route: Walk to start station → Bike to end station → Walk to destination.
        /// </summary>
        /// <param name="stations">List of available stations in the contract</param>
        /// <returns>Intra-contract itinerary result</returns>
        private ItineraryResult ComputeItinerary(
            double oLat,
            double oLon,
            double dLat,
            double dLon,
            List<BikeStation> stations)
        {
            Console.WriteLine("[RoutingService] Computing intra-contract itinerary");

            var startCandidates = GetThreeClosestStartStations(oLat, oLon, stations);
            var endCandidates = GetThreeClosestEndStations(dLat, dLon, stations);

            Console.WriteLine($"[RoutingService] Start station candidates: {startCandidates.Count}");
            Console.WriteLine($"[RoutingService] End station candidates: {endCandidates.Count}");

            if (!startCandidates.Any() || !endCandidates.Any())
            {
                Console.WriteLine("[RoutingService] Insufficient stations available, falling back to walking");
                return new ItineraryResult
                {
                    Success = true,
                    Message = "walk",
                    Data = GetWalkingSegment(oLat, oLon, dLat, dLon)
                };
            }

            var walkDirect = GetWalkingSegment(oLat, oLon, dLat, dLon);
            var realStartWalks = ComputeRealWalkOriginToStations(oLat, oLon, startCandidates);
            var realEndWalks = ComputeRealWalkStationsToDestination(dLat, dLon, endCandidates);

            var bestStart = realStartWalks.OrderBy(x => x.walkingDistance).First();
            var bestEnd = realEndWalks.OrderBy(x => x.walkingDistance).First();

            var startStation = bestStart.station;
            var endStation = bestEnd.station;
            var walk1 = bestStart.walkData;
            var walk2 = bestEnd.walkData;

            Console.WriteLine($"[RoutingService] Start station: {startStation.Name} ({walk1.TotalDistance:F0}m)");
            Console.WriteLine($"[RoutingService] End station: {endStation.Name} ({walk2.TotalDistance:F0}m)");

            if ((walk1.TotalDuration + walk2.TotalDuration) > walkDirect.TotalDuration * 0.5)
            {
                Console.WriteLine("[RoutingService] Walking segments too long, recommending direct walk");
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

            Console.WriteLine($"[RoutingService] Bike route duration: {totalBike / 60:F1}min vs walk: {walkTotalTime / 60:F1}min");

            string recommendation = totalBike < walkDirect.TotalDuration * HybridTimeAdvantageRatio ? "bike" : "walk";
            Console.WriteLine($"[RoutingService] Recommendation: {recommendation}");

            var allSteps = walk1.Steps.Concat(bike.Steps).Concat(walk2.Steps).ToArray();
            var combinedCoordinates = walk1.Geometry.Coordinates
                .Concat(bike.Geometry.Coordinates)
                .Concat(walk2.Geometry.Coordinates)
                .ToArray();

            Console.WriteLine($"[RoutingService] Total geometry points: {combinedCoordinates.Length}");

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

        #endregion

        #region Inter-Contract Itinerary

        /// <summary>
        /// Computes an itinerary spanning multiple contracts.
        /// Route: Walk → Bike (Contract A) → Walk between contracts → Bike (Contract B) → Walk.
        /// </summary>
        /// <param name="originContract">Origin contract name</param>
        /// <param name="destContract">Destination contract name</param>
        /// <returns>Inter-contract itinerary result</returns>
        private ItineraryResult ComputeInterContractItinerary(
            double oLat,
            double oLon,
            double dLat,
            double dLon,
            string originContract,
            string destContract)
        {
            Console.WriteLine("[RoutingService] ========================================");
            Console.WriteLine("[RoutingService] Computing inter-contract itinerary");
            Console.WriteLine($"[RoutingService] Origin contract: {originContract}");
            Console.WriteLine($"[RoutingService] Destination contract: {destContract}");

            var originStations = _proxyClient.GetStationsByContract(originContract).ToList();
            var destStations = _proxyClient.GetStationsByContract(destContract).ToList();

            Console.WriteLine($"[RoutingService] Origin stations: {originStations.Count}");
            Console.WriteLine($"[RoutingService] Destination stations: {destStations.Count}");

            var startCandidates = GetThreeClosestStartStations(oLat, oLon, originStations);

            if (!startCandidates.Any())
            {
                Console.WriteLine("[RoutingService] No available start stations, falling back to walking");
                return new ItineraryResult
                {
                    Success = true,
                    Message = "walk",
                    Data = GetWalkingSegment(oLat, oLon, dLat, dLon)
                };
            }

            var realStartWalks = ComputeRealWalkOriginToStations(oLat, oLon, startCandidates);
            var bestStartStation = realStartWalks.OrderBy(x => x.walkingDistance).First();
            var walk1 = bestStartStation.walkData;

            Console.WriteLine($"[RoutingService] Start station: {bestStartStation.station.Name}");
            Console.WriteLine($"[RoutingService] Walk to start: {walk1.TotalDistance:F0}m ({walk1.TotalDuration / 60:F1}min)");

            var allExitCandidates = originStations
                .Where(s => s.Status == "OPEN")
                .Where(s => s.TotalStands.Availabilities.Stands > 0)
                .ToList();

            if (!allExitCandidates.Any())
            {
                Console.WriteLine("[RoutingService] No available exit stations");
                return new ItineraryResult
                {
                    Success = true,
                    Message = "walk",
                    Data = GetWalkingSegment(oLat, oLon, dLat, dLon)
                };
            }

            Console.WriteLine($"[RoutingService] Exit station candidates in {originContract}: {allExitCandidates.Count}");

            var bestExitStation = FindBestExitStation(allExitCandidates, destStations);
            Console.WriteLine($"[RoutingService] Exit station selected: {bestExitStation.Name}");

            var bike1 = GetBikingSegment(
                bestStartStation.station.Position.Latitude,
                bestStartStation.station.Position.Longitude,
                bestExitStation.Position.Latitude,
                bestExitStation.Position.Longitude);

            Console.WriteLine($"[RoutingService] Bike in {originContract}: {bike1.TotalDistance:F0}m ({bike1.TotalDuration / 60:F1}min)");

            var entryCandidates = GetThreeClosestStartStations(
                bestExitStation.Position.Latitude,
                bestExitStation.Position.Longitude,
                destStations);

            if (!entryCandidates.Any())
            {
                Console.WriteLine("[RoutingService] No available entry stations in destination contract");
                var fallbackWalk = GetWalkingSegment(
                    bestExitStation.Position.Latitude,
                    bestExitStation.Position.Longitude,
                    dLat,
                    dLon);

                return CombineSegments(new[] { walk1, bike1, fallbackWalk }, "walk");
            }

            var realEntryWalks = ComputeRealWalkOriginToStations(
                bestExitStation.Position.Latitude,
                bestExitStation.Position.Longitude,
                entryCandidates);

            var bestEntryStation = realEntryWalks.OrderBy(x => x.walkingDistance).First();
            var walk2 = bestEntryStation.walkData;

            Console.WriteLine($"[RoutingService] Entry station in {destContract}: {bestEntryStation.station.Name}");
            Console.WriteLine($"[RoutingService] Walk between contracts: {walk2.TotalDistance:F0}m ({walk2.TotalDuration / 60:F1}min)");

            var finalCandidates = GetThreeClosestEndStations(dLat, dLon, destStations);

            if (!finalCandidates.Any())
            {
                Console.WriteLine("[RoutingService] No available final stations");
                var fallbackWalk = GetWalkingSegment(
                    bestEntryStation.station.Position.Latitude,
                    bestEntryStation.station.Position.Longitude,
                    dLat,
                    dLon);

                return CombineSegments(new[] { walk1, bike1, walk2, fallbackWalk }, "walk");
            }

            var realFinalWalks = ComputeRealWalkStationsToDestination(dLat, dLon, finalCandidates);
            var bestFinalStation = realFinalWalks.OrderBy(x => x.walkingDistance).First();

            Console.WriteLine($"[RoutingService] Final station: {bestFinalStation.station.Name}");

            var bike2 = GetBikingSegment(
                bestEntryStation.station.Position.Latitude,
                bestEntryStation.station.Position.Longitude,
                bestFinalStation.station.Position.Latitude,
                bestFinalStation.station.Position.Longitude);

            Console.WriteLine($"[RoutingService] Bike in {destContract}: {bike2.TotalDistance:F0}m ({bike2.TotalDuration / 60:F1}min)");

            var walk3 = bestFinalStation.walkData;
            Console.WriteLine($"[RoutingService] Walk to destination: {walk3.TotalDistance:F0}m ({walk3.TotalDuration / 60:F1}min)");

            return CombineSegments(new[] { walk1, bike1, walk2, bike2, walk3 }, "bike");
        }

        /// <summary>
        /// Finds the optimal exit station from contract A that minimizes distance to contract B.
        /// </summary>
        /// <param name="exitCandidates">Available exit stations in origin contract</param>
        /// <param name="destStations">Stations in destination contract</param>
        /// <returns>Best exit station</returns>
        private BikeStation FindBestExitStation(
            List<BikeStation> exitCandidates,
            List<BikeStation> destStations)
        {
            Console.WriteLine($"[RoutingService] Analyzing {exitCandidates.Count} exit station candidates");

            BikeStation bestExit = null;
            BikeStation bestEntryMatch = null;
            double minDistance = double.MaxValue;

            foreach (var exit in exitCandidates)
            {
                var closestEntry = destStations
                    .Where(s => s.Status == "OPEN" && s.TotalStands.Availabilities.Bikes > 0)
                    .OrderBy(s => Haversine(
                        exit.Position.Latitude,
                        exit.Position.Longitude,
                        s.Position.Latitude,
                        s.Position.Longitude))
                    .FirstOrDefault();

                if (closestEntry != null)
                {
                    double dist = Haversine(
                        exit.Position.Latitude,
                        exit.Position.Longitude,
                        closestEntry.Position.Latitude,
                        closestEntry.Position.Longitude);

                    if (dist < minDistance * 1.5 || minDistance == double.MaxValue)
                    {
                        Console.WriteLine($"[RoutingService] Candidate: {exit.Name} → {closestEntry.Name}: {dist:F0}m");
                    }

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestExit = exit;
                        bestEntryMatch = closestEntry;
                    }
                }
            }

            if (bestExit != null && bestEntryMatch != null)
            {
                Console.WriteLine("[RoutingService] ===================================================");
                Console.WriteLine($"[RoutingService] Best exit station: {bestExit.Name}");
                Console.WriteLine($"[RoutingService] Corresponding entry station: {bestEntryMatch.Name}");
                Console.WriteLine($"[RoutingService] Distance between contracts: {minDistance:F0}m ({minDistance / 1000:F2} km)");
                Console.WriteLine("[RoutingService] ===================================================");
            }

            return bestExit ?? exitCandidates.First();
        }

        /// <summary>
        /// Combines multiple itinerary segments into a single result.
        /// </summary>
        /// <param name="segments">Array of itinerary segments to combine</param>
        /// <param name="recommendation">Recommended mode of transport (walk/bike)</param>
        /// <returns>Combined itinerary result</returns>
        private ItineraryResult CombineSegments(ItineraryData[] segments, string recommendation)
        {
            var allSteps = segments.SelectMany(s => s.Steps).ToArray();
            var allCoordinates = segments.SelectMany(s => s.Geometry.Coordinates).ToArray();

            double totalDistance = segments.Sum(s => s.TotalDistance);
            double totalDuration = segments.Sum(s => s.TotalDuration);

            Console.WriteLine($"[RoutingService] Combined {segments.Length} segments into {allSteps.Length} steps");

            return new ItineraryResult
            {
                Success = true,
                Message = recommendation,
                Data = new ItineraryData
                {
                    TotalDistance = totalDistance,
                    TotalDuration = totalDuration,
                    Steps = allSteps,
                    Geometry = new Geometry { Coordinates = allCoordinates }
                }
            };
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Helper class to encapsulate station walk calculation results.
        /// </summary>
        private class StationWalkResult
        {
            public BikeStation station { get; set; }
            public double walkingDistance { get; set; }
            public ItineraryData walkData { get; set; }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Calculates the Haversine distance between two geographical coordinates.
        /// </summary>
        /// <returns>Distance in meters</returns>
        private double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadiusMeters = 6371000;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;

            lat1 *= Math.PI / 180;
            lat2 *= Math.PI / 180;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2) *
                       Math.Cos(lat1) * Math.Cos(lat2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusMeters * c;
        }

        /// <summary>
        /// Retrieves the three closest stations with available bikes.
        /// </summary>
        /// <param name="lat">Latitude of reference point</param>
        /// <param name="lon">Longitude of reference point</param>
        /// <param name="stations">List of available stations</param>
        /// <returns>List of up to 3 closest stations with bikes</returns>
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

        /// <summary>
        /// Retrieves the three closest stations with available stands.
        /// </summary>
        /// <param name="lat">Latitude of reference point</param>
        /// <param name="lon">Longitude of reference point</param>
        /// <param name="stations">List of available stations</param>
        /// <returns>List of up to 3 closest stations with stands</returns>
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

        /// <summary>
        /// Finds the JCDecaux contract for a given city name.
        /// Uses fuzzy matching to handle variations in city names.
        /// </summary>
        /// <param name="city">City name to search for</param>
        /// <returns>Contract name</returns>
        /// <exception cref="Exception">Thrown when no matching contract is found</exception>
        private string FindContractForCity(string city)
        {
            if (string.IsNullOrEmpty(city))
                throw new Exception("City name is empty");

            string normalizedCity = Normalize(city);
            var contracts = _proxyClient.GetAvailableContracts();

            foreach (var contract in contracts)
            {
                if (contract.Cities == null || contract.Cities.Count == 0)
                    continue;

                foreach (var contractCity in contract.Cities)
                {
                    string normalizedContractCity = Normalize(contractCity);

                    if (normalizedCity == normalizedContractCity ||
                        normalizedCity.StartsWith(normalizedContractCity) ||
                        normalizedContractCity.StartsWith(normalizedCity))
                    {
                        return contract.Name;
                    }
                }
            }

            throw new Exception($"No JCDecaux contract matches the city '{city}'");
        }

        /// <summary>
        /// Computes actual walking routes from origin to multiple station candidates.
        /// </summary>
        /// <param name="oLat">Origin latitude</param>
        /// <param name="oLon">Origin longitude</param>
        /// <param name="candidates">List of candidate stations</param>
        /// <returns>List of stations with calculated walking distances and routes</returns>
        private List<StationWalkResult> ComputeRealWalkOriginToStations(
                double oLat,
                double oLon,
                List<BikeStation> candidates)
        {
            var tasks = candidates.Select(st =>
            {
                return System.Threading.Tasks.Task.Run(() =>
                {
                    var walk = GetWalkingSegment(oLat, oLon, st.Position.Latitude, st.Position.Longitude);
                    return new StationWalkResult
                    {
                        station = st,
                        walkingDistance = walk.TotalDistance,
                        walkData = walk
                    };
                });
            }).ToArray();

            var results = System.Threading.Tasks.Task.WhenAll(tasks).Result;
            return results.ToList();
        }

        /// <summary>
        /// Computes actual walking routes from multiple station candidates to destination.
        /// </summary>
        /// <param name="dLat">Destination latitude</param>
        /// <param name="dLon">Destination longitude</param>
        /// <param name="candidates">List of candidate stations</param>
        /// <returns>List of stations with calculated walking distances and routes</returns>
        private List<StationWalkResult> ComputeRealWalkStationsToDestination(
                double dLat,
                double dLon,
                List<BikeStation> candidates)
        {
            var tasks = candidates.Select(st =>
            {
                return System.Threading.Tasks.Task.Run(() =>
                {
                    var walk = GetWalkingSegment(st.Position.Latitude, st.Position.Longitude, dLat, dLon);
                    return new StationWalkResult
                    {
                        station = st,
                        walkingDistance = walk.TotalDistance,
                        walkData = walk
                    };
                });
            }).ToArray();

            var results = System.Threading.Tasks.Task.WhenAll(tasks).Result;
            return results.ToList();
        }

        /// <summary>
        /// Normalizes a string by removing spaces, accents, and converting to lowercase.
        /// </summary>
        /// <param name="s">String to normalize</param>
        /// <returns>Normalized string</returns>
        private string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";

            return s
                .ToLower()
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("'", "")
                .Replace("'", "")
                .Replace("é", "e")
                .Replace("è", "e")
                .Replace("ê", "e")
                .Replace("à", "a")
                .Replace("â", "a")
                .Replace("ô", "o")
                .Replace("î", "i")
                .Replace("ç", "c");
        }

        #endregion

        #region Multi-Contract Itinerary

        /// <summary>
        /// Computes an intelligent multi-contract itinerary for long distances.
        /// Detects and uses all available contracts along the route.
        /// Example: Nice → Lyon (uses bike) → Paris.
        /// </summary>
        /// <returns>Multi-contract itinerary result</returns>
        private ItineraryResult ComputeMultiContractItinerary(
            double oLat,
            double oLon,
            double dLat,
            double dLon)
        {
            Console.WriteLine("[RoutingService] ========================================");
            Console.WriteLine("[RoutingService] Computing multi-contract itinerary");
            Console.WriteLine($"[RoutingService] Route: ({oLat}, {oLon}) → ({dLat}, {dLon})");

            // 1️⃣ Détecter tous les contrats sur le trajet
            var detector = new ContractDetector(_proxyClient);
            var contractsOnRoute = detector.DetectContractsOnRoute(oLat, oLon, dLat, dLon);

            if (contractsOnRoute.Count == 0)
            {
                Console.WriteLine("[RoutingService] No contracts found on route, falling back to walking");
                return new ItineraryResult
                {
                    Success = true,
                    Message = "walk",
                    Data = GetWalkingSegment(oLat, oLon, dLat, dLon)
                };
            }

            // 2️⃣ Construire l'itinéraire segment par segment
            var segments = new List<ItineraryData>();
            double currentLat = oLat;
            double currentLon = oLon;

            foreach (var contract in contractsOnRoute)
            {
                Console.WriteLine($"[RoutingService] Processing contract: {contract.ContractName}");

                // 🚶 MARCHE : Position actuelle → Entrée du contrat
                var entryStation = FindBestEntryStation(
                    currentLat, currentLon,
                    contract.Stations);

                if (entryStation == null)
                {
                    Console.WriteLine($"[RoutingService] No available entry station in {contract.ContractName}");
                    continue;
                }

                Console.WriteLine($"[RoutingService] Entry station: {entryStation.Name}");

                var walkToEntry = GetWalkingSegment(
                    currentLat, currentLon,
                    entryStation.Position.Latitude,
                    entryStation.Position.Longitude);

                segments.Add(walkToEntry);

                //  Traversée du contrat
                var exitStation = FindBestExitStation(
                    entryStation.Position.Latitude,
                    entryStation.Position.Longitude,
                    dLat, dLon,
                    contract.Stations);

                if (exitStation == null)
                {
                    Console.WriteLine($"[RoutingService] No available exit station in {contract.ContractName}");
                    exitStation = entryStation; // Fallback
                }

                Console.WriteLine($"[RoutingService] Exit station: {exitStation.Name}");

                var bikeSegment = GetBikingSegment(
                    entryStation.Position.Latitude,
                    entryStation.Position.Longitude,
                    exitStation.Position.Latitude,
                    exitStation.Position.Longitude);

                segments.Add(bikeSegment);

                // Mettre à jour position actuelle
                currentLat = exitStation.Position.Latitude;
                currentLon = exitStation.Position.Longitude;

                Console.WriteLine($"[RoutingService] Current position updated: ({currentLat}, {currentLon})");
            }

            // 3️⃣ Dernier segment : Dernière position → Destination
            Console.WriteLine("[RoutingService] Adding final segment to destination");
            var finalWalk = GetWalkingSegment(currentLat, currentLon, dLat, dLon);
            segments.Add(finalWalk);

            // 4️⃣ Combiner tous les segments
            Console.WriteLine($"[RoutingService] Combining {segments.Count} segments");

            return CombineSegments(segments.ToArray(), "bike");
        }

        /// <summary>
        /// Finds the best entry station close to the current position with available bikes.
        /// </summary>
        /// <param name="lat">Current latitude</param>
        /// <param name="lon">Current longitude</param>
        /// <param name="stations">Available stations</param>
        /// <returns>Best entry station or null if none available</returns>
        private BikeStation FindBestEntryStation(
            double lat,
            double lon,
            List<BikeStation> stations)
        {
            var candidates = stations
                .Where(s => s.Status == "OPEN")
                .Where(s => s.TotalStands.Availabilities.Bikes > 0)
                .OrderBy(s => Haversine(lat, lon, s.Position.Latitude, s.Position.Longitude))
                .Take(5)
                .ToList();

            if (!candidates.Any()) return null;

            // Calculer le trajet réel à pied pour chaque candidat
            var realWalks = ComputeRealWalkOriginToStations(lat, lon, candidates);

            return realWalks.OrderBy(x => x.walkingDistance).First().station;
        }

        /// <summary>
        /// Finds the best exit station in the direction of the destination with available stands.
        /// </summary>
        /// <param name="currentLat">Current latitude</param>
        /// <param name="currentLon">Current longitude</param>
        /// <param name="destLat">Destination latitude</param>
        /// <param name="destLon">Destination longitude</param>
        /// <param name="stations">Available stations</param>
        /// <returns>Best exit station or null if none available</returns>
        private BikeStation FindBestExitStation(
            double currentLat,
            double currentLon,
            double destLat,
            double destLon,
            List<BikeStation> stations)
        {
            var candidates = stations
                .Where(s => s.Status == "OPEN")
                .Where(s => s.TotalStands.Availabilities.Stands > 0)
                .ToList();

            if (!candidates.Any()) return null;

            var scored = candidates.Select(s => new
            {
                Station = s,
                DistToDest = Haversine(s.Position.Latitude, s.Position.Longitude, destLat, destLon),
                Score = Haversine(s.Position.Latitude, s.Position.Longitude, destLat, destLon)
            })
            .OrderBy(x => x.Score)
            .ToList();

            return scored.First().Station;
        }

        #endregion
    }
}