using SocketForge.Models;

namespace NetFlowAudit.Core.Models;

/// <summary>
/// Synthèse métrique d'un cycle de balayage d'audit des flux.
/// </summary>
public sealed record FlowAuditCycleResult(
    DateTimeOffset TimestampUtc,
    int TotalSocketsScanned,
    int ExcludedSocketsCount,
    int NewlyDiscoveredCount,
    int TotalUniqueFlowsTracked,
    IReadOnlyList<NetworkAuditEntry> NewlyDiscoveredEntries,
    IReadOnlyList<ActiveSocketConnection> ActiveSockets);
