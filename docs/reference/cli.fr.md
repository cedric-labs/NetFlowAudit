> **Language / Langue**: [English](cli.md) · **Français**

# Référence de la ligne de commande

PortAtlas est un ensemble de commandes. Chaque commande ne lit que ses propres options : une option donnée à une commande qui ne la lit pas, une option inconnue ou une valeur hors de la liste admise arrêtent le programme avec une erreur et le code de sortie 1.

```text
PortAtlas <command> [options]
```

| Commande | Rôle |
| :--- | :--- |
| [`audit`](#audit) | Balayer les sockets en continu et enregistrer les flux dédupliqués |
| [`map`](#map) | Produire une carte topologique HTML et/ou un diagramme Draw.io à partir des journaux |
| [`export rules`](#export-rules) | Consolider les journaux en matrice de règles de pare-feu |
| [`export ndjson`](#export-ndjson) | Convertir des journaux CSV en un fichier NDJSON |
| [`resolve`](#resolve) | Fusionner des caches DNS et écrire les noms d'hôtes dans les journaux |
| [`lookup`](#lookup) | Diagnostiquer la résolution de nom d'une adresse IP |
| [`config init`](#config-init) | Écrire un fichier de configuration avec les valeurs par défaut |

Sans commande, PortAtlas affiche l'aide et sort avec le code 0. `-h` ou `--help` après une commande affiche l'aide de cette commande.

## Origine des valeurs

Une valeur provient, par ordre de priorité :

1. de la ligne de commande ;
2. du fichier de configuration donné par `--config`, sinon de `portatlas.json` dans le dossier de travail, sinon de `portatlas.json` à côté de l'exécutable ;
3. de la valeur par défaut intégrée au programme.

La clé du fichier de configuration correspondant à chaque option figure dans les tableaux ci-dessous et est décrite dans la [référence de la configuration](configuration.fr.md).

## Option globale

| Option | Valeur | Défaut | Effet |
| :--- | :--- | :--- | :--- |
| `--config` | `<path>` | `portatlas.json` | Fichier de configuration lu par toutes les commandes |
| `--version` | aucune | | Affiche la version et quitte (commande racine uniquement) |

## Jetons de nom de fichier

Les chemins de sortie, et les chemins du fichier de configuration, acceptent ces jetons :

| Jeton | Remplacé par |
| :--- | :--- |
| `{ServerName}` | Nom de la machine |
| `{HostName}` | Nom de la machine (même valeur que `{ServerName}`) |
| `{Date}` | Date UTC, `yyyy-MM-dd` |
| `{Timestamp}` | Date et heure UTC, `yyyyMMdd_HHmmss` |

## Familles d'options partagées

### Entrées

Lues par `map`, `export rules`, `export ndjson` et `resolve`.

| Option | Valeur | Défaut | Effet |
| :--- | :--- | :--- | :--- |
| `-i`, `--input` | `<path>`, répétable | `OutputCsvPath` de la configuration | Un fichier, un dossier ou un motif sur le nom de fichier (`audits/nas-*.csv`) |

Un dossier apporte ses fichiers portant les extensions lues par la commande (`.csv` pour `map` et `export ndjson` ; `.csv`, `.ndjson`, `.jsonl` pour `export rules` et `resolve`). Les sous-dossiers ne sont pas lus. Quand aucun fichier ne correspond, la commande affiche `No audit log found for: …` et sort avec le code 1.

### Filtres de flux

Lus par `audit` (pour sa carte), `map` et `export rules`. Les valeurs de liste sont séparées par des virgules ; une option de liste peut être répétée et les valeurs s'additionnent.

| Option | Valeur | Clé de configuration | Effet |
| :--- | :--- | :--- | :--- |
| `--include-ports` | ports ou plages | `MapIncludedPorts` | Ne garder que ces ports de service (`80,443,8000-8080`) |
| `--exclude-ports` | ports ou plages | `MapExcludedPorts` | Retirer ces ports de service |
| `--include-hosts` | motifs | `MapIncludedHosts` | Ne garder que les serveurs dont le nom correspond (`nas*,*.home.arpa`) |
| `--exclude-hosts` | motifs | `MapExcludedHosts` | Retirer les serveurs dont le nom correspond |
| `--include-ips` | adresses ou CIDR | `MapIncludedIps` | Ne garder que ces adresses (`10.0.0.0/8`) |
| `--exclude-ips` | adresses ou CIDR | `MapExcludedIps` | Retirer ces adresses |
| `--include-processes` | noms | `MapIncludedProcesses` | Ne garder que ces noms de processus ou cibles applicatives |
| `--exclude-processes` | noms | `MapExcludedProcesses` | Retirer ces noms de processus ou cibles applicatives |
| `--direction` | `all`, `in`, `out` | `MapDirection` | Sens des flux à garder ; défaut `all` |
| `--listeners-only` | aucune | `MapListenersOnly` | Ne garder que les sockets en écoute |
| `--include-ephemeral-ports` | aucune | `MapExcludeEphemeralPorts` | Garder les ports 49152-65535, retirés par défaut |

### Rendu de la carte

Lues par `audit` et `map`. Les valeurs admises sont comparées sans tenir compte de la casse.

| Option | Valeur | Clé de configuration | Défaut |
| :--- | :--- | :--- | :--- |
| `--html` | `<path>` | `OutputHtmlMapPath` | aucun |
| `--drawio` | `<path>` | `OutputDrawioPath` | aucun |
| `--layout` | `layered`, `tree`, `horizontal-tree`, `organic`, `circular`, `grid` | `MapLayoutMode` | `layered` |
| `--sort` | `barycentric`, `tiers`, `subnet`, `degree`, `alpha`, `ip` | `MapSortMethod` | `barycentric` |
| `--routing` | `bezier`, `orthogonal`, `straight`, `bus` | `MapRoutingMode` | `bezier` |
| `--spacing` | `compact`, `balanced`, `spacious` | `MapSpacingProfile` | `balanced` |
| `--edge-labels` | `ports`, `none` | `MapEdgeLabels` | `ports` |
| `--drawio-edges` | `adaptive`, `direction`, `pair`, `none` | `DrawioEdgeMode` | `adaptive` |
| `--drawio-theme` | `dark`, `light` | `DrawioTheme` | `dark` |

`--layout`, `--sort`, `--routing`, `--spacing` et `--edge-labels` fixent l'état initial de la carte HTML ; la page permet de les changer.

- Routage `bus` : les flèches d'un serveur partagent un tronc par sens, puis se séparent en une branche par cible. Les branches qui atteignent un serveur par le même bord et dans le même sens aboutissent sous une seule pointe. Pensé pour la mise en page `tree`.
- `--edge-labels none` : décoche *Show Ports*. Les flèches perdent leurs étiquettes de ports et les flux d'une paire de serveurs sont fusionnés en une seule flèche, avec une pointe à chaque extrémité qui reçoit un flux. L'inspecteur du serveur liste toujours les ports.
- `--drawio-edges pair` : une arête par paire de serveurs dans le diagramme Draw.io, avec deux pointes quand les flux vont dans les deux sens.

## audit

Balaye à intervalle fixe les sockets TCP et UDP de la machine, rattache chaque socket à son processus et écrit une ligne par flux dédupliqué dans le journal CSV. Tourne jusqu'à `Q`, `Ctrl+C` ou `SIGTERM`.

```text
PortAtlas audit [options]
```

| Option | Valeur | Clé de configuration | Défaut | Effet |
| :--- | :--- | :--- | :--- | :--- |
| `-o`, `--output` | `<path>` | `OutputCsvPath` | `netflow_audit.csv` | Journal d'audit CSV |
| `--ndjson` | `<path>` | `OutputNdjsonPath` | aucun | Journal NDJSON écrit en plus du CSV |
| `--interval` | secondes, au moins 1 | `ScanIntervalSeconds` | `5` | Délai entre deux balayages |
| `--columns` | noms de colonnes | `CsvColumns` | les 20 | Colonnes CSV, dans l'ordre ([liste](output-formats.fr.md#journal-daudit-csv)) |
| `--reload-history` | `all` ou un nombre | `ReloadHistoryLimit` | `all` | Flux rechargés depuis le journal CSV existant au démarrage ; `0` désactive le rechargement |
| `--no-tui` | aucune | `EnableDashboard` | tableau de bord actif | Tourner sans le tableau de bord |
| `--no-dns` | aucune | `ResolveRemoteHostNames` | DNS actif | Aucune résolution de nom : ni DNS inverse, ni RDAP, ni alias |
| `--no-rdap` | aucune | `ResolveIpOwners` | RDAP actif | Aucune requête RDAP vers les registres Internet ; le DNS inverse reste actif |
| `--dns-timeout-ms` | millisecondes, au moins 1 | `DnsResolutionTimeoutMs` | `3000` | Délai d'attente d'une réponse DNS inverse |
| `--exclude-remote-ips` | adresses ou CIDR | `ExcludedRemoteIps` | aucune | Adresses distantes non enregistrées |
| `--alias` | `ip=host` ou `ip:host`, répétable | `CustomHostAliases` | aucun | Nom d'hôte utilisé à la place du DNS inverse |
| `--mask-server` | `<name>` | `MaskServerName` | aucun | Nom de serveur affiché par le tableau de bord |
| `--mask-ip` | `<address>` | `MaskLocalIp` | aucune | Adresse locale affichée par le tableau de bord |
| `--obfuscate` | aucune | aucune | inactif | Affiche `HOMELAB-01` et `192.168.1.10`, sauf si `--mask-server` ou `--mask-ip` est donné |

Familles lues : filtres de flux et rendu de la carte. Quand `--html` (ou `OutputHtmlMapPath`) est renseigné, la carte HTML est réécrite après chaque balayage ; la touche `M` l'écrit à la demande, avec le diagramme Draw.io quand `--drawio` (ou `OutputDrawioPath`) est renseigné.

Le tableau de bord n'est pas affiché quand l'entrée ou la sortie du processus est redirigée, même sans `--no-tui`.

Un nom de colonne invalide dans `--columns` ou `CsvColumns` arrête la commande avec `Column configuration error: …` et le code de sortie 1.

## map

Construit le graphe topologique à partir des journaux CSV et écrit les sorties demandées.

```text
PortAtlas map [-i <path>]... [--html <path>] [--drawio <path>] [options]
```

Sorties :

- `--html` écrit la carte HTML, `--drawio` le diagramme Draw.io ; les deux peuvent être donnés.
- Sans `--html` ni `--drawio`, la commande écrit la carte HTML dans `OutputHtmlMapPath` (ou `netflow_topology.html` si la clé est vide), et le diagramme Draw.io quand `OutputDrawioPath` est renseigné.

Familles lues : entrées (`.csv`), filtres de flux, rendu de la carte.

## export rules

Consolide les journaux en une matrice d'une ligne par source, destination, port, protocole et processus.

```text
PortAtlas export rules [-i <path>]... [-o <path>] [options]
```

| Option | Valeur | Défaut | Effet |
| :--- | :--- | :--- | :--- |
| `-o`, `--output` | `<path>` | `firewall_rules.csv` | Fichier de la matrice ([colonnes](output-formats.fr.md#matrice-de-règles-de-pare-feu)) |
| `--min-observations` | nombre, au moins 1 | `1` | Garder les flux observés au moins ce nombre de fois |
| `--active-within-days` | nombre, au moins 1 | sans limite | Garder les flux vus pendant les N derniers jours |
| `--zone` | `all`, `internal`, `public` | `all` | Zone de destination à garder |
| `--pairs-output` | `<chemin>` | non écrite | Écrit aussi la matrice par paire, une ligne par source et destination ([colonnes](output-formats.fr.md#matrice-par-paire)) ; mêmes jetons que `--output` |

Familles lues : entrées (`.csv`, `.ndjson`, `.jsonl`) et filtres de flux, avec deux différences :

- les ports de destination éphémères (49152 et au-delà) sont retirés sauf avec `--include-ephemeral-ports` ; la clé `MapExcludeEphemeralPorts` n'est pas lue ;
- les sockets en écoute ne sont exportés qu'avec `--listeners-only` (ou `MapListenersOnly`), sous forme de règles entrantes depuis n'importe quelle source (`0.0.0.0`, hôte `Any`).

Une destination est `Internal` si c'est une adresse de bouclage, une adresse d'une plage privée, ou `0.0.0.0` / `::` ; sinon elle est `Public`.

## export ndjson

Convertit des journaux CSV en un seul fichier NDJSON, un objet JSON par flux.

```text
PortAtlas export ndjson [-i <path>]... [-o <path>]
```

| Option | Valeur | Défaut | Effet |
| :--- | :--- | :--- | :--- |
| `-o`, `--output` | `<path>` | `netflow_audit.ndjson` | Fichier NDJSON ([champs](output-formats.fr.md#ndjson)) |

Famille lue : entrées (`.csv`). Les fichiers sont lus l'un après l'autre et seul le fichier en cours est gardé en mémoire.

## resolve

Fusionne des caches DNS, résout les adresses encore sans nom et écrit des copies enrichies des journaux.

```text
PortAtlas resolve [-i <path>]... [--cache <path>]... [options]
```

| Option | Valeur | Défaut | Effet |
| :--- | :--- | :--- | :--- |
| `--cache` | `<path>`, répétable | `DnsCacheFilePath` | Cache DNS à fusionner : un fichier, un dossier (ses fichiers `.json`) ou un motif |
| `--output-cache` | `<path>` | `DnsCacheFilePath` | Cache fusionné à écrire |
| `--in-place` | aucune | inactif | Remplacer les journaux au lieu d'écrire `<name>_enriched.<ext>` |
| `--force-resolve` | aucune | inactif | Remplacer les noms d'hôtes déjà présents dans les journaux |
| `--rdap-delay-ms` | millisecondes, au moins 0 | `200` | Délai entre deux requêtes RDAP |
| `--cache-only` | aucune | inactif | N'envoyer aucune requête DNS ni RDAP ; n'utiliser que les caches fusionnés |
| `--dns-timeout-ms` | millisecondes, au moins 1 | `DnsResolutionTimeoutMs` | Délai d'attente d'une réponse DNS inverse |

Famille lue : entrées (`.csv`, `.ndjson`, `.jsonl`). Quand deux caches donnent des noms différents pour une même adresse, l'entrée la plus récente est gardée et le conflit est affiché.

## lookup

Affiche un diagnostic en six étapes de la résolution d'une adresse : classification, cache local, réglages DNS du système, requête DNS inverse, requête RDAP, entrée de cache résultante.

```text
PortAtlas lookup <ip> [--dns-timeout-ms <milliseconds>]
```

`<ip>` doit être une adresse IPv4 ou IPv6. La commande lit `ExcludedRemoteIps`, `DnsCacheFilePath` et `DnsResolutionTimeoutMs` dans la configuration. Elle envoie la requête DNS inverse avec le même délai que `audit`, si bien qu'un `SUCCESS` ici signifie que l'audit nomme aussi l'adresse, et la requête RDAP pour les adresses publiques uniquement ; `ResolveIpOwners` et `--no-rdap` ne s'y appliquent pas.

## config init

Écrit un fichier de configuration contenant la valeur par défaut de chaque clé, avec les 20 colonnes CSV listées.

```text
PortAtlas config init [<path>]
```

`<path>` est un fichier ou un dossier ; par défaut `portatlas.json` dans le dossier de travail. Un fichier existant est remplacé.

## Matrice des options

| Option | `audit` | `map` | `export rules` | `export ndjson` | `resolve` | `lookup` | `config init` |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| `--config` | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| `-i`, `--input` | | ✔ | ✔ | ✔ | ✔ | | |
| `-o`, `--output` | ✔ | | ✔ | ✔ | | | |
| Filtres de flux | ✔ | ✔ | ✔ | | | | |
| Rendu de la carte, `--html`, `--drawio` | ✔ | ✔ | | | | | |
| `--ndjson`, `--interval`, `--columns`, `--reload-history` | ✔ | | | | | | |
| `--no-tui`, `--no-dns`, `--no-rdap` | ✔ | | | | | | |
| `--dns-timeout-ms` | ✔ | | | | ✔ | ✔ | |
| `--exclude-remote-ips`, `--alias` | ✔ | | | | | | |
| `--mask-server`, `--mask-ip`, `--obfuscate` | ✔ | | | | | | |
| `--min-observations`, `--active-within-days`, `--zone`, `--pairs-output` | | | ✔ | | | | |
| `--cache`, `--output-cache`, `--in-place`, `--force-resolve`, `--rdap-delay-ms`, `--cache-only` | | | | | ✔ | | |

`config init` accepte `--config` parce que l'option est globale, mais ne la lit pas.

## Codes de sortie

| Code | Signification |
| :--- | :--- |
| `0` | Succès, ou aide affichée |
| `1` | Ligne de commande invalide, aucun journal trouvé, nom de colonne CSV invalide, ou fichier de configuration avec `"IsolatedMode": true` |
