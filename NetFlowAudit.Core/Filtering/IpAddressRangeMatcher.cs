using System.Net;
using System.Net.Sockets;

namespace NetFlowAudit.Core.Filtering;

/// <summary>
/// Evaluates whether an IP address matches a configured set of address rules (exact IP or CIDR subnets).
/// </summary>
public sealed class IpAddressRangeMatcher
{
    private readonly List<IPNetwork> _networks = [];
    private readonly HashSet<IPAddress> _addresses = [];

    /// <summary>
    /// Initializes a new matcher instance with the given filtering rules (CIDR subnets or exact IP addresses).
    /// </summary>
    /// <param name="rules">Exclusion rules (e.g. "192.168.1.50", "10.0.0.0/8").</param>
    public IpAddressRangeMatcher(IEnumerable<string>? rules)
    {
        if (rules is null)
        {
            return;
        }

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule))
            {
                continue;
            }

            var trimmed = rule.Trim();
            if (trimmed.Contains('/', StringComparison.Ordinal))
            {
                if (IPNetwork.TryParse(trimmed, out var network))
                {
                    _networks.Add(network);
                }
            }
            else if (IPAddress.TryParse(trimmed, out var address))
            {
                _addresses.Add(address);
                if (address.IsIPv4MappedToIPv6)
                {
                    _addresses.Add(address.MapToIPv4());
                }
                else if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    _addresses.Add(address.MapToIPv6());
                }
            }
        }
    }

    /// <summary>
    /// Indicates whether at least one matching rule is configured.
    /// </summary>
    public bool HasRules => _addresses.Count > 0 || _networks.Count > 0;

    /// <summary>
    /// Determines whether the specified IP address matches any configured address or CIDR range.
    /// </summary>
    /// <param name="address">IP address to evaluate.</param>
    /// <returns>True if the address matches any rule; otherwise false.</returns>
    public bool IsMatch(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (_addresses.Contains(address))
        {
            return true;
        }

        var mapped = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : null;
        if (mapped is not null && _addresses.Contains(mapped))
        {
            return true;
        }

        for (var i = 0; i < _networks.Count; i++)
        {
            if (_networks[i].Contains(address) || (mapped is not null && _networks[i].Contains(mapped)))
            {
                return true;
            }
        }

        return false;
    }
}
