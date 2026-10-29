> **Language / Langue**: **English** · [Français](export-firewall-rules.fr.md)

# Export a firewall rule matrix

Goal: a CSV list of the flows to allow, one row per source, destination, port and process, to prepare firewall rules.

## Export the observed flows

```bash
PortAtlas export rules -i collected -o firewall_rules.csv
```

`-i` reads CSV and NDJSON logs (a folder, a pattern or files). Ephemeral destination ports (49152 and above) are left out.

## Remove noise

Keep flows seen at least five times during the last 14 days, towards internal addresses, without DNS and NTP:

```bash
PortAtlas export rules -i collected -o internal_rules.csv --min-observations 5 --active-within-days 14 --zone internal --exclude-ports 53,123
```

`--zone public` keeps only destinations outside the private ranges, for example to list the Internet services a server needs.

## List the services offered

```bash
PortAtlas export rules -i collected -o services.csv --listeners-only
```

Each listening socket becomes an inbound rule from any source (`0.0.0.0`, host `Any`) to its address and port: the starting point of an inbound rule set.

## Read the result

The command prints the number of files read, flows scanned and rules written. Each row carries the process, its path and the observation dates, so that each rule can be justified ([column list](../reference/output-formats.md#firewall-rule-matrix)).
