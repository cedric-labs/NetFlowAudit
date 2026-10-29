using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PortAtlas.Cli.Configuration;
using PortAtlas.Core.Resolution;

namespace PortAtlas.Cli.Diagnostics;

/// <summary>
/// Diagnostic reporter executing granular, verbose probes for reverse DNS (PTR) and RDAP registration against a target IP address.
/// </summary>
/// <param name="output">Writer receiving the report; the console when omitted.</param>
public sealed class IpResolutionDiagnosticReporter(TextWriter? output = null)
{
    private static readonly HttpClient RdapHttpClient = CreateRdapHttpClient();

    private readonly TextWriter _output = output ?? Console.Out;

    private static HttpClient CreateRdapHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(3),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };
        var client = new HttpClient(handler, disposeHandler: true);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PortAtlas-Diagnostic/1.0");
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

        await _output.WriteLineAsync("================================================================================").ConfigureAwait(false);
        await _output.WriteLineAsync("           PortAtlas - IP & Network Resolution Diagnostic").ConfigureAwait(false);
        await _output.WriteLineAsync("================================================================================").ConfigureAwait(false);

        // [1/6] Syntax analysis & classification
        await _output.WriteLineAsync("\n[1/6] Syntax Analysis & Classification").ConfigureAwait(false);
        await _output.WriteLineAsync($"  * Input provided         : {targetIpString}").ConfigureAwait(false);

        if (!IPAddress.TryParse(targetIpString.Trim(), out var address))
        {
            await _output.WriteLineAsync("  * Status                 : ERROR - Invalid IP address format.").ConfigureAwait(false);
            await _output.WriteLineAsync("================================================================================").ConfigureAwait(false);
            return;
        }

        var isLoopback = IPAddress.IsLoopback(address);
        var isAny = address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
        var isNone = address.Equals(IPAddress.None);
        var isPublic = RdapIpOrganizationResolver.IsPublicRoutable(address);
        var exclusionEvaluator = new DnsAddressExclusionEvaluator(config.ExcludedRemoteIps);
        var isConfigExcluded = exclusionEvaluator.ShouldExclude(address);

        await _output.WriteLineAsync($"  * Normalized IP          : {address} ({address.AddressFamily})").ConfigureAwait(false);
        await _output.WriteLineAsync($"  * Loopback / Local       : {(isLoopback ? "YES (127.0.0.1 / ::1)" : "NO")}").ConfigureAwait(false);
        await _output.WriteLineAsync($"  * Unspecified / Any      : {(isAny ? "YES (0.0.0.0 / ::)" : "NO")}").ConfigureAwait(false);
        await _output.WriteLineAsync($"  * Broadcast / None       : {(isNone ? "YES (255.255.255.255)" : "NO")}").ConfigureAwait(false);
        await _output.WriteLineAsync($"  * Publicly routable      : {(isPublic ? "YES (Public WAN / Internet)" : "NO (Private RFC 1918 / CGNAT / Link-Local / Reserved)")}").ConfigureAwait(false);
        await _output.WriteLineAsync($"  * Excluded by config     : {(isConfigExcluded ? "YES (--exclude-remote-ips)" : "NO")}").ConfigureAwait(false);

        // [2/6] Cache inspection
        await _output.WriteLineAsync($"\n[2/6] Local Cache Inspection (File: {config.DnsCacheFilePath})").ConfigureAwait(false);
        DnsCacheEntry? cachedEntry = null;
        if (File.Exists(config.DnsCacheFilePath))
        {
            try
            {
                var cacheStore = new PersistentDnsCacheStore(config.DnsCacheFilePath);
                var loaded = cacheStore.Load();
                if (loaded.TryGetValue(address.ToString(), out cachedEntry))
                {
                    await _output.WriteLineAsync("  * Found in cache         : YES").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - HostName             : {cachedEntry.HostName}").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - Organization         : {cachedEntry.Organization ?? "(not populated)"}").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - Country              : {cachedEntry.Country ?? "(not populated)"}").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - Network Range        : {cachedEntry.NetworkRange ?? "(not populated)"}").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - Source               : {cachedEntry.Source}").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - Resolved at (UTC)    : {cachedEntry.ResolvedAtUtc:yyyy-MM-dd HH:mm:ss}Z").ConfigureAwait(false);
                }
                else
                {
                    await _output.WriteLineAsync("  * Found in cache         : NO (Entry not present in file)").ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await _output.WriteLineAsync($"  * Cache access error     : {ex.GetType().Name} - {ex.Message}").ConfigureAwait(false);
            }
        }
        else
        {
            await _output.WriteLineAsync("  * Found in cache         : NO (Cache file does not exist)").ConfigureAwait(false);
        }

        // [3/6] System Network Configuration
        await _output.WriteLineAsync("\n[3/6] OS Network Configuration & DNS Resolvers").ConfigureAwait(false);
        try
        {
            await _output.WriteLineAsync($"  * OS Host Name           : {Dns.GetHostName()}").ConfigureAwait(false);
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            if (interfaces.Count == 0)
            {
                await _output.WriteLineAsync("  * Active Interfaces      : No active physical network interface detected.").ConfigureAwait(false);
            }
            else
            {
                foreach (var nic in interfaces)
                {
                    var ipProps = nic.GetIPProperties();
                    var dnsServers = ipProps.DnsAddresses.Select(d => d.ToString()).ToList();
                    var suffix = string.IsNullOrWhiteSpace(ipProps.DnsSuffix) ? "(none)" : ipProps.DnsSuffix;
                    await _output.WriteLineAsync($"  * Interface: {nic.Name} ({nic.Description})").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - Local DNS Suffix     : {suffix}").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - DNS Servers          : {(dnsServers.Count > 0 ? string.Join(", ", dnsServers) : "(not configured)")}").ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or SocketException)
        {
            await _output.WriteLineAsync($"  * Network probe error    : {ex.Message}").ConfigureAwait(false);
        }

        // [4/6] Live Reverse DNS (PTR) Probe
        await _output.WriteLineAsync("\n[4/6] Live .NET Reverse DNS Probe (PTR)").ConfigureAwait(false);
        string? reverseDnsResult = null;
        var dnsSw = Stopwatch.StartNew();

        if (isAny || isNone)
        {
            dnsSw.Stop();
            await _output.WriteLineAsync("  * Query Dns.GetHostEntryAsync skipped (unspecified or non-resolvable address).").ConfigureAwait(false);
            await _output.WriteLineAsync("  * Status                 : SKIPPED (Defensive guard)").ConfigureAwait(false);
        }
        else
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                // Same timeout as the audit, so that a SUCCESS here means the audit names the address too.
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(config.DnsResolutionTimeoutMs));

                await _output.WriteLineAsync($"  * Query target           : {address}").ConfigureAwait(false);
                await _output.WriteLineAsync($"  * Configured timeout     : {config.DnsResolutionTimeoutMs} ms (DnsResolutionTimeoutMs)").ConfigureAwait(false);

                var hostEntry = await Dns.GetHostEntryAsync(address.ToString(), timeoutCts.Token).ConfigureAwait(false);
                dnsSw.Stop();

                await _output.WriteLineAsync($"  * Call duration          : {dnsSw.ElapsedMilliseconds} ms").ConfigureAwait(false);
                await _output.WriteLineAsync("  * Status                 : SUCCESS").ConfigureAwait(false);
                await _output.WriteLineAsync($"  * Returned HostName      : {hostEntry.HostName}").ConfigureAwait(false);
                if (hostEntry.Aliases.Length > 0)
                {
                    await _output.WriteLineAsync($"  * Returned Aliases       : {string.Join(", ", hostEntry.Aliases)}").ConfigureAwait(false);
                }
                if (hostEntry.AddressList.Length > 0)
                {
                    await _output.WriteLineAsync($"  * Associated Addresses   : {string.Join(", ", hostEntry.AddressList.Select(a => a.ToString()))}").ConfigureAwait(false);
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
                await _output.WriteLineAsync($"  * Call duration          : {dnsSw.ElapsedMilliseconds} ms").ConfigureAwait(false);
                await _output.WriteLineAsync($"  * Status                 : FAILED SocketException (Code: {ex.SocketErrorCode})").ConfigureAwait(false);
                await _output.WriteLineAsync($"  * Raw message            : {ex.Message}").ConfigureAwait(false);
                await _output.WriteLineAsync("  * Explanation            : The queried DNS server did not find a PTR record or returned NXDOMAIN.").ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                dnsSw.Stop();
                await _output.WriteLineAsync($"  * Call duration          : {dnsSw.ElapsedMilliseconds} ms").ConfigureAwait(false);
                await _output.WriteLineAsync("  * Status                 : TIMEOUT / CANCELED").ConfigureAwait(false);
                await _output.WriteLineAsync($"  * Explanation            : The DNS server did not respond within {config.DnsResolutionTimeoutMs} ms. Raise DnsResolutionTimeoutMs or --dns-timeout-ms.").ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                dnsSw.Stop();
                await _output.WriteLineAsync($"  * Status                 : ERROR ArgumentException - {ex.Message}").ConfigureAwait(false);
            }
        }

        // [5/6] Live RDAP Probe
        await _output.WriteLineAsync("\n[5/6] Live RDAP Probe (RFC 7480/7482/7484)").ConfigureAwait(false);
        IpOwnerMetadata? rdapDetails = null;
        var rdapSw = Stopwatch.StartNew();

        if (!isPublic)
        {
            rdapSw.Stop();
            await _output.WriteLineAsync("  * RDAP query skipped     : Address is not a publicly routable Internet address.").ConfigureAwait(false);
            await _output.WriteLineAsync("  * Status                 : SKIPPED (RIR registry queries are reserved for public IP blocks)").ConfigureAwait(false);
        }
        else
        {
            var rdapUri = $"https://rdap.org/ip/{address}";
            await _output.WriteLineAsync($"  * HTTP request target    : GET {rdapUri}").ConfigureAwait(false);
            await _output.WriteLineAsync("  * Configured timeout     : 4000 ms").ConfigureAwait(false);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));

                using var response = await RdapHttpClient.GetAsync(rdapUri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
                rdapSw.Stop();

                await _output.WriteLineAsync($"  * Call duration          : {rdapSw.ElapsedMilliseconds} ms").ConfigureAwait(false);
                await _output.WriteLineAsync($"  * Returned HTTP Status   : {(int)response.StatusCode} {response.ReasonPhrase}").ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                    rdapDetails = RdapResponseParser.ExtractOwnerDetails(content);

                    await _output.WriteLineAsync("  * RDAP Status            : SUCCESS").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - Organization         : {rdapDetails?.Organization ?? "(not found in JSON payload)"}").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - Country              : {rdapDetails?.Country ?? "(not populated)"}").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - Network Range        : {rdapDetails?.NetworkRange ?? "(not populated)"}").ConfigureAwait(false);
                    await _output.WriteLineAsync($"    - RIR Handle           : {rdapDetails?.Handle ?? "(not populated)"}").ConfigureAwait(false);
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    await _output.WriteLineAsync("  * RDAP Status            : 404 NOT FOUND - No record present in RIR databases (ARIN, RIPE, etc.).").ConfigureAwait(false);
                }
                else
                {
                    await _output.WriteLineAsync($"  * RDAP Status            : HTTP Error {(int)response.StatusCode}").ConfigureAwait(false);
                }
            }
            catch (HttpRequestException ex)
            {
                rdapSw.Stop();
                await _output.WriteLineAsync($"  * Call duration          : {rdapSw.ElapsedMilliseconds} ms").ConfigureAwait(false);
                await _output.WriteLineAsync($"  * RDAP Status            : FAILED HttpRequestException - {ex.Message}").ConfigureAwait(false);
                await _output.WriteLineAsync("  * Explanation            : No Internet access, proxy required, or rdap.org unreachable.").ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                rdapSw.Stop();
                await _output.WriteLineAsync($"  * Call duration          : {rdapSw.ElapsedMilliseconds} ms").ConfigureAwait(false);
                await _output.WriteLineAsync("  * RDAP Status            : TIMEOUT (rdap.org did not respond within 4000 ms).").ConfigureAwait(false);
            }
        }

        // [6/6] Synthesis & Pipeline Simulation
        await _output.WriteLineAsync("\n[6/6] Synthesis & PortAtlas Cache Model").ConfigureAwait(false);
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

        await _output.WriteLineAsync($"  * Synthesized HostName   : {(string.IsNullOrEmpty(synthesizedHostName) ? "(none)" : synthesizedHostName)}").ConfigureAwait(false);
        await _output.WriteLineAsync($"  * Organization           : {rdapDetails?.Organization ?? "(none)"}").ConfigureAwait(false);
        await _output.WriteLineAsync($"  * Country                : {rdapDetails?.Country ?? "(none)"}").ConfigureAwait(false);
        await _output.WriteLineAsync($"  * Network Range          : {rdapDetails?.NetworkRange ?? "(none)"}").ConfigureAwait(false);
        await _output.WriteLineAsync($"  * Synthesized Source     : {synthesizedSource}").ConfigureAwait(false);

        if (cachedEntry is not null && !string.Equals(cachedEntry.HostName, synthesizedHostName, StringComparison.OrdinalIgnoreCase))
        {
            await _output.WriteLineAsync($"  * NOTE                   : Local cache differs from live resolution ({cachedEntry.HostName} vs {synthesizedHostName}).").ConfigureAwait(false);
        }

        await _output.WriteLineAsync("================================================================================").ConfigureAwait(false);
    }
}
