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
  -h, --help                 Affiche cette aide exhaustive et quitte l'application.
  --no-tui, --daemon         Désactive le tableau de bord interactif plein écran (recommandé en service).
  --config <chemin>          Chemin du fichier de configuration JSON (par défaut : netflowaudit.json).
  --output <chemin>          Chemin du fichier CSV de sortie (par défaut : netflow_audit.csv).
  --interval <secondes>      Intervalle en secondes entre deux balayages (par défaut : 5).
  --columns <col1,col2,...>  Liste personnalisée ordonnée de colonnes à exporter dans le CSV.
  --resolve-dns <true|false> Active ou désactive la résolution DNS inverse des IP distantes.

TOUCHES INTERACTIVES DU TABLEAU DE BORD (TUI) :
  [Q]                        Arrêt propre et synchronisation du fichier CSV.
  [C]                        Effacement de l'écran et réaffichage forcé du tableau de bord.

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
  "EnableDashboard": true,
  "ResolveRemoteHostNames": true,
  "ExcludeLoopbackFlows": true,
  "ExcludedLocalPorts": [ 22, 3389 ],
  "ExcludedRemotePorts": [ 53 ],
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
