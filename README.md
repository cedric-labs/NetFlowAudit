# NetFlowAudit - Agent d'Audit Continu des Flux Réseau

<p align="center">
  <img src="assets/netflowaudit-icon.png" alt="NetFlowAudit Logo" width="200" />
</p>

**NetFlowAudit** est un agent d'audit réseau multiplateforme (Windows et Linux) conçu pour fonctionner en continu sous la forme d'un service ou d'un daemon d'arrière-plan. Il inspecte l'intégralité du trafic réseau entrant et sortant (TCP et UDP), identifie les processus locaux responsables des communications et journalise ces informations dans un fichier CSV horodaté et dédupliqué.

---

## 1. Caractéristiques Principales

- **Zéro pilote de capture tiers** : N'utilise ni Npcap, ni WinPcap, ni libpcap. 
  - **Sur Windows** : interrogation de l'API native IP Helper Win32 (`iphlpapi.dll`).
  - **Sur Linux / WSL** : lecture directe du pseudo-système de fichiers `/proc/net/{tcp,tcp6,udp,udp6}` et corrélation des descripteurs de fichiers `/proc/<pid>/fd`.
- **Détection des cibles sous-jacentes (Wrappers)** : Ne se contente pas d'identifier le binaire lanceur (`java.exe`, `python`, `cmd.exe`, `powershell.exe`, `bash`), mais extrait la ligne de commande complète et isole la charge utile réelle (`.jar`, `.py`, `.bat`, `.cmd`, `.ps1`, `.sh`).
- **Résolution DNS inverse asynchrone & non-bloquante** :
  - Résout automatiquement les adresses IP distantes en noms d'hôtes (FQDN) à la détection de nouveaux flux.
  - Doté d'un cache mémoire haute performance avec politique de cache négatif (évite de requêter le serveur DNS en boucle en cas d'absence d'enregistrement PTR).
  - Affichage direct du nom d'hôte sur le tableau de bord console et consigne dans une colonne dédiée du fichier CSV.
- **Déduplication intelligente** :
  - Pour les flux sortants, neutralise l'impact des ports clients éphémères afin de ne pas multiplier les lignes identiques pour une même destination.
  - Met à jour le compteur d'observations (`ObservationCount`) et l'horodatage de dernière vue (`LastSeenUtc`).
  - Reprise automatique : recharge le fichier CSV existant au démarrage pour conserver l'historique sans doublon.
- **Filtrage avancé des processus & du réseau** : 
  - Exclusion de processus par identifiant (PID), nom exact, préfixe, suffixe, sous-chaîne ou comptes système (`SYSTEM`, `root`).
  - Exclusion automatique des flux internes intra-machine (`LocalIp == RemoteIp`, adresses loopback `127.0.0.0/8`, `::1` et IP locales de l'hôte).
  - Exclusion ciblée de ports locaux (`ExcludedLocalPorts`), de ports distants (`ExcludedRemotePorts`) ou d'adresses IP / plages CIDR distantes (`ExcludedRemoteIps`).
- **Colonnes d'export CSV 100 % paramétrables** :
  - Choix libre des colonnes à inclure et de leur ordre d'apparition parmi 20 colonnes disponibles, via le fichier de configuration JSON ou l'option `--columns`.
  - Délimiteur personnalisable (virgule, point-virgule, tabulation).
- **Tableau de bord interactif responsive (style `btop`)** :
  - Interface plein écran ANSI s'adaptant dynamiquement à la largeur et à la hauteur de la fenêtre du terminal.
  - Redimensionnement automatique instantané (scrutation 100 ms) sans distorsion ni décalage de séparateurs.
  - Répartition instantanée des protocoles (TCP/UDP) et directions (écoute, entrant, sortant).
  - Top des processus locaux les plus actifs en nombre de sockets.
  - Défilement en temps réel des dernières découvertes de flux réseau horodatées.
  - Écran de garde automatique lorsque le terminal est trop petit (< 100 cols x 16 lignes).
  - Désactivation automatique ou manuelle via `--no-tui` / `--daemon` lors d'une exécution en service ou redirection de flux.
- **Multiplateforme & Autonome** : Fonctionne sous Windows et Linux (x64 / ARM64), diffusable sous forme de binaire autonome (*self-contained single-file*) sans prérequis d'installation du SDK/Runtime .NET.

---

## 2. Guide de Démarrage Rapide

### 2.1. Options de ligne de commande

NetFlowAudit propose une aide intégrée et plusieurs paramètres en ligne de commande :

```
NetFlowAudit.Cli [OPTIONS]

Options :
  -h, --help                 Affiche l'aide complète (avec le catalogue de toutes les colonnes CSV) et quitte.
  --no-tui, --daemon         Désactive le tableau de bord interactif (recommandé en service d'arrière-plan).
  --config <chemin>          Spécifie un fichier de configuration alternatif (par défaut : netflowaudit.json).
  --output <chemin>          Spécifie le fichier CSV de sortie (par défaut : netflow_audit.csv).
  --interval <secondes>      Intervalle de scan en secondes (par défaut : 5).
  --columns <col1,col2,...>           Liste personnalisée ordonnée des colonnes CSV à exporter.
  --resolve-dns <true|false>          Active ou désactive la résolution DNS inverse des IP distantes.
  --exclude-remote-ips <ip1,cidr2,..> Liste d'adresses IP ou plages CIDR distantes à exclure de l'audit.
  --map <chemin.html>                 Génère une carte topologique HTML5 autonome hors-ligne (TopologyForge).
  --map-only                          Génère immédiatement la carte HTML à partir du CSV (--output) et quitte.
  --include-ports <p1,p2-p3,...>      Inclut uniquement ces ports ou plages de service (ex: 80,443,1433,8000-8080).
  --exclude-ports <p1,p2-p3,...>      Exclut ces ports ou plages de service de la carte (ex: 53,123,135-139).
  --include-hosts <m1,m2,...>         Inclut uniquement les serveurs correspondant aux motifs (ex: SRV-*,*SQL*).
  --exclude-hosts <m1,m2,...>         Exclut les serveurs correspondant aux motifs (ex: *.local,SRV-MON-*).
  --include-ips <ip1,cidr2,...>       Inclut uniquement ces adresses IP ou sous-réseaux CIDR (ex: 10.0.0.0/8).
  --exclude-ips <ip1,cidr2,...>       Exclut ces adresses IP ou sous-réseaux CIDR (ex: 169.254.0.0/16).
  --include-processes <proc1,...>     Inclut uniquement ces processus ou cibles applicatives.
  --exclude-processes <proc1,...>     Exclut ces processus ou cibles applicatives.
  --map-sort <barycentric|degree|alpha|subnet>
                                      Méthode de tri des serveurs (anti-croisement Sugiyama par défaut).
  --map-routing <bezier|orthogonal|straight>
                                      Mode de routage des flèches avec contournement des boîtes serveurs.
  --map-spacing <compact|balanced|spacious>
                                      Profil d'écartement initial entre les serveurs et les flèches.
  --listeners-only                    Cartographie uniquement les ports en écoute passive.
  --include-ephemeral-ports           Désactive l'exclusion par défaut des ports éphémères (49152-65535).
```

### 2.2. Exécution sous Windows

#### Option A : Depuis les sources .NET
```powershell
dotnet run --project NetFlowAudit.Cli/NetFlowAudit.Cli.csproj
```

#### Option B : Publication en exécutable autonome
```powershell
dotnet publish NetFlowAudit.Cli/NetFlowAudit.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish/win-x64
.\bin\publish\win-x64\NetFlowAudit.Cli.exe
```

---

### 2.3. Exécution sous Linux / WSL

#### Publication du binaire autonome Linux
```powershell
dotnet publish NetFlowAudit.Cli/NetFlowAudit.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish/linux-x64
```

#### Lancement sous Linux / WSL
Sur Linux, l'inspection des descripteurs de sockets `/proc/<pid>/fd` appartenant à d'autres utilisateurs ou démons système nécessite les privilèges root ou la capacité `CAP_NET_ADMIN` :
```bash
chmod +x ./NetFlowAudit.Cli
sudo ./NetFlowAudit.Cli
```

---

### 2.4. Arrêt de l'agent

L'arrêt coopératif assure la fermeture et la synchronisation propre du fichier CSV sur le disque :
- **En mode interactif (console)** : Appuyez sur la touche `q` ou `Ctrl+C`.
- **En mode service / arrière-plan (Linux)** : Envoyez un signal POSIX standard :
  ```bash
  kill -TERM <PID>
  # ou
  kill -INT <PID>
  ```

---

## 3. Configuration (`netflowaudit.json`)

Au démarrage, l'application recherche un fichier optionnel nommé `netflowaudit.json` dans le répertoire d'exécution (ou spécifié via `--config`). Si ce fichier est absent, les valeurs par défaut sont appliquées.

### Exemple complet de fichier `netflowaudit.json` :

```json
{
  "ScanIntervalSeconds": 5,
  "OutputCsvPath": "netflow_audit.csv",
  "OutputHtmlMapPath": "netflow_topology.html",
  "MapExcludeEphemeralPorts": true,
  "MapListenersOnly": false,
  "MapIncludedPorts": [],
  "MapExcludedPorts": [ "53", "123" ],
  "MapIncludedHosts": [],
  "MapExcludedHosts": [],
  "MapIncludedIps": [],
  "MapExcludedIps": [],
  "MapIncludedProcesses": [],
  "MapExcludedProcesses": [],
  "MapSortMethod": "barycentric",
  "MapRoutingMode": "bezier",
  "MapSpacingProfile": "balanced",
  "EnableDashboard": true,
  "ResolveRemoteHostNames": true,
  "DnsCacheFilePath": "netflow_dns_cache.json",
  "DnsExcludedNetworks": [
    "0.0.0.0",
    "255.255.255.255",
    "169.254.0.0/16",
    "224.0.0.0/4",
    "fe80::/10",
    "ff00::/8"
  ],
  "ExcludeLoopbackFlows": true,
  "ExcludedLocalPorts": [ 22, 3389 ],
  "ExcludedRemotePorts": [ 53 ],
  "ExcludedRemoteIps": [ "192.168.1.50", "10.0.0.0/8" ],
  "ExcludedPids": [ 0, 4 ],
  "ExcludedExactNames": [ "System", "Idle" ],
  "ExcludedPrefixes": [ "svchost" ],
  "ExcludedSuffixes": [],
  "ExcludedContains": [],
  "ExcludeSystemAccounts": false,
  "CsvDelimiter": ",",
  "CsvColumns": [
    "FirstSeenUtc",
    "LastSeenUtc",
    "ObservationCount",
    "ServerName",
    "ServerOsVersion",
    "ServerPrimaryIp",
    "Protocol",
    "Direction",
    "TcpState",
    "LocalIp",
    "LocalPort",
    "RemoteIp",
    "RemotePort",
    "RemoteHostName",
    "ProcessId",
    "ProcessName",
    "ExecutablePath",
    "CommandLine",
    "TargetExecutable",
    "UserName"
  ]
}
```

### Description des paramètres :

| Paramètre | Type | Valeur par défaut | Description |
| :--- | :--- | :--- | :--- |
| `ScanIntervalSeconds` | Entier | `5` | Fréquence en secondes entre deux balayages de la table des sockets actives. |
| `OutputCsvPath` | Chaîne | `"netflow_audit.csv"` | Chemin relatif ou absolu du fichier CSV de sortie. |
| `OutputHtmlMapPath` | Chaîne | `""` | Chemin du fichier HTML de cartographie topologique (vide si désactivé par défaut). |
| `MapSortMethod` | Chaîne | `"barycentric"` | Méthode de tri initial des serveurs sur la carte (`"barycentric"`, `"degree"`, `"alpha"`, `"subnet"`). |
| `MapRoutingMode` | Chaîne | `"bezier"` | Mode de routage initial des flèches (`"bezier"`, `"orthogonal"`, `"straight"`). |
| `MapSpacingProfile` | Chaîne | `"balanced"` | Profil d'écartement initial entre serveurs et flèches (`"compact"`, `"balanced"`, `"spacious"`). |
| `EnableDashboard` | Booléen | `true` | Active le tableau de bord interactif dans le terminal en mode interactif. |
| `ResolveRemoteHostNames` | Booléen | `true` | Active la résolution DNS inverse asynchrone non-bloquante des IP distantes. |
| `DnsCacheFilePath` | Chaîne | `"netflow_dns_cache.json"` | Chemin du fichier JSON de persistance du cache DNS inverse entre les redémarrages. |
| `DnsExcludedNetworks` | Tableau de chaînes | `["0.0.0.0", "255.255.255.255", "169.254.0.0/16", ...]` | Plages réseaux (CIDR) ou adresses IP pour lesquelles la résolution DNS est ignorée d'office. |
| `ExcludeLoopbackFlows` | Booléen | `true` | Ignore les flux internes lorsque `LocalIp == RemoteIp`, adresses loopback (`127.0.0.0/8`, `::1`) ou interfaces locales de la machine. |
| `ExcludedLocalPorts` | Tableau d'entiers | `[]` | Liste des ports d'écoute ou locaux à ignorer (ex: `[22, 3389]`). |
| `ExcludedRemotePorts` | Tableau d'entiers | `[]` | Liste des ports distants cibles à ignorer (ex: `[53, 443]`). |
| `ExcludedRemoteIps` | Tableau de chaînes | `[]` | Liste des adresses IP ou sous-réseaux distants (CIDR) à ignorer (ex: `["192.168.1.50", "10.0.0.0/8"]`). |
| `ExcludedPids` | Tableau d'entiers | `[0, 4]` | Liste des identifiants de processus (PID) à ignorer systématiquement. |
| `ExcludedExactNames` | Tableau de chaînes | `["System", "Idle"]` | Noms exacts de processus exclus (insensible à la casse). |
| `ExcludedPrefixes` | Tableau de chaînes | `[]` | Préfixes de noms de processus à exclure. |
| `ExcludedSuffixes` | Tableau de chaînes | `[]` | Suffixes de noms de processus à exclure. |
| `ExcludedContains` | Tableau de chaînes | `[]` | Sous-chaînes contenues dans le nom du processus à exclure. |
| `ExcludeSystemAccounts` | Booléen | `true` | Exclut automatiquement les processus exécutés sous des comptes système (`SYSTEM`, `LOCAL SERVICE`, `NETWORK SERVICE`, `root`). |
| `CsvDelimiter` | Chaîne | `","` | Caractère délimiteur de champs CSV (`","` ou `";"`). |
| `CsvColumns` | Tableau de chaînes | `[]` | Liste ordonnée des colonnes à exporter. Si vide, l'ensemble des 20 colonnes est exporté. |

---

## 4. Structure du Journal d'Audit CSV & Catalogue des Colonnes

Le fichier généré est encodé en UTF-8 conforme RFC 4180 avec séparateur paramétrable (virgule `,` par défaut). Il est partagé en lecture/écriture (`FileShare.ReadWrite`), ce qui permet de le consulter ou de l'importer en temps réel dans un outil tiers (SIEM, Excel, PowerBI) sans bloquer l'agent.

### Catalogue exhaustif des 20 colonnes disponibles :

| N° | Nom technique | Catégorie | Description détaillée | Exemple |
| :---: | :--- | :--- | :--- | :--- |
| 1 | `FirstSeenUtc` | Horodatage | Date et heure UTC de la première détection du flux réseau (format ISO 8601). | `2026-10-23T07:15:00.0000000Z` |
| 2 | `LastSeenUtc` | Horodatage | Date et heure UTC du dernier balayage ayant observé ce flux actif. | `2026-10-23T07:20:00.0000000Z` |
| 3 | `ObservationCount` | Métriques | Nombre cumulé de balayages ayant constaté la présence active de ce flux. | `42` |
| 4 | `ServerName` | Serveur | Nom d'hôte de la machine locale auditée. | `SRV-PROD-APP01` |
| 5 | `ServerOsVersion` | Serveur | Version détaillée et description du système d'exploitation hôte. | `Microsoft Windows 10.0.26100` |
| 6 | `ServerPrimaryIp` | Serveur | Adresse IP principale routable attribuée à la machine hôte. | `192.168.1.50` |
| 7 | `Protocol` | Réseau | Protocole de transport réseau (`Tcp` ou `Udp`). | `Tcp` |
| 8 | `Direction` | Réseau | Direction déduite du flux (`Listening`, `Inbound`, `Outbound`). | `Outbound` |
| 9 | `TcpState` | Réseau | État TCP (`Listen`, `Established`, `TimeWait`, `CloseWait`, `SynSent`, etc.). | `Established` |
| 10 | `LocalIp` | Réseau | Adresse IP locale associée à la socket. | `192.168.1.50` |
| 11 | `LocalPort` | Réseau | Numéro de port local associé à la socket. | `52431` |
| 12 | `RemoteIp` | Réseau | Adresse IP distante (ou `0.0.0.0` / `::` si socket en écoute). | `140.82.121.4` |
| 13 | `RemotePort` | Réseau | Numéro de port distant (ou `0` si socket en écoute). | `443` |
| 14 | `RemoteHostName` | Réseau | Nom d'hôte distant résolu par DNS inverse (ou vide si non résolu). | `lb-140-82-121-4-iad.github.com` |
| 15 | `ProcessId` | Processus | Identifiant numérique du processus propriétaire (PID). | `4812` |
| 16 | `ProcessName` | Processus | Nom court de l'exécutable (`java`, `python3`, `node`, `nginx`, `git`). | `git` |
| 17 | `ExecutablePath` | Processus | Chemin d'accès absolu complet au binaire exécutable sur le disque. | `C:\Program Files\Git\cmd\git.exe` |
| 18 | `CommandLine` | Processus | Ligne de commande complète ayant lancé le processus avec tous ses arguments. | `git fetch --all` |
| 19 | `TargetExecutable` | Processus | Cible applicative réelle sous-jacente extraite de la ligne de commande (script, `.jar`). | `app.jar` |
| 20 | `UserName` | Processus | Compte utilisateur propriétaire du processus (si accessible). | `NT AUTHORITY\SYSTEM` |

---

## 5. Déploiement en Service d'Arrière-Plan

### 5.1. Déploiement sous Linux avec systemd

1. Copier le binaire autonome dans `/usr/local/bin/` :
   ```bash
   sudo cp bin/publish/linux-x64/NetFlowAudit.Cli /usr/local/bin/netflowaudit
   sudo chmod +x /usr/local/bin/netflowaudit
   ```

2. Créer le fichier d'unité `/etc/systemd/system/netflowaudit.service` :
   ```ini
   [Unit]
   Description=NetFlowAudit - Agent d'audit des flux reseau
   After=network.target

   [Service]
   Type=simple
   WorkingDirectory=/var/log/netflowaudit
   ExecStart=/usr/local/bin/netflowaudit --no-tui
   Restart=always
   RestartSec=10
   KillSignal=SIGTERM

   [Install]
   WantedBy=multi-user.target
   ```

3. Créer le dossier de journalisation, activer et démarrer le service :
   ```bash
   sudo mkdir -p /var/log/netflowaudit
   sudo systemctl daemon-reload
   sudo systemctl enable --now netflowaudit.service
   ```

4. Suivre l'activité :
   ```bash
   sudo systemctl status netflowaudit.service
   tail -f /var/log/netflowaudit/netflow_audit.csv
   ```

---

### 5.2. Déploiement sous Windows avec NSSM

Le binaire `NetFlowAudit.Cli.exe` peut être enregistré comme service Windows à l'aide de l'utilitaire standard `NSSM` (Non-Sucking Service Manager) :
```cmd
nssm install NetFlowAudit "C:\NetFlowAudit\NetFlowAudit.Cli.exe" --no-tui
nssm set NetFlowAudit AppDirectory "C:\NetFlowAudit"
nssm start NetFlowAudit
```

---

## 6. Architecture & Dépendances Partagées

Le projet repose sur 4 bibliothèques partagées modulaires conformes aux règles de Clean Architecture et de SonarQube Quality Gate :

- [**FileForge.Core**](file:///D:/Dev/Framework/FileForge/FRAMEWORK.md) : Moteur d'I/O streaming thread-safe, écrivain délimité RFC 4180 avec verrouillage partagé non bloquant.
- [**ProcessForge**](file:///D:/Dev/Framework/ProcessForge/FRAMEWORK.md) : Résolution des processus multiplateforme, analyseurs de lignes de commande et moteur de règles d'exclusion.
- [**SocketForge**](file:///D:/Dev/Framework/SocketForge/FRAMEWORK.md) : Scanneur de sockets multiplateforme (Win32 IP Helper & Linux `/proc/net`).
- [**TopologyForge**](file:///D:/Dev/Framework/TopologyForge/FRAMEWORK.md) : Moteur de cartographie topologique HTML5/SVG 100 % hors-ligne, filtrage d'inclusion/exclusion, tri anti-croisement Sugiyama, routage avec contournement des boîtes serveurs et pointes directionnelles `IN`/`OUT`.

Pour lancer la chaîne complète de validation qualité (formatage, compilation Release, couverture de code et SonarQube) :
```powershell
.\verify.ps1
```
