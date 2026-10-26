namespace NetFlowAudit.Core.Export;

/// <summary>
/// Represents a consolidated, normalized network flow rule suitable for firewall policies and flow matrices.
/// </summary>
public sealed record FirewallRuleItem
{
    /// <summary>
    /// Sequential rule identifier (e.g. "FLUX-001").
    /// </summary>
    public string RuleId { get; init; } = string.Empty;

    /// <summary>
    /// Flow direction from the viewpoint of the audited host ("Inbound" or "Outbound").
    /// </summary>
    public string Direction { get; init; } = string.Empty;

    /// <summary>
    /// Effective source IP address initiating the connection.
    /// </summary>
    public string SourceIp { get; init; } = string.Empty;

    /// <summary>
    /// Resolved source hostname or server identifier.
    /// </summary>
    public string SourceHost { get; init; } = string.Empty;

    /// <summary>
    /// Effective destination IP address receiving the connection.
    /// </summary>
    public string DestinationIp { get; init; } = string.Empty;

    /// <summary>
    /// Resolved destination hostname or owning organization.
    /// </summary>
    public string DestinationHost { get; init; } = string.Empty;

    /// <summary>
    /// Network zone categorization of the destination ("Internal" for RFC 1918 / loopback, "Public" for WAN/Internet).
    /// </summary>
    public string DestinationZone { get; init; } = string.Empty;

    /// <summary>
    /// Transport protocol ("TCP" or "UDP").
    /// </summary>
    public string Protocol { get; init; } = string.Empty;

    /// <summary>
    /// Destination service port (non-ephemeral listening or target port).
    /// </summary>
    public int DestinationPort { get; init; }

    /// <summary>
    /// Well-known service name deduced from port or process (e.g. "HTTPS", "Oracle-TNS", "DNS").
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Operating system process name associated with the flow.
    /// </summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>
    /// Full executable path proving application identity.
    /// </summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>
    /// Operating system service account or user identity executing the process.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Consolidated count of observations across all individual polling cycles.
    /// </summary>
    public long ObservationCount { get; init; }

    /// <summary>
    /// Earliest recorded UTC observation timestamp for this rule tuple.
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; init; }

    /// <summary>
    /// Most recent recorded UTC observation timestamp proving current activity.
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; init; }
}
