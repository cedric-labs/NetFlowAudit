namespace PortAtlas.Core.Filtering;

/// <summary>
/// Network flow filtering options (loopback traffic, local ports, remote ports, remote IP addresses).
/// </summary>
public sealed record NetworkFlowFilterOptions
{
    /// <summary>
    /// Excludes intra-machine flows (LocalIp == RemoteIp or loopback address ranges).
    /// </summary>
    public bool ExcludeLoopbackFlows { get; init; } = true;

    /// <summary>
    /// Local ports to exclude from flow auditing.
    /// </summary>
    public IReadOnlySet<int> ExcludedLocalPorts { get; init; } = new HashSet<int>();

    /// <summary>
    /// Remote ports to exclude from flow auditing.
    /// </summary>
    public IReadOnlySet<int> ExcludedRemotePorts { get; init; } = new HashSet<int>();

    /// <summary>
    /// Remote IP addresses or CIDR subnets to exclude from flow auditing.
    /// </summary>
    public IReadOnlyList<string> ExcludedRemoteIps { get; init; } = [];
}
