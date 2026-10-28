> **Language / Langue**: **English** · [Français](configuration.fr.md)

# Configuration file reference

NetFlowAudit reads a JSON file: the one given by `--config`, otherwise `netflowaudit.json` in the working directory, otherwise `netflowaudit.json` next to the executable. A missing key takes its default value; an unknown key is ignored. A file that cannot be read or parsed prints a warning and the defaults are used.

`NetFlowAudit.Cli config init` writes a file holding every key with its default value. Command-line options take precedence over the file ([CLI reference](cli.md#sources-of-values)).

Paths accept the [file name tokens](cli.md#file-name-tokens) `{ServerName}`, `{HostName}`, `{Date}` and `{Timestamp}`.

## Audit

| Key | Type | Default | Effect | Option |
| :--- | :--- | :--- | :--- | :--- |
| `ScanIntervalSeconds` | integer | `5` | Seconds between two scans | `--interval` |
| `OutputCsvPath` | path | `netflow_audit.csv` | CSV audit log; default input of the other commands | `-o` (`audit`) |
| `OutputNdjsonPath` | path | `""` | NDJSON log written alongside the CSV log; empty for none | `--ndjson` |
| `CsvColumns` | list of names | `[]` | Ordered CSV columns; empty for the 20 columns | `--columns` |
| `CsvDelimiter` | string | `","` | CSV field separator (first character used) | none |
| `ReloadHistoryLimit` | integer or `null` | `null` | Flows reloaded from the CSV log at startup; `null` for all, `0` for none | `--reload-history` |
| `EnableDashboard` | boolean | `true` | Terminal dashboard | `--no-tui` |
| `MaskServerName` | string | `""` | Server name shown by the dashboard | `--mask-server` |
| `MaskLocalIp` | string | `""` | Local address shown by the dashboard | `--mask-ip` |

## Recording filters

These keys decide which sockets are recorded by `audit`. The dashboard adds entries to some of them when you exclude a cell ([dashboard reference](dashboard.md#excluding-from-the-dashboard)).

| Key | Type | Default | Effect | Option |
| :--- | :--- | :--- | :--- | :--- |
| `ExcludeLoopbackFlows` | boolean | `true` | Skip traffic that stays on the machine: sockets bound to a loopback address (listeners included), flows to a loopback address, and flows between two addresses of the machine. Listeners on other addresses are kept. | none |
| `ExcludedLocalPorts` | list of integers | `[]` | Local ports not recorded | none |
| `ExcludedRemotePorts` | list of integers | `[]` | Remote ports not recorded | none |
| `ExcludedRemoteIps` | list of addresses or CIDR | `[]` | Remote addresses not recorded | `--exclude-remote-ips` |
| `ExcludedPids` | list of integers | `[0, 4]` | Process identifiers not recorded | none |
| `ExcludedExactNames` | list of names | `["System", "Idle"]` | Process names not recorded (whole name, case ignored) | none |
| `ExcludedPrefixes` | list of strings | `[]` | Process names starting with one of these values (case ignored) | none |
| `ExcludedSuffixes` | list of strings | `[]` | Process names ending with one of these values (case ignored) | none |
| `ExcludedContains` | list of strings | `[]` | Process names containing one of these values | none |
| `ExcludeSystemAccounts` | boolean | `false` | Skip processes of system accounts (see the note below) | none |

`ExcludeSystemAccounts` skips processes whose account contains `NT AUTHORITY` or equals `SYSTEM` or `root`. Most services run under these accounts, hence the `false` default. A process whose account cannot be read (protected process, insufficient rights) has an empty account and is not skipped by this filter.

## Name resolution

| Key | Type | Default | Effect | Option |
| :--- | :--- | :--- | :--- | :--- |
| `ResolveRemoteHostNames` | boolean | `true` | Name resolution of remote addresses; `false` also disables RDAP and aliases | `--no-dns` |
| `ResolveIpOwners` | boolean | `true` | RDAP query for the owner of public addresses, used as name when there is no DNS name | `--no-rdap` |
| `DnsCacheFilePath` | path | `netflow_dns_cache.json` | Persistent DNS cache; default cache of `resolve` and `lookup` | `--cache`, `--output-cache` (`resolve`) |
| `DnsExcludedNetworks` | list of addresses or CIDR | `["0.0.0.0", "255.255.255.255", "169.254.0.0/16", "224.0.0.0/4", "fe80::/10", "ff00::/8"]` | Addresses never resolved | none |
| `CustomHostAliases` | object `{ "ip": "name" }` | `{}` | Names used instead of reverse DNS | `--alias` |

## Topology map and rule matrix

These keys are read by the map of `audit`, by `map`, and for the filters by `export rules`.

| Key | Type | Default | Option |
| :--- | :--- | :--- | :--- |
| `OutputHtmlMapPath` | path | `""` | `--html` |
| `OutputDrawioPath` | path | `""` | `--drawio` |
| `MapIncludedPorts` | list of ports or ranges | `[]` | `--include-ports` |
| `MapExcludedPorts` | list of ports or ranges | `[]` | `--exclude-ports` |
| `MapIncludedHosts` | list of patterns | `[]` | `--include-hosts` |
| `MapExcludedHosts` | list of patterns | `[]` | `--exclude-hosts` |
| `MapIncludedIps` | list of addresses or CIDR | `[]` | `--include-ips` |
| `MapExcludedIps` | list of addresses or CIDR | `[]` | `--exclude-ips` |
| `MapIncludedProcesses` | list of names | `[]` | `--include-processes` |
| `MapExcludedProcesses` | list of names | `[]` | `--exclude-processes` |
| `MapDirection` | `all`, `in`, `out` | `all` | `--direction` |
| `MapListenersOnly` | boolean | `false` | `--listeners-only` |
| `MapExcludeEphemeralPorts` | boolean | `true` | `--include-ephemeral-ports` (map only) |
| `MapLayoutMode` | see `--layout` | `layered` | `--layout` |
| `MapSortMethod` | see `--sort` | `barycentric` | `--sort` |
| `MapRoutingMode` | see `--routing` | `bezier` | `--routing` |
| `MapSpacingProfile` | see `--spacing` | `balanced` | `--spacing` |
| `DrawioEdgeMode` | `adaptive`, `direction`, `none` | `adaptive` | `--drawio-edges` |
| `DrawioTheme` | `dark`, `light` | `dark` | `--drawio-theme` |

The topology map (not the rule matrix) also removes the ports of `ExcludedLocalPorts` and `ExcludedRemotePorts` and the addresses of `ExcludedRemoteIps`.

Values of these keys are not checked when the file is read: an unknown layout, sort, routing, spacing, edge mode or theme falls back to the default. The command-line options reject unknown values.

## Removed key

| Key | Removed in | Replacement |
| :--- | :--- | :--- |
| `IsolatedMode` | 2.0.0 | `"ResolveIpOwners": false`. A file with `"IsolatedMode": true` stops every command with an error; `false` prints a warning. |
