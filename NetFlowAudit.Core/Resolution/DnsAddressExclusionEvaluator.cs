using System.Net;
using NetFlowAudit.Core.Filtering;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Evaluator determining whether an IP address belongs to an address or CIDR range excluded from reverse DNS resolution.
/// </summary>
public sealed class DnsAddressExclusionEvaluator(IEnumerable<string>? rules)
{
    private readonly IpAddressRangeMatcher _matcher = new(rules);

    /// <summary>
    /// Determines whether the specified IP address should be excluded from reverse DNS queries.
    /// </summary>
    /// <param name="address">Target IP address to evaluate.</param>
    /// <returns>True if the address is excluded; otherwise false.</returns>
    public bool ShouldExclude(IPAddress address) => _matcher.IsMatch(address);
}
