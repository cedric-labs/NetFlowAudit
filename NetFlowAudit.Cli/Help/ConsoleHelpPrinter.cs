using NetFlowAudit.Core.Export;

namespace NetFlowAudit.Cli.Help;

/// <summary>
/// Console help printer for NetFlowAudit built-in CLI documentation.
/// </summary>
public static class ConsoleHelpPrinter
{
    /// <summary>
    /// Prints detailed CLI usage, options, interactive shortcuts, and the full CSV column catalog.
    /// </summary>
    public static void PrintHelp()
    {
        Console.WriteLine("""
========================================================================================
  NetFlowAudit - Continuous Network Flow Auditing Agent (Windows & Linux)
========================================================================================

USAGE:
  NetFlowAudit.Cli [OPTIONS]

DESCRIPTION:
  Self-contained network flow auditing agent that tracks inbound, outbound, and listening
  TCP/UDP sockets without requiring any third-party packet capture driver (no Npcap/libpcap).
  Correlates sockets with owning processes, unwraps script/runtime launchers, and deduplicates flows.

COMMAND-LINE OPTIONS:
  -h, --help                          Display this help message and exit.
  --no-tui, --daemon                  Disable the interactive full-screen terminal dashboard (for services).
  --config <path>                     Path to JSON configuration file (default: netflowaudit.json).
  --init-config, --generate-config [p]Generate a baseline JSON configuration template (default: netflowaudit.json) and exit.
  --reload-history <all|0|n>          Historical CSV flow entries to reload at startup (default: all, 0 to disable).
  -o, --output <path>                 Path to output CSV file (default: netflow_audit.csv).
                                      Supports dynamic tokens: {ServerName}, {HostName}, {Date}, {Timestamp}.
  --ndjson <path>                     Path to continuous output NDJSON stream file (JSON Lines).
                                      Supports dynamic tokens: {ServerName}, {HostName}, {Date}, {Timestamp}.
  --export-ndjson [path.ndjson]       Convert an existing CSV audit log to NDJSON format and exit.
  --export-rules [path.csv]           Export consolidated network flow matrix CSV and exit.
  --min-observations <n>              Minimum observation count required for firewall rule export (default: 1).
  --active-within-days <days>         Freshness filter: only include rules observed within the last N days.
  --destination-zone <internal|public>Filter rules by destination zone (RFC 1918 internal vs WAN/Internet public).
  --internal-only, --public-only      Shorthand filters for internal vs public firewall rules.
  --exclude-ephemeral                 Exclude ephemeral destination ports (>= 49152) from firewall rules.
  --resolve-offline                   Offline post-processing: consolidate caches and enrich audit hostnames.
  --in-place                          Overwrite original audit files directly during offline resolution.
  --force-resolve                     Force replacement of existing hostnames during offline resolution.
  --cache, --caches <path1,...>       Input cache files or directories to consolidate into master cache.
  --output-cache <path.json>          Output path for consolidated master DNS cache (default: netflow_dns_cache.json).
  --interval <seconds>                Scan interval in seconds (default: 5).
  --columns <col1,col2,...>           Custom ordered list of CSV columns to export.
  --resolve-dns <true|false>          Enable or disable reverse DNS resolution for remote IP addresses.
  --resolve-ip-owners <true|false>    Enable or disable RDAP IP organization fallback when PTR is missing.
  --isolated, --offline               Air-gapped / offline server mode: disable external resolution queries.
  --exclude-remote-ips <ip1,cidr2,..> Comma-separated remote IP addresses or CIDR subnets to exclude.
  --alias <ip>=<host>                 Static IP-to-hostname alias taking precedence over reverse DNS PTR.
  --mask-server <name>                Fictitious server name for dashboard display/screenshots.
  --mask-ip <ip>                      Fictitious local IP for dashboard display/screenshots.
  --obfuscate, --anonymize            Mask both server name (SRV-AUDIT-01) and local IP (10.0.0.10).

TOPOLOGY MAP OPTIONS (TopologyForge - Self-Contained Offline HTML):
  --map <path.html>                   Generate a self-contained HTML5 topology map of servers and service ports.
                                      Supports dynamic tokens: {ServerName}, {HostName}, {Date}, {Timestamp}.
  --map-only                          Generate the HTML topology map from audit logs and exit.
  --export-drawio, --drawio <file>    Export the topology to an uncompressed Draw.io (.drawio) XML diagram.
  --drawio-edge-mode <adaptive|direction|none>
                                      Edge aggregation strategy for Draw.io (default: adaptive).
  --drawio-theme <dark|light>         Visual theme for Draw.io diagrams (default: dark).
  -i, --input <path.csv>              Single input audit CSV file (default: OutputCsvPath).
  --dir, --input-dir <dir>            Consolidate all audit CSV files from a directory into a unified map.
  --pattern, --input-filter <glob>    File search filter when using --input-dir (default: *.csv).
  --inputs, --input-files <f1,f2,...> Comma-separated list of audit CSV files to consolidate.
  --include-ports, --include-port <p1,p2-p3,...>
                                      Include only these service ports or port ranges (e.g. 80,443,1433,8000-8080).
  --exclude-ports, --exclude-port <p1,p2-p3,...>
                                      Exclude these ports or port ranges from the map (e.g. 53,123,135-139).
  --include-hosts, --include-host <m1,m2,...>
                                      Include only servers matching wildcard or substring patterns (e.g. SRV-*,*SQL*).
  --exclude-hosts, --exclude-host <m1,m2,...>
                                      Exclude servers matching wildcard or substring patterns (e.g. *.local,SRV-MON-*).
  --include-ips, --include-ip <ip1,cidr2,...>
                                      Include only these IP addresses or CIDR subnets (e.g. 10.0.0.0/8).
  --exclude-ips, --exclude-ip <ip1,cidr2,...>
                                      Exclude these IP addresses or CIDR subnets (e.g. 169.254.0.0/16).
  --include-processes, --include-process <proc1,...>
                                      Include only these processes or application targets.
  --exclude-processes, --exclude-process <proc1,...>
                                      Exclude these processes or application targets.
  --map-layout, --layout, --algorithm <layered|tree|horizontal-tree|organic|circular|grid>
                                      Initial geometric layout algorithm (layered hierarchical by default).
  --map-sort, --sort <barycentric|tiers|subnet|degree|alpha|ip>
                                      Server sorting method (Sugiyama anti-crossing by default).
  --map-routing, --routing <bezier|orthogonal|straight>
                                      Arrow routing mode with server box obstacle avoidance.
  --map-spacing, --spacing <compact|balanced|spacious>
                                      Initial spacing profile between servers and arrows.
  --direction <all|out|in>            Directional flow filtering (all flows by default).
  --listeners-only                    Map passive listening ports only.
  --include-ephemeral-ports           Disable default ephemeral port exclusion (49152-65535).

INTERACTIVE DASHBOARD SHORTCUTS (TUI):
  [Q]                        Graceful shutdown and CSV flush.
  [C]                        Clear screen and force full dashboard redraw.
  [V]                        Toggle view mode (Audit Log discoveries / Active Sockets snapshot).
  [M]                        Immediately generate/refresh the HTML topology map (netflow_topology.html).
  [N / PageDown]             Go to next page of flow discoveries.
  [P / PageUp]               Go to previous page of flow discoveries.
  [L / +]                    Load +100 additional flow rows into the paginated buffer.
  [A]                        Load all discovered flows into the paginated buffer.
  [↑ / ↓ / ← / →]            Navigate rows and columns on the current page.
  [Enter / X]                Exclude the selected Local Port, Remote IP, Process, or Target.

========================================================================================
  AVAILABLE CSV COLUMNS (--columns or "CsvColumns" in netflowaudit.json)
========================================================================================
""");

        var descriptors = AuditCsvColumnCatalog.GetAllDescriptors();

        Console.WriteLine(string.Format("{0,-18} | {1,-10} | {2,-45} | {3}", "COLUMN NAME", "CATEGORY", "DESCRIPTION", "EXAMPLE"));
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
  SAMPLE CONFIGURATION FILE (netflowaudit.json)
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
  "ReloadHistoryLimit": null,
  "ResolveRemoteHostNames": true,
  "ResolveIpOwners": true,
  "IsolatedMode": false,
  "CustomHostAliases": {
    "203.0.113.30": "nextcloud.home.arpa"
  },
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
