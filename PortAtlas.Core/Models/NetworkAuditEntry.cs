using SocketForge.Models;

namespace PortAtlas.Core.Models;

/// <summary>
/// Consolidated audit record associating an active socket with server host context and observation timestamps.
/// </summary>
/// <param name="FirstSeenUtc">UTC timestamp of first detection.</param>
/// <param name="LastSeenUtc">UTC timestamp of most recent detection.</param>
/// <param name="ObservationCount">Cumulative scan observation count where the flow was active.</param>
/// <param name="Server">Host server metadata.</param>
/// <param name="Socket">Underlying socket and process payload details.</param>
/// <param name="RemoteHostName">Resolved remote FQDN / host name (or empty if unresolved).</param>
public sealed record NetworkAuditEntry(
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    long ObservationCount,
    ServerHostMetadata Server,
    ActiveSocketConnection Socket,
    string RemoteHostName = "")
{
    /// <summary>
    /// Computes the logical deduplication key for this audit entry.
    /// </summary>
    public string ComputeAuditKey() => Socket.ComputeLogicalKey();
}
