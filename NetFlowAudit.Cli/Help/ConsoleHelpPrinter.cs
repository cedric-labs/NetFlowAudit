using NetFlowAudit.Core.Export;

namespace NetFlowAudit.Cli.Help;

/// <summary>
/// Afficheur d'aide console pour la documentation intégrée de NetFlowAudit.
/// </summary>
public static class ConsoleHelpPrinter
{
    /// <summary>
    /// Affiche l'aide détaillée en console, incluant la syntaxe, les options et la liste exhaustive des colonnes CSV.
    /// </summary>
    public static void PrintHelp()
    {
        Console.WriteLine("""
========================================================================================
  NetFlowAudit - Agent d'Audit Continu des Flux Réseau (Windows & Linux)
========================================================================================

UTILISATION :
  NetFlowAudit.Cli [OPTIONS]

DESCRIPTION :
  Agent autonome de traçage et de journalisation des flux réseau entrants, sortants et
  d'écoute sans nécessiter de pilote tiers (zéro Npcap/libpcap). Associe chaque socket
  à son processus propriétaire, résout les wrappers applicatifs et dédoublonne les flux.

OPTIONS DE LIGNE DE COMMANDE :
  -h, --help                          Affiche cette aide exhaustive et quitte l'application.
  --no-tui, --daemon                  Désactive le tableau de bord interactif plein écran (recommandé en service).
  --config <chemin>                   Chemin du fichier de configuration JSON (par défaut : netflowaudit.json).
  --output <chemin>                   Chemin du fichier CSV de sortie (par défaut : netflow_audit.csv).
  --interval <secondes>               Intervalle en secondes entre deux balayages (par défaut : 5).
  --columns <col1,col2,...>           Liste personnalisée ordonnée de colonnes à exporter dans le CSV.
  --resolve-dns <true|false>          Active ou désactive la résolution DNS inverse des IP distantes.
  --resolve-ip-owners <true|false>    Active ou désactive la résolution d'organisation IP (RDAP) en repli DNS.
  --isolated, --offline               Mode serveur isolé d'Internet : désactive les résolutions réseau sortantes.
  --exclude-remote-ips <ip1,cidr2,..> Liste d'adresses IP ou plages CIDR distantes à exclure de l'audit.

OPTIONS DE CARTOGRAPHIE TOPOLOGIQUE (TopologyForge - HTML Autonome Hors-Ligne) :
  --map <chemin.html>                 Fichier HTML autonome de la carte des serveurs et ports non éphémères.
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

TOUCHES INTERACTIVES DU TABLEAU DE BORD (TUI) :
  [Q]                        Arrêt propre et synchronisation du fichier CSV.
  [C]                        Effacement de l'écran et réaffichage forcé du tableau de bord.
  [M]                        Génère instantanément la carte topologique HTML (netflow_topology.html).

========================================================================================
  CATALOGUE DES COLONNES CSV DISPONIBLES (--columns ou "CsvColumns" dans netflowaudit.json)
========================================================================================
""");

        var descriptors = AuditCsvColumnCatalog.GetAllDescriptors();

        Console.WriteLine(string.Format("{0,-18} | {1,-10} | {2,-45} | {3}", "NOM TECHNIQUE", "CATÉGORIE", "DESCRIPTION", "EXEMPLE"));
        Console.WriteLine(new string('-', 110));

        foreach (var desc in descriptors)
        {
            Console.WriteLine(string.Format(
                "{0,-18} | {1,-10} | {2,-45} | {3}",
                desc.Name,
                desc.Category,
                desc.Description,
                desc.Example));
        }

        Console.WriteLine("""

========================================================================================
  EXEMPLE DE FICHIER DE CONFIGURATION (netflowaudit.json)
========================================================================================
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
  "ResolveIpOwners": true,
  "IsolatedMode": false,
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
""");
    }
}
