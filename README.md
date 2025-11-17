# LetsGoBiking

## Description

LetsGoBiking est une application de calcul d'itinéraires vélo qui utilise les données JCDecaux pour les stations de vélos en libre-service et OpenRouteService pour le calcul des trajets. L'architecture est basée sur des services WCF communiquant entre eux.

## Architecture

Le projet est composé de trois services principaux :

- **ProxyCacheService** : Service de cache et proxy pour les appels aux API externes (JCDecaux et OpenRouteService)
- **RoutingService** : Service REST qui calcule les itinéraires complets incluant marche et vélo
- **SharedModels** : Bibliothèque partagée contenant les modèles de données communs

## Configuration requise

- .NET Framework 4.7.2
- Visual Studio 2019 ou supérieur
- Apache ActiveMQ (pour les futures fonctionnalités de notification)

## Installation

### 1. Installation d'Apache ActiveMQ ( On l'a pas encore)

1. Télécharger Apache ActiveMQ depuis le site officiel : https://activemq.apache.org/
2. Extraire l'archive dans un répertoire de votre choix
3. Pour démarrer ActiveMQ :
   - Windows : Exécuter `bin\activemq.bat start`
   - Linux/Mac : Exécuter `bin/activemq start`
4. Vérifier que ActiveMQ fonctionne en accédant à http://localhost:8161/admin
   - Identifiant par défaut : admin
   - Mot de passe par défaut : admin

### 2. Configuration des clés API

Créer un fichier de configuration `config\jcdecaux.json` dans le répertoire de ProxyCacheServer avec le contenu suivant :

```json
{
  "JCDApiKey": "VOTRE_CLE_JCDECAUX",
  "ORSApiKey": "VOTRE_CLE_OPENROUTESERVICE"
}
```

Pour obtenir les clés API :
- JCDecaux API : https://developer.jcdecaux.com/
- OpenRouteService API : https://openrouteservice.org/dev/#/signup

### 3. Compilation du projet

1. Ouvrir la solution dans Visual Studio
2. Restaurer les packages NuGet
3. Compiler la solution en mode Release ou Debug

## Démarrage des services

Les services doivent être démarrés dans l'ordre suivant :

### 1. Lancement de ProxyCacheService

Exécuter `ProxyCacheServer.exe` depuis le répertoire de sortie.

Le service démarre sur le port **8080** :
```
http://localhost:8080/ProxyCacheService
```

Ce service gère :
- Le cache des contrats JCDecaux
- Le cache des stations de vélos
- Le cache des itinéraires OpenRouteService
- Les appels aux API externes

### 2. Lancement de RoutingService

Exécuter `RoutingServer.exe` depuis le répertoire de sortie.

Le service démarre sur le port **8733** :
```
http://localhost:8733/RoutingService
```

Ce service expose une API REST pour le calcul d'itinéraires complets.

## Utilisation de l'API

### Endpoint principal

**GET** `/itinerary`

Paramètres de requête :
- `originLat` : Latitude du point de départ
- `originLon` : Longitude du point de départ
- `originCity` : Ville de départ
- `destLat` : Latitude du point d'arrivée
- `destLon` : Longitude du point d'arrivée
- `destCity` : Ville d'arrivée

Exemple de requête :
```
http://localhost:8733/RoutingService/itinerary?originLat=45.7640&originLon=4.8357&originCity=Lyon&destLat=45.7489&destLon=4.8467&destCity=Lyon
```

Réponse:
```json
{
  "Success": true,
  "Message": "bike",
  "Data": {
    "TotalDistance": 2547.3,
    "TotalDuration": 850.2,
    "Steps": [
      {
        "Type": "walk",
        "Instructions": "Partir vers le sud-est",
        "Distance": 156.4,
        "Duration": 112.6
      },
      {
        "Type": "bike",
        "Instructions": "Continuer tout droit",
        "Distance": 2234.5,
        "Duration": 625.0
      }
    ],
    "Geometry": {
      "Coordinates": [[4.8357, 45.7640], [4.8467, 45.7489]]
    }
  }
}
```

## Ports utilisés

| Service | Port | URL |
|---------|------|-----|
| ProxyCacheService | 8080 | http://localhost:8080/ProxyCacheService |
| RoutingService | 8733 | http://localhost:8733/RoutingService |
| ActiveMQ Admin | 8161 | http://localhost:8161/admin |
| ActiveMQ Broker | 61616 | tcp://localhost:61616 |

## Fonctionnement

1. Le client envoie une requête au RoutingService avec les coordonnées de départ et d'arrivée
2. Le RoutingService contacte le ProxyCacheService pour récupérer les contrats et stations disponibles
3. Le RoutingService sélectionne les meilleures stations de départ et d'arrivée
4. Le ProxyCacheService calcule les segments de marche et de vélo via OpenRouteService
5. Le RoutingService compare l'itinéraire vélo avec un trajet 100% à pied
6. Le service retourne une recommandation (bike ou walk) avec l'itinéraire détaillé

## Cache

Le ProxyCacheService met en cache :
- **Contrats JCDecaux** : 24 heures
- **Stations de vélos** : 10 minutes
- **Itinéraires** : 10 minutes

Cela permet de réduire le nombre d'appels aux API externes et d'améliorer les performances.

## Résolution des problèmes

Le service ne démarre pas :
- Vérifier que les ports 8080 et 8733 ne sont pas déjà utilisés
- Vérifier que le fichier `config\jcdecaux.json` existe et contient les bonnes clés API

- Erreur "No JCDecaux contract for city" :
- Vérifier que la ville demandée dispose d'un service de vélos en libre-service JCDecaux
- Le système effectue un fallback automatique vers le contrat le plus proche si la ville n'est pas reconnue

Erreur "OpenRouteService error" :
- Vérifier que la clé API OpenRouteService est valide
- Vérifier la limite de requêtes de votre compte OpenRouteService

## Support CORS

Le RoutingService inclut le support CORS pour permettre les requêtes depuis des applications web hébergées sur d'autres domaines.
