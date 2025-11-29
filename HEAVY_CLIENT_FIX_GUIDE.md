# ?? Guide de Correction - Heavy Client

## ? Problèmes Corrigés

### 1. **Erreur WCF - Message size quota exceeded**
   
L'erreur `The maximum message size quota for incoming messages (65536) has been exceeded` a été corrigée dans **3 endroits** :

#### a) **HeavyClient** (Client final)
- **Fichier** : `HeavyClient\App.config`
- **Changement** : Augmentation de `maxReceivedMessageSize` de 65536 ? 2147483647 (2GB)

#### b) **RoutingServer** (Client du ProxyCacheService)
- **Fichier** : `RoutingServer\App.config`
- **Changement** : Les DEUX bindings ont été configurés avec les grandes limites
  - `BasicHttpBinding_IProxyCacheService` ?
  - `BasicHttpBinding_IProxyCacheService1` ? (était manquant)

#### c) **ProxyCacheServer** (Serveur)
- **Fichier** : `ProxyCacheServer\Program.cs`
- **Changement** : Configuration explicite du binding avec grandes limites de messages

### 2. **Erreurs de Compilation C#**
- **Problème** : Namespace mismatch entre XAML et C#
- **Solution** : Changement de namespace de `LetsGoBikingHeavyClient` ? `HeavyClient`

### 3. **Problème JSON Serialization**
- **Problème** : Utilisation de `JsonSerializer` (System.Text.Json) non disponible en .NET Framework 4.7.2
- **Solution** : Utilisation de `JsonConvert` (Newtonsoft.Json)

### 4. **Syntaxe C# 7.3**
- **Problème** : Utilisation de nullable reference types `object?` non supporté en C# 7.3
- **Solution** : Changement vers `object`

---

## ?? Comment Tester

### Étape 1 : Démarrer les Services dans l'Ordre

1. **ProxyCacheServer** (Port 8080)
   ```
   Démarrer le projet ProxyCacheServer
   Vérifier le message : "Max message size: 2047 MB"
   ```

2. **RoutingServer** (Ports 8733 REST + 8734 SOAP)
   ```
   Démarrer le projet RoutingServer
   Vérifier les messages :
   - REST: Max message size: 2047 MB
   - SOAP: Max message size: 2047 MB
   ```

3. **HeavyClient** (Application WPF)
   ```
   Démarrer le projet HeavyClient
   ```

### Étape 2 : Vérifier le Bon Fonctionnement

Le HeavyClient devrait :
1. ? Charger la carte (Leaflet.js)
2. ? Afficher le panneau d'instructions à gauche
3. ? Appeler le service SOAP sans erreur de quota
4. ? Dessiner l'itinéraire Lyon ? Paris sur la carte
5. ? Afficher les instructions étape par étape
6. ? Animer le marqueur bleu le long du trajet

### Étape 3 : Messages Console Attendus

#### ProxyCacheServer
```
[ProxyCacheService] Service started at http://localhost:8080/ProxyCacheService
[ProxyCacheService] WSDL available at http://localhost:8080/ProxyCacheService?wsdl
[ProxyCacheService] Max message size: 2047 MB
```

#### RoutingServer
```
[RoutingService] REST endpoint started successfully
[RoutingService] REST URL: http://localhost:8733/RoutingService
[RoutingService] Max message size: 2047 MB
[RoutingService] SOAP endpoint started successfully
[RoutingService] SOAP URL: http://localhost:8734/RoutingServiceSOAP?wsdl
[RoutingService] Max message size: 2047 MB
```

#### HeavyClient
```
[INFO] 2024-XX-XX HH:mm:ss - WebView2 initialized successfully
[INFO] 2024-XX-XX HH:mm:ss - Map initialized from: C:\...\HeavyClient\www\map.html
[INFO] 2024-XX-XX HH:mm:ss - Requesting itinerary from routing service
[INFO] 2024-XX-XX HH:mm:ss - Received itinerary with X steps
[INFO] 2024-XX-XX HH:mm:ss - Route segments drawn on map
[INFO] 2024-XX-XX HH:mm:ss - Instructions displayed in panel
```

---

## ?? Si Vous Avez Encore des Erreurs

### Erreur : "The maximum message size quota..."
- ? **Vérifier** : Avez-vous redémarré TOUS les services après les modifications ?
- ? **Vérifier** : Les 3 App.config/Program.cs ont-ils été sauvegardés ?
- ? **Solution** : Arrêtez tous les services, rebuilder la solution, redémarrer dans l'ordre

### Erreur : "WebView2 initialization failed"
- ? **Vérifier** : WebView2 Runtime est-il installé ?
- ? **Télécharger** : https://developer.microsoft.com/microsoft-edge/webview2/

### Erreur : "Map HTML file not found"
- ? **Vérifier** : Le fichier `HeavyClient\www\map.html` existe-t-il ?
- ? **Vérifier** : Est-il configuré pour être copié dans le répertoire de sortie ?

### Erreur de Connection
- ? **Vérifier** : ProxyCacheServer est démarré AVANT RoutingServer
- ? **Vérifier** : RoutingServer est démarré AVANT HeavyClient

---

## ?? Fichiers Modifiés

1. ?? `HeavyClient\MainWindow.xaml.cs`
2. ?? `HeavyClient\App.config`
3. ?? `RoutingServer\App.config`
4. ?? `ProxyCacheServer\Program.cs`

---

## ?? Résumé des Configurations

| Service | Type | Max Message Size | Port |
|---------|------|------------------|------|
| ProxyCacheServer | Server | 2GB | 8080 |
| RoutingServer (REST) | Server | 2GB | 8733 |
| RoutingServer (SOAP) | Server | 2GB | 8734 |
| RoutingServer ? ProxyCache | Client | 2GB | - |
| HeavyClient ? RoutingServer | Client | 2GB | - |

Tous les services sont maintenant configurés pour gérer des messages jusqu'à **2GB** (int.MaxValue).

---

## ? Fonctionnalités du Heavy Client

### Carte Interactive
- ??? Carte Leaflet.js avec OpenStreetMap
- ?? Marqueurs : Départ (vert), Arrivée (rouge), Position (bleu)
- ?? Segments de marche (gris)
- ?? Segments de vélo (bleu)
- ?? Stations vélo (orange = pickup, violet = dropoff)

### Panneau d'Instructions
- ?? Liste détaillée des étapes
- ?? Mode de déplacement (Walk/Bike)
- ?? Distance par étape
- ?? Durée par étape
- ?? Résumé total (distance + durée)

### Animation
- ?? Marqueur animé suivant l'itinéraire
- ? Animation fluide à 50ms par frame

---

Bon test ! ??
