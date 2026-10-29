> **Language / Langue**: **English** · [Français](cli.fr.md)

# Command-line reference

PortAtlas is a set of commands. Each command reads only its own options: an option given to a command that does not read it, an unknown option or a value outside the accepted list stops the program with an error and exit code 1.

```text
PortAtlas <command> [options]
```

| Command | Purpose |
| :--- | :--- |
| [`audit`](#audit) | Scan sockets continuously and record deduplicated flows |
| [`map`](#map) | Render an HTML topology map and/or a Draw.io diagram from audit logs |
| [`export rules`](#export-rules) | Consolidate audit logs into a firewall rule matrix |
| [`export ndjson`](#export-ndjson) | Convert CSV audit logs into one NDJSON file |
| [`resolve`](#resolve) | Merge DNS caches and write host names into audit logs |
| [`lookup`](#lookup) | Diagnose the name resolution of one IP address |
| [`config init`](#config-init) | Write a configuration file with the default values |

Without a command, PortAtlas prints the help and exits with code 0. `-h` or `--help` after any command prints the help of that command.

## Sources of values

A value comes from, in order of precedence:

1. the command line;
2. the configuration file given by `--config`, or `portatlas.json` in the working directory, or `portatlas.json` next to the executable;
3. the default built into the program.

The key of the configuration file matching each option is listed in the tables below and described in the [configuration reference](configuration.md).

## Global option

| Option | Value | Default | Effect |
| :--- | :--- | :--- | :--- |
| `--config` | `<path>` | `portatlas.json` | Configuration file read by every command |
| `--version` | none | | Prints the version and exits (root command only) |

## File name tokens

Output paths, and the paths of the configuration file, accept these tokens:

| Token | Replaced by |
| :--- | :--- |
| `{ServerName}` | Name of the machine |
| `{HostName}` | Name of the machine (same value as `{ServerName}`) |
| `{Date}` | UTC date, `yyyy-MM-dd` |
| `{Timestamp}` | UTC date and time, `yyyyMMdd_HHmmss` |

## Shared option families

### Inputs

Read by `map`, `export rules`, `export ndjson` and `resolve`.

| Option | Value | Default | Effect |
| :--- | :--- | :--- | :--- |
| `-i`, `--input` | `<path>`, repeatable | `OutputCsvPath` of the configuration | A file, a directory or a wildcard pattern on the file name (`audits/nas-*.csv`) |

A directory contributes its files with the extensions read by the command (`.csv` for `map` and `export ndjson`; `.csv`, `.ndjson`, `.jsonl` for `export rules` and `resolve`). Subdirectories are not read. When no file matches, the command prints `No audit log found for: …` and exits with code 1.

### Flow filters

Read by `audit` (for its map), `map` and `export rules`. List values are separated by commas; a list option can be repeated and the values add up.

| Option | Value | Configuration key | Effect |
| :--- | :--- | :--- | :--- |
| `--include-ports` | ports or ranges | `MapIncludedPorts` | Keep only these service ports (`80,443,8000-8080`) |
| `--exclude-ports` | ports or ranges | `MapExcludedPorts` | Remove these service ports |
| `--include-hosts` | wildcard patterns | `MapIncludedHosts` | Keep only servers whose name matches (`nas*,*.home.arpa`) |
| `--exclude-hosts` | wildcard patterns | `MapExcludedHosts` | Remove servers whose name matches |
| `--include-ips` | addresses or CIDR | `MapIncludedIps` | Keep only these addresses (`10.0.0.0/8`) |
| `--exclude-ips` | addresses or CIDR | `MapExcludedIps` | Remove these addresses |
| `--include-processes` | names | `MapIncludedProcesses` | Keep only these process names or application targets |
| `--exclude-processes` | names | `MapExcludedProcesses` | Remove these process names or application targets |
| `--direction` | `all`, `in`, `out` | `MapDirection` | Flow direction to keep; default `all` |
| `--listeners-only` | none | `MapListenersOnly` | Keep only listening sockets |
| `--include-ephemeral-ports` | none | `MapExcludeEphemeralPorts` | Keep ports 49152-65535, removed by default |

### Map rendering

Read by `audit` and `map`. Accepted values are compared without case.

| Option | Value | Configuration key | Default |
| :--- | :--- | :--- | :--- |
| `--html` | `<path>` | `OutputHtmlMapPath` | none |
| `--drawio` | `<path>` | `OutputDrawioPath` | none |
| `--layout` | `layered`, `tree`, `horizontal-tree`, `organic`, `circular`, `grid` | `MapLayoutMode` | `layered` |
| `--sort` | `barycentric`, `tiers`, `subnet`, `degree`, `alpha`, `ip` | `MapSortMethod` | `barycentric` |
| `--routing` | `bezier`, `orthogonal`, `straight`, `bus` | `MapRoutingMode` | `bezier` |
| `--spacing` | `compact`, `balanced`, `spacious` | `MapSpacingProfile` | `balanced` |
| `--edge-labels` | `ports`, `none` | `MapEdgeLabels` | `ports` |
| `--drawio-edges` | `adaptive`, `direction`, `pair`, `none` | `DrawioEdgeMode` | `adaptive` |
| `--drawio-theme` | `dark`, `light` | `DrawioTheme` | `dark` |

`--layout`, `--sort`, `--routing`, `--spacing` and `--edge-labels` set the initial state of the HTML map; the map page can change them.

- `bus` routing: the arrows of a server share one trunk per direction, then split into one branch per target. The branches that reach a server through the same side and in the same direction end under one arrow head. Designed for the `tree` layout.
- `--edge-labels none`: unchecks *Show Ports*. The arrows lose their port labels and the flows of a server pair are merged into one arrow, with a head at each end that receives a flow. The server inspector still lists the ports.
- `--drawio-edges pair`: one edge per server pair in the Draw.io diagram, with two heads when flows run both ways.

## audit

Scans the TCP and UDP sockets of the machine at a fixed interval, attributes each socket to its process and writes one line per deduplicated flow in the CSV audit log. Runs until `Q`, `Ctrl+C` or `SIGTERM`.

```text
PortAtlas audit [options]
```

| Option | Value | Configuration key | Default | Effect |
| :--- | :--- | :--- | :--- | :--- |
| `-o`, `--output` | `<path>` | `OutputCsvPath` | `netflow_audit.csv` | CSV audit log |
| `--ndjson` | `<path>` | `OutputNdjsonPath` | none | NDJSON log written alongside the CSV log |
| `--interval` | seconds, at least 1 | `ScanIntervalSeconds` | `5` | Time between two scans |
| `--columns` | column names | `CsvColumns` | all 20 | Ordered CSV columns ([list](output-formats.md#csv-audit-log)) |
| `--reload-history` | `all` or a number | `ReloadHistoryLimit` | `all` | Flows reloaded from the existing CSV log at startup; `0` disables the reload |
| `--no-tui` | none | `EnableDashboard` | dashboard on | Run without the terminal dashboard |
| `--no-dns` | none | `ResolveRemoteHostNames` | DNS on | No name resolution at all: no reverse DNS, no RDAP, no alias |
| `--no-rdap` | none | `ResolveIpOwners` | RDAP on | No RDAP query to Internet registries; reverse DNS stays active |
| `--dns-timeout-ms` | milliseconds, at least 1 | `DnsResolutionTimeoutMs` | `3000` | Time to wait for a reverse DNS answer |
| `--exclude-remote-ips` | addresses or CIDR | `ExcludedRemoteIps` | none | Remote addresses that are not recorded |
| `--alias` | `ip=host` or `ip:host`, repeatable | `CustomHostAliases` | none | Host name used instead of reverse DNS |
| `--mask-server` | `<name>` | `MaskServerName` | none | Server name shown by the dashboard |
| `--mask-ip` | `<address>` | `MaskLocalIp` | none | Local address shown by the dashboard |
| `--obfuscate` | none | none | off | Shows `HOMELAB-01` and `192.168.1.10` unless `--mask-server` or `--mask-ip` is given |

Families read: flow filters and map rendering. When `--html` (or `OutputHtmlMapPath`) is set, the HTML map is rewritten after every scan; the `M` key writes it on demand, with the Draw.io diagram when `--drawio` (or `OutputDrawioPath`) is set.

The dashboard is not shown when the input or the output of the process is redirected, even without `--no-tui`.

An invalid column name in `--columns` or `CsvColumns` stops the command with `Column configuration error: …` and exit code 1.

## map

Builds the topology graph from CSV audit logs and writes the requested outputs.

```text
PortAtlas map [-i <path>]... [--html <path>] [--drawio <path>] [options]
```

Outputs:

- `--html` writes the HTML map, `--drawio` the Draw.io diagram; both can be given.
- Without `--html` and `--drawio`, the command writes the HTML map to `OutputHtmlMapPath` (or `netflow_topology.html` when the key is empty), plus the Draw.io diagram when `OutputDrawioPath` is set.

Families read: inputs (`.csv`), flow filters, map rendering.

## export rules

Consolidates audit logs into a matrix with one row per source, destination, port, protocol and process.

```text
PortAtlas export rules [-i <path>]... [-o <path>] [options]
```

| Option | Value | Default | Effect |
| :--- | :--- | :--- | :--- |
| `-o`, `--output` | `<path>` | `firewall_rules.csv` | Matrix file ([columns](output-formats.md#firewall-rule-matrix)) |
| `--pairs-output` | `<path>` | not written | Also writes the pair matrix, one row per source and destination ([columns](output-formats.md#pair-matrix)); same tokens as `--output` |
| `--min-observations` | number, at least 1 | `1` | Keep flows observed at least this number of times |
| `--active-within-days` | number, at least 1 | no limit | Keep flows last seen during the last N days |
| `--zone` | `all`, `internal`, `public` | `all` | Destination zone to keep |

Families read: inputs (`.csv`, `.ndjson`, `.jsonl`) and flow filters, with two differences:

- ephemeral destination ports (49152 and above) are removed unless `--include-ephemeral-ports` is given; the `MapExcludeEphemeralPorts` key is not read;
- listening sockets are exported only with `--listeners-only` (or `MapListenersOnly`), as inbound rules from any source (`0.0.0.0`, host `Any`).

A destination is `Internal` when it is a loopback address, an address of a private range, or `0.0.0.0` / `::`; otherwise it is `Public`.

## export ndjson

Converts CSV audit logs into a single NDJSON file, one JSON object per flow.

```text
PortAtlas export ndjson [-i <path>]... [-o <path>]
```

| Option | Value | Default | Effect |
| :--- | :--- | :--- | :--- |
| `-o`, `--output` | `<path>` | `netflow_audit.ndjson` | NDJSON file ([fields](output-formats.md#ndjson)) |

Family read: inputs (`.csv`). Input files are read sequentially and only the current file is held in memory.

## resolve

Merges DNS caches, resolves the addresses that still have no host name and writes enriched copies of the audit logs.

```text
PortAtlas resolve [-i <path>]... [--cache <path>]... [options]
```

| Option | Value | Default | Effect |
| :--- | :--- | :--- | :--- |
| `--cache` | `<path>`, repeatable | `DnsCacheFilePath` | DNS cache to merge: a file, a directory (its `.json` files) or a pattern |
| `--output-cache` | `<path>` | `DnsCacheFilePath` | Merged cache to write |
| `--in-place` | none | off | Overwrite the audit logs instead of writing `<name>_enriched.<ext>` |
| `--force-resolve` | none | off | Replace host names already present in the logs |
| `--rdap-delay-ms` | milliseconds, at least 0 | `200` | Delay between two RDAP queries |
| `--cache-only` | none | off | Send no DNS or RDAP query; use only the merged caches |
| `--dns-timeout-ms` | milliseconds, at least 1 | `DnsResolutionTimeoutMs` | Time to wait for a reverse DNS answer |

Family read: inputs (`.csv`, `.ndjson`, `.jsonl`). When two caches give different names for the same address, the most recent entry is kept and the conflict is printed.

## lookup

Prints a six-step diagnostic of the resolution of one address: classification, local cache, operating system DNS settings, reverse DNS query, RDAP query, resulting cache entry.

```text
PortAtlas lookup <ip> [--dns-timeout-ms <milliseconds>]
```

`<ip>` must be an IPv4 or IPv6 address. The command reads `ExcludedRemoteIps`, `DnsCacheFilePath` and `DnsResolutionTimeoutMs` from the configuration. It sends the reverse DNS query with the same timeout as `audit`, so that a `SUCCESS` here means the audit names the address too, and the RDAP query for public addresses only; `ResolveIpOwners` and `--no-rdap` do not apply to it.

## config init

Writes a configuration file holding the default value of every key, with the 20 CSV columns listed.

```text
PortAtlas config init [<path>]
```

`<path>` is a file or a directory; the default is `portatlas.json` in the working directory. An existing file is overwritten.

## Option matrix

| Option | `audit` | `map` | `export rules` | `export ndjson` | `resolve` | `lookup` | `config init` |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| `--config` | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| `-i`, `--input` | | ✔ | ✔ | ✔ | ✔ | | |
| `-o`, `--output` | ✔ | | ✔ | ✔ | | | |
| Flow filters | ✔ | ✔ | ✔ | | | | |
| Map rendering, `--html`, `--drawio` | ✔ | ✔ | | | | | |
| `--ndjson`, `--interval`, `--columns`, `--reload-history` | ✔ | | | | | | |
| `--no-tui`, `--no-dns`, `--no-rdap` | ✔ | | | | | | |
| `--dns-timeout-ms` | ✔ | | | | ✔ | ✔ | |
| `--exclude-remote-ips`, `--alias` | ✔ | | | | | | |
| `--mask-server`, `--mask-ip`, `--obfuscate` | ✔ | | | | | | |
| `--min-observations`, `--active-within-days`, `--zone`, `--pairs-output` | | | ✔ | | | | |
| `--cache`, `--output-cache`, `--in-place`, `--force-resolve`, `--rdap-delay-ms`, `--cache-only` | | | | | ✔ | | |

`config init` accepts `--config` because the option is global, but does not read it.

## Exit codes

| Code | Meaning |
| :--- | :--- |
| `0` | Success, or help printed |
| `1` | Invalid command line, no audit log found, invalid CSV column name, or a configuration file with `"IsolatedMode": true` |
