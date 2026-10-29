namespace PortAtlas.Core.Export;

/// <summary>
/// Filtering and formatting options for consolidating network audits into firewall and security flow matrices.
/// </summary>
public sealed record FirewallRuleExportOptions
{
    /// <summary>
    /// Exclude high ephemeral ports (49152-65535) on the destination side.
    /// </summary>
    public bool ExcludeEphemeralPorts { get; init; } = true;

    /// <summary>
    /// Minimum gross observation count required for a rule to be included in the export (default: 1).
    /// </summary>
    public int MinObservations { get; init; } = 1;

    /// <summary>
    /// Optional freshness filter in days: excludes rules that have not been observed within the specified recent window.
    /// </summary>
    public int? ActiveWithinDays { get; init; }

    /// <summary>
    /// Destination zone filter ("all", "internal" for RFC 1918, "public" for WAN/Internet).
    /// </summary>
    public string DestinationZoneFilter { get; init; } = "all";

    /// <summary>
    /// When true, includes passive listening ports only.
    /// </summary>
    public bool ListenersOnly { get; init; } = false;

    /// <summary>
    /// Flow direction filter ("all", "out", "in").
    /// </summary>
    public string Direction { get; init; } = "all";

    /// <summary>
    /// Destination port numbers or ranges to include (e.g. "80", "443", "8000-8080").
    /// </summary>
    public IReadOnlyList<string> IncludedPorts { get; init; } = [];

    /// <summary>
    /// Destination port numbers or ranges to exclude.
    /// </summary>
    public IReadOnlyList<string> ExcludedPorts { get; init; } = [];

    /// <summary>
    /// Host name wildcard patterns to include (supports '*').
    /// </summary>
    public IReadOnlyList<string> IncludedHosts { get; init; } = [];

    /// <summary>
    /// Host name wildcard patterns to exclude (supports '*').
    /// </summary>
    public IReadOnlyList<string> ExcludedHosts { get; init; } = [];

    /// <summary>
    /// IP addresses or CIDR subnets to include.
    /// </summary>
    public IReadOnlyList<string> IncludedIps { get; init; } = [];

    /// <summary>
    /// IP addresses or CIDR subnets to exclude.
    /// </summary>
    public IReadOnlyList<string> ExcludedIps { get; init; } = [];

    /// <summary>
    /// Process names or targets to include.
    /// </summary>
    public IReadOnlyList<string> IncludedProcesses { get; init; } = [];

    /// <summary>
    /// Process names or targets to exclude.
    /// </summary>
    public IReadOnlyList<string> ExcludedProcesses { get; init; } = [];

    /// <summary>
    /// Field delimiter character for the CSV output (comma ',' by default).
    /// </summary>
    public char Delimiter { get; init; } = ',';
}
