namespace PortAtlas.Core.Export;

/// <summary>
/// One row of the pair matrix: every consolidated rule from one source address to one destination address.
/// </summary>
/// <param name="PairId">Sequential identifier (<c>PAIR-001</c>) assigned after sorting.</param>
/// <param name="SourceIp">Source address of the flows.</param>
/// <param name="SourceHost">Source host name, or the address when unresolved.</param>
/// <param name="DestinationIp">Destination address of the flows.</param>
/// <param name="DestinationHost">Destination host name, or the address when unresolved.</param>
/// <param name="FlowCount">Number of rows of the detailed matrix merged into this pair.</param>
/// <param name="Ports">Distinct destination ports, formatted <c>port/protocol</c> and sorted by port.</param>
/// <param name="ObservationCount">Sum of the observations of the merged rules.</param>
/// <param name="FirstSeenUtc">Earliest first observation among the merged rules.</param>
/// <param name="LastSeenUtc">Latest last observation among the merged rules.</param>
public sealed record FirewallRulePairItem(
    string PairId,
    string SourceIp,
    string SourceHost,
    string DestinationIp,
    string DestinationHost,
    int FlowCount,
    IReadOnlyList<string> Ports,
    long ObservationCount,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc);
