> **Language / Langue**: [English](configuration.md) · **Français**

# Référence du fichier de configuration

NetFlowAudit lit un fichier JSON : celui donné par `--config`, sinon `netflowaudit.json` dans le dossier de travail, sinon `netflowaudit.json` à côté de l'exécutable. Une clé absente prend sa valeur par défaut ; une clé inconnue est ignorée. Un fichier illisible ou mal formé affiche un avertissement et les valeurs par défaut s'appliquent.

`NetFlowAudit.Cli config init` écrit un fichier contenant chaque clé avec sa valeur par défaut. Les options de la ligne de commande passent avant le fichier ([référence de la CLI](cli.fr.md#origine-des-valeurs)).

Les chemins acceptent les [jetons de nom de fichier](cli.fr.md#jetons-de-nom-de-fichier) `{ServerName}`, `{HostName}`, `{Date}` et `{Timestamp}`.

## Audit

| Clé | Type | Défaut | Effet | Option |
| :--- | :--- | :--- | :--- | :--- |
| `ScanIntervalSeconds` | entier | `5` | Secondes entre deux balayages | `--interval` |
| `OutputCsvPath` | chemin | `netflow_audit.csv` | Journal d'audit CSV ; entrée par défaut des autres commandes | `-o` (`audit`) |
| `OutputNdjsonPath` | chemin | `""` | Journal NDJSON écrit en plus du CSV ; vide pour aucun | `--ndjson` |
| `CsvColumns` | liste de noms | `[]` | Colonnes CSV, dans l'ordre ; vide pour les 20 colonnes | `--columns` |
| `CsvDelimiter` | chaîne | `","` | Séparateur des champs CSV (premier caractère utilisé) | aucune |
| `ReloadHistoryLimit` | entier ou `null` | `null` | Flux rechargés depuis le journal CSV au démarrage ; `null` pour tous, `0` pour aucun | `--reload-history` |
| `EnableDashboard` | booléen | `true` | Tableau de bord du terminal | `--no-tui` |
| `MaskServerName` | chaîne | `""` | Nom de serveur affiché par le tableau de bord | `--mask-server` |
| `MaskLocalIp` | chaîne | `""` | Adresse locale affichée par le tableau de bord | `--mask-ip` |

## Filtres d'enregistrement

Ces clés décident quels sockets `audit` enregistre. Le tableau de bord en complète certaines quand vous excluez une cellule ([référence du tableau de bord](dashboard.fr.md#exclure-depuis-le-tableau-de-bord)).

| Clé | Type | Défaut | Effet | Option |
| :--- | :--- | :--- | :--- | :--- |
| `ExcludeLoopbackFlows` | booléen | `true` | Ignorer le trafic qui reste sur la machine : sockets liés à une adresse de bouclage (écoutes comprises), flux vers une adresse de bouclage, et flux entre deux adresses de la machine. Les écoutes sur les autres adresses sont conservées. | aucune |
| `ExcludedLocalPorts` | liste d'entiers | `[]` | Ports locaux non enregistrés | aucune |
| `ExcludedRemotePorts` | liste d'entiers | `[]` | Ports distants non enregistrés | aucune |
| `ExcludedRemoteIps` | liste d'adresses ou CIDR | `[]` | Adresses distantes non enregistrées | `--exclude-remote-ips` |
| `ExcludedPids` | liste d'entiers | `[0, 4]` | Identifiants de processus non enregistrés | aucune |
| `ExcludedExactNames` | liste de noms | `["System", "Idle"]` | Noms de processus non enregistrés (nom entier, sans tenir compte de la casse) | aucune |
| `ExcludedPrefixes` | liste de chaînes | `[]` | Noms de processus commençant par l'une de ces valeurs (sans tenir compte de la casse) | aucune |
| `ExcludedSuffixes` | liste de chaînes | `[]` | Noms de processus se terminant par l'une de ces valeurs (sans tenir compte de la casse) | aucune |
| `ExcludedContains` | liste de chaînes | `[]` | Noms de processus contenant l'une de ces valeurs | aucune |
| `ExcludeSystemAccounts` | booléen | `true` | Ignorer les processus des comptes système (voir la note ci-dessous) | aucune |

`ExcludeSystemAccounts` écarte les processus dont le compte contient `NT AUTHORITY` ou vaut `SYSTEM` ou `root`. ProcessForge 1.0.2, qui lit les informations des processus, ne renvoie pas encore le compte d'un processus : ce filtre ne s'applique donc qu'aux identifiants de processus 0 et inférieurs, déjà couverts par `ExcludedPids`.

## Résolution de noms

| Clé | Type | Défaut | Effet | Option |
| :--- | :--- | :--- | :--- | :--- |
| `ResolveRemoteHostNames` | booléen | `true` | Résolution de nom des adresses distantes ; `false` désactive aussi RDAP et les alias | `--no-dns` |
| `ResolveIpOwners` | booléen | `true` | Requête RDAP sur le propriétaire des adresses publiques, utilisé comme nom faute de nom DNS | `--no-rdap` |
| `DnsCacheFilePath` | chemin | `netflow_dns_cache.json` | Cache DNS persistant ; cache par défaut de `resolve` et `lookup` | `--cache`, `--output-cache` (`resolve`) |
| `DnsExcludedNetworks` | liste d'adresses ou CIDR | `["0.0.0.0", "255.255.255.255", "169.254.0.0/16", "224.0.0.0/4", "fe80::/10", "ff00::/8"]` | Adresses jamais résolues | aucune |
| `CustomHostAliases` | objet `{ "ip": "nom" }` | `{}` | Noms utilisés à la place du DNS inverse | `--alias` |

## Carte topologique et matrice de règles

Ces clés sont lues par la carte de `audit`, par `map`, et pour les filtres par `export rules`.

| Clé | Type | Défaut | Option |
| :--- | :--- | :--- | :--- |
| `OutputHtmlMapPath` | chemin | `""` | `--html` |
| `OutputDrawioPath` | chemin | `""` | `--drawio` |
| `MapIncludedPorts` | liste de ports ou plages | `[]` | `--include-ports` |
| `MapExcludedPorts` | liste de ports ou plages | `[]` | `--exclude-ports` |
| `MapIncludedHosts` | liste de motifs | `[]` | `--include-hosts` |
| `MapExcludedHosts` | liste de motifs | `[]` | `--exclude-hosts` |
| `MapIncludedIps` | liste d'adresses ou CIDR | `[]` | `--include-ips` |
| `MapExcludedIps` | liste d'adresses ou CIDR | `[]` | `--exclude-ips` |
| `MapIncludedProcesses` | liste de noms | `[]` | `--include-processes` |
| `MapExcludedProcesses` | liste de noms | `[]` | `--exclude-processes` |
| `MapDirection` | `all`, `in`, `out` | `all` | `--direction` |
| `MapListenersOnly` | booléen | `false` | `--listeners-only` |
| `MapExcludeEphemeralPorts` | booléen | `true` | `--include-ephemeral-ports` (carte uniquement) |
| `MapLayoutMode` | voir `--layout` | `layered` | `--layout` |
| `MapSortMethod` | voir `--sort` | `barycentric` | `--sort` |
| `MapRoutingMode` | voir `--routing` | `bezier` | `--routing` |
| `MapSpacingProfile` | voir `--spacing` | `balanced` | `--spacing` |
| `DrawioEdgeMode` | `adaptive`, `direction`, `none` | `adaptive` | `--drawio-edges` |
| `DrawioTheme` | `dark`, `light` | `dark` | `--drawio-theme` |

La carte topologique (pas la matrice de règles) retire aussi les ports de `ExcludedLocalPorts` et `ExcludedRemotePorts` et les adresses de `ExcludedRemoteIps`.

Les valeurs de ces clés ne sont pas vérifiées à la lecture du fichier : une disposition, un tri, un routage, un espacement, un mode de flèches ou un thème inconnu prend la valeur par défaut. Les options de la ligne de commande refusent les valeurs inconnues.

## Clé supprimée

| Clé | Supprimée en | Remplacement |
| :--- | :--- | :--- |
| `IsolatedMode` | 2.0.0 | `"ResolveIpOwners": false`. Un fichier avec `"IsolatedMode": true` arrête toutes les commandes avec une erreur ; `false` affiche un avertissement. |
