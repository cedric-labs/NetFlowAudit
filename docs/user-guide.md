# NetFlowAudit - User Guide & Production Operations

## 1. Installation & Distribution

NetFlowAudit is packaged as standalone, self-contained, single-file executables for **x64 Windows** and **x64 Linux**. The .NET 8 runtime is embedded inside the binary; no runtime or SDK installation is required on target servers.

### 1.1. Download Pre-Built Binaries
Pre-compiled packages and standalone executables are published on GitHub Releases:
- **GitHub Release**: [NetFlowAudit v1.0.0](https://github.com/curvecoder/NetFlowAudit/releases/tag/v1.0.0)

| OS / Architecture | Artifact | Usage |
|---|---|---|
| **Windows x64** | `NetFlowAudit-1.0.0-win-x64.zip` | Archive containing `NetFlowAudit.Cli.exe` and default `netflowaudit.json`. |
| **Windows x64 (Direct)** | `NetFlowAudit-1.0.0-win-x64.exe` | Single standalone `.exe` without archive extraction. |
| **Linux x64** | `NetFlowAudit-1.0.0-linux-x64.tar.gz` | Archive containing executable binary and baseline configuration. |
| **Linux x64 (Direct)** | `NetFlowAudit-1.0.0-linux-x64` | Single standalone ELF executable. |

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

## 2. Operational Modes

NetFlowAudit supports two execution paradigms:

### 2.1. Interactive Terminal Dashboard (TUI)
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

### 2.2. Headless Daemon / Background Service
Designed for 24/7 background auditing, data center fleet monitoring, and automated SIEM ingestion:
```bash
# Windows
.\NetFlowAudit.Cli.exe --daemon --output "C:\Logs\netflow_audit_{ServerName}.csv"

# Linux
sudo ./NetFlowAudit.Cli --no-tui --output "/var/log/netflow/netflow_{ServerName}.csv"
```
In headless mode, the TUI is disabled, and the agent logs standard startup/shutdown banners to standard output while quietly persisting deduplicated flows to the CSV file.

---

## 3. Production Deployment

### 3.1. Windows Service Deployment

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

# Or direct download from https://nssm.cc/download:
# Extract 'win64/nssm.exe' to C:\Windows\System32 (or any directory in your PATH)
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

:: 2. Start the service
net start NetFlowAudit
```

#### Option B: Using Windows Task Scheduler
```powershell
$Action = New-ScheduledTaskAction -Execute "C:\Program Files\NetFlowAudit\NetFlowAudit.Cli.exe" -Argument "--daemon --config netflowaudit.json" -WorkingDirectory "C:\Program Files\NetFlowAudit"
$Trigger = New-ScheduledTaskTrigger -AtStartup
$Principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName "NetFlowAudit" -Action $Action -Trigger $Trigger -Principal $Principal -Settings $Settings
```

### 3.2. Linux Systemd Service Deployment

Create `/etc/systemd/system/netflowaudit.service`:
```ini
[Unit]
Description=NetFlowAudit Continuous Network Flow Auditing Agent
After=network.target

[Service]
Type=simple
ExecStart=/opt/netflowaudit/NetFlowAudit.Cli --no-tui --config /opt/netflowaudit/netflowaudit.json
WorkingDirectory=/opt/netflowaudit
Restart=always
RestartSec=5
User=root

# Security sandboxing
ProtectSystem=full
ProtectHome=true
NoNewPrivileges=true
CapabilityBoundingSet=CAP_NET_ADMIN

[Install]
WantedBy=multi-user.target
```

Enable and start the daemon:
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now netflowaudit
sudo systemctl status netflowaudit
```

---

## 4. Configuration Reference (`netflowaudit.json`)

To generate a baseline template configuration file:
```bash
NetFlowAudit.Cli --init-config
# Or output to a custom path:
NetFlowAudit.Cli --init-config /etc/netflowaudit/config.json
```

### Complete Configuration Schema

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
  "ReloadHistoryLimit": null,
  "ResolveRemoteHostNames": true,
  "ResolveIpOwners": true,
  "IsolatedMode": false,
  "CustomHostAliases": {
    "192.168.1.10": "storage.corp.internal",
    "10.0.0.1": "gw-core.corp.internal"
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
  "ExcludedRemoteIps": [ "10.0.0.0/8" ],
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

### Key Parameter Explanations

- **`ScanIntervalSeconds`**: Frequency of socket table queries (default: `5`).
- **`ReloadHistoryLimit`**: Number of historical CSV lines loaded at startup to preserve continuity across restarts:
  - `null` (or omitted): Loads all historical rows.
  - `0`: Disables history reloading (starts fresh).
  - `1000`: Loads the first 1,000 historical rows.
- **`DnsExcludedNetworks`**: CIDR ranges skipped during reverse DNS queries to eliminate pointless DNS timeouts on broadcast, link-local, or multicast addresses.
- **`ResolveIpOwners`**: When reverse DNS PTR lookup fails, queries RDAP (Regional Internet Registries) to resolve autonomous system ownership (e.g. `AMAZON-02`, `MICROSOFT-CORP`).
- **`CustomHostAliases`**: Explicit key-value mapping of IP addresses to hostnames. Always takes precedence over DNS.

---

## 5. Datacenter Fleet Auditing & Map Consolidation

When auditing an entire cluster or fleet of servers, configure each agent to write to a central shared directory using dynamic path tokens:

### 5.1. Dynamic Path Tokens
In `netflowaudit.json` or via command-line arguments:
- `{ServerName}`: Local machine hostname.
- `{HostName}`: FQDN or short computer name.
- `{Date}`: Current UTC date in `yyyyMMdd` format.
- `{Timestamp}`: UTC timestamp in `yyyyMMdd_HHmmss` format.

**Example:**
```json
{
  "OutputCsvPath": "\\\\fileshare\\audits\\{Date}\\netflow_{ServerName}.csv"
}
```

### 5.2. Consolidating Multi-Server CSVs into a Unified Map
Once multiple server audit logs have been gathered, use `--map-only` with `--dir` or `--inputs` to synthesize a global topology map:

```powershell
# Synthesize all CSVs from a directory into one unified HTML5 map
.\NetFlowAudit.Cli.exe --map-only `
  --dir "\\fileshare\audits\20261024" `
  --pattern "*.csv" `
  --map "\\fileshare\audits\datacenter_topology.html" `
  --drawio "\\fileshare\audits\datacenter_topology.drawio" `
  --drawio-edge-mode adaptive `
  --drawio-theme dark `
  --exclude-ports 53,123,135-139,445
```

### 5.3. Exporting to Draw.io & Managing Large Networks

When exporting raw audit captures containing outbound traffic to public clouds, CDNs, or hundreds of external subnets, diagrams can expand significantly along the horizontal axis. Use the following recipes to produce concise, readable Draw.io diagrams:

#### Recipe 1: Restrict to Internal Corporate Subnets (Eliminate Internet Sprawl)
Filter out external public IP ranges to focus strictly on corporate infrastructure:
```powershell
.\NetFlowAudit.Cli.exe --export-drawio internal_network.drawio -i netflow_audit.csv `
  --include-ips 10.0.0.0/8,172.16.0.0/12,192.168.0.0/16
```

#### Recipe 2: Critical Tier Services & Infrastructure Filtering
Map only core business services (Web, DB, Cache) while suppressing background network noise (DNS, NTP, NetBIOS, SMB):
```powershell
.\NetFlowAudit.Cli.exe --export-drawio tier_topology.drawio -i netflow_audit.csv `
  --include-ports 80,443,1433,5432,3306,6379,8080,8443 `
  --exclude-ports 53,123,135-139,445
```

#### Recipe 3: Simplified High-Level Flow Arrows (By Direction)
Collapse multiple parallel port arrows between servers into a single bidirectional flow arrow with port badge labels:
```powershell
.\NetFlowAudit.Cli.exe --export-drawio high_level.drawio -i netflow_audit.csv `
  --drawio-edge-mode direction
```

#### Recipe 4: Listening Services Inventory (Listeners Only)
Map only passive services and daemon listeners without transient client outbound sockets:
```powershell
.\NetFlowAudit.Cli.exe --export-drawio listeners.drawio -i netflow_audit.csv `
  --listeners-only
```

> [!NOTE]
> **Readability & Layout Roadmap**:
> In the current release, Draw.io subnets are laid out along a single horizontal sequence. Future releases will introduce an automatic 2D multi-line wrapping grid layout for subnets.
>
> In the meantime, to reorganize an oversized diagram in Draw.io Desktop or Web in one click:
> 1. Select all elements (`Ctrl + A`).
> 2. Open menu **Arrange > Layout > Hierarchical** (or **Tree / Organic**).
> 3. Draw.io will instantly recompact all swimlanes and nodes into a balanced rectangular layout.

---

## 6. Air-Gapped & Secure Environment Operations

In isolated networks with no Internet or internal DNS reachability:

1. **Activate Isolated Mode**:
   ```powershell
   .\NetFlowAudit.Cli.exe --offline --output "netflow_audit.csv"
   ```
   Or set `"IsolatedMode": true` in `netflowaudit.json`. This completely disables PTR and RDAP queries; NetFlowAudit generates **zero network packets**.
2. **Pre-Seed Static Aliases**:
   Add critical internal IPs to `"CustomHostAliases"` in `netflowaudit.json`:
   ```json
   "CustomHostAliases": {
     "10.10.1.5": "db-primary.prod",
     "10.10.1.6": "db-replica.prod"
   }
   ```
3. **Pre-Seed Persistent DNS Cache**:
   Populate `netflow_dns_cache.json` with pre-resolved entries. NetFlowAudit will read the cache directly from disk without performing network queries.
4. **Post-Process Audits with Offline Resolution (`--resolve-offline`)**:
   Retrieve audit files and local cache files from isolated servers, then run offline consolidation and enrichment on a connected machine:
   ```powershell
   .\NetFlowAudit.Cli.exe --resolve-offline -i ".\audits\*.csv" --caches ".\caches\*.json" --output-cache master_cache.json
   ```
   The consolidated `master_cache.json` can be redistributed back to the isolated servers for future scans.

---

## 7. Practical Recipes

### Recipe 1: Listening Services Discovery
To discover what services are listening for incoming traffic across your machines without showing outbound client connections:
```bash
NetFlowAudit.Cli --map-only -i netflow_audit.csv --listeners-only --map listening_services.html
```

### Recipe 2: Isolating Database Architecture
To visualize only SQL Server (1433), PostgreSQL (5432), and MySQL (3306) traffic:
```bash
NetFlowAudit.Cli --map-only -i netflow_audit.csv --include-ports 1433,5432,3306 --map db_topology.html
```

### Recipe 3: Auditing Application Runtimes
NetFlowAudit automatically inspects command lines to unwrap scripts and runtime targets. To filter for specific runtimes:
```bash
NetFlowAudit.Cli --map-only -i netflow_audit.csv --include-processes java,python,dotnet,node --map runtimes.html
```

### Recipe 4: Masking Data for Documentation or External Audits
To generate reports or screenshots without exposing genuine server hostnames or IP addresses:
```bash
# Obfuscates hostname to 'SRV-AUDIT-01' and primary IP to '10.0.0.10'
NetFlowAudit.Cli --obfuscate
```

### Recipe 5: Generating Firewall Change Ticket Matrices
To consolidate raw connection history into a deduplicated firewall rule matrix for network administrators, excluding ephemeral destination ports and transient test flows:
```bash
NetFlowAudit.Cli --export-rules firewall_rules.csv -i netflow_audit.csv --exclude-ephemeral --min-observations 5 --active-within-days 14
```

---

## 8. Keyboard Shortcuts (Interactive TUI)

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
