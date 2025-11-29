# Let's Go Biking - Architecture Détaillée

## Vue d'Ensemble du Système

Le système "Let's Go Biking" est une architecture SOA (Service-Oriented Architecture) distribuée qui calcule des itinéraires multimodaux (vélo + marche) en utilisant les services de vélos partagés JCDecaux.

```
???????????????????????????????????????????????????????????????????????????
?                          EXTERNAL APIs                                   ?
???????????????????????????????????????????????????????????????????????????
?  • JCDecaux API (Bike Stations)                                         ?
?  • OpenRouteService API (Routing)                                       ?
?  • Open-Meteo API (Weather Alerts)                                      ?
???????????????????????????????????????????????????????????????????????????
                                    ? HTTP/REST
???????????????????????????????????????????????????????????????????????????
?                    ProxyCacheService (Port 8080)                         ?
?                      (.NET Framework 4.7.2)                              ?
???????????????????????????????????????????????????????????????????????????
?  Rôle: Cache + Proxy pour APIs externes                                 ?
?  Protocol: WCF SOAP (BasicHttpBinding)                                  ?
?  Endpoints:                                                              ?
?    • GetAvailableContracts()                                            ?
?    • GetStationsByContract(contractName)                                ?
?    • CallORS(profile, start, end)                                       ?
?    • ComputeRoute(startLat, startLon, endLat, endLon, isBike)          ?
?  Cache: GenericProxyCache<T> avec TTL configurable                      ?
?    - Contracts: 24h (86400s)                                            ?
?    - Stations: 10min (600s)                                             ?
?    - Routes: 10min (600s)                                               ?
???????????????????????????????????????????????????????????????????????????
                                    ? WCF SOAP
???????????????????????????????????????????????????????????????????????????
?                      RoutingService (Ports 8733/8734)                    ?
?                      (.NET Framework 4.7.2)                              ?
???????????????????????????????????????????????????????????????????????????
?  Rôle: Service de calcul d'itinéraires intelligents                     ?
?  Protocols:                                                              ?
?    • REST: http://localhost:8733/RoutingService (WebHttpBinding)        ?
?    • SOAP: http://localhost:8734/RoutingServiceSOAP (BasicHttpBinding)  ?
?  Endpoints:                                                              ?
?    • GetItinerary(originLat, originLon, originCity,                     ?
?                   destLat, destLon, destCity)                           ?
?  Stratégies de Routage:                                                 ?
?    1. Intra-contract: Même contrat JCDecaux                             ?
?    2. Inter-contract: Deux contrats différents                          ?
?    3. Hybrid walk-to-bike: Pas de contrat origine                       ?
?    4. Hybrid bike-to-walk: Pas de contrat destination                   ?
?    5. Multi-contract: Distance > 50km (chaînage de contrats)           ?
?    6. Walk-only: Aucun contrat disponible                              ?
???????????????????????????????????????????????????????????????????????????
                    ? REST/SOAP                     ? SOAP
        ?????????????????????????       ??????????????????????????
        ?                       ?       ?                        ?
????????????????????    ????????????????????    ??????????????????????
?  Frontend Web    ?    ?   HeavyClient     ?    ?  Other SOAP        ?
?  (Port 8081)     ?    ?   (.NET 8 WPF)    ?    ?  Clients           ?
????????????????????    ?????????????????????    ??????????????????????
? • HTML/CSS/JS    ?    ? • WPF Window      ?
? • Leaflet Map    ?    ? • WebView2        ?
? • REST calls     ?    ? • Leaflet Map     ?
?   to Routing     ?    ? • SOAP calls      ?
?   Service        ?    ?   via proxy       ?
????????????????????    ?????????????????????
                                    
???????????????????????????????????????????????????????????????????????????
?                 NotificationService (Port 61616)                         ?
?                      (.NET Framework 4.8)                                ?
???????????????????????????????????????????????????????????????????????????
?  Rôle: Publication d'événements temps réel                              ?
?  Protocol: Apache ActiveMQ (NMS/JMS)                                    ?
?  Broker: tcp://localhost:61616                                          ?
?  Topic: "notifications.global"                                          ?
?  Types d'événements:                                                    ?
?    • POLLUTION_ALERT                                                    ?
?    • STATION_MAINTENANCE                                                ?
?    • BIKE_AVAILABILITY_LOW / HIGH                                       ?
?    • STATION_FULL                                                       ?
?    • WEATHER_WARNING (avec API Open-Meteo)                             ?
?    • TRAFFIC_ALERT                                                      ?
?  Message Properties:                                                    ?
?    - EventType: Type de l'événement                                     ?
?    - Timestamp: ISO 8601                                                ?
?    - Severity: HIGH / MEDIUM / LOW                                      ?
?  TTL: 5 minutes                                                         ?
???????????????????????????????????????????????????????????????????????????
                                    ? ActiveMQ Subscribe
                        ??????????????????????????
                        ?  ActiveMQ Broker       ?
                        ?  (Port 61616)          ?
                        ?  Topic: notifications  ?
                        ??????????????????????????
                                    ?
                        ??????????????????????????
                        ?  Subscribers           ?
                        ?  (Frontend, Clients)   ?
                        ??????????????????????????

???????????????????????????????????????????????????????????????????????????
?                            SharedModels                                  ?
?                         (.NET Standard 2.0)                              ?
???????????????????????????????????????????????????????????????????????????
?  Rôle: Modèles de données partagés entre tous les projets               ?
?  Classes principales:                                                    ?
?    • BikeStation: Représente une station de vélos                       ?
?      - Number, Name, Address                                            ?
?      - Position (Latitude, Longitude)                                   ?
?      - TotalStands, MainStands, OverflowStands                          ?
?      - Availabilities (Bikes, Stands)                                   ?
?      - Status: OPEN/CLOSED                                              ?
?    • BikeContract: Contrat JCDecaux                                     ?
?      - Name, Commercial_Name                                            ?
?      - Cities: List<string>                                             ?
?      - Country_Code                                                     ?
?    • ORSResult: Résultat OpenRouteService                               ?
?      - Features, Routes, Segments, Steps                                ?
?      - Geometry (Coordinates)                                           ?
?      - Summary (Distance, Duration)                                     ?
?  Utilisation: Sérialization JSON (Newtonsoft.Json)                      ?
???????????????????????????????????????????????????????????????????????????
```

---

## 1. ProxyCacheService

### 1.1 Rôle et Responsabilités
- **Proxy intelligent** vers les APIs externes (JCDecaux, OpenRouteService)
- **Système de cache** pour réduire les appels API et améliorer les performances
- **Point d'entrée unique** pour toutes les données externes

### 1.2 Architecture Interne

```
ProxyCacheServiceImpl
??? _httpClient (HttpClient)
??? _contractsCache (GenericProxyCache<Contracts>)
??? _stationsCache (GenericProxyCache<Stations>)
??? _routeCache (GenericProxyCache<OpenRouteResult>)
??? _JCDapiKey (clé API JCDecaux)
??? _ORSapiKey (clé API OpenRouteService)
```

### 1.3 Endpoints WCF SOAP

**URL de base:** `http://localhost:8080/ProxyCacheService`

#### GetAvailableContracts()
```
INPUT:  Aucun
OUTPUT: List<BikeContract>
CACHE:  86400 secondes (24 heures)
API:    https://api.jcdecaux.com/vls/v1/contracts?apiKey={key}
```

#### GetStationsByContract(contractName)
```
INPUT:  string contractName
OUTPUT: List<BikeStation>
CACHE:  600 secondes (10 minutes)
API:    https://api.jcdecaux.com/vls/v1/stations?contract={contract}&apiKey={key}
```

#### CallORS(profile, start, end)
```
INPUT:  
  - profile: "foot-walking" | "cycling-regular"
  - start: "longitude,latitude"
  - end: "longitude,latitude"
OUTPUT: string (JSON)
CACHE:  600 secondes (10 minutes)
API:    https://api.openrouteservice.org/v2/directions/{profile}/geojson
METHOD: POST
BODY:   { coordinates: [[lon1,lat1], [lon2,lat2]], instructions: true }
HEADER: Authorization: {ORSApiKey}
```

#### ComputeRoute(startLat, startLon, endLat, endLon, isBike)
```
INPUT:  
  - startLatitude: double
  - startLongitude: double
  - endLatitude: double
  - endLongitude: double
  - isBike: bool
OUTPUT: string (JSON)
CACHE:  600 secondes
LOGIC:  profile = isBike ? "cycling-regular" : "foot-walking"
```

### 1.4 Système de Cache

**GenericProxyCache<T>**
```csharp
Dictionary<string, CacheEntry<T>> _cache
CacheEntry<T> {
    T Value
    DateTime ExpiryTime
}
```

**Stratégie:**
- Thread-safe (lock)
- TTL (Time To Live) configurable par type de données
- Lazy loading: appel API seulement si cache expiré
- Clés de cache: composées des paramètres de la requête

**Exemples de clés:**
```
"jcdecaux_contracts"
"Lyon" (pour stations)
"ORS_foot-walking_4.85,45.75_2.35,48.85"
```

### 1.5 ContractResolver

Résout les contrats JCDecaux basés sur des coordonnées géographiques.

```
ContractInfo {
    Name: string
    MinLat, MaxLat, MinLon, MaxLon: double
    Area: double (calculée)
}
```

**Stratégie:**
1. Précharge les contrats populaires au démarrage
2. Vérifie si les coordonnées sont dans une bounding box
3. Si aucune correspondance: charge tous les contrats
4. Fallback: trouve le contrat le plus proche (Haversine)

**Contrats populaires préchargés:**
- Lyon, Paris, Marseille, Toulouse, Nantes

### 1.6 Configuration

**Fichier:** `config/jcdecaux.json`
```json
{
  "JCDApiKey": "votre-clé-jcdecaux",
  "ORSApiKey": "votre-clé-openrouteservice"
}
```

---

## 2. RoutingService

### 2.1 Rôle et Responsabilités
- **Calcul d'itinéraires intelligents** multimodaux (vélo + marche)
- **Sélection des meilleures stations** (disponibilité en temps réel)
- **Optimisation des trajets** selon plusieurs stratégies
- **Gestion multi-contrats** pour les longues distances

### 2.2 Architecture Dual Protocol

**REST Endpoint:**
```
URL:     http://localhost:8733/RoutingService
Binding: WebHttpBinding
Format:  JSON
CORS:    Activé (CorsEnablingBehavior)
```

**SOAP Endpoint:**
```
URL:     http://localhost:8734/RoutingServiceSOAP
Binding: BasicHttpBinding
Format:  XML
WSDL:    http://localhost:8734/RoutingServiceSOAP?wsdl
```

### 2.3 Stratégies de Routage

#### 2.3.1 Intra-Contract (Même ville)
```
Origine et Destination dans le même contrat JCDecaux

FLOW:
1. GetStationsByContract(contract)
2. Trouver 3 stations départ les plus proches avec vélos disponibles
3. Calculer vraie distance de marche (ORS) pour chaque station
4. Sélectionner meilleure station départ
5. Trouver 3 stations arrivée les plus proches avec places disponibles
6. Calculer vraie distance de marche vers destination
7. Sélectionner meilleure station arrivée
8. Calculer segment vélo entre stations
9. Combiner: Marche1 + Vélo + Marche2

EXEMPLE: Lyon Part-Dieu ? Lyon Bellecour
```

#### 2.3.2 Inter-Contract (Entre deux villes)
```
Origine et Destination dans des contrats différents

FLOW:
1. Segment 1: Marche origine ? Station départ (Contract A)
2. Segment 2: Vélo dans Contract A ? Station sortie optimale
3. Station sortie = plus proche de l'entrée du Contract B
4. Segment 3: Marche entre les contrats
5. Segment 4: Vélo dans Contract B ? Station finale
6. Segment 5: Marche station finale ? destination

EXEMPLE: Lyon ? Villeurbanne (contrats séparés)
```

#### 2.3.3 Hybrid Walk-to-Bike
```
Pas de contrat à l'origine, contrat à la destination

FLOW:
1. Marche origine ? Station entrée (Contract destination)
2. Vélo dans le contrat ? Station sortie proche destination
3. Marche station sortie ? destination

DECISION:
- Compare temps total vs marche directe
- Recommande vélo si gain > 10%

EXEMPLE: Campagne ? Centre-ville Lyon
```

#### 2.3.4 Hybrid Bike-to-Walk
```
Contrat à l'origine, pas de contrat à la destination

FLOW:
1. Marche origine ? Station départ (Contract origine)
2. Vélo dans le contrat ? Station sortie proche destination
3. Marche station sortie ? destination

DECISION:
- Compare temps total vs marche directe
- Recommande vélo si gain > 10%

EXEMPLE: Centre-ville Lyon ? Banlieue
```

#### 2.3.5 Multi-Contract (Longue distance > 50km)
```
Distance crow-fly > 50km

FLOW:
1. Détection de tous les contrats sur le trajet (ContractDetector)
2. Pour chaque contrat:
   a. Marche ? Station entrée
   b. Vélo ? Station sortie (direction destination)
   c. Mise à jour position courante
3. Dernier segment: Marche ? destination

OPTIMISATIONS:
- Station sortie = celle qui minimise distance vers prochain contrat
- Utilise bounding boxes pour performance
- Fallback intelligent si stations indisponibles

EXEMPLE: Lyon ? Paris
- Lyon (vélo)
- Villefranche-sur-Saône (vélo)
- Mâcon (vélo)
- ... (autres contrats)
- Paris (vélo)
```

#### 2.3.6 Walk-Only (Pas de contrat)
```
Aucun contrat disponible

FLOW:
1. Calcul trajet piéton direct (ORS foot-walking)

EXEMPLE: Zone rurale sans vélos partagés
```

### 2.4 Algorithmes de Sélection de Stations

#### GetThreeClosestStartStations
```
CRITÈRES:
- Status = "OPEN"
- TotalStands.Availabilities.Bikes > 0
- Distance Haversine la plus courte
- Limite: 3 stations

RETOUR: List<BikeStation> (max 3)
```

#### GetThreeClosestEndStations
```
CRITÈRES:
- Status = "OPEN"
- TotalStands.Availabilities.Stands > 0 (places libres)
- Distance Haversine la plus courte
- Limite: 3 stations

RETOUR: List<BikeStation> (max 3)
```

#### ComputeRealWalkOriginToStations
```
INPUT: Liste de 3 stations candidates
PROCESS:
- Calcul parallèle (Task.Run) du trajet piéton réel (ORS)
- Stockage { station, walkingDistance, walkData }
OUTPUT: Stations triées par distance de marche réelle
```

#### FindBestExitStation (Inter-contract)
```
POUR chaque station de sortie possible dans Contract A:
  1. Trouver station d'entrée la plus proche dans Contract B
  2. Calculer distance Haversine entre les deux
  3. Stocker distance minimale
RETOUR: Station qui minimise la distance entre contrats
```

### 2.5 Modèles de Données Retournés

#### ItineraryResult
```csharp
class ItineraryResult {
    bool Success
    string Message  // "walk" | "bike"
    ItineraryData Data
}
```

#### ItineraryData
```csharp
class ItineraryData {
    double TotalDistance  // mètres
    double TotalDuration  // secondes
    Step[] Steps
    Geometry Geometry
}
```

#### Step
```csharp
class Step {
    string Type           // "walk" | "bike"
    string Instructions   // "Tourner à gauche..."
    double Distance       // mètres
    double Duration       // secondes
    double[][] Coordinates // [[lon,lat], ...]
}
```

#### Geometry
```csharp
class Geometry {
    double[][] Coordinates  // Toutes les coordonnées du trajet
}
```

### 2.6 ContractDetector

Détecte les contrats disponibles le long d'un trajet.

```
DetectContractsOnRoute(originLat, originLon, destLat, destLon, maxDistance)

ALGORITHME:
1. Charger tous les contrats JCDecaux
2. Pour chaque contrat:
   a. Charger toutes les stations
   b. Calculer centre géographique (moyenne lat/lon)
   c. Calculer distance perpendiculaire à la ligne origine-destination
   d. Si distance < maxDistance: ajouter à la liste
3. Trier par distance depuis l'origine

RETOUR: List<ContractOnRoute> {
    ContractName
    CenterLat, CenterLon
    DistanceFromOrigin
    Stations: List<BikeStation>
}

CONSTANTE: maxDistance = 100 km par défaut
```

### 2.7 Parsing ORS Response

```
ParseORSJson(orsJson, stepType)

INPUT: 
- orsJson: Réponse OpenRouteService (GeoJSON)
- stepType: "walk" | "bike"

PROCESS:
1. Désérialisation JSON ? ORSResult
2. Extraction Features[0].Properties.Segments[0].Steps
3. Extraction Geometry.Coordinates (toutes les coordonnées)
4. Pour chaque Step ORS:
   a. Déterminer indices de coordonnées (WayPoints)
   b. Extraire sous-ensemble de coordonnées
   c. Créer Step interne {Type, Instructions, Distance, Duration, Coordinates}

OUTPUT: ItineraryData

NOTES:
- Conversion coordonnées ORS [lon,lat] ? interne [lat,lon] pour certaines APIs
- Gestion fallback si WayPoints absents (calcul proportionnel)
```

---

## 3. HeavyClient (WPF)

### 3.1 Rôle et Responsabilités
- **Interface utilisateur riche** pour visualiser les itinéraires
- **Carte interactive** avec Leaflet.js
- **Consommation SOAP** du RoutingService
- **Affichage temps réel** des instructions de navigation

### 3.2 Architecture

```
MainWindow.xaml.cs
??? Browser (WebView2)
??? RoutingServiceClient (SOAP Proxy)
??? MAP_INITIALIZATION_DELAY_MS = 1000
??? ANIMATION_FRAME_DELAY_MS = 50

FLOW:
1. InitializeComponent()
2. Browser.EnsureCoreWebView2Async()
3. Browser_Initialized event
4. InitMap() ? Charge www/map.html
5. Task.Delay(1000) ? Attend carte chargée
6. LoadItinerary() ? Appelle SOAP
7. Traitement réponse
8. Browser.ExecuteScriptAsync() ? Affichage carte
9. AnimateMarker() ? Animation du trajet
```

### 3.3 Communication SOAP

**Service Reference:** `Connected Services\RoutingProxy\Reference.cs`

```csharp
RoutingServiceClient client = new RoutingServiceClient(
    EndpointConfiguration.BasicHttpBinding_IRoutingService
);

ItineraryResult response = await client.GetItineraryAsync(
    "45.75", "4.85", "Lyon",      // origine
    "48.8566", "2.3522", "Paris"  // destination
);
```

**Endpoint Configuration:**
```
URL: http://localhost:8734/RoutingServiceSOAP
Binding: BasicHttpBinding
MaxReceivedMessageSize: int.MaxValue
```

### 3.4 Carte Interactive (map.html + Leaflet)

**Structure:**
```html
<div id="container">
  <div id="instructions-panel">
    <div id="instructions-header">
      <h2>Route Instructions</h2>
      <div id="instructions-summary">
        <div id="total-distance">Distance: -</div>
        <div id="total-duration">Duration: -</div>
      </div>
    </div>
    <ul id="instructions-list"></ul>
  </div>
  <div id="map"></div>
  <div class="legend">...</div>
</div>
```

**Variables JavaScript globales:**
```javascript
var map                 // Instance Leaflet
var routePolyline       // Ligne du trajet
var movingMarker        // Marqueur animé
var startMarker         // Marqueur départ (vert)
var endMarker           // Marqueur arrivée (rouge)
var bikeStartMarker     // Station prise vélo
var bikeEndMarker       // Station dépôt vélo
var stationMarkers[]    // Toutes les stations
```

**Fonctions JavaScript Exposées (window.*):**

#### drawSegments(segments)
```javascript
INPUT: [
  { mode: "WALK", points: [[lat,lon], ...] },
  { mode: "BIKE", points: [[lat,lon], ...] }
]

PROCESS:
1. Supprimer anciens layers
2. Pour chaque segment:
   - Couleur selon mode (WALK=gray, BIKE=blue)
   - Dessiner polyline L.polyline()
   - Ajouter à la carte
3. Marqueur départ (vert) sur premier point
4. Marqueur arrivée (rouge) sur dernier point
5. Marqueur animé (bleu) sur premier point
6. Ajuster vue carte: map.fitBounds()
```

#### displayInstructions(steps, totalDistance, totalDuration)
```javascript
INPUT:
- steps: [{ mode, instruction, distance, duration }, ...]
- totalDistance: nombre (mètres)
- totalDuration: nombre (secondes)

PROCESS:
1. Mettre à jour résumé (distance formatée, durée formatée)
2. Pour chaque step:
   - Créer élément <li>
   - Numéro d'étape
   - Badge mode (Walk/Bike)
   - Instruction textuelle
   - Distance et durée formatées
   - Ajouter à #instructions-list
```

#### moveMarker(lat, lng)
```javascript
INPUT: lat, lng (coordonnées)
PROCESS: movingMarker.setLatLng([lat, lng])
```

#### drawStations(stations) [Pas encore utilisé]
```javascript
INPUT: [
  { 
    name, lat, lon, type: "pickup"|"dropoff",
    bikes, stands 
  }
]

PROCESS:
1. Supprimer anciens marqueurs de stations
2. Pour chaque station:
   - Icône orange (pickup) ou violette (dropoff)
   - Créer marker L.marker()
   - Popup avec infos station
   - Ajouter à stationMarkers[]
```

#### drawRoute(points) [Legacy, conservé pour compatibilité]
```javascript
INPUT: [[lat,lon], ...]
PROCESS: Dessiner une simple polyline bleue
```

**Helpers JavaScript:**
```javascript
formatDistance(meters) ? "1.5 km" | "150 m"
formatDuration(seconds) ? "1h 30min" | "45 min"
```

### 3.5 Processus d'Animation

```csharp
AnimateMarker(List<double[]> points)

POUR chaque point dans points:
  1. await Browser.ExecuteScriptAsync($"moveMarker({lat}, {lon})")
  2. await Task.Delay(50ms)

RÉSULTAT: Marqueur bleu se déplace le long du trajet
VITESSE: 20 points/seconde
```

### 3.6 Logging

```csharp
LogInfo(message)    ? [INFO]  2025-01-15 10:30:45 - message
LogWarning(message) ? [WARN]  2025-01-15 10:30:45 - message
LogError(message)   ? [ERROR] 2025-01-15 10:30:45 - message

OUTPUT: Console.WriteLine()
```

---

## 4. NotificationService

### 4.1 Rôle et Responsabilités
- **Publication d'événements temps réel** sur ActiveMQ
- **Génération d'alertes simulées** (pollution, maintenance, disponibilité)
- **Intégration météo réelle** via Open-Meteo API

### 4.2 Architecture Publish/Subscribe

```
NotificationService (Publisher)
        ?
Apache ActiveMQ Broker (tcp://localhost:61616)
        ?
Topic: "notifications.global"
        ?
Subscribers (Frontend, Applications clientes)
```

### 4.3 Configuration ActiveMQ

```csharp
ConnectionFactory factory = new ConnectionFactory("tcp://localhost:61616");
IConnection connection = factory.CreateConnection();
connection.Start();

ISession session = connection.CreateSession(
    AcknowledgementMode.AutoAcknowledge
);

IDestination topic = session.GetTopic("notifications.global");
IMessageProducer producer = session.CreateProducer(topic);
producer.DeliveryMode = MsgDeliveryMode.NonPersistent;
producer.TimeToLive = TimeSpan.FromMinutes(5);
```

### 4.4 Types d'Événements

| Type | Sévérité | Fréquence | Description |
|------|----------|-----------|-------------|
| POLLUTION_ALERT | HIGH | 10s | Niveau pollution > 80 ?g/m³ |
| STATION_MAINTENANCE | MEDIUM | 5s | Station temporairement fermée |
| BIKE_AVAILABILITY_LOW | MEDIUM | 3s | Moins de 3 vélos disponibles |
| BIKE_AVAILABILITY_HIGH | LOW | 3s | Station réapprovisionnée |
| STATION_FULL | MEDIUM | 5s | Aucune place de parking |
| WEATHER_WARNING | HIGH | 10s | Alerte météo (API réelle) |
| TRAFFIC_ALERT | LOW | 5s | Congestion routière |

### 4.5 Structure des Messages

```csharp
ITextMessage message = producer.CreateTextMessage(msgText);

message.Properties["EventType"] = "POLLUTION_ALERT";
message.Properties["Timestamp"] = DateTime.UtcNow.ToString("o"); // ISO 8601
message.Properties["Severity"] = "HIGH";

producer.Send(message);
```

**Exemple de message:**
```
TEXT: "POLLUTION ALERT: Lyon - Level 125 ?g/m³ (Threshold: 80)"

PROPERTIES:
- EventType: POLLUTION_ALERT
- Timestamp: 2025-01-15T10:30:45.1234567Z
- Severity: HIGH
```

### 4.6 Intégration Open-Meteo

```csharp
FetchRealWeatherAlert(city)

API: https://api.open-meteo.com/v1/forecast
PARAMS:
  - latitude, longitude (selon ville)
  - forecast_days=1
  - timezone=Europe/Paris
  - weather_alerts=true

PROCESS:
1. Mapping ville ? coordonnées
2. Appel HTTP GET
3. Parse JSON: weather_alerts.alerts[0]
4. Extraction: event, description

RETOUR:
"WEATHER WARNING: Lyon - Heavy Rain - 50-80mm expected"

FALLBACK:
Si pas d'alerte API ? génère alerte aléatoire
```

**Villes supportées:**
- Lyon (45.75, 4.85)
- Paris (48.85, 2.35)
- Nantes (47.22, -1.55)
- Toulouse (43.60, 1.44)

### 4.7 Boucle d'Événements

```csharp
while (_isRunning) {
    1. GetRandomEventType()
    2. GenerateNotificationMessage(eventType)
    3. CreateTextMessage()
    4. Set Properties (EventType, Timestamp, Severity)
    5. producer.Send()
    6. Console.WriteLine() ? Log
    7. Thread.Sleep(GetDelayForEventType())
}
```

**Arrêt gracieux:**
```csharp
Console.CancelKeyPress += (sender, e) => {
    _isRunning = false;
    e.Cancel = true;  // Empêche arrêt immédiat
};
```

---

## 5. SharedModels

### 5.1 Rôle et Responsabilités
- **Bibliothèque de modèles partagés** entre tous les projets
- **Désérialisation JSON** standardisée
- **Contrats de données** pour APIs externes

### 5.2 Classes Principales

#### BikeStation
```csharp
class BikeStation {
    int Number                      // ID unique dans le contrat
    string ContractName             // "Lyon", "Paris"...
    string Name                     // "Bellecour"
    string Address                  // "Place Bellecour"
    Position Position               // Lat/Lon
    bool Banking                    // Terminal de paiement
    bool Bonus                      // Points bonus
    string Status                   // "OPEN" | "CLOSED"
    DateTime? LastUpdate            // Dernière mise à jour
    bool Connected                  // Connecté au réseau
    bool Overflow                   // Parking supplémentaire
    object Shape                    // Forme géographique
    Stands TotalStands              // Total (main + overflow)
    Stands MainStands               // Parking principal
    Stands OverflowStands           // Parking supplémentaire
}

class Position {
    double Latitude
    double Longitude
}

class Stands {
    Availabilities Availabilities   // Disponibilité actuelle
    int Capacity                    // Capacité totale
}

class Availabilities {
    int Bikes                       // Vélos disponibles
    int Stands                      // Places libres
    int MechanicalBikes             // Vélos mécaniques
    int ElectricalBikes             // Vélos électriques
}
```

#### BikeContract
```csharp
class BikeContract {
    string Name                     // "Lyon"
    string Commercial_Name          // "Vélo'v"
    List<string> Cities             // ["LYON", "VILLEURBANNE"]
    string Country_Code             // "FR"
}
```

#### ORSResult (OpenRouteService)
```csharp
class ORSResult {
    List<ORSFeature> Features
    List<ORSRoute> Routes
    List<double> BBox              // [minLon, minLat, maxLon, maxLat]
}

class ORSFeature {
    ORSProperties Properties
    ORSGeometry Geometry
}

class ORSProperties {
    List<ORSSegment> Segments
    ORSSummary Summary
}

class ORSSummary {
    double Distance                // mètres
    double Duration                // secondes
}

class ORSSegment {
    double Distance
    double Duration
    List<ORSStep> Steps
}

class ORSStep {
    double Distance
    double Duration
    int Type                       // Code de manœuvre
    string Instruction             // "Tourner à gauche..."
    string Name                    // Nom de rue
    List<int> WayPoints            // Indices de coordonnées
}

class ORSGeometry {
    string Type                    // "LineString"
    List<List<double>> Coordinates // [[lon,lat], ...]
}
```

### 5.3 Attributs JSON

```csharp
using Newtonsoft.Json;

[JsonProperty("number")]
public int Number { get; set; }

[JsonProperty("position")]
public Position Position { get; set; }
```

**Conventions:**
- Propriétés C# en PascalCase
- JSON en camelCase
- Attributs `[JsonProperty]` pour mapping

---

## 6. Flux de Communication Détaillés

### 6.1 Scénario 1: Calcul d'Itinéraire (HeavyClient)

```
????????????????
? HeavyClient  ?
????????????????
       ? 1. GetItineraryAsync("45.75", "4.85", "Lyon", "48.8566", "2.3522", "Paris")
       ?    Protocol: SOAP
       ?
????????????????????
? RoutingService   ?
? (Port 8734)      ?
????????????????????
       ? 2. Détermination stratégie: Multi-contract (distance > 50km)
       ?
       ? 3. Pour chaque segment:
       ?    ?? GetStationsByContract("Lyon") ? ProxyCacheService
       ?    ?? CallORS("foot-walking", "4.85,45.75", "4.86,45.76") ? ProxyCacheService
       ?    ?? CallORS("cycling-regular", "4.86,45.76", "4.87,45.77") ? ProxyCacheService
       ?
????????????????????
? ProxyCacheService?
? (Port 8080)      ?
????????????????????
       ? 4. Vérification cache
       ?    ?? Cache HIT ? Retourne données
       ?    ?? Cache MISS:
       ?         ?? JCDecaux API (stations)
       ?         ?? OpenRouteService API (routing)
       ?
????????????????????
? External APIs    ?
????????????????????
       ? 5. Données brutes
       ?
????????????????????
? ProxyCacheService?
????????????????????
       ? 6. Mise en cache + retour
       ?
????????????????????
? RoutingService   ?
????????????????????
       ? 7. Assemblage itinéraire complet:
       ?    - Tous les segments
       ?    - Instructions détaillées
       ?    - Coordonnées complètes
       ?    - Distance/durée totales
       ?
????????????????????
? HeavyClient      ?
????????????????????
       ? 8. Traitement réponse:
       ?    ?? Conversion coordonnées
       ?    ?? Sérialisation JSON
       ?    ?? Envoi vers JavaScript
       ?
????????????????????
? WebView2         ?
? (map.html)       ?
????????????????????
       ? 9. Affichage:
       ?    ?? drawSegments() ? Polylines colorées
       ?    ?? displayInstructions() ? Panneau latéral
       ?    ?? AnimateMarker() ? Animation
```

### 6.2 Scénario 2: Cache Flow

```
REQUEST: GetStationsByContract("Lyon")
    ?
[ProxyCacheService]
    ?
_stationsCache.GetOrAdd("Lyon", 600, factoryFunc)
    ?
Dictionary lookup: key="Lyon"
    ?
    ?? KEY EXISTS + NOT EXPIRED
    ?       ?
    ?   Return cached value
    ?   [Total time: ~1ms]
    ?
    ?? KEY NOT EXISTS OR EXPIRED
            ?
        Execute factoryFunc:
            ?
        new Stations(_httpClient, "Lyon")
            ?
        HttpClient.GetAsync("https://api.jcdecaux.com/...")
            ?
        JSON Deserialization
            ?
        Store in cache with TTL=600s
            ?
        Return fresh value
        [Total time: ~200-500ms]
```

### 6.3 Scénario 3: Notification Flow

```
????????????????????????
? NotificationService  ?
????????????????????????
       ? 1. GenerateNotificationMessage("WEATHER_WARNING", rand)
       ?    ?? FetchRealWeatherAlert("Lyon")
       ?    ?    ?
       ?    ?  [Open-Meteo API]
       ?    ?    ?
       ?    ?? Parse weather_alerts.alerts[0]
       ?
       ? 2. CreateTextMessage(msgText)
       ?    Properties: {EventType, Timestamp, Severity}
       ?
????????????????????????
? ActiveMQ Producer    ?
? Topic: notifications ?
????????????????????????
       ? 3. Send(message)
       ?    DeliveryMode: NonPersistent
       ?    TTL: 5 minutes
       ?
????????????????????????
? ActiveMQ Broker      ?
? tcp://localhost:61616?
????????????????????????
       ? 4. Distribute to all subscribers
       ?
????????????????????????  ????????????????????????
? Frontend (WebSocket) ?  ? Other Applications   ?
? or JMS Client        ?  ?                      ?
????????????????????????  ????????????????????????
       ?
       ? 5. Display notification
       ?    ?? Alert banner
       ?    ?? Sound notification
       ?    ?? Log to console
```

---

## 7. Considérations Techniques

### 7.1 Performance

**ProxyCacheService:**
- Cache essentiel pour respecter rate limits APIs
- Contracts: 24h (rarement changent)
- Stations: 10min (disponibilité temps réel)
- Routes: 10min (trajets stables)

**RoutingService:**
- Calculs parallèles (Task.Run) pour stations candidates
- Limite 3 stations pour éviter combinatoire explosive
- Optimisation Haversine avant appel ORS (pré-filtrage)

**HeavyClient:**
- WebView2 pour performances natives
- Animation throttlée (50ms/frame)
- Pas de recalcul constant du trajet

### 7.2 Scalabilité

**ProxyCacheService:**
- Instance unique (Singleton)
- Thread-safe (lock sur cache)
- Limite: un serveur, pas de load balancing intégré
- Solution: déployer plusieurs instances avec load balancer externe

**RoutingService:**
- Stateless (chaque requête indépendante)
- Peut être dupliqué facilement
- Goulot: ProxyCacheService (dépendance externe)

**NotificationService:**
- Peut être dupliqué (plusieurs publishers sur même topic)
- ActiveMQ supporte clustering

### 7.3 Sécurité

**API Keys:**
- Stockées dans config files (non versionné!)
- Pas d'encryption (? amélioration nécessaire)
- Exposition limitée (localhost uniquement)

**CORS:**
- Activé sur REST endpoint (CorsEnablingBehavior)
- Permet appels depuis frontend JavaScript

**SOAP:**
- Pas d'authentification (? amélioration nécessaire)
- BasicHttpBinding (pas de sécurité intégrée)

### 7.4 Gestion d'Erreurs

**ProxyCacheService:**
```csharp
try {
    // API call
} catch (Exception ex) {
    throw new FaultException($"Error: {ex.Message}");
}
```

**RoutingService:**
```csharp
try {
    // Routing logic
} catch (Exception ex) {
    return new ItineraryResult {
        Success = false,
        Message = $"Error: {ex.Message}"
    };
}
```

**HeavyClient:**
```csharp
try {
    // SOAP call
} catch (Exception ex) {
    LogError($"Error: {ex.Message}");
    MessageBox.Show(...);
}
```

**NotificationService:**
```csharp
try {
    // Send notification
} catch (Exception ex) {
    Console.WriteLine($"Send error: {ex.Message}");
    Thread.Sleep(1000); // Retry after delay
}
```

### 7.5 Limitations Connues

1. **UsedStations non implémenté:**
   - Référence SOAP HeavyClient ne contient pas UsedStations
   - Nécessite régénération du proxy après mise à jour service

2. **Pas de persistance:**
   - Cache en mémoire uniquement (perdu au redémarrage)
   - Pas de base de données

3. **Pas de monitoring:**
   - Logs console uniquement
   - Pas d'alerting automatique
   - Pas de métriques (temps réponse, taux d'erreur)

4. **Single-threaded NotificationService:**
   - Un seul thread de publication
   - Peut être bloqué si ActiveMQ lent

5. **Pas de retry logic:**
   - Si API externe échoue, requête échoue
   - Pas de circuit breaker

---

## 8. Déploiement

### 8.1 Ordre de Démarrage

```bash
# 1. ActiveMQ
cd C:\apache-activemq\bin\win64
activemq.bat

# 2. ProxyCacheService
cd ProxyCacheServer\bin\Debug
ProxyCacheService.exe

# 3. RoutingService
cd RoutingServer\bin\Debug
RoutingServer.exe

# 4. NotificationService (optionnel)
cd NotificationService\bin\Debug
NotificationService.exe

# 5. HeavyClient
cd HeavyClient\bin\Debug\net8.0-windows
HeavyClient.exe

# OU Frontend Web
cd Frontend
python -m http.server 8081
# Ouvrir http://localhost:8081
```

### 8.2 Scripts de Lancement

**launch_all.bat:** (fourni)
```batch
start activemq.bat
start ProxyCacheService.exe
start RoutingServer.exe
start NotificationService.exe
cd Frontend
start python -m http.server 8081
start http://localhost:8081
```

### 8.3 Ports Utilisés

| Service | Port | Protocol | Description |
|---------|------|----------|-------------|
| ProxyCacheService | 8080 | HTTP/SOAP | WCF BasicHttpBinding |
| RoutingService REST | 8733 | HTTP/REST | WebHttpBinding |
| RoutingService SOAP | 8734 | HTTP/SOAP | BasicHttpBinding |
| Frontend | 8081 | HTTP | Python SimpleHTTPServer |
| ActiveMQ | 61616 | TCP | OpenWire (NMS) |
| ActiveMQ Web Console | 8161 | HTTP | Admin interface |

### 8.4 Configuration Requise

**APIs Externes:**
- ? JCDecaux API Key (config/jcdecaux.json)
- ? OpenRouteService API Key (config/jcdecaux.json)
- ? ActiveMQ installé et démarré

**Frameworks:**
- .NET Framework 4.7.2 (ProxyCacheService, RoutingService)
- .NET Framework 4.8 (NotificationService)
- .NET 8 (HeavyClient)
- Java 8+ (ActiveMQ)
- Python 3.x (Frontend server)

---

## 9. Diagrammes de Séquence

### 9.1 Itinéraire Intra-Contract

```
HeavyClient -> RoutingService: GetItinerary(Lyon, Lyon)
RoutingService -> RoutingService: Détecter stratégie (Intra-contract)
RoutingService -> ProxyCache: GetStationsByContract("Lyon")
ProxyCache -> JCDecaux API: GET /stations?contract=Lyon
JCDecaux API -> ProxyCache: [Stations JSON]
ProxyCache -> RoutingService: List<BikeStation>
RoutingService -> RoutingService: Sélectionner 3 stations départ
RoutingService -> ProxyCache: CallORS("foot-walking", origine, station1)
RoutingService -> ProxyCache: CallORS("foot-walking", origine, station2)
RoutingService -> ProxyCache: CallORS("foot-walking", origine, station3)
ProxyCache -> ORS API: POST /directions/foot-walking
ORS API -> ProxyCache: [Route JSON]
ProxyCache -> RoutingService: Route data (x3)
RoutingService -> RoutingService: Choisir meilleure station départ
RoutingService -> RoutingService: Sélectionner 3 stations arrivée
RoutingService -> ProxyCache: CallORS("foot-walking", station, destination) x3
RoutingService -> RoutingService: Choisir meilleure station arrivée
RoutingService -> ProxyCache: CallORS("cycling-regular", stationDépart, stationArrivée)
RoutingService -> RoutingService: Combiner segments (walk + bike + walk)
RoutingService -> HeavyClient: ItineraryResult
HeavyClient -> WebView2: drawSegments(segments)
HeavyClient -> WebView2: displayInstructions(steps)
HeavyClient -> WebView2: AnimateMarker(points)
```

---

## 10. Métriques et KPIs

### 10.1 Cache Hit Rates

**Attendus:**
- Contracts: >95% (rarement changent)
- Stations: ~70% (10min TTL, usage fréquent)
- Routes: ~50% (dépend de la variété des requêtes)

### 10.2 Temps de Réponse

**Sans cache:**
- JCDecaux stations: 200-500ms
- OpenRouteService: 300-800ms
- Total itinéraire: 2-5 secondes

**Avec cache:**
- Stations (hit): <10ms
- Routes (hit): <10ms
- Total itinéraire: 100-500ms

### 10.3 Volumes

**ProxyCacheService:**
- ~100 contracts (tous JCDecaux France)
- ~200-1000 stations par contrat majeur
- Mémoire: ~50-100 MB

**NotificationService:**
- 10-20 messages/minute
- Rétention ActiveMQ: 5 minutes
- Stockage minimal (non persistant)

---

## Résumé Architecture

**Style:** SOA (Service-Oriented Architecture) + Pub/Sub (Messaging)
**Protocols:** WCF SOAP, REST JSON, ActiveMQ JMS
**Langages:** C# (.NET Framework 4.7.2/4.8/.NET 8), JavaScript (Leaflet)
**APIs Externes:** JCDecaux, OpenRouteService, Open-Meteo
**Cache:** In-memory avec TTL configurable
**UI:** WPF + WebView2 + Leaflet.js

**Forces:**
- ? Séparation claire des responsabilités
- ? Cache intelligent (performance + rate limits)
- ? Stratégies de routage multiples
- ? Notifications temps réel
- ? Interface riche et interactive

**Améliorations Futures:**
- ? Authentification/Sécurité
- ? Persistance (base de données)
- ? Monitoring/Logging centralisé
- ? Retry logic + circuit breaker
- ? Containerisation (Docker)
- ? Load balancing
