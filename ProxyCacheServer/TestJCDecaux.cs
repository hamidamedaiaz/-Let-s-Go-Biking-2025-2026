using System;
using System.Linq;

namespace ProxyCacheService
{
    class TestJCDecaux
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Test JCDecaux API ===\n");

            try
            {
                var service = new ProxyCacheServiceImpl();

                // ✅ TEST 1 : Récupérer les stations de Paris
                Console.WriteLine("TEST 1 : Stations de Lyon");
                var stationsParis = service.GetStations("Lyon");
                
                if (stationsParis.Count > 0)
                {
                    Console.WriteLine($"✅ SUCCESS : {stationsParis.Count} stations trouvées");
                    
                    // Afficher quelques exemples
                    Console.WriteLine("\n📍 Exemples de stations :");
                    foreach (var station in stationsParis.Take(3))
                    {
                        Console.WriteLine($"   - {station.name} ({station.address})");
                        Console.WriteLine($"     Vélos: {station.available_bikes}/{station.bike_stands} | Statut: {station.status}");
                        Console.WriteLine($"     Position: {station.position.lat}, {station.position.lng}");
                    }
                }
                else
                {
                    Console.WriteLine("❌ FAIL : Aucune station récupérée");
                }

                // ✅ TEST 2 : Même appel → Cache HIT attendu
                Console.WriteLine("\n\nTEST 2 : Même requête (cache hit attendu)");
                var stationsParis2 = service.GetStations("Paris");
                Console.WriteLine($"Cache fonctionne : {(stationsParis.Count == stationsParis2.Count ? "✅ OUI" : "❌ NON")}");

                // ✅ TEST 3 : Autre ville (Lyon)
                Console.WriteLine("\n\nTEST 3 : Stations de Lyon");
                var stationsLyon = service.GetStations("Lyon");
                
                if (stationsLyon.Count > 0)
                {
                    Console.WriteLine($"✅ SUCCESS : {stationsLyon.Count} stations trouvées");
                    
                    var firstStation = stationsLyon.First();
                    Console.WriteLine($"\n📍 Première station : {firstStation.name}");
                    Console.WriteLine($"   Adresse: {firstStation.address}");
                    Console.WriteLine($"   Vélos: {firstStation.available_bikes}");
                }

                // ✅ TEST 4 : Contrat invalide
                Console.WriteLine("\n\nTEST 4 : Contrat invalide (gestion erreur)");
                var stationsInvalid = service.GetStations("VilleInexistante");
                Console.WriteLine($"Gestion erreur : {(stationsInvalid.Count == 0 ? "✅ OK" : "❌ FAIL")}");

                service.Dispose();
                Console.WriteLine("\n✅ Tous les tests terminés !");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERREUR : {ex.Message}");
                Console.WriteLine($"StackTrace : {ex.StackTrace}");
            }

            Console.WriteLine("\nAppuyez sur une touche pour quitter...");
            Console.ReadKey();
        }
    }
}