# Comment lancer ?

## Le docker compose

- La base de donnée (utilisateur `postgres` et mot de passe `motdepasse` sur la base `postgres` qui sort sur le port `5432`)
- Un serveur MQTT Mosquito qui sort sur le port `1883`
- Le site (`front`) qui sort sur le port `80`
- L'aggrégateur (`recorder`) qui sort sur le port `81̀` (*api REST*)
- Les tâches de fonds (`worker`)
- Le serveur MQTT filtré (`router`) qui sort sur le port `1883` et `82` (*api REST*). **Il est désactivé par défaut**

## Configuration

Il faut configurer *chaque fichier* `appsettings.xxx.json` avant de tout lancer.

### Description du fichier appsettings.json

- `ConnectionStrings.Default` : identifiant de connexion à la base de donnée
- `FrontUrl` : lien externe vers le site par exemple `192.168.1.x:80`
- `AllowSend` : autorise l'envoi de trames depuis le site via la page `/send`
- `NodesIgnored` : tableau d'ID de noeuds (décimal) à ignorer quand on les voit
- `PurgeDays` : quantité de jours à garder dans la base de données (trames, positions, voisins, historiques de signaux, messages, points d'intérêts, pax)

### Configuration pour le serveur MQTT filtré (routeur) uniquement

Après avoir décomentée les lignes dans le fichier `docker-compose.yaml`:
- `CorsHosts` : URLs autorisées séparées par des virgules pour les API Rest
- `IssuerSigningKey` : clé pour le token JWT servant à l'API Rest du router

## Lancement

1. Dans le dossier où se trouve le fichier `docker-compose.yaml`, tapez dans un terminal `docker compose up -d`.
2. Si tout va bien, vous pouvez aller sur [la page d'ajout de serveur](http://localhost:80/admin/server)
3. Sur la page qui s'ouvre, rentrez votre serveur MQTT à ingérer (voir plus bas)
4. Dans votre terminal, tapez `docker compose restart`
5. Attendez quelques secondes et envoyez quelques paquets via votre noeud Meshtastic connecté
6. Allez sur la page [pour voir les trames reçues](http://localhost:80/admin/packets) et vérifiez qu'ils sont bien arrivés

### Ajout d'un serveur MQTT

Voici la déscription des champs de [la page d'ajout de serveur](http://localhost:80/admin/server) :

- **Nom** : un nom pour ce serveur
- **URL** : si vous utilisez le mosquitto du `docker-compose.yaml`, alors **mosquitto**
- **Port** : souvent `1883` par défault pour MQTT, `80` pour un noeud Meshtastic HTTP
- **Identifiant** et **Mot de passe** : identifiants de connexion au serveur MQTT
- **Topics** : `msh/#` suffit normalement. Vous pouvez les séparer par des virgules
- **Activé** : plutôt évident :)

#### Pour les plus avancés

- **Type** : `MqttClient` pour un serveur MQTT classique. `MqttServer` pour le serveur interne filtré. `NodeHttp` pour un noeud Meshtastic classique
- **Type de relais** : Est-ce que les trames qui arrivent d'autres serveurs doivent être relayées sur celui-ci et lesquels (`Toutes`, `Seulement les Map Report`, `Seulement les Map Report, NodeInfo et les Positions`, `Seulement les Map Report, NodeInfo, Positions, TraceRoute et NeighborInfo`)
- **Précision de la position si relayé sur une autre serveur MQTT** : 32 bits (position exacte). Pour le serveur officiel Meshtastic c'est 16
- **Utilise le Worker** : au lieu d'utiliser le projet *Recorder* pour traiter les trames, c'est plutôt le projet *Worker* qui est une file d'attente de chose à faire en continue. Utile pour les grosses charges quand on se branché au serveur officiel par exemple
- **Prioritaire dans le Worker** : si le *Worker* est utilisé, si *prioritaire* alors les trames de ce serveur passent avant les autres. Sinon elles attendent qu'il n'y est plus de prioritaires. Les serveurs qui ne sont pas prioritaires ne prennent pas de nouvelles trames s'il y a plus de 20 trames prioritaires avant eux ou s'il y a déjà 100 trames non prioritaires à traiter
- **Traduire en JSON** : à chaque trame reçue, elle est republié sur le même serveur sur le topic `msh-json` en *JSON*
- **Peut être relayé** : ce serveur à le droit d'être relayé sur les autres qui on un *Type de relais*