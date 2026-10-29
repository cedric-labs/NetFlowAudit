using SocketForge.Models;

namespace PortAtlas.Core.Models;

/// <summary>
/// Metric summary of an audit scan cycle across network flows.
/// </summary>
public sealed record FlowAuditCycleResult(
    DateTimeOffset TimestampUtc,
    int TotalSocketsScanned,
    int ExcludedSocketsCount,
    int NewlyDiscoveredCount,
    int TotalUniqueFlowsTracked,
    IReadOnlyList<NetworkAuditEntry> NewlyDiscoveredEntries,
    IReadOnlyList<ActiveSocketConnection> ActiveSockets);
