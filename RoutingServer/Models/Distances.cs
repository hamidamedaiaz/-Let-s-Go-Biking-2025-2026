using SharedModels;
namespace RoutingServer.Models
{
    internal class Distances
    {
        /// <summary>Distance directe à pied entre l’origine et la destination (en mètres).</summary>
        public double DirectWalkingDistance { get; set; }

        /// <summary>Distance à pied entre l’origine et la station de départ (en mètres).</summary>
        public double OriginToOriginStationDistance { get; set; }

        /// <summary>Distance à pied entre la station d’arrivée et la destination (en mètres).</summary>
        public double DestinationStationToFinalDestinationDistance { get; set; }

        /// <summary>Distance à vélo entre la station de départ et la station d’arrivée (en mètres).</summary>
        public double BikeStationToStationDistance { get; set; }

        /// <summary>Distance totale du trajet combiné (marche + vélo + marche) en mètres.</summary>
        public double TotalBikingDistance { get; set; }

        /// <summary>Station de départ recommandée (avec vélos disponibles).</summary>
        public BikeStation OriginStationRecommendedWithDisponibleBikes { get; set; }

        /// <summary>Station d’arrivée recommandée (avec places libres).</summary>
        public BikeStation DestinationStationRecommendedWithFreePlaces { get; set; }
    }
}
