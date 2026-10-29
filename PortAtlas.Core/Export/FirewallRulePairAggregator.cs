namespace PortAtlas.Core.Export;

/// <summary>
/// Merges the rules of the detailed firewall matrix into one row per source and destination address.
/// </summary>
public sealed class FirewallRulePairAggregator
{
    /// <summary>
    /// Groups the consolidated rules by oriented address pair.
    /// </summary>
    /// <param name="rules">Rules of the detailed matrix, already filtered.</param>
    /// <returns>One item per oriented pair, sorted by source then destination address.</returns>
    public IReadOnlyList<FirewallRulePairItem> Aggregate(IReadOnlyList<FirewallRuleItem> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        // A firewall rule is oriented: the pair keeps the source → destination direction of the detailed matrix,
        // so the two directions between two servers stay on two rows.
        var pairs = rules
            .GroupBy(r => (Source: r.SourceIp.ToUpperInvariant(), Destination: r.DestinationIp.ToUpperInvariant()))
            .OrderBy(g => g.Key.Source, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Destination, StringComparer.Ordinal)
            .Select((group, index) =>
            {
                var first = group.First();
                return new FirewallRulePairItem(
                    $"PAIR-{index + 1:D3}",
                    first.SourceIp,
                    first.SourceHost,
                    first.DestinationIp,
                    first.DestinationHost,
                    group.Count(),
                    [.. group
                        .Select(r => (r.DestinationPort, r.Protocol))
                        .Distinct()
                        .OrderBy(p => p.DestinationPort)
                        .ThenBy(p => p.Protocol, StringComparer.Ordinal)
                        .Select(p => $"{p.DestinationPort}/{p.Protocol}")],
                    group.Sum(r => r.ObservationCount),
                    group.Min(r => r.FirstSeenUtc),
                    group.Max(r => r.LastSeenUtc));
            });

        return [.. pairs];
    }
}
