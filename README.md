# LetsGoBiking

## Description

LetsGoBiking est une application de calcul d'itinéraires vélo qui utilise les données JCDecaux pour les stations de vélos en libre-service et OpenRouteService pour le calcul des trajets. L'architecture est basée sur des services WCF communiquant entre eux.

## Architecture

Le projet est composé de trois services principaux :

- **ProxyCacheService** : Service de cache et proxy pour les appels aux API externes (JCDecaux et OpenRouteService)
- **RoutingService** : Service REST qui calcule les itinéraires complets incluant marche et vélo
- **SharedModels** : Bibliothèque partagée contenant les modèles de données communs
- **NotificationService** : Service producteur de notifications à l'aide de ActiveMQ
- **HeavyClient** : Client SOAP en c sharp qui consomme le serveur SOAP RoutingServerSOAP pour avoir les itinéraires.
- **Frontend** : Client REST qui consomme le serveur REST RoutingServer.
## Configuration requise

- .NET Framework 4.7.2
- Visual Studio 2019 ou supérieur
- Apache ActiveMQ pour les fonctionnalités de notification

## Installation

### 1. Installation d'Apache ActiveMQ

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

### 3. Compilation du projet

1. Ouvrir la solution dans Visual Studio
2. Restaurer les packages NuGet
3. Compiler la solution en mode Debug

## Démarrage des services

Lancer le launch_all.bat en mode administrateur.
Il lance lui meme les serveurs et le frontend dans le bon ordre, sans avoir à lancer chacun à part.

## Ports utilisés

| Service | Port | URL |
|---------|------|-----|
| ProxyCacheService | 8080 | http://localhost:8080/ProxyCacheService |
| RoutingService | 8733 | http://localhost:8733/RoutingService |  (REST)
| RoutingServiceSOAP | 8734 | http://localhost:8734/RoutingServiceSOAP | (SOAP)
| ActiveMQ Admin | 8161 | http://localhost:8161/admin |
| ActiveMQ Broker | 61616 | tcp://localhost:61616 |
| Frontend | 8081 | http://localhost:8081 |


## Fonctionnement

1. Le Frontend ( ou Le heavyClient ) envoie une requête au RoutingService avec les coordonnées de départ et d'arrivée
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
- Vérifier toujours si le RoutingServerSOAP est bien lancé pour avoir le heavyClient qui marche
- Vérifier que le fichier `config\jcdecaux.json` existe et contient les bonnes clés API

- Erreur "No JCDecaux contract for city" :
- Vérifier que la ville demandée dispose d'un service de vélos en libre-service JCDecaux
- Le système effectue un fallback automatique vers le contrat le plus proche si la ville n'est pas reconnue

Erreur "OpenRouteService error" :
- Vérifier que la clé API OpenRouteService est valide
- Vérifier la limite de requêtes de votre compte OpenRouteService

## Support CORS

Le RoutingService inclut le support CORS pour permettre les requêtes depuis des applications web hébergées sur d'autres domaines.
