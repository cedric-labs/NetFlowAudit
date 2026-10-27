# NetFlowAudit - User Guide & Production Operations

## 1. Installation & Distribution

NetFlowAudit is packaged as standalone, self-contained, single-file executables for **x64 Windows** and **x64 Linux**. The .NET 8 runtime is embedded inside the binary; no runtime or SDK installation is required on target servers.

### 1.1. Download Pre-Built Binaries
Pre-compiled packages and standalone executables are published on GitHub Releases:
- **GitHub Release**: [NetFlowAudit Releases](https://github.com/curvecoder/NetFlowAudit/releases)

| OS / Architecture | Artifact | Usage |
|---|---|---|
| **Windows x64** | `NetFlowAudit-1.6.0-preview.2-win-x64.zip` | Archive containing `NetFlowAudit.Cli.exe` and default `netflowaudit.json`. |
| **Windows x64 (Direct)** | `NetFlowAudit-1.6.0-preview.2-win-x64.exe` | Single standalone `.exe` without archive extraction. |
| **Linux x64** | `NetFlowAudit-1.6.0-preview.2-linux-x64.tar.gz` | Archive containing executable binary and baseline configuration. |
| **Linux x64 (Direct)** | `NetFlowAudit-1.6.0-preview.2-linux-x64` | Single standalone ELF executable. |

### 1.2. Building from Source
To compile directly from source:
```bash
# Clone the repository
git clone https://github.com/curvecoder/NetFlowAudit.git
cd NetFlowAudit

# Build self-contained Windows executable
dotnet publish NetFlowAudit.Cli/NetFlowAudit.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish/win-x64

# Build self-contained Linux executable
dotnet publish NetFlowAudit.Cli/NetFlowAudit.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish/linux-x64
```

---

## 2. Command-Line Options Reference

### 2.1. Complete Command-Line Options Catalog

| Option | Alias / Short | Default | Description |
|---|---|---|---|
| `--help` | `-h`, `-?` | — | Display the CLI help message and exit. |
| `--config <path>` | — | `netflowaudit.json` | Path to the JSON configuration file. |
| `--init-config [path]` | `--generate-config` | `netflowaudit.json` | Generate a baseline JSON configuration file and exit immediately. |
| `--daemon` | `--no-tui` | `false` | Run in headless mode without the interactive terminal dashboard (suitable for Windows Services / systemd). |
| `--output <path>` | `-o` | `netflow_audit.csv` | Output path for deduplicated CSV flow records. Supports dynamic tokens `{ServerName}`, `{HostName}`, `{Date}`, `{Timestamp}`. |
| `--output-ndjson <path>` | `--ndjson` | — | Continuous output path for streaming NDJSON (JSON Lines) flow records. |
| `--export-ndjson [path]` | — | `netflow_audit.ndjson` | Convert an existing CSV audit log into NDJSON format and exit. |
| `--export-rules [path]` | `--firewall-matrix`, `--rules-csv` | `firewall_rules.csv` | Aggregate flow history into a normalized 5-tuple firewall rule matrix and exit. |
| `--min-observations <n>` | — | `1` | Filter for `--export-rules`: minimum observation count required to retain a rule. |
| `--active-within-days <n>` | — | `null` (all) | Filter for `--export-rules`: only retain rules observed within the last N days. |
| `--destination-zone <zone>` | — | `all` | Filter for `--export-rules`: filter by destination zone (`internal` vs `public`). |
| `--internal-only` | — | `false` | Shorthand filter for `--export-rules`: retain only internal-to-internal flows. |
| `--public-only` | — | `false` | Shorthand filter for `--export-rules`: retain only public/WAN flows. |
| `--exclude-ephemeral` | — | `false` | Filter for `--export-rules`: exclude destination ports >= 49152 (dynamic/ephemeral ports). |
| `--direction <dir>` | — | `all` | Filter for topology map and rules: `inbound`, `outbound`, or `all`. |
| `--resolve-offline` | — | `false` | Consolidate DNS caches and enrich audit files offline in air-gapped environments, then exit. |
| `--in-place` | — | `false` | Used with `--resolve-offline`: overwrite original audit files directly instead of creating `*_enriched.csv`. |
| `--force-resolve` | — | `false` | Used with `--resolve-offline`: re-resolve hostnames even if already present in input files. |
| `--caches <paths>` | `--cache` | — | Used with `--resolve-offline`: input cache files or directories to consolidate into master cache. |
| `--output-cache <path>` | — | `netflow_dns_cache.json` | Destination path for the consolidated master DNS cache. |
| `--test-ip <ip>` | `--lookup` | — | Perform verbose 6-stage DNS and RDAP resolution diagnostics for a single IP address and exit. |
| `--interval <seconds>` | — | `5` | Scan interval in seconds for continuous socket polling. |
| `--columns <c1,c2,..>` | — | Default 16 cols | Ordered list of CSV columns to record. |
| `--resolve-dns <bool>` | — | `true` | Enable or disable reverse DNS PTR lookups for remote endpoints. |
| `--resolve-ip-owners <bool>` | — | `true` | Enable or disable RDAP queries for organization and autonomous system names. |
| `--isolated` | `--offline` | `false` | Completely disable outbound network queries (DNS PTR and RDAP). Zero network packets emitted. |
| `--exclude-remote-ips <ips>` | `--exclude-remote-ip` | — | Comma-separated list of remote IP addresses or CIDR subnets to exclude from auditing. |
| `--alias <ip>=<host>` | `--aliases` | — | Static IP-to-hostname mapping taking precedence over DNS resolution. |
| `--reload-history <n / all / 0>` | `--history-limit` | `all` | Number of historical CSV flow records loaded into memory at startup to resume deduplication. |
| `--mask-server <name>` | `--obfuscate-server` | — | Fictitious server name used for screenshots and sanitized exports. |
| `--mask-ip <ip>` | `--obfuscate-ip` | — | Fictitious IP address used for screenshots and sanitized exports. |
| `--obfuscate` | `--anonymize` | `false` | Shorthand: masks server name as `SRV-AUDIT-01` and local IP as `10.0.0.10`. |
| `--map-only` | — | `false` | Generate an HTML/Draw.io topology map from existing CSV files and exit immediately. |
| `--map <path>` | — | `netflow_topology.html` | Path for the generated interactive HTML5 topology diagram. |
| `--export-drawio [path]` | `--drawio` | `netflow_topology.drawio` | Export an editable Draw.io XML topology diagram and exit immediately. |
| `--drawio-theme <theme>` | — | `dark` | Visual theme for Draw.io diagrams (`dark` or `light`). |
| `--drawio-edge-mode <mode>` | — | `adaptive` | Draw.io edge routing mode: `direction`, `port`, or `adaptive`. |
| `--drawio-swimlanes <bool>` | — | `true` | Group nodes into subnet swimlane containers in Draw.io. |
| `--include-ports <ports>` | — | — | Filter for topology map / rules: only include specified ports or ranges (e.g. `80,443,1433-1434`). |
| `--exclude-ports <ports>` | — | — | Filter for topology map / rules: exclude specified ports or ranges (e.g. `53,135-139,445`). |
| `--include-ips <ips>` | — | — | Filter for topology map / rules: only include specified remote IPs or CIDR blocks. |
| `--include-processes <names>` | — | — | Filter for topology map: only include specified process names. |
| `--include-hosts <patterns>` | — | — | Filter for topology map: only include specified hostnames (supports wildcards `*web*`). |
| `--listeners-only` | — | `false` | Filter for topology map / rules: include only listening service sockets. |

---

## 3. Options Compatibility & Mutual Exclusivity Matrix

### 3.1. Execution Mode Hierarchy

NetFlowAudit provides **7 distinct execution modes**. Standalone modes exit immediately upon task completion without launching background socket polling or the interactive TUI.

| Execution Mode | Triggering Option | Behavior |
|---|---|---|
| **1. Diagnostic Lookup** | `--test-ip <ip>` / `--lookup <ip>` | Executes 6-stage verbose resolution report and exits. |
| **2. Config Template Generation** | `--init-config` / `--generate-config` | Writes default `netflowaudit.json` template and exits. |
| **3. Offline Consolidation & Enrichment** | `--resolve-offline` | Merges DNS caches, enriches audit files, and exits. |
| **4. Firewall Rule Matrix Export** | `--export-rules` / `--firewall-matrix` | Aggregates flows into normalized 5-tuple CSV and exits. |
| **5. NDJSON Conversion** | `--export-ndjson` | Converts CSV audit log to NDJSON format and exits. |
| **6. Topology Map Generation** | `--map-only` / `--export-drawio` | Generates HTML / Draw.io diagrams from CSV inputs and exits. |
| **7. Continuous Socket Auditing** | *(default)* | Continuously polls socket tables, correlations, and deduplication. |

### 3.2. Option Incompatibility and Dependency Rules

| Option | Requires / Prerequisite | Incompatible With |
|---|---|---|
| `--in-place` | `--resolve-offline` | Continuous auditing, `--map-only` |
| `--force-resolve` | `--resolve-offline` | Continuous auditing, `--map-only` |
| `--output-cache` | `--resolve-offline` | Continuous auditing (uses `DnsCachePath`) |
| `--min-observations` | `--export-rules` | `--resolve-offline`, `--test-ip` |
| `--active-within-days` | `--export-rules` | `--resolve-offline`, `--test-ip` |
| `--destination-zone` | `--export-rules` | `--resolve-offline`, `--test-ip` |
| `--exclude-ephemeral` | `--export-rules` | `--resolve-offline`, `--test-ip` |
| `--drawio-*` | `--export-drawio` or `--map-only` | `--export-rules`, `--resolve-offline` |
| `--daemon` / `--no-tui` | Continuous mode (default) | Standalone modes (`--map-only`, etc.) |
| `--isolated` / `--offline` | Any mode | Forces `--resolve-dns=false` & RDAP=off |

> [!WARNING]
> Combining mutually exclusive flags (e.g. `--resolve-offline` with `--export-rules`) is not supported: the CLI dispatches standalone modes in order of precedence: `--test-ip` > `--resolve-offline` > `--export-rules` > `--export-ndjson` > `--map-only`.

---

## 4. Operational Modes

### 4.1. Interactive Terminal Dashboard (TUI)
Designed for active administrator sessions, real-time troubleshooting, and ad-hoc investigations:
```powershell
# Windows (Administrator prompt recommended to correlate system PIDs)
.\NetFlowAudit.Cli.exe

# Linux (Root / sudo recommended to inspect foreign socket descriptors)
sudo ./NetFlowAudit.Cli
```

The TUI provides:
- Live KPI banners (Active Sockets, Discovered Flows, Resolved Hostnames, Cache Hits).
- Two switchable viewports: **Discovered Flows** (consolidated historical log) and **Active Sockets** (instantaneous snapshot).
- Interactive pagination (`[N]` / `[P]` or `[PgDn]` / `[PgUp]`).
- Incremental buffer loading (`[L]` to add +100 rows, `[A]` to load all).
- Instant HTML map generation (`[M]`).
- In-place exclusion toggling (`[Enter]` or `[X]`).

### 4.2. Headless Daemon / Background Service
Designed for 24/7 background auditing, data center fleet monitoring, and automated SIEM ingestion:
```bash
# Windows
.\NetFlowAudit.Cli.exe --daemon --output "C:\Logs\netflow_audit_{ServerName}.csv"

# Linux
sudo ./NetFlowAudit.Cli --no-tui --output "/var/log/netflow/netflow_{ServerName}.csv"
```
In headless mode, the TUI is disabled, and the agent logs standard startup/shutdown banners to standard output while quietly persisting deduplicated flows to the CSV file.

### 4.3. Verbose Single IP Diagnostics (`--test-ip`)
To troubleshoot DNS and RDAP resolution discrepancies or test why an IP is not resolving as expected:
```bash
NetFlowAudit.Cli --test-ip 34.120.255.184
```
Outputs a full 6-stage diagnostic report including classification, local cache status, OS network configuration, reverse DNS PTR execution time, RDAP HTTP response code and payload, and the final consolidated cache model.

---

## 5. Production Deployment

### 5.1. Windows Service Deployment

#### Option A: Using NSSM (Non-Sucking Service Manager)
> [!NOTE]
> NSSM is an open-source service wrapper that enables standard CLI applications to run as native Windows services with automatic restarts and logging. It is not preinstalled on Windows and can be installed in seconds:

```powershell
# Install via Winget (Windows 11 / Windows 10 modern)
winget install NSSM.NSSM

# Or install via Chocolatey
choco install nssm -y

# Or install via Scoop
scoop install nssm
```

Once installed, register and launch the NetFlowAudit background service:
```cmd
:: 1. Create and configure the service
nssm install NetFlowAudit "C:\Program Files\NetFlowAudit\NetFlowAudit.Cli.exe"
nssm set NetFlowAudit AppParameters "--daemon --config \"C:\Program Files\NetFlowAudit\netflowaudit.json\""
nssm set NetFlowAudit AppDirectory "C:\Program Files\NetFlowAudit"
nssm set NetFlowAudit DisplayName "NetFlowAudit Flow Monitoring Service"
nssm set NetFlowAudit Description "Continuous background network socket and flow auditing agent."
nssm set NetFlowAudit Start SERVICE_AUTO_START

:: 2. Configure stdout/stderr log rotation
nssm set NetFlowAudit AppStdout "C:\ProgramData\NetFlowAudit\service.log"
nssm set NetFlowAudit AppStderr "C:\ProgramData\NetFlowAudit\service_error.log"

:: 3. Start the service
nssm start NetFlowAudit
```

#### Option B: Windows Task Scheduler (No Third-Party Tools)
For strict enterprise environments where installing NSSM is restricted:
```powershell
$Action = New-ScheduledTaskAction -Execute "C:\Program Files\NetFlowAudit\NetFlowAudit.Cli.exe" -Argument "--daemon --config `"C:\Program Files\NetFlowAudit\netflowaudit.json`""
$Trigger = New-ScheduledTaskTrigger -AtStartup
$Principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit 0

Register-ScheduledTask -TaskName "NetFlowAudit-Daemon" -Action $Action -Trigger $Trigger -Principal $Principal -Settings $Settings
Start-ScheduledTask -TaskName "NetFlowAudit-Daemon"
```

---

## 6. Configuration File Reference (`netflowaudit.json`)

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

## 7. Practical Recipes

### Recipe 1: Verbose IP Diagnostic & Resolution Analysis
```bash
NetFlowAudit.Cli --test-ip 34.120.255.184
```

### Recipe 2: Normalized Firewall Rule Matrix Export
```bash
NetFlowAudit.Cli --export-rules firewall_rules.csv -i netflow_audit.csv \
  --destination-zone internal \
  --exclude-ephemeral \
  --min-observations 5 \
  --active-within-days 30
```

### Recipe 3: Multi-Cache Consolidation in Air-Gapped Networks
```powershell
.\NetFlowAudit.Cli.exe --resolve-offline `
  -i ".\audits\*.csv" `
  --caches ".\caches\*.json" `
  --output-cache master_dns_cache.json `
  --in-place
```

### Recipe 4: Listening Services Inventory (Listeners Only)
```bash
NetFlowAudit.Cli --map-only -i netflow_audit.csv --listeners-only --map listening_services.html
```

### Recipe 5: Core Business Services Map (Web, DB, Cache)
```bash
NetFlowAudit.Cli --map-only -i netflow_audit.csv \
  --include-ports 80,443,1433,5432,3306,6379,8080,8443 \
  --exclude-ports 53,123,135-139,445 \
  --drawio tier_topology.drawio
```

---

## 8. Troubleshooting DNS & RDAP Resolution

### Why Does `ping -a` Resolve an IP While .NET DNS Fails?

When running `ping -a <IP>` on a Windows corporate workstation, the Windows operating system performs name resolution using multiple successive protocols:
1. **Local DNS Cache & Hosts file** (`%SystemRoot%\System32\drivers\etc\hosts`).
2. **NetBIOS over TCP/IP (NBT)** and **LLMNR (Link-Local Multicast Name Resolution)** broadcast queries.
3. **Primary and Secondary DNS servers** using the network adapter's **DNS Suffix Search List** (e.g. `corp.internal.contoso.com`).
4. **Active Directory WINS servers**.

In contrast, `.NET`'s `Dns.GetHostEntryAsync` performs a strict standard **DNS PTR query** (`x.x.x.x.in-addr.arpa`) against the configured DNS resolver:
* If the corporate internal DNS zones do not maintain in-addr.arpa reverse lookup zones, or if the DNS server returns `NXDOMAIN`, .NET raises a `SocketException (HostNotFound)`.
* If the endpoint has a NetBIOS name but no DNS PTR record, `ping -a` succeeds via NetBIOS while .NET PTR fails.
* **Solution**:
  1. Run `NetFlowAudit.Cli --test-ip <IP>` to see the exact OS DNS suffix, queried servers, and socket error codes.
  2. Map unresolvable internal servers in `"CustomHostAliases"` (`netflowaudit.json`) or via `--alias <IP>=<HostName>`.

### Unspecified and Non-Routable Addresses (`0.0.0.0`, `::`, `255.255.255.255`)
In NetFlow and socket table traces, passive listening sockets or unrouted broadcast flows report destination addresses like `0.0.0.0`, `::`, or `255.255.255.255`.
* NetFlowAudit intercepts these addresses before invoking `Dns.GetHostEntryAsync` to prevent unhandled runtime exceptions.
* They are assigned `Source = "Excluded"` and skipped during remote resolution.

### Missing Organization for Public External Addresses
* Reverse DNS PTR records provide the host FQDN (e.g. `184.255.120.34.bc.googleusercontent.com`) but do not include autonomous system or organization metadata.
* NetFlowAudit automatically supplements successful DNS lookups with an RDAP query for public routable IP blocks to populate `Organization`, `Country`, and `NetworkRange`.

---

## 9. Keyboard Shortcuts (Interactive TUI)

| Keybinding | Action |
|---|---|
| `[Q]` | Graceful shutdown, flush pending writes, and exit. |
| `[C]` | Clear console buffer and force complete screen redraw. |
| `[V]` | Toggle view mode: **Audit Log Discoveries** vs **Active Sockets Snapshot**. |
| `[M]` | Instantly generate/refresh the HTML topology map (`netflow_topology.html`). |
| `[N]` or `[PageDown]` | Navigate to the **Next** page of flow discoveries. |
| `[P]` or `[PageUp]` | Navigate to the **Previous** page of flow discoveries. |
| `[L]` or `[+]` | Load **+100 additional flow rows** into the paginated buffer. |
| `[A]` | Load **all discovered flows** into the paginated buffer. |
| `[↑ / ↓ / ← / →]` | Navigate rows and columns on the active page. |
| `[Enter]` or `[X]` | Exclude the selected Local Port, Remote IP, Process, or Target. |
