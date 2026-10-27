using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Core.Resolution;

namespace NetFlowAudit.Cli.Diagnostics;

/// <summary>
/// Diagnostic reporter executing granular, verbose probes for reverse DNS (PTR) and RDAP registration against a target IP address.
/// </summary>
public sealed class IpResolutionDiagnosticReporter
{
    private static readonly HttpClient RdapHttpClient = CreateRdapHttpClient();

    private static HttpClient CreateRdapHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(3),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };
        var client = new HttpClient(handler, disposeHandler: true);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NetFlowAudit-Diagnostic/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/rdap+json, application/json");
        return client;
    }

    /// <summary>
    /// Executes the complete 6-stage diagnostic pipeline for the target IP address and prints a comprehensive technical report to the console.
    /// </summary>
    /// <param name="targetIpString">The IP string to diagnose.</param>
    /// <param name="config">Application configuration containing cache paths and exclusion rules.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    public async Task RunDiagnosticAsync(
        string targetIpString,
        AppConfiguration config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetIpString);
        ArgumentNullException.ThrowIfNull(config);

        Console.WriteLine("================================================================================");
        Console.WriteLine("           NetFlowAudit - IP & Network Resolution Diagnostic");
        Console.WriteLine("================================================================================");

        // [1/6] Syntax analysis & classification
        Console.WriteLine("\n[1/6] Syntax Analysis & Classification");
        Console.WriteLine($"  * Input provided         : {targetIpString}");

        if (!IPAddress.TryParse(targetIpString.Trim(), out var address))
        {
            Console.WriteLine("  * Status                 : ERROR - Invalid IP address format.");
            Console.WriteLine("================================================================================");
            return;
        }

        var isLoopback = IPAddress.IsLoopback(address);
        var isAny = address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
        var isNone = address.Equals(IPAddress.None);
        var isPublic = RdapIpOrganizationResolver.IsPublicRoutable(address);
        var exclusionEvaluator = new DnsAddressExclusionEvaluator(config.ExcludedRemoteIps);
        var isConfigExcluded = exclusionEvaluator.ShouldExclude(address);

        Console.WriteLine($"  * Normalized IP          : {address} ({address.AddressFamily})");
        Console.WriteLine($"  * Loopback / Local       : {(isLoopback ? "YES (127.0.0.1 / ::1)" : "NO")}");
        Console.WriteLine($"  * Unspecified / Any      : {(isAny ? "YES (0.0.0.0 / ::)" : "NO")}");
        Console.WriteLine($"  * Broadcast / None       : {(isNone ? "YES (255.255.255.255)" : "NO")}");
        Console.WriteLine($"  * Publicly routable      : {(isPublic ? "YES (Public WAN / Internet)" : "NO (Private RFC 1918 / CGNAT / Link-Local / Reserved)")}");
        Console.WriteLine($"  * Excluded by config     : {(isConfigExcluded ? "YES (--exclude-remote-ips)" : "NO")}");

        // [2/6] Cache inspection
        Console.WriteLine($"\n[2/6] Local Cache Inspection (File: {config.DnsCacheFilePath})");
        DnsCacheEntry? cachedEntry = null;
        if (File.Exists(config.DnsCacheFilePath))
        {
            try
            {
                var cacheStore = new PersistentDnsCacheStore(config.DnsCacheFilePath);
                var loaded = cacheStore.Load();
                if (loaded.TryGetValue(address.ToString(), out cachedEntry))
                {
                    Console.WriteLine("  * Found in cache         : YES");
                    Console.WriteLine($"    - HostName             : {cachedEntry.HostName}");
                    Console.WriteLine($"    - Organization         : {cachedEntry.Organization ?? "(not populated)"}");
                    Console.WriteLine($"    - Country              : {cachedEntry.Country ?? "(not populated)"}");
                    Console.WriteLine($"    - Network Range        : {cachedEntry.NetworkRange ?? "(not populated)"}");
                    Console.WriteLine($"    - Source               : {cachedEntry.Source}");
                    Console.WriteLine($"    - Resolved at (UTC)    : {cachedEntry.ResolvedAtUtc:yyyy-MM-dd HH:mm:ss}Z");
                }
                else
                {
                    Console.WriteLine("  * Found in cache         : NO (Entry not present in file)");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"  * Cache access error     : {ex.GetType().Name} - {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("  * Found in cache         : NO (Cache file does not exist)");
        }

        // [3/6] System Network Configuration
        Console.WriteLine("\n[3/6] OS Network Configuration & DNS Resolvers");
        try
        {
            Console.WriteLine($"  * OS Host Name           : {Dns.GetHostName()}");
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            if (interfaces.Count == 0)
            {
                Console.WriteLine("  * Active Interfaces      : No active physical network interface detected.");
            }
            else
            {
                foreach (var nic in interfaces)
                {
                    var ipProps = nic.GetIPProperties();
                    var dnsServers = ipProps.DnsAddresses.Select(d => d.ToString()).ToList();
                    var suffix = string.IsNullOrWhiteSpace(ipProps.DnsSuffix) ? "(none)" : ipProps.DnsSuffix;
                    Console.WriteLine($"  * Interface: {nic.Name} ({nic.Description})");
                    Console.WriteLine($"    - Local DNS Suffix     : {suffix}");
                    Console.WriteLine($"    - DNS Servers          : {(dnsServers.Count > 0 ? string.Join(", ", dnsServers) : "(not configured)")}");
                }
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or SocketException)
        {
            Console.WriteLine($"  * Network probe error    : {ex.Message}");
        }

        // [4/6] Live Reverse DNS (PTR) Probe
        Console.WriteLine("\n[4/6] Live .NET Reverse DNS Probe (PTR)");
        string? reverseDnsResult = null;
        var dnsSw = Stopwatch.StartNew();

        if (isAny || isNone)
        {
            dnsSw.Stop();
            Console.WriteLine("  * Query Dns.GetHostEntryAsync skipped (unspecified or non-resolvable address).");
            Console.WriteLine("  * Status                 : SKIPPED (Defensive guard)");
        }
        else
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

                Console.WriteLine($"  * Query target           : {address}");
                Console.WriteLine("  * Configured timeout     : 3000 ms");

                var hostEntry = await Dns.GetHostEntryAsync(address.ToString(), timeoutCts.Token).ConfigureAwait(false);
                dnsSw.Stop();

                Console.WriteLine($"  * Call duration          : {dnsSw.ElapsedMilliseconds} ms");
                Console.WriteLine("  * Status                 : SUCCESS");
                Console.WriteLine($"  * Returned HostName      : {hostEntry.HostName}");
                if (hostEntry.Aliases.Length > 0)
                {
                    Console.WriteLine($"  * Returned Aliases       : {string.Join(", ", hostEntry.Aliases)}");
                }
                if (hostEntry.AddressList.Length > 0)
                {
                    Console.WriteLine($"  * Associated Addresses   : {string.Join(", ", hostEntry.AddressList.Select(a => a.ToString()))}");
                }

                if (!string.IsNullOrWhiteSpace(hostEntry.HostName) &&
                    !string.Equals(hostEntry.HostName, address.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    reverseDnsResult = hostEntry.HostName;
                }
            }
            catch (SocketException ex)
            {
                dnsSw.Stop();
                Console.WriteLine($"  * Call duration          : {dnsSw.ElapsedMilliseconds} ms");
                Console.WriteLine($"  * Status                 : FAILED SocketException (Code: {ex.SocketErrorCode})");
                Console.WriteLine($"  * Raw message            : {ex.Message}");
                Console.WriteLine("  * Explanation            : The queried DNS server did not find a PTR record or returned NXDOMAIN.");
            }
            catch (OperationCanceledException)
            {
                dnsSw.Stop();
                Console.WriteLine($"  * Call duration          : {dnsSw.ElapsedMilliseconds} ms");
                Console.WriteLine("  * Status                 : TIMEOUT / CANCELED");
                Console.WriteLine("  * Explanation            : The DNS server did not respond within the allocated timeout (3000 ms).");
            }
            catch (ArgumentException ex)
            {
                dnsSw.Stop();
                Console.WriteLine($"  * Status                 : ERROR ArgumentException - {ex.Message}");
            }
        }

        // [5/6] Live RDAP Probe
        Console.WriteLine("\n[5/6] Live RDAP Probe (RFC 7480/7482/7484)");
        IpOwnerMetadata? rdapDetails = null;
        var rdapSw = Stopwatch.StartNew();

        if (!isPublic)
        {
            rdapSw.Stop();
            Console.WriteLine("  * RDAP query skipped     : Address is not a publicly routable Internet address.");
            Console.WriteLine("  * Status                 : SKIPPED (RIR registry queries are reserved for public IP blocks)");
        }
        else
        {
            var rdapUri = $"https://rdap.org/ip/{address}";
            Console.WriteLine($"  * HTTP request target    : GET {rdapUri}");
            Console.WriteLine("  * Configured timeout     : 4000 ms");

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));

                using var response = await RdapHttpClient.GetAsync(rdapUri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
                rdapSw.Stop();

                Console.WriteLine($"  * Call duration          : {rdapSw.ElapsedMilliseconds} ms");
                Console.WriteLine($"  * Returned HTTP Status   : {(int)response.StatusCode} {response.ReasonPhrase}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                    rdapDetails = RdapResponseParser.ExtractOwnerDetails(content);

                    Console.WriteLine("  * RDAP Status            : SUCCESS");
                    Console.WriteLine($"    - Organization         : {rdapDetails?.Organization ?? "(not found in JSON payload)"}");
                    Console.WriteLine($"    - Country              : {rdapDetails?.Country ?? "(not populated)"}");
                    Console.WriteLine($"    - Network Range        : {rdapDetails?.NetworkRange ?? "(not populated)"}");
                    Console.WriteLine($"    - RIR Handle           : {rdapDetails?.Handle ?? "(not populated)"}");
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine("  * RDAP Status            : 404 NOT FOUND - No record present in RIR databases (ARIN, RIPE, etc.).");
                }
                else
                {
                    Console.WriteLine($"  * RDAP Status            : HTTP Error {(int)response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                rdapSw.Stop();
                Console.WriteLine($"  * Call duration          : {rdapSw.ElapsedMilliseconds} ms");
                Console.WriteLine($"  * RDAP Status            : FAILED HttpRequestException - {ex.Message}");
                Console.WriteLine("  * Explanation            : No Internet access, proxy required, or rdap.org unreachable.");
            }
            catch (OperationCanceledException)
            {
                rdapSw.Stop();
                Console.WriteLine($"  * Call duration          : {rdapSw.ElapsedMilliseconds} ms");
                Console.WriteLine("  * RDAP Status            : TIMEOUT (rdap.org did not respond within 4000 ms).");
            }
        }

        // [6/6] Synthesis & Pipeline Simulation
        Console.WriteLine("\n[6/6] Synthesis & NetFlowAudit Cache Model");
        string synthesizedHostName;
        string synthesizedSource;

        if (isLoopback)
        {
            synthesizedHostName = address.ToString();
            synthesizedSource = "Local";
        }
        else if (isAny || isNone || isConfigExcluded)
        {
            synthesizedHostName = string.Empty;
            synthesizedSource = "Excluded";
        }
        else if (!string.IsNullOrWhiteSpace(reverseDnsResult))
        {
            synthesizedHostName = reverseDnsResult;
            synthesizedSource = "DNS";
        }
        else if (!string.IsNullOrWhiteSpace(rdapDetails?.Organization))
        {
            synthesizedHostName = rdapDetails.Organization;
            synthesizedSource = "RDAP";
        }
        else
        {
            synthesizedHostName = string.Empty;
            synthesizedSource = "None";
        }

        Console.WriteLine($"  * Synthesized HostName   : {(string.IsNullOrEmpty(synthesizedHostName) ? "(none)" : synthesizedHostName)}");
        Console.WriteLine($"  * Organization           : {rdapDetails?.Organization ?? "(none)"}");
        Console.WriteLine($"  * Country                : {rdapDetails?.Country ?? "(none)"}");
        Console.WriteLine($"  * Network Range          : {rdapDetails?.NetworkRange ?? "(none)"}");
        Console.WriteLine($"  * Synthesized Source     : {synthesizedSource}");

        if (cachedEntry is not null && !string.Equals(cachedEntry.HostName, synthesizedHostName, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  * NOTE                   : Local cache differs from live resolution ({cachedEntry.HostName} vs {synthesizedHostName}).");
        }

        Console.WriteLine("================================================================================");
    }
}
