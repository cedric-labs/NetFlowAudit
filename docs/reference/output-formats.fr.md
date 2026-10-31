> **Language / Langue**: [English](output-formats.md) · **Français**

# Référence des formats de sortie

## Journal d'audit CSV

Écrit par `audit` (`-o`, `OutputCsvPath`). Les champs suivent les règles de guillemets de la RFC 4180 ; le séparateur est `CsvDelimiter` (`,` par défaut). Une ligne par flux dédupliqué. Un flux est identifié par son protocole, son sens, son application (`TargetExecutable`, ou `ExecutablePath` en l'absence de cible) et :

| Sens | Identifié aussi par | Ignoré |
| :--- | :--- | :--- |
| `Outbound` | adresse et port distants | port local (client) |
| `Inbound` | port local et adresse distante | port distant (client) |
| `Listening` | port local | adresses |

Les connexions répétées vers un même service mettent donc à jour une seule ligne au lieu d'en ajouter.

`--columns` et `CsvColumns` choisissent et ordonnent les colonnes ; sans eux, les 20 colonnes ci-dessous sont écrites dans cet ordre.

| Colonne | Catégorie | Contenu |
| :--- | :--- | :--- |
| `FirstSeenUtc` | Horodatage | Date et heure UTC de la première observation du flux |
| `LastSeenUtc` | Horodatage | Date et heure UTC de la dernière observation du flux |
| `ObservationCount` | Mesure | Nombre de balayages où le flux a été vu |
| `ServerName` | Serveur | Nom de la machine auditée |
| `ServerOsVersion` | Serveur | Description du système de la machine auditée |
| `ServerPrimaryIp` | Serveur | Adresse IP principale de la machine auditée |
| `Protocol` | Réseau | `Tcp` ou `Udp` |
| `Direction` | Réseau | `Listening`, `Inbound` ou `Outbound` |
| `TcpState` | Réseau | État TCP (`Established`, `Listen`, `TimeWait`, `CloseWait`…) |
| `LocalIp` | Réseau | Adresse locale du socket |
| `LocalPort` | Réseau | Port local du socket |
| `RemoteIp` | Réseau | Adresse distante (`0.0.0.0` pour une écoute) |
| `RemotePort` | Réseau | Port distant (`0` pour une écoute) |
| `RemoteHostName` | Réseau | Nom issu d'un alias, du DNS inverse ou du propriétaire RDAP |
| `ProcessId` | Processus | Identifiant du processus |
| `ProcessName` | Processus | Nom de l'exécutable du processus |
| `ExecutablePath` | Processus | Chemin complet de l'exécutable ; vide quand le processus ne peut pas être ouvert |
| `CommandLine` | Processus | Ligne de commande du processus |
| `TargetExecutable` | Processus | Application lancée par un lanceur (`.jar`, `.py`, `.ps1`, `.sh`, `.dll`…) |
| `UserName` | Processus | Compte du processus : `DOMAINE\compte` sous Windows (le SID quand le compte n'a plus de nom), le nom d'utilisateur sous Linux (l'UID numérique quand il n'est pas dans `/etc/passwd`) ; vide quand le processus ne peut pas être ouvert |

Les processus d'identifiant 0 ou inférieur affichent `SYSTEM`.

Au démarrage, `audit` relit le journal CSV existant (`--reload-history`, `ReloadHistoryLimit`) pour que les compteurs et les dates de première observation se poursuivent d'un redémarrage à l'autre.

## NDJSON

Écrit par `audit --ndjson` et par `export ndjson`. Un objet JSON par ligne, avec les 20 mêmes valeurs que le journal CSV, sous des noms en camel case :

`firstSeenUtc`, `lastSeenUtc`, `observationCount`, `serverName`, `serverOsVersion`, `serverPrimaryIp`, `protocol`, `direction`, `tcpState`, `localIp`, `localPort`, `remoteIp`, `remotePort`, `remoteHostName`, `processId`, `processName`, `executablePath`, `commandLine`, `targetExecutable`, `userName`.

`export rules` et `resolve` lisent les fichiers NDJSON (`.ndjson`, `.jsonl`) comme les fichiers CSV.

## Matrice de règles de pare-feu

Écrite par `export rules`. Une ligne par sens, adresse source, adresse de destination, port de destination, protocole et processus, triée par sens, zone, port, destination et source.

| Colonne | Contenu |
| :--- | :--- |
| `RuleId` | `FLUX-001`, `FLUX-002`… dans l'ordre du tri |
| `Direction` | `Outbound` ou `Inbound` |
| `SourceIp` | Adresse du client ; `0.0.0.0` pour une écoute |
| `SourceHost` | Nom du client : le serveur audité pour un flux sortant, le nom distant pour un flux entrant, `Any` pour une écoute |
| `DestinationIp` | Adresse du serveur |
| `DestinationHost` | Nom du serveur |
| `DestinationZone` | `Internal` (bouclage, plage privée, `0.0.0.0`, `::`) ou `Public` |
| `Protocol` | `TCP` ou `UDP` |
| `DestinationPort` | Port du service |
| `ServiceName` | Nom déduit du port et du processus (par exemple `Oracle-TNS` pour 1521) |
| `ProcessName` | Processus propriétaire du socket |
| `ExecutablePath` | Chemin de ce processus |
| `UserName` | Compte de ce processus, comme dans le journal |
| `ObservationCount` | Somme des compteurs des flux regroupés |
| `FirstSeenUtc` | Première observation la plus ancienne |
| `LastSeenUtc` | Observation la plus récente |

## Matrice par paire

Écrite par `export rules --pairs-output`, à partir des lignes de la matrice de règles après filtrage. Une ligne par adresse source et adresse de destination : les deux sens entre deux serveurs restent sur deux lignes, comme des règles de pare-feu. Triée par adresse source puis de destination.

| Colonne | Contenu |
| :--- | :--- |
| `PairId` | `PAIR-001`, `PAIR-002`… dans l'ordre du tri |
| `SourceIp`, `SourceHost` | Adresse et nom du client, comme dans la matrice de règles |
| `DestinationIp`, `DestinationHost` | Adresse et nom du serveur, comme dans la matrice de règles |
| `FlowCount` | Nombre de lignes de la matrice de règles regroupées dans la paire |
| `Ports` | Ports de destination distincts, `port/protocole`, triés par port (`443/TCP, 873/TCP`) |
| `ObservationCount` | Somme des compteurs des lignes regroupées |
| `FirstSeenUtc` | Première observation la plus ancienne |
| `LastSeenUtc` | Observation la plus récente |

## Carte topologique

`map --html` et `audit --html` écrivent un seul fichier HTML qui s'ouvre sans accès réseau. La page propose des choix de disposition, de tri, de routage et d'espacement, des filtres texte appliqués par le bouton `Apply Filters` ou `Enter`, la pagination des serveurs, un export SVG, et une section `CLI Command` qui affiche la commande `PortAtlas map` reproduisant les filtres et choix de disposition en cours. La commande copiée ne contient pas d'option `-i` : ajoutez les journaux à lire.

`map --drawio` écrit un fichier Draw.io non compressé (`.drawio`) pour Draw.io Desktop ou diagrams.net. Les serveurs cibles sont regroupés en une zone par sous-réseau /24 (toutes les adresses IPv4 publiques partagent une seule zone « Internet »), et les zones passent à la ligne au-delà d'une largeur fixe pour que le schéma reste lisible sur un grand réseau. Avec `--drawio-subnets off` (`DrawioSubnets`), aucune zone n'est dessinée et les serveurs sont disposés en grille simple. `--sort` (`MapSortMethod`) fixe l'ordre des serveurs et, pour les méthodes par sous-réseau, des zones.

## Cache DNS

`netflow_dns_cache.json` (`DnsCacheFilePath`) associe chaque adresse résolue à son nom et à l'origine de ce nom, par exemple :

```json
{ "198.51.100.20": { "HostName": "app.example.com", "Source": "DNS" } }
```

`audit` le lit et le met à jour ; `resolve` en fusionne plusieurs ; `lookup` le lit.
