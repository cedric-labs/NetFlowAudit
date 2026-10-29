namespace PortAtlas.Cli.Configuration;

/// <summary>
/// Global configuration model for the PortAtlas network flow auditing agent.
/// </summary>
public sealed record AppConfiguration
{
    /// <summary>
    /// Connection scan interval in seconds (default: 5s).
    /// </summary>
    public int ScanIntervalSeconds { get; init; } = 5;

    /// <summary>
    /// Output CSV file path.
    /// </summary>
    public string OutputCsvPath { get; init; } = "netflow_audit.csv";

    /// <summary>
    /// Output NDJSON (newline-delimited JSON) file path (empty if disabled by default).
    /// </summary>
    public string OutputNdjsonPath { get; init; } = string.Empty;

    /// <summary>
    /// Output HTML topology map file path (empty if disabled by default).
    /// </summary>
    public string OutputHtmlMapPath { get; init; } = string.Empty;

    /// <summary>
    /// Output Draw.io (.drawio) XML topology diagram file path (empty if disabled by default).
    /// </summary>
    public string OutputDrawioPath { get; init; } = string.Empty;

    /// <summary>
    /// Edge aggregation mode for Draw.io diagrams: adaptive, direction, or none.
    /// </summary>
    public string DrawioEdgeMode { get; init; } = "adaptive";

    /// <summary>
    /// Visual theme for Draw.io diagrams: dark or light.
    /// </summary>
    public string DrawioTheme { get; init; } = "dark";

    /// <summary>
    /// Exclude ephemeral ports (49152-65535) during HTML map generation.
    /// </summary>
    public bool MapExcludeEphemeralPorts { get; init; } = true;

    /// <summary>
    /// Restrict the topology map strictly to passive listening ports.
    /// </summary>
    public bool MapListenersOnly { get; init; } = false;

    /// <summary>
    /// Flow direction filter for map generation ("all", "out", "in").
    /// </summary>
    public string MapDirection { get; init; } = "all";

    /// <summary>
    /// List of ports or port ranges (e.g. "80", "443", "8000-8080") to include in the map.
    /// </summary>
    public List<string> MapIncludedPorts { get; init; } = [];

    /// <summary>
    /// List of ports or port ranges to exclude from the map.
    /// </summary>
    public List<string> MapExcludedPorts { get; init; } = [];

    /// <summary>
    /// List of host name wildcard patterns to include in the map (supports '*').
    /// </summary>
    public List<string> MapIncludedHosts { get; init; } = [];

    /// <summary>
    /// List of host name wildcard patterns to exclude from the map (supports '*').
    /// </summary>
    public List<string> MapExcludedHosts { get; init; } = [];

    /// <summary>
    /// List of IP addresses or CIDR subnets to include in the map.
    /// </summary>
    public List<string> MapIncludedIps { get; init; } = [];

    /// <summary>
    /// List of IP addresses or CIDR subnets to exclude from the map.
    /// </summary>
    public List<string> MapExcludedIps { get; init; } = [];

    /// <summary>
    /// List of process names or application targets to include in the map.
    /// </summary>
    public List<string> MapIncludedProcesses { get; init; } = [];

    /// <summary>
    /// List of process names or application targets to exclude from the map.
    /// </summary>
    public List<string> MapExcludedProcesses { get; init; } = [];

    /// <summary>
    /// Initial layout mode on the map ("layered", "tree", "horizontal-tree", "organic", "circular", "grid").
    /// </summary>
    public string MapLayoutMode { get; init; } = "layered";

    /// <summary>
    /// Initial server sorting method on the map ("barycentric", "tiers", "subnet", "degree", "alpha", "ip").
    /// </summary>
    public string MapSortMethod { get; init; } = "barycentric";

    /// <summary>
    /// Initial arrow routing mode on the map ("bezier", "orthogonal", "straight").
    /// </summary>
    public string MapRoutingMode { get; init; } = "bezier";

    /// <summary>
    /// Initial spacing profile between servers and arrows ("compact", "balanced", "spacious").
    /// </summary>
    public string MapSpacingProfile { get; init; } = "balanced";

    /// <summary>
    /// Ordered columns to export to the CSV file.
    /// If empty, the default set of 20 columns is exported.
    /// </summary>
    public List<string> CsvColumns { get; init; } = [];

    /// <summary>
    /// Field delimiter for the CSV file (comma ',' by default).
    /// </summary>
    public string CsvDelimiter { get; init; } = ",";

    /// <summary>
    /// Enables or disables asynchronous reverse DNS resolution for remote IP addresses.
    /// </summary>
    public bool ResolveRemoteHostNames { get; init; } = true;

    /// <summary>
    /// Milliseconds to wait for a reverse DNS answer before giving up on an address for this attempt. Corporate DNS
    /// servers can take several seconds to answer PTR queries; resolution runs in the background, so a long wait does
    /// not delay the scans.
    /// </summary>
    public int DnsResolutionTimeoutMs { get; init; } = 3000;

    /// <summary>
    /// Enables or disables RDAP IP organization resolution when PTR records are absent.
    /// </summary>
    public bool ResolveIpOwners { get; init; } = true;

    /// <summary>
    /// Reverse DNS persistent cache file path (JSON format).
    /// </summary>
    public string DnsCacheFilePath { get; init; } = "netflow_dns_cache.json";

    /// <summary>
    /// List of CIDR subnets or IP addresses excluded from reverse DNS resolution.
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
    /// Process IDs (PIDs) to exclude systematically.
    /// </summary>
    public List<int> ExcludedPids { get; init; } = [0, 4];

    /// <summary>
    /// Exact process names to ignore.
    /// </summary>
    public List<string> ExcludedExactNames { get; init; } = ["System", "Idle"];

    /// <summary>
    /// Process name prefixes to exclude.
    /// </summary>
    public List<string> ExcludedPrefixes { get; init; } = [];

    /// <summary>
    /// Process name suffixes to exclude.
    /// </summary>
    public List<string> ExcludedSuffixes { get; init; } = [];

    /// <summary>
    /// Process name substrings to exclude.
    /// </summary>
    public List<string> ExcludedContains { get; init; } = [];

    /// <summary>
    /// Exclude processes run by system accounts (NT AUTHORITY, SYSTEM, root). Off by default: most services run under
    /// these accounts and are usually what an audit is meant to record.
    /// </summary>
    public bool ExcludeSystemAccounts { get; init; }

    /// <summary>
    /// Exclude intra-host flows (LocalIp == RemoteIp or loopback address range).
    /// </summary>
    public bool ExcludeLoopbackFlows { get; init; } = true;

    /// <summary>
    /// List of local ports to exclude from auditing.
    /// </summary>
    public List<int> ExcludedLocalPorts { get; init; } = [];

    /// <summary>
    /// List of remote ports to exclude from auditing.
    /// </summary>
    public List<int> ExcludedRemotePorts { get; init; } = [];

    /// <summary>
    /// List of remote IP addresses or CIDR subnets to exclude from auditing.
    /// </summary>
    public List<string> ExcludedRemoteIps { get; init; } = [];

    /// <summary>
    /// Enables interactive terminal dashboard (TUI) display mode.
    /// </summary>
    public bool EnableDashboard { get; init; } = true;

    /// <summary>
    /// Masked server name displayed in the dashboard header for anonymized presentations.
    /// </summary>
    public string MaskServerName { get; init; } = string.Empty;

    /// <summary>
    /// Masked local IP address displayed in dashboard rows for anonymized presentations.
    /// </summary>
    public string MaskLocalIp { get; init; } = string.Empty;

    /// <summary>
    /// User-defined static IP-to-hostname mappings taking precedence over DNS reverse PTR records.
    /// </summary>
    public Dictionary<string, string> CustomHostAliases { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maximum number of recent historical flow records to reload from the output CSV on startup
    /// (null or negative to reload all records, 0 to disable history reloading).
    /// </summary>
    public int? ReloadHistoryLimit { get; init; } = null;
}
