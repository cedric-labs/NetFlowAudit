> **Langue / Language**: **[ 🇫🇷 Français ]** · [ 🇬🇧 English (user-guide.en.md)](user-guide.en.md)

# NetFlowAudit - Guide d'Utilisation & Exploitation en Production

## 1. Installation & Distribution

NetFlowAudit est distribué sous forme de binaires autonomes (*single-file*), précompilés et auto-contenus pour architectures **x64 Windows** et **x64 Linux**. Le runtime .NET 8 est directement intégré dans l'exécutable ; aucune installation de runtime ou de SDK préalable n'est nécessaire sur les serveurs cibles.

### 1.1. Téléchargement des Binaires Précompilés
Les packages et exécutables autonomes sont publiés sur les Releases GitHub du projet :
- **Releases GitHub** : [NetFlowAudit Releases](https://github.com/curvecoder/NetFlowAudit/releases)

| Système / Architecture | Artefact | Description |
|---|---|---|
| **Windows x64** | `NetFlowAudit-1.6.0-preview.2-win-x64.zip` | Archive contenant `NetFlowAudit.Cli.exe` et la configuration type `netflowaudit.json`. |
| **Windows x64 (Direct)** | `NetFlowAudit-1.6.0-preview.2-win-x64.exe` | Exécutable unique autonome sans décompression d'archive. |
| **Linux x64** | `NetFlowAudit-1.6.0-preview.2-linux-x64.tar.gz` | Archive tarball contenant le binaire ELF et le fichier de configuration de base. |
| **Linux x64 (Direct)** | `NetFlowAudit-1.6.0-preview.2-linux-x64` | Exécutable binaire ELF unique autonome. |

### 1.2. Compilation depuis les Sources
Pour compiler directement depuis le code source :
```bash
# Cloner le dépôt
git clone https://github.com/curvecoder/NetFlowAudit.git
cd NetFlowAudit

# Générer l'exécutable autonome pour Windows
dotnet publish NetFlowAudit.Cli/NetFlowAudit.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish/win-x64

# Générer l'exécutable autonome pour Linux
dotnet publish NetFlowAudit.Cli/NetFlowAudit.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish/linux-x64
```

---

## 2. Référentiel des Options de Ligne de Commande

### 2.1. Catalogue Complet des Options CLI

| Option | Alias / Raccourci | Valeur par défaut | Description |
|---|---|---|---|
| `--help` | `-h`, `-?` | — | Affiche l'aide en ligne de commande et quitte. |
| `--config <chemin>` | — | `netflowaudit.json` | Chemin vers le fichier de configuration JSON. |
| `--init-config [chemin]` | `--generate-config` | `netflowaudit.json` | Génère un fichier modèle de configuration JSON et quitte immédiatement. |
| `--daemon` | `--no-tui` | `false` | Exécute l'agent en tâche de fond sans tableau de bord interactif (adapté aux services Windows / systemd). |
| `--output <chemin>` | `-o` | `netflow_audit.csv` | Chemin du fichier CSV de sortie. Accepte les jetons dynamiques `{ServerName}`, `{HostName}`, `{Date}`, `{Timestamp}`. |
| `--output-ndjson <chemin>` | `--ndjson` | — | Chemin d'écriture continue du flux NDJSON (JSON Lines). |
| `--export-ndjson [chemin]` | — | `netflow_audit.ndjson` | Convertit un historique d'audit CSV existant au format NDJSON et quitte. |
| `--export-rules [chemin]` | `--firewall-matrix`, `--rules-csv` | `firewall_rules.csv` | Agrège l'historique des flux en une matrice 5-tuple normalisée pour règles de pare-feu et quitte. |
| `--min-observations <n>` | — | `1` | Filtre pour `--export-rules` : nombre minimum d'observations requis pour conserver une règle. |
| `--active-within-days <n>` | — | `null` (tous) | Filtre pour `--export-rules` : ne retient que les flux observés au cours des N derniers jours. |
| `--destination-zone <zone>` | — | `all` | Filtre pour `--export-rules` : filtre par zone de destination (`internal` ou `public`). |
| `--internal-only` | — | `false` | Filtre raccourci pour `--export-rules` : conserve uniquement les flux internes vers internes. |
| `--public-only` | — | `false` | Filtre raccourci pour `--export-rules` : conserve uniquement les flux vers les IPs publiques WAN. |
| `--exclude-ephemeral` | — | `false` | Filtre pour `--export-rules` : exclut les ports destination éphémères (>= 49152). |
| `--direction <dir>` | — | `all` | Filtre de direction pour la topologie et les règles : `inbound`, `outbound` ou `all`. |
| `--resolve-offline` | — | `false` | Consolide les caches DNS et enrichit les fichiers d'audit en environnement isolé, puis quitte. |
| `--in-place` | — | `false` | Utilisé avec `--resolve-offline` : remplace directement les fichiers d'audit d'origine au lieu de créer `*_enriched.csv`. |
| `--force-resolve` | — | `false` | Utilisé avec `--resolve-offline` : force la réévaluation des noms même si déjà présents dans les fichiers sources. |
| `--caches <chemins>` | `--cache` | — | Utilisé avec `--resolve-offline` : fichiers ou répertoires de caches DNS à fusionner. |
| `--output-cache <chemin>` | — | `netflow_dns_cache.json` | Chemin de destination pour le cache DNS maître consolidé. |
| `--test-ip <ip>` | `--lookup` | — | Exécute un diagnostic verbeux de résolution DNS et RDAP en 6 étapes pour une adresse IP et quitte. |
| `--interval <secondes>` | — | `5` | Intervalle en secondes entre deux analyses consécutives des tables de sockets. |
| `--columns <c1,c2,..>` | — | 16 cols standard | Liste ordonnée personnalisée des colonnes CSV à exporter. |
| `--resolve-dns <bool>` | — | `true` | Active ou désactive la résolution inverse DNS (PTR) pour les adresses IP distantes. |
| `--resolve-ip-owners <bool>` | — | `true` | Active ou désactive l'interrogation RDAP pour identifier l'organisation propriétaire du bloc IP. |
| `--isolated` | `--offline` | `false` | Désactive totalement les requêtes réseau sortantes (DNS PTR et RDAP). Émission de zéro paquet réseau. |
| `--exclude-remote-ips <ips>` | `--exclude-remote-ip` | — | Liste d'adresses IP distantes ou de sous-réseaux CIDR à exclure de l'audit. |
| `--alias <ip>=<nom>` | `--aliases` | — | Alias statique IP-vers-nom d'hôte prioritaire sur toute résolution DNS. |
| `--reload-history <n / all / 0>` | `--history-limit` | `all` | Nombre de lignes CSV historiques rechargées en mémoire au démarrage pour assurer la déduplication. |
| `--mask-server <nom>` | `--obfuscate-server` | — | Nom de serveur fictif affiché dans le terminal et les captures. |
| `--mask-ip <ip>` | `--obfuscate-ip` | — | Adresse IP fictive affichée dans le terminal et les captures. |
| `--obfuscate` | `--anonymize` | `false` | Raccourci : masque le nom du serveur en `SRV-AUDIT-01` et l'IP locale en `10.0.0.10`. |
| `--map-only` | — | `false` | Génère une carte topologique HTML / Draw.io depuis les fichiers CSV d'entrée et quitte immédiatement. |
| `--map <chemin>` | — | `netflow_topology.html` | Chemin du diagramme topologique interactif HTML5 généré. |
| `--export-drawio [chemin]` | `--drawio` | `netflow_topology.drawio` | Exporte un diagramme topologique vectoriel modifiable Draw.io XML et quitte immédiatement. |
| `--drawio-theme <thème>` | — | `dark` | Thème visuel du diagramme Draw.io (`dark` ou `light`). |
| `--drawio-edge-mode <mode>` | — | `adaptive` | Mode de routage des flèches Draw.io : `direction`, `port` ou `adaptive`. |
| `--drawio-swimlanes <bool>` | — | `true` | Regroupe les nœuds par couloirs (*swimlanes*) de sous-réseaux dans Draw.io. |
| `--include-ports <ports>` | — | — | Filtre pour la topologie / règles : ne conserve que les ports indiqués (ex. `80,443,1433-1434`). |
| `--exclude-ports <ports>` | — | — | Filtre pour la topologie / règles : exclut les ports indiqués (ex. `53,135-139,445`). |
| `--include-ips <ips>` | — | — | Filtre pour la topologie / règles : ne conserve que les IPs ou blocs CIDR spécifiés. |
| `--include-processes <noms>` | — | — | Filtre pour la topologie : ne conserve que les noms de processus indiqués. |
| `--include-hosts <motifs>` | — | — | Filtre pour la topologie : ne conserve que les noms d'hôtes ciblés (supporte les jokers `*web*`). |
| `--listeners-only` | — | `false` | Filtre pour la topologie / règles : ne conserve que les sockets de services en écoute passive. |

---

## 3. Matrice de Compatibilité des Options & Exclusions Mutuelles

### 3.1. Hiérarchie des Modes d'Exécution

NetFlowAudit propose **7 modes d'exécution distincts**. Les modes autonomes s'exécutent en une seule passe et quittent immédiatement sans lancer l'audit continu ni l'interface TUI.

| Mode d'Exécution | Déclencheur Principal | Comportement Opérationnel |
|---|---|---|
| **1. Diagnostic Ponctuel** | `--test-ip <ip>` / `--lookup <ip>` | Exécute le rapport de diagnostic verbeux en 6 étapes et quitte. |
| **2. Génération de Modèle JSON** | `--init-config` / `--generate-config` | Écrit le fichier modèle `netflowaudit.json` et quitte. |
| **3. Consolidation & Enrichissement Hors-Ligne** | `--resolve-offline` | Fusionne les caches DNS, enrichit les logs d'audit et quitte. |
| **4. Export de Matrice Pare-Feu** | `--export-rules` / `--firewall-matrix` | Agrège les flux en matrice 5-tuple CSV normalisée et quitte. |
| **5. Conversion NDJSON** | `--export-ndjson` | Convertit le journal d'audit CSV au format NDJSON et quitte. |
| **6. Rendu Topologique Dédié** | `--map-only` / `--export-drawio` | Génère les diagrammes HTML / Draw.io depuis des fichiers d'audit et quitte. |
| **7. Audit Continu des Sockets** | *(par défaut)* | Scrute les sockets en continu, déduplique les flux et affiche la console TUI. |

### 3.2. Règles d'Incompatibilité et Dépendances

| Option | Dépendance Requise | Incompatible Avec |
|---|---|---|
| `--in-place` | `--resolve-offline` | Audit continu, `--map-only` |
| `--force-resolve` | `--resolve-offline` | Audit continu, `--map-only` |
| `--output-cache` | `--resolve-offline` | Audit continu (utilise `DnsCachePath`) |
| `--min-observations` | `--export-rules` | `--resolve-offline`, `--test-ip` |
| `--active-within-days` | `--export-rules` | `--resolve-offline`, `--test-ip` |
| `--destination-zone` | `--export-rules` | `--resolve-offline`, `--test-ip` |
| `--exclude-ephemeral` | `--export-rules` | `--resolve-offline`, `--test-ip` |
| `--drawio-*` | `--export-drawio` ou `--map-only` | `--export-rules`, `--resolve-offline` |
| `--daemon` / `--no-tui` | Mode continu (défaut) | Modes autonomes (`--map-only`, etc.) |
| `--isolated` / `--offline` | Tous modes | Force `--resolve-dns=false` & RDAP désactivé |

> [!WARNING]
> La combinaison d'options autonomes mutuellement exclusives (ex. `--resolve-offline` avec `--export-rules`) n'est pas supportée. La CLI traite les commandes selon l'ordre de priorité : `--test-ip` > `--resolve-offline` > `--export-rules` > `--export-ndjson` > `--map-only`.

---

## 4. Modes d'Exploitation

### 4.1. Tableau de Bord Interactif (TUI)
Destiné aux sessions d'administration interactives, au diagnostic en temps réel et aux investigations :
```powershell
# Windows (Invite Administrateur recommandée pour corréler les PIDs système)
.\NetFlowAudit.Cli.exe

# Linux (Root / sudo recommandé pour inspecter les descripteurs des autres utilisateurs)
sudo ./NetFlowAudit.Cli
```

Fonctionnalités du TUI :
- Bandeaux KPI en direct (Sockets actifs, Flux découverts, Noms résolus, Taux de succès cache).
- Deux vues basculables : **Flux Découverts** (historique consolidé) et **Sockets Actifs** (instantané immédiat).
- Pagination interactive (`[N]` / `[P]` ou `[PgDn]` / `[PgUp]`).
- Chargement progressif du tampon (`[L]` pour charger +100 lignes, `[A]` pour tout charger).
- Génération instantanée de la carte HTML (`[M]`).
- Exclusion contextuelle à la volée (`[Entrée]` ou `[X]`).

### 4.2. Mode Démon / Service en Arrière-Plan
Conçu pour l'audit permanent 24/7 sur serveurs de production et l'ingestion par un collecteur SIEM :
```bash
# Windows
.\NetFlowAudit.Cli.exe --daemon --output "C:\Logs\netflow_audit_{ServerName}.csv"

# Linux
sudo ./NetFlowAudit.Cli --no-tui --output "/var/log/netflow/netflow_{ServerName}.csv"
```
En mode démon, l'interface TUI est désactivée. L'agent consigne uniquement les messages de démarrage et d'arrêt standard tout en écrivant en flux continu les enregistrements dédupliqués dans le fichier CSV.

### 4.3. Diagnostic Verbeux d'une Adresse IP (`--test-ip`)
Pour analyser les divergences de résolution entre l'OS et .NET ou comprendre pourquoi une IP ne résout pas :
```bash
NetFlowAudit.Cli --test-ip 34.120.255.184
```
Affiche un rapport technique en 6 étapes : analyse syntaxique/zone, présence dans le cache local, configuration des interfaces et suffixes DNS de l'OS, test reverse DNS PTR avec latence et erreurs SocketException, test HTTP direct RDAP, et modèle final de cache.

---

## 5. Déploiement en Production

### 5.1. Déploiement en Service Windows

#### Option A : Utilisation de NSSM (Non-Sucking Service Manager)
> [!NOTE]
> NSSM est un wrapper open-source permettant d'exécuter toute application console en tant que service natif Windows avec redémarrages automatiques et gestion des journaux.

```powershell
# Installation via Winget (Windows 11 / Windows 10 moderne)
winget install NSSM.NSSM

# Ou installation via Chocolatey
choco install nssm -y

# Ou installation via Scoop
scoop install nssm
```

Une fois installé, enregistrez et lancez le service :
```cmd
:: 1. Déclarer et configurer le service
nssm install NetFlowAudit "C:\Program Files\NetFlowAudit\NetFlowAudit.Cli.exe"
nssm set NetFlowAudit AppParameters "--daemon --config \"C:\Program Files\NetFlowAudit\netflowaudit.json\""
nssm set NetFlowAudit AppDirectory "C:\Program Files\NetFlowAudit"
nssm set NetFlowAudit DisplayName "NetFlowAudit Flow Monitoring Service"
nssm set NetFlowAudit Description "Agent d'audit continu des flux et sockets réseau."
nssm set NetFlowAudit Start SERVICE_AUTO_START

:: 2. Configurer la rotation des logs console
nssm set NetFlowAudit AppStdout "C:\ProgramData\NetFlowAudit\service.log"
nssm set NetFlowAudit AppStderr "C:\ProgramData\NetFlowAudit\service_error.log"

:: 3. Démarrer le service
nssm start NetFlowAudit
```

#### Option B : Planificateur de Tâches Windows (Sans outil tiers)
Pour les environnements d'entreprise stricts interdisant l'installation de logiciels tiers :
```powershell
$Action = New-ScheduledTaskAction -Execute "C:\Program Files\NetFlowAudit\NetFlowAudit.Cli.exe" -Argument "--daemon --config `"C:\Program Files\NetFlowAudit\netflowaudit.json`""
$Trigger = New-ScheduledTaskTrigger -AtStartup
$Principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit 0

Register-ScheduledTask -TaskName "NetFlowAudit-Daemon" -Action $Action -Trigger $Trigger -Principal $Principal -Settings $Settings
Start-ScheduledTask -TaskName "NetFlowAudit-Daemon"
```

---

## 6. Structure du Fichier de Configuration (`netflowaudit.json`)

```json
{
  "ScanIntervalSeconds": 5,
  "OutputCsvPath": "netflow_audit.csv",
  "OutputNdjsonPath": null,
  "OutputHtmlMapPath": "netflow_topology.html",
  "OutputDrawioPath": "netflow_topology.drawio",
  "ReloadHistoryLimit": null,
  "ResolveRemoteHostNames": true,
  "DnsResolutionTimeoutMs": 500,
  "DnsExcludedNetworks": [
    "224.0.0.0/4",
    "240.0.0.0/4",
    "255.255.255.255/32",
    "0.0.0.0/8"
  ],
  "DnsCacheFilePath": "netflow_dns_cache.json",
  "ResolveIpOwners": true,
  "CustomHostAliases": {
    "10.0.1.50": "dc-primary.corp",
    "10.0.1.51": "dc-secondary.corp"
  },
  "ExcludedRemoteIps": [
    "127.0.0.1",
    "::1",
    "0.0.0.0"
  ],
  "ExcludedPorts": [ 137, 138, 5353 ],
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
    "Protocol",
    "Direction",
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

---

## 7. Recettes Pratiques d'Exploitation

### Recette 1 : Diagnostic Verbeux de Résolution d'une IP
```bash
NetFlowAudit.Cli --test-ip 34.120.255.184
```

### Recette 2 : Export d'une Matrice de Pare-Feu Normalisée
```bash
NetFlowAudit.Cli --export-rules firewall_rules.csv -i netflow_audit.csv \
  --destination-zone internal \
  --exclude-ephemeral \
  --min-observations 5 \
  --active-within-days 30
```

### Recette 3 : Consolidation Multi-Caches en Réseau Isolé (*Air-Gapped*)
```powershell
.\NetFlowAudit.Cli.exe --resolve-offline `
  -i ".\audits\*.csv" `
  --caches ".\caches\*.json" `
  --output-cache master_dns_cache.json `
  --in-place
```

### Recette 4 : Cartographie Exclusive des Services en Écoute
```bash
NetFlowAudit.Cli --map-only -i netflow_audit.csv --listeners-only --map listening_services.html
```

### Recette 5 : Cartographie Isolée des Tiers Applicatifs (Web, DB, Cache)
```bash
NetFlowAudit.Cli --map-only -i netflow_audit.csv \
  --include-ports 80,443,1433,5432,3306,6379,8080,8443 \
  --exclude-ports 53,123,135-139,445 \
  --drawio tier_topology.drawio
```

---

## 8. Dépannage & Diagnostic de Résolution DNS / RDAP

### Pourquoi `ping -a` résout-il une IP alors que la résolution .NET échoue ?

Lorsqu'on exécute `ping -a <IP>` sur un poste de travail Windows d'entreprise, le système d'exploitation tente de résoudre le nom via une succession de mécanismes :
1. **Cache DNS local de l'OS et fichier Hosts** (`%SystemRoot%\System32\drivers\etc\hosts`).
2. **Requêtes broadcast NetBIOS sur TCP/IP (NBT)** et **LLMNR (Link-Local Multicast Name Resolution)**.
3. **Serveurs DNS configurés** combinés à la **liste de recherche des suffixes DNS** du domaine d'entreprise (`corp.contoso.com`).
4. **Serveurs WINS d'Active Directory**.

À l'inverse, l'appel `Dns.GetHostEntryAsync` de `.NET` réalise une requête standard stricte de **reverse DNS (PTR)** (`x.x.x.x.in-addr.arpa`) auprès du serveur DNS :
* Si les zones DNS internes de l'entreprise ne possèdent pas de zone de recherche inversée (`in-addr.arpa`) ou renvoient `NXDOMAIN`, .NET lève une `SocketException (HostNotFound)`.
* Si l'équipement possède un nom NetBIOS mais aucun enregistrement PTR DNS, `ping -a` réussit via NetBIOS alors que le reverse DNS .NET échoue.
* **Solutions** :
  1. Lancer `NetFlowAudit.Cli --test-ip <IP>` pour observer les serveurs DNS interrogés, les suffixes de l'OS et le code d'erreur socket exact.
  2. Déclarer les serveurs internes non résolus dans `"CustomHostAliases"` (`netflowaudit.json`) ou via l'option `--alias <IP>=<NomDHote>`.

### Adresses non spécifiées et non routables (`0.0.0.0`, `::`, `255.255.255.255`)
Dans les traces NetFlow et les tables de sockets, les écoutes passives ou flux de broadcast présentent des adresses comme `0.0.0.0`, `::` ou `255.255.255.255`.
* NetFlowAudit intercepte ces adresses avant tout appel à `Dns.GetHostEntryAsync` pour éviter les exceptions `ArgumentException` du runtime .NET.
* Elles reçoivent directement le statut `Source = "Excluded"` et ne consomment aucune ressource réseau.

### Enrichissement RDAP des Adresses IP Publiques
* Les enregistrements DNS PTR retournent le FQDN (ex. `184.255.120.34.bc.googleusercontent.com`) mais ne contiennent pas l'organisation propriétaire du bloc IP.
* NetFlowAudit complète systématiquement la résolution par une requête RDAP pour les blocs IP publics afin de renseigner `Organization`, `Country` et `NetworkRange`.

---

## 9. Raccourcis Clavier (Tableau de Bord TUI)

| Raccourci | Action |
|---|---|
| `[Q]` | Fermeture propre de l'agent, purge des écritures en attente et sortie. |
| `[C]` | Efface le tampon console et force le réaffichage complet de l'écran. |
| `[V]` | Bascule entre **Journal des Flux Découverts** et **Instantané des Sockets Actifs**. |
| `[M]` | Génère ou rafraîchit immédiatement la carte interactive HTML5 (`netflow_topology.html`). |
| `[N]` ou `[PageDown]` | Page suivante dans la liste des flux découverts. |
| `[P]` ou `[PageUp]` | Page précédente dans la liste des flux découverts. |
| `[L]` ou `[+]` | Charge **+100 lignes de flux supplémentaires** dans le tampon paginé. |
| `[A]` | Charge **l'intégralité des flux découverts** dans le tampon paginé. |
| `[↑ / ↓ / ← / →]` | Déplacement dans la grille de la page active. |
| `[Entrée]` ou `[X]` | Exclut l'élément sélectionné (Port local, IP distante, Processus ou Cible). |
