> **Language / Langue**: **English** · [Français](output-formats.fr.md)

# Output formats reference

## CSV audit log

Written by `audit` (`-o`, `OutputCsvPath`). Fields follow RFC 4180 quoting; the separator is `CsvDelimiter` (`,` by default). One line per deduplicated flow. A flow is identified by its protocol, its direction, its application (`TargetExecutable`, or `ExecutablePath` when there is no target) and:

| Direction | Also identified by | Ignored |
| :--- | :--- | :--- |
| `Outbound` | remote address and remote port | local (client) port |
| `Inbound` | local port and remote address | remote (client) port |
| `Listening` | local port | addresses |

Repeated connections to the same service therefore update one line instead of adding new ones.

`--columns` and `CsvColumns` choose and order the columns; without them, the 20 columns below are written in this order.

| Column | Category | Content |
| :--- | :--- | :--- |
| `FirstSeenUtc` | Timestamp | UTC date and time when the flow was first observed |
| `LastSeenUtc` | Timestamp | UTC date and time when the flow was last observed |
| `ObservationCount` | Metrics | Number of scans in which the flow was seen |
| `ServerName` | Server | Name of the audited machine |
| `ServerOsVersion` | Server | Operating system description of the audited machine |
| `ServerPrimaryIp` | Server | Primary IP address of the audited machine |
| `Protocol` | Network | `Tcp` or `Udp` |
| `Direction` | Network | `Listening`, `Inbound` or `Outbound` |
| `TcpState` | Network | TCP state (`Established`, `Listen`, `TimeWait`, `CloseWait`…) |
| `LocalIp` | Network | Local address of the socket |
| `LocalPort` | Network | Local port of the socket |
| `RemoteIp` | Network | Remote address (`0.0.0.0` for a listener) |
| `RemotePort` | Network | Remote port (`0` for a listener) |
| `RemoteHostName` | Network | Name from an alias, reverse DNS or the RDAP owner |
| `ProcessId` | Process | Process identifier |
| `ProcessName` | Process | Executable name of the process |
| `ExecutablePath` | Process | Full path of the executable; empty when the process cannot be opened |
| `CommandLine` | Process | Command line of the process |
| `TargetExecutable` | Process | Application started by a launcher (`.jar`, `.py`, `.ps1`, `.sh`, `.dll`…) |
| `UserName` | Process | Account of the process: `DOMAIN\account` on Windows (the SID when the account has no name any more), the user name on Linux (the numeric UID when it is not in `/etc/passwd`); empty when the process cannot be opened |

Processes with identifier 0 or below show `SYSTEM`.

At startup, `audit` reads the existing CSV log (`--reload-history`, `ReloadHistoryLimit`) so that counts and first-seen dates continue across restarts.

## NDJSON

Written by `audit --ndjson` and by `export ndjson`. One JSON object per line, with the same 20 values as the CSV log under camel-case names:

`firstSeenUtc`, `lastSeenUtc`, `observationCount`, `serverName`, `serverOsVersion`, `serverPrimaryIp`, `protocol`, `direction`, `tcpState`, `localIp`, `localPort`, `remoteIp`, `remotePort`, `remoteHostName`, `processId`, `processName`, `executablePath`, `commandLine`, `targetExecutable`, `userName`.

`export rules` and `resolve` read NDJSON files (`.ndjson`, `.jsonl`) as well as CSV files.

## Firewall rule matrix

Written by `export rules`. One row per direction, source address, destination address, destination port, protocol and process, sorted by direction, zone, port, destination and source.

| Column | Content |
| :--- | :--- |
| `RuleId` | `FLUX-001`, `FLUX-002`… in sort order |
| `Direction` | `Outbound` or `Inbound` |
| `SourceIp` | Client address; `0.0.0.0` for a listener |
| `SourceHost` | Client name: the audited server for an outbound flow, the remote name for an inbound flow, `Any` for a listener |
| `DestinationIp` | Server address |
| `DestinationHost` | Server name |
| `DestinationZone` | `Internal` (loopback, private range, `0.0.0.0`, `::`) or `Public` |
| `Protocol` | `TCP` or `UDP` |
| `DestinationPort` | Service port |
| `ServiceName` | Name deduced from the port and process (for example `Oracle-TNS` for 1521) |
| `ProcessName` | Process owning the socket |
| `ExecutablePath` | Path of that process |
| `UserName` | Account of that process, as in the audit log |
| `ObservationCount` | Sum of the observation counts of the merged flows |
| `FirstSeenUtc` | Earliest first observation |
| `LastSeenUtc` | Latest observation |

## Pair matrix

Written by `export rules --pairs-output`, from the rows of the firewall rule matrix after filtering. One row per source address and destination address: the two directions between two servers stay on two rows, as firewall rules do. Sorted by source then destination address.

| Column | Content |
| :--- | :--- |
| `PairId` | `PAIR-001`, `PAIR-002`… in sort order |
| `SourceIp`, `SourceHost` | Client address and name, as in the rule matrix |
| `DestinationIp`, `DestinationHost` | Server address and name, as in the rule matrix |
| `FlowCount` | Number of rule matrix rows merged into the pair |
| `Ports` | Distinct destination ports, `port/protocol`, sorted by port (`443/TCP, 873/TCP`) |
| `ObservationCount` | Sum of the observation counts of the merged rows |
| `FirstSeenUtc` | Earliest first observation |
| `LastSeenUtc` | Latest observation |

## Topology map

`map --html` and `audit --html` write a single HTML file that opens without network access. The page offers layout, sorting, routing and spacing choices, text filters applied with the `Apply Filters` button or `Enter`, pagination of servers, an SVG export, and a `CLI Command` section showing the `PortAtlas map` command that reproduces the current filters and layout choices. The copied command contains no `-i` option: add the audit logs to read.

`map --drawio` writes an uncompressed Draw.io file (`.drawio`) for Draw.io Desktop or diagrams.net.

## DNS cache

`netflow_dns_cache.json` (`DnsCacheFilePath`) maps each resolved address to its name and to where the name came from, for example:

```json
{ "198.51.100.20": { "HostName": "app.example.com", "Source": "DNS" } }
```

`audit` reads and updates it; `resolve` merges several of them; `lookup` reads it.
