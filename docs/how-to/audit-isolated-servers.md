> **Language / Langue**: **English** · [Français](audit-isolated-servers.fr.md)

# Audit servers without Internet access

Goal: audit servers that cannot reach the Internet, then name their remote addresses from a machine with Internet access.

## On each isolated server

Run the audit without RDAP queries, which would only time out:

```bash
PortAtlas audit --no-tui --no-rdap
```

Or set `"ResolveIpOwners": false` in the configuration file. Reverse DNS stays active and names the addresses known to the local DNS server. To send no name query at all, use `--no-dns` instead.

Collect the audit logs and the DNS cache files (`netflow_dns_cache.json`, or `DnsCacheFilePath`) of each server.

## On a machine with Internet access

Merge the caches, resolve the addresses that are still unnamed and write enriched copies of the logs:

```bash
PortAtlas resolve -i "collected/*.csv" --cache "collected/*.json" --output-cache master_cache.json
```

Each `audit_x.csv` gets an `audit_x_enriched.csv`; `--in-place` overwrites the originals instead. RDAP queries are spaced by 200 ms (`--rdap-delay-ms`).

## Without any network access

To use only the collected caches, with no DNS or RDAP query:

```bash
PortAtlas resolve -i "collected/*.csv" --cache master_cache.json --cache-only
```

## Upgrading from 1.x

The 1.x isolated mode no longer exists. A configuration file with `"IsolatedMode": true` stops PortAtlas with an error until the key is replaced by `"ResolveIpOwners": false` ([upgrade guide](upgrade-to-2.0.md)).
