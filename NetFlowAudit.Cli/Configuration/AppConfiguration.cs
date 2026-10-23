namespace NetFlowAudit.Cli.Configuration;

/// <summary>
/// Configuration globale de l'agent d'audit réseau NetFlowAudit.
/// </summary>
public sealed record AppConfiguration
{
    /// <summary>
    /// Intervalle de scan des connexions en secondes (par défaut : 5s).
    /// </summary>
    public int ScanIntervalSeconds { get; init; } = 5;

    /// <summary>
    /// Chemin du fichier CSV de sortie.
    /// </summary>
    public string OutputCsvPath { get; init; } = "netflow_audit.csv";

    /// <summary>
    /// Colonnes ordonnées à exporter dans le fichier CSV.
    /// Si la liste est vide, l'ensemble des 20 colonnes par défaut est exporté.
    /// </summary>
    public List<string> CsvColumns { get; init; } = [];

    /// <summary>
    /// Séparateur de champ pour le fichier CSV (virgule ',' par défaut).
    /// </summary>
    public string CsvDelimiter { get; init; } = ",";

    /// <summary>
    /// Active ou désactive la résolution DNS inverse des adresses IP distantes.
    /// </summary>
    public bool ResolveRemoteHostNames { get; init; } = true;

    /// <summary>
    /// Chemin du fichier de persistance du cache DNS inverse (JSON).
    /// </summary>
    public string DnsCacheFilePath { get; init; } = "netflow_dns_cache.json";

    /// <summary>
    /// Liste des sous-réseaux (CIDR) ou adresses IP exclues de la résolution DNS inverse.
    /// </summary>
    public List<string> DnsExcludedNetworks { get; init; } =
    [
        "0.0.0.0",
        "255.255.255.255",
        "169.254.0.0/16",
        "224.0.0.0/4",
        "fe80::/10",
        "ff00::/8"
    ];

    /// <summary>
    /// PIDs à exclure systématiquement.
    /// </summary>
    public List<int> ExcludedPids { get; init; } = [0, 4];

    /// <summary>
    /// Noms exacts de processus à ignorer.
    /// </summary>
    public List<string> ExcludedExactNames { get; init; } = ["System", "Idle"];

    /// <summary>
    /// Préfixes de noms de processus à exclure.
    /// </summary>
    public List<string> ExcludedPrefixes { get; init; } = [];

    /// <summary>
    /// Suffixes de noms de processus à exclure.
    /// </summary>
    public List<string> ExcludedSuffixes { get; init; } = [];

    /// <summary>
    /// Segments de noms de processus à exclure.
    /// </summary>
    public List<string> ExcludedContains { get; init; } = [];

    /// <summary>
    /// Exclure les processus système (NT AUTHORITY, SYSTEM, root).
    /// </summary>
    public bool ExcludeSystemAccounts { get; init; } = true;

    /// <summary>
    /// Exclure les flux intra-machine (LocalIp == RemoteIp ou boucle locale).
    /// </summary>
    public bool ExcludeLoopbackFlows { get; init; } = true;

    /// <summary>
    /// Liste des ports locaux à exclure de l'audit.
    /// </summary>
    public List<int> ExcludedLocalPorts { get; init; } = [];

    /// <summary>
    /// Liste des ports distants à exclure de l'audit.
    /// </summary>
    public List<int> ExcludedRemotePorts { get; init; } = [];

    /// <summary>
    /// Liste des adresses IP ou sous-réseaux distants (CIDR) à exclure de l'audit.
    /// </summary>
    public List<string> ExcludedRemoteIps { get; init; } = [];

    /// <summary>
    /// Active l'interface terminal dynamique (TUI style btop) en mode interactif.
    /// </summary>
    public bool EnableDashboard { get; init; } = true;
}
