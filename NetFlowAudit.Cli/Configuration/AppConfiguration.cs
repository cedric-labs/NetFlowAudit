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
    /// Chemin du fichier HTML de cartographie topologique (vide si désactivé par défaut).
    /// </summary>
    public string OutputHtmlMapPath { get; init; } = string.Empty;

    /// <summary>
    /// Exclut les ports éphémères (49152-65535) lors de la génération de la cartographie HTML.
    /// </summary>
    public bool MapExcludeEphemeralPorts { get; init; } = true;

    /// <summary>
    /// Limite la cartographie uniquement aux ports en écoute passive.
    /// </summary>
    public bool MapListenersOnly { get; init; } = false;

    /// <summary>
    /// Liste des ports ou plages de ports (ex: "80", "443", "8000-8080") à inclure dans la carte.
    /// </summary>
    public List<string> MapIncludedPorts { get; init; } = [];

    /// <summary>
    /// Liste des ports ou plages de ports à exclure de la carte.
    /// </summary>
    public List<string> MapExcludedPorts { get; init; } = [];

    /// <summary>
    /// Liste des motifs de noms d'hôtes à inclure dans la carte (supporte '*').
    /// </summary>
    public List<string> MapIncludedHosts { get; init; } = [];

    /// <summary>
    /// Liste des motifs de noms d'hôtes à exclure de la carte (supporte '*').
    /// </summary>
    public List<string> MapExcludedHosts { get; init; } = [];

    /// <summary>
    /// Liste des adresses IP ou sous-réseaux CIDR à inclure dans la carte.
    /// </summary>
    public List<string> MapIncludedIps { get; init; } = [];

    /// <summary>
    /// Liste des adresses IP ou sous-réseaux CIDR à exclure de la carte.
    /// </summary>
    public List<string> MapExcludedIps { get; init; } = [];

    /// <summary>
    /// Liste des processus ou cibles applicatives à inclure dans la carte.
    /// </summary>
    public List<string> MapIncludedProcesses { get; init; } = [];

    /// <summary>
    /// Liste des processus ou cibles applicatives à exclure de la carte.
    /// </summary>
    public List<string> MapExcludedProcesses { get; init; } = [];

    /// <summary>
    /// Méthode de tri initial des serveurs sur la carte ("barycentric", "degree", "alpha", "subnet").
    /// </summary>
    public string MapSortMethod { get; init; } = "barycentric";

    /// <summary>
    /// Mode de routage initial des flèches sur la carte ("bezier", "orthogonal", "straight").
    /// </summary>
    public string MapRoutingMode { get; init; } = "bezier";

    /// <summary>
    /// Profil d'écartement initial entre serveurs et flèches ("compact", "balanced", "spacious").
    /// </summary>
    public string MapSpacingProfile { get; init; } = "balanced";

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
    /// Active ou désactive la résolution de l'organisation ou du propriétaire IP (RDAP) en cas d'absence de PTR DNS.
    /// </summary>
    public bool ResolveIpOwners { get; init; } = true;

    /// <summary>
    /// Mode isolé / hors-ligne : désactive toute tentative de résolution externe vers Internet.
    /// </summary>
    public bool IsolatedMode { get; init; } = false;

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

    /// <summary>
    /// Nom de serveur fictif affichÃ© dans le tableau de bord (pour captures d'Ã©cran anonymisÃ©es).
    /// </summary>
    public string MaskServerName { get; init; } = string.Empty;

    /// <summary>
    /// Adresse IP locale fictive affichÃ©e dans le tableau de bord (pour captures d'Ã©cran anonymisÃ©es).
    /// </summary>
    public string MaskLocalIp { get; init; } = string.Empty;
}
