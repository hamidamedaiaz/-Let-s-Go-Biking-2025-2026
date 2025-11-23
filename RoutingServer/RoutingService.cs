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

        #region Méthodes d'obtention de segments

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

        // ===================================================================
        // MÉTHODE ParseORSJson - VERSION COMPLÈTE ET CORRIGÉE
        // ===================================================================
        // À placer dans RoutingService.cs
        // Remplace complètement l'ancienne version de ParseORSJson

        private ItineraryData ParseORSJson(string orsJson, string stepType)
        {
            // ═══════════════════════════════════════════════════════════════
            // ÉTAPE 1 : Désérialiser la réponse JSON d'OpenRouteService
            // ═══════════════════════════════════════════════════════════════
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

            // ═══════════════════════════════════════════════════════════════
            // ÉTAPE 2 : Extraire les données de base
            // ═══════════════════════════════════════════════════════════════
            var segment = feature.Properties.Segments[0];
            var summary = feature.Properties.Summary;
            var rawSteps = segment.Steps;

            // ═══════════════════════════════════════════════════════════════
            // ÉTAPE 3 : ✅ NOUVEAU - Récupérer TOUTES les coordonnées
            // ═══════════════════════════════════════════════════════════════
            double[][] allCoordinates = Array.Empty<double[]>();

            if (feature.Geometry?.Coordinates != null && feature.Geometry.Coordinates.Count > 0)
            {
                allCoordinates = feature.Geometry.Coordinates
                    .Select(c => c.ToArray())
                    .ToArray();
            }

            // ═══════════════════════════════════════════════════════════════
            // ÉTAPE 4 : ✅ NOUVEAU - Extraire les coordonnées PAR STEP
            //           en utilisant les WayPoints d'OpenRouteService
            // ═══════════════════════════════════════════════════════════════
            var convertedSteps = new List<Step>();

            Console.WriteLine($"[ParseORS] Type: {stepType}, {rawSteps.Count} steps, {allCoordinates.Length} coords totales");

            for (int i = 0; i < rawSteps.Count; i++)
            {
                var orsStep = rawSteps[i];

                // ───────────────────────────────────────────────────────────
                // ✅ Déterminer les indices de début et fin pour ce step
                // ───────────────────────────────────────────────────────────
                int startIdx = 0;
                int endIdx = allCoordinates.Length;

                // OpenRouteService fournit des WayPoints qui indiquent
                // les indices de début et fin de chaque step dans le tableau global
                if (orsStep.WayPoints != null && orsStep.WayPoints.Count > 0)
                {
                    // L'index de début est le premier WayPoint du step actuel
                    startIdx = orsStep.WayPoints[0];

                    // L'index de fin est le premier WayPoint du step SUIVANT
                    // (ou la fin du tableau si c'est le dernier step)
                    if (i < rawSteps.Count - 1)
                    {
                        var nextStep = rawSteps[i + 1];
                        if (nextStep.WayPoints != null && nextStep.WayPoints.Count > 0)
                        {
                            endIdx = nextStep.WayPoints[0];
                        }
                    }
                    // Sinon, on va jusqu'à la fin du tableau
                }

                // ───────────────────────────────────────────────────────────
                // ✅ Extraire UNIQUEMENT les coordonnées de ce step
                // ───────────────────────────────────────────────────────────
                var stepCoordinates = allCoordinates
                    .Skip(startIdx)              // Commence à l'index de début
                    .Take(endIdx - startIdx)     // Prend exactement le bon nombre
                    .ToArray();

                // Log pour debug
                Console.WriteLine($"[ParseORS]   Step {i + 1}/{rawSteps.Count}: {stepType} - " +
                                 $"indices [{startIdx}, {endIdx}] = {stepCoordinates.Length} coords - " +
                                 $"\"{orsStep.Instruction}\"");

                // ───────────────────────────────────────────────────────────
                // ✅ Créer le Step avec ses coordonnées spécifiques
                // ───────────────────────────────────────────────────────────
                convertedSteps.Add(new Step
                {
                    Type = stepType,                    // "walk" ou "bike"
                    Instructions = orsStep.Instruction, // "Marcher vers le nord", etc.
                    Distance = orsStep.Distance,        // En mètres
                    Duration = orsStep.Duration,        // En secondes
                    Coordinates = stepCoordinates       // ✅ NOUVEAU : Coordonnées exactes de ce step !
                });
            }

            // ═══════════════════════════════════════════════════════════════
            // ÉTAPE 5 : Retourner l'ItineraryData avec les steps enrichis
            // ═══════════════════════════════════════════════════════════════
            Console.WriteLine($"[ParseORS] ✓ {convertedSteps.Count} steps créés avec coordonnées individuelles");

            return new ItineraryData
            {
                TotalDistance = summary.Distance,           // Distance totale en mètres
                TotalDuration = summary.Duration,           // Durée totale en secondes
                Steps = convertedSteps.ToArray(),          // ✅ Steps avec Coordinates
                Geometry = new Geometry
                {
                    Coordinates = allCoordinates           // Coordonnées globales (fallback)
                }
            };
        }

        #endregion

        #region Point d'entrée principal

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

                Console.WriteLine($"[RoutingService] ========================================");
                Console.WriteLine($"[RoutingService] Origine: {originCity} ({oLat}, {oLon})");
                Console.WriteLine($"[RoutingService] Destination: {destCity} ({dLat}, {dLon})");

                string originContract = null;
                string destContract = null;

                // Tentative de résolution des contrats
                try
                {
                    originContract = FindContractForCity(originCity);
                    Console.WriteLine($"[RoutingService] ✓ Contrat origine: {originContract}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RoutingService] ⚠️ Contrat origine non trouvé: {ex.Message}");
                    originContract = null;
                }

                try
                {
                    destContract = FindContractForCity(destCity);
                    Console.WriteLine($"[RoutingService] ✓ Contrat destination: {destContract}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RoutingService] ⚠️ Contrat destination non trouvé: {ex.Message}");
                    destContract = null;
                }

                // Cas 1 : Aucune ville identifiée → Marche directe
                if (string.IsNullOrEmpty(originCity) && string.IsNullOrEmpty(destCity))
                {
                    Console.WriteLine($"[RoutingService] ❌ Aucune ville identifiée → Marche directe uniquement");

                    var walkOnly = GetWalkingSegment(oLat, oLon, dLat, dLon);

                    return new ItineraryResult
                    {
                        Success = true,
                        Message = "walk",
                        Data = walkOnly
                    };
                }

                // Cas 2 : Départ SANS contrat, Arrivée AVEC contrat → Hybride (marche puis vélo)
                if (originContract == null && destContract != null)
                {
                    Console.WriteLine($"[RoutingService] 🚶→🚴 Départ sans contrat → Arrivée avec contrat ({destContract}) → Mode Hybride");
                    return ComputeHybridWalkToBike(oLat, oLon, dLat, dLon, destContract);
                }

                // Cas 3 : Départ AVEC contrat, Arrivée SANS contrat → Hybride (vélo puis marche)
                if (originContract != null && destContract == null)
                {
                    Console.WriteLine($"[RoutingService] 🚴→🚶 Départ avec contrat ({originContract}) → Arrivée sans contrat → Mode Hybride");
                    return ComputeHybridBikeToWalk(oLat, oLon, dLat, dLon, originContract);
                }

                // Cas 4 : Les deux villes identifiées mais aucun contrat trouvé
                if (originContract == null && destContract == null)
                {
                    Console.WriteLine($"[RoutingService] ❌ Aucun contrat trouvé → Marche directe uniquement");

                    var walkOnly = GetWalkingSegment(oLat, oLon, dLat, dLon);

                    return new ItineraryResult
                    {
                        Success = true,
                        Message = "walk",
                        Data = walkOnly
                    };
                }

                // Cas 5 : Contrats différents → V2 Inter-contrats
                if (!originContract.Equals(destContract, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[RoutingService] 🔄 Contrats différents ({originContract} ≠ {destContract}) → V2 Inter-contrats");
                    return ComputeInterContractItinerary(oLat, oLon, dLat, dLon, originContract, destContract);
                }

                // Cas 6 : Même contrat → V1 Intra-contrat
                Console.WriteLine($"[RoutingService] ✓ Même contrat ({originContract}) → V1 Intra-contrat");
                var stations = _proxyClient.GetStationsByContract(originContract).ToList();
                return ComputeItinerary(oLat, oLon, dLat, dLon, stations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RoutingService ERROR] {ex.Message}");
                Console.WriteLine($"[RoutingService ERROR] StackTrace: {ex.StackTrace}");

                return new ItineraryResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    Data = null
                };
            }
        }

        #endregion

        #region Cas Hybrides : Départ ou Arrivée sans contrat

        /// <summary>
        /// CAS HYBRIDE 1 : Départ SANS contrat → Arrivée AVEC contrat
        /// Trajet : Marche → Station d'entrée → Vélo → Station finale → Marche
        /// </summary>
        private ItineraryResult ComputeHybridWalkToBike(
            double oLat,
            double oLon,
            double dLat,
            double dLon,
            string destContract)
        {
            Console.WriteLine($"[HYBRID] ========================================");
            Console.WriteLine($"[HYBRID] === MODE HYBRIDE : MARCHE → VÉLO ===");
            Console.WriteLine($"[HYBRID] Départ sans contrat → Arrivée dans {destContract}");

            // Récupérer les stations du contrat de destination
            var destStations = _proxyClient.GetStationsByContract(destContract).ToList();
            Console.WriteLine($"[HYBRID] Stations dans {destContract}: {destStations.Count}");

            // ===== ÉTAPE 1 : Trouver la station d'ENTRÉE la plus proche du départ =====
            Console.WriteLine($"[HYBRID] --- Étape 1 : Recherche station d'ENTRÉE dans {destContract} ---");

            var entryCandidates = GetThreeClosestStartStations(oLat, oLon, destStations);

            if (!entryCandidates.Any())
            {
                Console.WriteLine("[HYBRID] ❌ Aucune station disponible → Marche directe");
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

            Console.WriteLine($"[HYBRID] ✓ Station d'ENTRÉE choisie: {bestEntryStation.station.Name}");
            Console.WriteLine($"[HYBRID] ✓ Segment 1 (MARCHE) : {walk1.TotalDistance:F0}m ({walk1.TotalDuration / 60:F1}min)");

            // ===== ÉTAPE 2 : Trouver la station FINALE proche de la destination =====
            Console.WriteLine($"[HYBRID] --- Étape 2 : Recherche station FINALE dans {destContract} ---");

            var finalCandidates = GetThreeClosestEndStations(dLat, dLon, destStations);

            if (!finalCandidates.Any())
            {
                Console.WriteLine("[HYBRID] ❌ Aucune station de dépôt disponible");

                var fallbackWalk = GetWalkingSegment(
                    bestEntryStation.station.Position.Latitude,
                    bestEntryStation.station.Position.Longitude,
                    dLat,
                    dLon);

                return CombineSegments(
                    new[] { walk1, fallbackWalk },
                    "walk");
            }

            var realFinalWalks = ComputeRealWalkStationsToDestination(dLat, dLon, finalCandidates);
            var bestFinalStation = realFinalWalks.OrderBy(x => x.walkingDistance).First();

            Console.WriteLine($"[HYBRID] ✓ Station FINALE choisie: {bestFinalStation.station.Name}");

            // ===== ÉTAPE 3 : Vélo entre les deux stations =====
            Console.WriteLine($"[HYBRID] --- Étape 3 : Vélo dans {destContract} ---");

            var bike = GetBikingSegment(
                bestEntryStation.station.Position.Latitude,
                bestEntryStation.station.Position.Longitude,
                bestFinalStation.station.Position.Latitude,
                bestFinalStation.station.Position.Longitude);

            Console.WriteLine($"[HYBRID] ✓ Segment 2 (VÉLO) : {bike.TotalDistance:F0}m ({bike.TotalDuration / 60:F1}min)");

            // ===== ÉTAPE 4 : Marche vers la destination =====
            var walk2 = bestFinalStation.walkData;

            Console.WriteLine($"[HYBRID] ✓ Segment 3 (MARCHE) : {walk2.TotalDistance:F0}m ({walk2.TotalDuration / 60:F1}min)");

            // ===== COMPARAISON avec marche directe =====
            var walkDirect = GetWalkingSegment(oLat, oLon, dLat, dLon);
            double hybridTime = walk1.TotalDuration + bike.TotalDuration + walk2.TotalDuration;

            Console.WriteLine($"[HYBRID] Durée hybride: {hybridTime / 60:F1}min vs marche: {walkDirect.TotalDuration / 60:F1}min");

            string recommendation = hybridTime < walkDirect.TotalDuration * 0.9 ? "bike" : "walk";

            Console.WriteLine($"[HYBRID] Recommandation: {recommendation}");

            return CombineSegments(
                new[] { walk1, bike, walk2 },
                recommendation);
        }

        /// <summary>
        /// CAS HYBRIDE 2 : Départ AVEC contrat → Arrivée SANS contrat
        /// Trajet : Marche → Station départ → Vélo → Station sortie → Marche
        /// </summary>
        private ItineraryResult ComputeHybridBikeToWalk(
            double oLat,
            double oLon,
            double dLat,
            double dLon,
            string originContract)
        {
            Console.WriteLine($"[HYBRID] ========================================");
            Console.WriteLine($"[HYBRID] === MODE HYBRIDE : VÉLO → MARCHE ===");
            Console.WriteLine($"[HYBRID] Départ dans {originContract} → Arrivée sans contrat");

            // Récupérer les stations du contrat d'origine
            var originStations = _proxyClient.GetStationsByContract(originContract).ToList();
            Console.WriteLine($"[HYBRID] Stations dans {originContract}: {originStations.Count}");

            // ===== ÉTAPE 1 : Trouver la station de DÉPART proche de l'origine =====
            Console.WriteLine($"[HYBRID] --- Étape 1 : Recherche station de DÉPART dans {originContract} ---");

            var startCandidates = GetThreeClosestStartStations(oLat, oLon, originStations);

            if (!startCandidates.Any())
            {
                Console.WriteLine("[HYBRID] ❌ Aucune station disponible → Marche directe");
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

            Console.WriteLine($"[HYBRID] ✓ Station de DÉPART choisie: {bestStartStation.station.Name}");
            Console.WriteLine($"[HYBRID] ✓ Segment 1 (MARCHE) : {walk1.TotalDistance:F0}m ({walk1.TotalDuration / 60:F1}min)");

            // ===== ÉTAPE 2 : Trouver la station de SORTIE la plus proche de la destination =====
            Console.WriteLine($"[HYBRID] --- Étape 2 : Recherche station de SORTIE dans {originContract} ---");

            // On cherche parmi TOUTES les stations celle qui est la plus proche de la DESTINATION
            var allExitCandidates = originStations
                .Where(s => s.Status == "OPEN")
                .Where(s => s.TotalStands.Availabilities.Stands > 0)
                .ToList();

            if (!allExitCandidates.Any())
            {
                Console.WriteLine("[HYBRID] ❌ Aucune station de sortie disponible");
                return new ItineraryResult
                {
                    Success = true,
                    Message = "walk",
                    Data = GetWalkingSegment(oLat, oLon, dLat, dLon)
                };
            }

            // Trouver la station la plus proche de la DESTINATION
            var bestExitStation = allExitCandidates
                .OrderBy(s => Haversine(s.Position.Latitude, s.Position.Longitude, dLat, dLon))
                .First();

            Console.WriteLine($"[HYBRID] ✓ Station de SORTIE choisie: {bestExitStation.Name}");
            Console.WriteLine($"[HYBRID]   Distance vers destination: {Haversine(bestExitStation.Position.Latitude, bestExitStation.Position.Longitude, dLat, dLon):F0}m");

            // ===== ÉTAPE 3 : Vélo entre les deux stations =====
            Console.WriteLine($"[HYBRID] --- Étape 3 : Vélo dans {originContract} ---");

            var bike = GetBikingSegment(
                bestStartStation.station.Position.Latitude,
                bestStartStation.station.Position.Longitude,
                bestExitStation.Position.Latitude,
                bestExitStation.Position.Longitude);

            Console.WriteLine($"[HYBRID] ✓ Segment 2 (VÉLO) : {bike.TotalDistance:F0}m ({bike.TotalDuration / 60:F1}min)");

            // ===== ÉTAPE 4 : Marche vers la destination =====
            Console.WriteLine($"[HYBRID] --- Étape 4 : Marche vers destination ---");

            var walk2 = GetWalkingSegment(
                bestExitStation.Position.Latitude,
                bestExitStation.Position.Longitude,
                dLat,
                dLon);

            Console.WriteLine($"[HYBRID] ✓ Segment 3 (MARCHE) : {walk2.TotalDistance:F0}m ({walk2.TotalDuration / 60:F1}min)");

            // ===== COMPARAISON avec marche directe =====
            var walkDirect = GetWalkingSegment(oLat, oLon, dLat, dLon);
            double hybridTime = walk1.TotalDuration + bike.TotalDuration + walk2.TotalDuration;

            Console.WriteLine($"[HYBRID] Durée hybride: {hybridTime / 60:F1}min vs marche: {walkDirect.TotalDuration / 60:F1}min");

            string recommendation = hybridTime < walkDirect.TotalDuration * 0.9 ? "bike" : "walk";

            Console.WriteLine($"[HYBRID] Recommandation: {recommendation}");

            return CombineSegments(
                new[] { walk1, bike, walk2 },
                recommendation);
        }

        #endregion

        #region V1 : Itinéraire intra-contrat (même contrat)

        private ItineraryResult ComputeItinerary(
            double oLat,
            double oLon,
            double dLat,
            double dLon,
            List<BikeStation> stations)
        {
            Console.WriteLine($"[V1] === CALCUL ITINÉRAIRE INTRA-CONTRAT ===");

            var startCandidates = GetThreeClosestStartStations(oLat, oLon, stations);
            var endCandidates = GetThreeClosestEndStations(dLat, dLon, stations);

            Console.WriteLine($"[V1] Stations de départ candidates: {startCandidates.Count}");
            Console.WriteLine($"[V1] Stations d'arrivée candidates: {endCandidates.Count}");

            if (!startCandidates.Any() || !endCandidates.Any())
            {
                Console.WriteLine($"[V1] ❌ Pas assez de stations → Marche directe");
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

            Console.WriteLine($"[V1] Station de départ: {startStation.Name} ({walk1.TotalDistance:F0}m)");
            Console.WriteLine($"[V1] Station d'arrivée: {endStation.Name} ({walk2.TotalDistance:F0}m)");

            if ((walk1.TotalDuration + walk2.TotalDuration) > walkDirect.TotalDuration * 0.5)
            {
                Console.WriteLine($"[V1] ⚠️ Marches trop longues → Marche directe");
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

            Console.WriteLine($"[V1] Durée vélo: {totalBike / 60:F1}min vs marche: {walkTotalTime / 60:F1}min");

            string recommendation = totalBike < walkDirect.TotalDuration * 0.9
                ? "bike"
                : "walk";

            Console.WriteLine($"[V1] Recommandation: {recommendation}");

            var allSteps = walk1.Steps.Concat(bike.Steps).Concat(walk2.Steps).ToArray();

            var combinedCoordinates = walk1.Geometry.Coordinates
                .Concat(bike.Geometry.Coordinates)
                .Concat(walk2.Geometry.Coordinates)
                .ToArray();

            Console.WriteLine($"[V1] Géométrie totale: {combinedCoordinates.Length} points");
            Console.WriteLine($"[V1] === RÉSULTAT FINAL V1 ===");

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

        #region V2 : Itinéraire inter-contrats

        private ItineraryResult ComputeInterContractItinerary(
            double oLat,
            double oLon,
            double dLat,
            double dLon,
            string originContract,
            string destContract)
        {
            Console.WriteLine($"[V2] ========================================");
            Console.WriteLine($"[V2] === CALCUL ITINÉRAIRE INTER-CONTRATS ===");
            Console.WriteLine($"[V2] Contrat Départ: {originContract}");
            Console.WriteLine($"[V2] Contrat Arrivée: {destContract}");

            // Récupérer les stations des deux contrats
            var originStations = _proxyClient.GetStationsByContract(originContract).ToList();
            var destStations = _proxyClient.GetStationsByContract(destContract).ToList();

            Console.WriteLine($"[V2] Stations contrat origine: {originStations.Count}");
            Console.WriteLine($"[V2] Stations contrat destination: {destStations.Count}");

            // ===== ÉTAPE 1 : Départ → Station de départ dans Contrat A =====
            Console.WriteLine($"[V2] --- Étape 1 : Recherche station de DÉPART dans {originContract} ---");

            var startCandidates = GetThreeClosestStartStations(oLat, oLon, originStations);

            if (!startCandidates.Any())
            {
                Console.WriteLine("[V2] ❌ Aucune station disponible dans contrat origine → Marche directe");
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

            Console.WriteLine($"[V2] ✓ Station de DÉPART choisie: {bestStartStation.station.Name}");
            Console.WriteLine($"[V2] ✓ Segment 1 (MARCHE) : {walk1.TotalDistance:F0}m ({walk1.TotalDuration / 60:F1}min)");

            // ===== ÉTAPE 2 : Trouver la station de SORTIE de Contrat A =====
            Console.WriteLine($"[V2] --- Étape 2 : Recherche station de SORTIE dans {originContract} ---");

            // ⚠️ IMPORTANT : On cherche parmi TOUTES les stations du Contrat A
            // qui acceptent les dépôts (pas seulement celles proches de la station de départ)
            var allExitCandidates = originStations
                .Where(s => s.Status == "OPEN")
                .Where(s => s.TotalStands.Availabilities.Stands > 0)  // Disponible pour déposer
                .ToList();

            if (!allExitCandidates.Any())
            {
                Console.WriteLine("[V2] ❌ Aucune station de sortie disponible");
                return new ItineraryResult
                {
                    Success = true,
                    Message = "walk",
                    Data = GetWalkingSegment(oLat, oLon, dLat, dLon)
                };
            }

            Console.WriteLine($"[V2] Nombre de stations de sortie possibles dans {originContract}: {allExitCandidates.Count}");

            // Trouver la station de sortie qui minimise la distance vers Contrat B
            var bestExitStation = FindBestExitStation(allExitCandidates, destStations);

            Console.WriteLine($"[V2] ✓ Station de SORTIE choisie: {bestExitStation.Name}");

            // ===== ÉTAPE 3 : Vélo dans Contrat A =====
            Console.WriteLine($"[V2] --- Étape 3 : Vélo dans {originContract} ---");

            var bike1 = GetBikingSegment(
                bestStartStation.station.Position.Latitude,
                bestStartStation.station.Position.Longitude,
                bestExitStation.Position.Latitude,
                bestExitStation.Position.Longitude);

            Console.WriteLine($"[V2] ✓ Segment 2 (VÉLO) : {bike1.TotalDistance:F0}m ({bike1.TotalDuration / 60:F1}min)");

            // ===== ÉTAPE 4 : Trouver la station d'ENTRÉE de Contrat B =====
            Console.WriteLine($"[V2] --- Étape 4 : Recherche station d'ENTRÉE dans {destContract} ---");

            var entryCandidates = GetThreeClosestStartStations(
                bestExitStation.Position.Latitude,
                bestExitStation.Position.Longitude,
                destStations);

            if (!entryCandidates.Any())
            {
                Console.WriteLine("[V2] ❌ Aucune station disponible dans contrat destination");

                var fallbackWalk = GetWalkingSegment(
                    bestExitStation.Position.Latitude,
                    bestExitStation.Position.Longitude,
                    dLat,
                    dLon);

                return CombineSegments(
                    new[] { walk1, bike1, fallbackWalk },
                    "walk");
            }

            var realEntryWalks = ComputeRealWalkOriginToStations(
                bestExitStation.Position.Latitude,
                bestExitStation.Position.Longitude,
                entryCandidates);

            var bestEntryStation = realEntryWalks.OrderBy(x => x.walkingDistance).First();
            var walk2 = bestEntryStation.walkData; // Marche entre les deux contrats

            Console.WriteLine($"[V2] ✓ Station d'ENTRÉE choisie: {bestEntryStation.station.Name}");
            Console.WriteLine($"[V2] ✓ Segment 3 (MARCHE ENTRE CONTRATS) : {walk2.TotalDistance:F0}m ({walk2.TotalDuration / 60:F1}min)");

            // ===== ÉTAPE 5 : Trouver la station FINALE dans Contrat B =====
            Console.WriteLine($"[V2] --- Étape 5 : Recherche station FINALE dans {destContract} ---");

            var finalCandidates = GetThreeClosestEndStations(dLat, dLon, destStations);

            if (!finalCandidates.Any())
            {
                Console.WriteLine("[V2] ❌ Aucune station de dépôt disponible dans contrat destination");

                var fallbackWalk = GetWalkingSegment(
                    bestEntryStation.station.Position.Latitude,
                    bestEntryStation.station.Position.Longitude,
                    dLat,
                    dLon);

                return CombineSegments(
                    new[] { walk1, bike1, walk2, fallbackWalk },
                    "walk");
            }

            var realFinalWalks = ComputeRealWalkStationsToDestination(dLat, dLon, finalCandidates);
            var bestFinalStation = realFinalWalks.OrderBy(x => x.walkingDistance).First();

            Console.WriteLine($"[V2] ✓ Station FINALE choisie: {bestFinalStation.station.Name}");

            // ===== ÉTAPE 6 : Vélo dans Contrat B =====
            Console.WriteLine($"[V2] --- Étape 6 : Vélo dans {destContract} ---");

            var bike2 = GetBikingSegment(
                bestEntryStation.station.Position.Latitude,
                bestEntryStation.station.Position.Longitude,
                bestFinalStation.station.Position.Latitude,
                bestFinalStation.station.Position.Longitude);

            Console.WriteLine($"[V2] ✓ Segment 4 (VÉLO) : {bike2.TotalDistance:F0}m ({bike2.TotalDuration / 60:F1}min)");

            // ===== ÉTAPE 7 : Station finale → Destination =====
            Console.WriteLine($"[V2] --- Étape 7 : Marche vers destination ---");

            var walk3 = bestFinalStation.walkData;

            Console.WriteLine($"[V2] ✓ Segment 5 (MARCHE) : {walk3.TotalDistance:F0}m ({walk3.TotalDuration / 60:F1}min)");

            // ===== COMBINAISON FINALE =====
            Console.WriteLine($"[V2] --- Combinaison des segments ---");

            return CombineSegments(
                new[] { walk1, bike1, walk2, bike2, walk3 },
                "bike");
        }

        /// <summary>
        /// Trouve la station de sortie du contrat A qui minimise la distance
        /// vers la station d'entrée la plus proche du contrat B
        /// 
        /// Cette méthode cherche parmi TOUTES les stations du Contrat A (qui acceptent les dépôts)
        /// et trouve celle qui est la plus proche de la frontière vers Contrat B
        /// </summary>
        private BikeStation FindBestExitStation(
            List<BikeStation> exitCandidates,
            List<BikeStation> destStations)
        {
            Console.WriteLine($"[V2] Analyse de {exitCandidates.Count} stations de sortie possibles...");

            BikeStation bestExit = null;
            BikeStation bestEntryMatch = null;
            double minDistance = double.MaxValue;

            foreach (var exit in exitCandidates)
            {
                // Trouver la station la plus proche dans le contrat de destination
                // qui a des vélos disponibles
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

                    // Log seulement les 10 meilleures pour ne pas surcharger la console
                    if (dist < minDistance * 1.5 || minDistance == double.MaxValue)
                    {
                        Console.WriteLine($"[V2]   - {exit.Name} → {closestEntry.Name} : {dist:F0}m");
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
                Console.WriteLine($"[V2] ===================================================");
                Console.WriteLine($"[V2] ✓ MEILLEURE STATION DE SORTIE: {bestExit.Name}");
                Console.WriteLine($"[V2] ✓ Station d'entrée correspondante: {bestEntryMatch.Name}");
                Console.WriteLine($"[V2] ✓ Distance entre contrats: {minDistance:F0}m ({minDistance / 1000:F2} km)");
                Console.WriteLine($"[V2] ===================================================");
            }

            return bestExit ?? exitCandidates.First();
        }

        /// <summary>
        /// Combine plusieurs segments en un seul itinéraire
        /// </summary>
        private ItineraryResult CombineSegments(ItineraryData[] segments, string recommendation)
        {
            var allSteps = segments.SelectMany(s => s.Steps).ToArray();
            var allCoordinates = segments.SelectMany(s => s.Geometry.Coordinates).ToArray();

            double totalDistance = segments.Sum(s => s.TotalDistance);
            double totalDuration = segments.Sum(s => s.TotalDuration);

            Console.WriteLine($"[COMBINE] Segments: {segments.Length}");
            Console.WriteLine($"[COMBINE] Total steps: {allSteps.Length}");

            // ✅ NOUVEAU : Vérifier que chaque step a ses coordonnées
            Console.WriteLine($"[COMBINE] Steps finaux:");
            foreach (var step in allSteps)
            {
                Console.WriteLine($"  - {step.Type}: {step.Coordinates?.Length ?? 0} coords");
            }

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

        #region Méthodes utilitaires

        private double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371000; // Rayon de la Terre en mètres
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
            if (string.IsNullOrEmpty(city))
                throw new Exception("City name is empty");

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
    }
}