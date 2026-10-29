> **Language / Langue**: **English** · [Français](diagnose-name-resolution.fr.md)

# Diagnose a missing host name

Goal: understand why an address appears without a name in the audit, and fix it.

## Run the diagnostic

```bash
PortAtlas lookup 192.0.2.25
```

The report has six steps: address classification, local cache entry, DNS settings of the operating system, reverse DNS query with its duration and error, RDAP query (public addresses only), and the resulting cache entry.

## When the DNS server is slow

Step 4 shows the call duration and the timeout used, the same as the audit (`DnsResolutionTimeoutMs`, 3000 ms by default). If it reports `TIMEOUT`, or a duration close to the timeout, raise the timeout in the configuration file or on the command line:

```bash
PortAtlas audit --dns-timeout-ms 6000
```

Check the new value with `PortAtlas lookup 192.0.2.25 --dns-timeout-ms 6000`. Resolution runs in the background, so a longer wait does not delay the scans. An address whose query timed out is asked again one minute later; an address cached without a name (step 2 shows an empty `HostName` with `Source: None`) is asked again 24 hours after its `Resolved at` date.

## Why ping -a finds a name and PortAtlas does not

On Windows, `ping -a` asks the hosts file, the DNS cache, NetBIOS, LLMNR and the DNS servers with the domain suffixes. PortAtlas sends only a reverse DNS query (a `PTR` record in `in-addr.arpa`). A machine known through NetBIOS or LLMNR but without a `PTR` record therefore stays unnamed. The reverse DNS step of `lookup` shows the error returned.

## Fix it

- Add the missing `PTR` record to the reverse DNS zone (router, Pi-hole…).
- Or declare the name in the configuration file:

  ```json
  {
    "CustomHostAliases": { "192.0.2.25": "nas" }
  }
  ```

  or on the command line: `PortAtlas audit --alias 192.0.2.25=nas`.

Aliases take precedence over DNS. Addresses that are never resolved (broadcast, multicast, link-local) are listed in `DnsExcludedNetworks`.
