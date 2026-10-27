using System.Net;
using NetFlowAudit.Core.Filtering;
using NetFlowAudit.Core.Models;
using SocketForge.Models;

namespace NetFlowAudit.Core.Export;

/// <summary>
/// Normalizes and aggregates raw network connection flows into consolidated firewall rule records.
/// </summary>
public sealed class FirewallRuleAggregator
{
    private const string AnySourceHost = "Any";

    private static readonly IpAddressRangeMatcher PrivateNetworkMatcher = new(
    [
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "127.0.0.0/8",
        "169.254.0.0/16",
        "::1/128",
        "fc00::/7",
        "fe80::/10"
    ]);

    private static readonly Dictionary<int, string> WellKnownServices = new()
    {
        [22] = "SSH",
        [25] = "SMTP",
        [53] = "DNS",
        [80] = "HTTP",
        [123] = "NTP",
        [139] = "NetBIOS",
        [389] = "LDAP",
        [443] = "HTTPS",
        [445] = "SMB",
        [587] = "Submission",
        [636] = "LDAPS",
        [1433] = "MS-SQL",
        [1521] = "Oracle-TNS",
        [3306] = "MySQL",
        [3389] = "RDP",
        [5432] = "PostgreSQL",
        [6379] = "Redis",
        [8080] = "HTTP-Alt",
        [8443] = "HTTPS-Alt",
        [27017] = "MongoDB"
    };

    /// <summary>
    /// Aggregates historical audit entries into a consolidated, deduplicated list of firewall rule items.
    /// </summary>
    /// <param name="entries">Raw network audit entries.</param>
    /// <param name="options">Filtering and consolidation options.</param>
    /// <returns>A sorted, indexed list of consolidated firewall rule items.</returns>
    public IReadOnlyList<FirewallRuleItem> Aggregate(
        IEnumerable<NetworkAuditEntry> entries,
        FirewallRuleExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);

        var groups = new Dictionary<(string Direction, string SourceIp, string DestIp, int DestPort, string Protocol, string Process), AggregationAccumulator>();

        var portFilter = BuildPortMatcher(options.IncludedPorts, options.ExcludedPorts);
        var ipMatcher = BuildIpMatcher(options.IncludedIps, options.ExcludedIps);

        foreach (var entry in entries)
        {
            if (entry is null)
            {
                continue;
            }

            if (options.ListenersOnly && !string.Equals(entry.Socket.State, "Listen", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // A listening socket becomes an inbound rule from any source to its local port. It is exported only on
            // request: in the default matrix the established inbound flows already describe the real clients.
            var isListener = entry.Socket.Direction == SocketFlowDirection.Listening;
            if (isListener && !options.ListenersOnly)
            {
                continue;
            }

            var isOutbound = entry.Socket.Direction == SocketFlowDirection.Outbound;
            if (!isOutbound && !isListener && entry.Socket.Direction != SocketFlowDirection.Inbound)
            {
                continue;
            }

            var directionStr = isOutbound ? "Outbound" : "Inbound";
            if (string.Equals(options.Direction, "out", StringComparison.OrdinalIgnoreCase) && !isOutbound)
            {
                continue;
            }
            if (string.Equals(options.Direction, "in", StringComparison.OrdinalIgnoreCase) && isOutbound)
            {
                continue;
            }

            var sourceIp = isOutbound ? entry.Socket.LocalEndpoint.Address.ToString() : entry.Socket.RemoteEndpoint.Address.ToString();
            var sourceHost = isOutbound
                ? entry.Server.HostName
                : isListener ? AnySourceHost : (!string.IsNullOrWhiteSpace(entry.RemoteHostName) ? entry.RemoteHostName : sourceIp);

            var destIp = isOutbound ? entry.Socket.RemoteEndpoint.Address.ToString() : entry.Socket.LocalEndpoint.Address.ToString();
            var destHost = isOutbound ? (!string.IsNullOrWhiteSpace(entry.RemoteHostName) ? entry.RemoteHostName : destIp) : entry.Server.HostName;
            var destPort = isOutbound ? entry.Socket.RemoteEndpoint.Port : entry.Socket.LocalEndpoint.Port;

            // Ephemeral destination port exclusion
            if (options.ExcludeEphemeralPorts && destPort >= 49152)
            {
                continue;
            }

            // Port inclusion / exclusion
            if (!portFilter.IsAllowed(destPort))
            {
                continue;
            }

            // Host inclusion / exclusion
            if (!IsHostAllowed(sourceHost, options.IncludedHosts, options.ExcludedHosts) ||
                !IsHostAllowed(destHost, options.IncludedHosts, options.ExcludedHosts))
            {
                continue;
            }

            // IP inclusion / exclusion
            if (IPAddress.TryParse(destIp, out var parsedDestIp) && !ipMatcher.IsAllowed(parsedDestIp))
            {
                continue;
            }

            var processName = entry.Socket.Process.ProcessName;
            if (!IsProcessAllowed(processName, entry.Socket.Process.TargetExecutable, options.IncludedProcesses, options.ExcludedProcesses))
            {
                continue;
            }

            var protocolStr = entry.Socket.Protocol == SocketTransportProtocol.Udp ? "UDP" : "TCP";
            var key = (directionStr, sourceIp, destIp, destPort, protocolStr, processName);

            if (!groups.TryGetValue(key, out var acc))
            {
                acc = new AggregationAccumulator
                {
                    Direction = directionStr,
                    SourceIp = sourceIp,
                    SourceHost = sourceHost,
                    DestinationIp = destIp,
                    DestinationHost = destHost,
                    DestinationPort = destPort,
                    Protocol = protocolStr,
                    ProcessName = processName,
                    ExecutablePath = entry.Socket.Process.ExecutablePath,
                    UserName = entry.Socket.Process.UserName,
                    ObservationCount = 0,
                    FirstSeenUtc = entry.FirstSeenUtc,
                    LastSeenUtc = entry.LastSeenUtc
                };
                groups[key] = acc;
            }

            acc.ObservationCount += entry.ObservationCount;
            if (entry.FirstSeenUtc < acc.FirstSeenUtc)
            {
                acc.FirstSeenUtc = entry.FirstSeenUtc;
            }
            if (entry.LastSeenUtc > acc.LastSeenUtc)
            {
                acc.LastSeenUtc = entry.LastSeenUtc;
            }

            if (string.IsNullOrWhiteSpace(acc.DestinationHost) && !string.IsNullOrWhiteSpace(destHost))
            {
                acc.DestinationHost = destHost;
            }
        }

        var results = new List<FirewallRuleItem>();
        var now = DateTimeOffset.UtcNow;

        foreach (var acc in groups.Values)
        {
            if (acc.ObservationCount < options.MinObservations)
            {
                continue;
            }

            if (options.ActiveWithinDays.HasValue && options.ActiveWithinDays.Value > 0)
            {
                var cutoff = now.AddDays(-options.ActiveWithinDays.Value);
                if (acc.LastSeenUtc < cutoff)
                {
                    continue;
                }
            }

            // A listener bound to 0.0.0.0 or :: is reached through the server's own addresses, hence internal.
            var isInternal = IPAddress.TryParse(acc.DestinationIp, out var parsedIp) &&
                             (IPAddress.IsLoopback(parsedIp) || PrivateNetworkMatcher.IsMatch(parsedIp) ||
                              parsedIp.Equals(IPAddress.Any) || parsedIp.Equals(IPAddress.IPv6Any));
            var zone = isInternal ? "Internal" : "Public";

            if (string.Equals(options.DestinationZoneFilter, "internal", StringComparison.OrdinalIgnoreCase) && !isInternal)
            {
                continue;
            }
            if (string.Equals(options.DestinationZoneFilter, "public", StringComparison.OrdinalIgnoreCase) && isInternal)
            {
                continue;
            }

            var serviceName = DeduceServiceName(acc.DestinationPort, acc.Protocol, acc.ProcessName);

            results.Add(new FirewallRuleItem
            {
                RuleId = string.Empty, // Assigned sequentially after sorting
                Direction = acc.Direction,
                SourceIp = acc.SourceIp,
                SourceHost = acc.SourceHost,
                DestinationIp = acc.DestinationIp,
                DestinationHost = acc.DestinationHost,
                DestinationZone = zone,
                Protocol = acc.Protocol,
                DestinationPort = acc.DestinationPort,
                ServiceName = serviceName,
                ProcessName = acc.ProcessName,
                ExecutablePath = acc.ExecutablePath,
                UserName = acc.UserName,
                ObservationCount = acc.ObservationCount,
                FirstSeenUtc = acc.FirstSeenUtc,
                LastSeenUtc = acc.LastSeenUtc
            });
        }

        // Deterministic sorting for firewall rule consistency
        var sorted = results
            .OrderBy(r => r.Direction)
            .ThenBy(r => r.DestinationZone)
            .ThenBy(r => r.DestinationPort)
            .ThenBy(r => r.DestinationIp, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.SourceIp, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i] = sorted[i] with { RuleId = $"FLUX-{(i + 1):D3}" };
        }

        return sorted;
    }

    private static string DeduceServiceName(int port, string protocol, string processName)
    {
        if (WellKnownServices.TryGetValue(port, out var wellKnown))
        {
            return wellKnown;
        }

        if (!string.IsNullOrWhiteSpace(processName) &&
            !string.Equals(processName, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(processName, "System", StringComparison.OrdinalIgnoreCase))
        {
            return processName;
        }

        return $"{port}/{protocol}";
    }

    private static bool IsHostAllowed(string host, IReadOnlyList<string> included, IReadOnlyList<string> excluded)
    {
        if (excluded.Count > 0 && MatchesAnyPattern(host, excluded))
        {
            return false;
        }

        if (included.Count > 0 && !MatchesAnyPattern(host, included))
        {
            return false;
        }

        return true;
    }

    private static bool IsProcessAllowed(string processName, string targetExecutable, IReadOnlyList<string> included, IReadOnlyList<string> excluded)
    {
        if (excluded.Count > 0 && (MatchesAnyPattern(processName, excluded) || MatchesAnyPattern(targetExecutable, excluded)))
        {
            return false;
        }

        if (included.Count > 0 && !(MatchesAnyPattern(processName, included) || MatchesAnyPattern(targetExecutable, included)))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesAnyPattern(string text, IReadOnlyList<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (!pattern.Contains('*') && !pattern.Contains('?'))
            {
                if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace(@"\*", ".*", StringComparison.Ordinal)
                    .Replace(@"\?", ".", StringComparison.Ordinal) + "$";

                if (System.Text.RegularExpressions.Regex.IsMatch(text, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static PortMatcher BuildPortMatcher(IReadOnlyList<string> included, IReadOnlyList<string> excluded)
    {
        var inc = ParsePortTokens(included);
        var exc = ParsePortTokens(excluded);
        return new PortMatcher(inc, exc);
    }

    private static HashSet<int> ParsePortTokens(IReadOnlyList<string> tokens)
    {
        var result = new HashSet<int>();
        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var parts = token.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end))
            {
                for (var p = start; p <= end; p++)
                {
                    result.Add(p);
                }
            }
            else if (int.TryParse(token, out var single))
            {
                result.Add(single);
            }
        }
        return result;
    }

    private static IpMatcher BuildIpMatcher(IReadOnlyList<string> included, IReadOnlyList<string> excluded)
    {
        var inc = included.Count > 0 ? new IpAddressRangeMatcher(included) : null;
        var exc = excluded.Count > 0 ? new IpAddressRangeMatcher(excluded) : null;
        return new IpMatcher(inc, exc);
    }

    private sealed class AggregationAccumulator
    {
        public required string Direction { get; init; }
        public required string SourceIp { get; init; }
        public required string SourceHost { get; set; }
        public required string DestinationIp { get; init; }
        public required string DestinationHost { get; set; }
        public required int DestinationPort { get; init; }
        public required string Protocol { get; init; }
        public required string ProcessName { get; init; }
        public required string ExecutablePath { get; init; }
        public required string UserName { get; init; }
        public long ObservationCount { get; set; }
        public DateTimeOffset FirstSeenUtc { get; set; }
        public DateTimeOffset LastSeenUtc { get; set; }
    }

    private sealed class PortMatcher(HashSet<int> included, HashSet<int> excluded)
    {
        public bool IsAllowed(int port)
        {
            if (excluded.Contains(port))
            {
                return false;
            }
            if (included.Count > 0 && !included.Contains(port))
            {
                return false;
            }
            return true;
        }
    }

    private sealed class IpMatcher(IpAddressRangeMatcher? included, IpAddressRangeMatcher? excluded)
    {
        public bool IsAllowed(IPAddress ip)
        {
            if (excluded is not null && excluded.IsMatch(ip))
            {
                return false;
            }
            if (included is not null && !included.IsMatch(ip))
            {
                return false;
            }
            return true;
        }
    }
}
