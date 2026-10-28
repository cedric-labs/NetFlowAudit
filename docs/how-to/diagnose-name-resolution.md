> **Language / Langue**: **English** · [Français](diagnose-name-resolution.fr.md)

# Diagnose a missing host name

Goal: understand why an address appears without a name in the audit, and fix it.

## Run the diagnostic

```bash
NetFlowAudit.Cli lookup 192.0.2.25
```

The report has six steps: address classification, local cache entry, DNS settings of the operating system, reverse DNS query with its duration and error, RDAP query (public addresses only), and the resulting cache entry.

## Why ping -a finds a name and NetFlowAudit does not

On Windows, `ping -a` asks the hosts file, the DNS cache, NetBIOS, LLMNR and the DNS servers with the domain suffixes. NetFlowAudit sends only a reverse DNS query (a `PTR` record in `in-addr.arpa`). A machine known through NetBIOS or LLMNR but without a `PTR` record therefore stays unnamed. The reverse DNS step of `lookup` shows the error returned.

## Fix it

- Ask for the missing `PTR` record in the reverse DNS zone.
- Or declare the name in the configuration file:

  ```json
  {
    "CustomHostAliases": { "192.0.2.25": "srv-db-01" }
  }
  ```

  or on the command line: `NetFlowAudit.Cli audit --alias 192.0.2.25=srv-db-01`.

Aliases take precedence over DNS. Addresses that are never resolved (broadcast, multicast, link-local) are listed in `DnsExcludedNetworks`.
