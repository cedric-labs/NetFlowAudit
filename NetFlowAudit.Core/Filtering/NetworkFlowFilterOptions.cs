namespace NetFlowAudit.Core.Filtering;

/// <summary>
/// Options de filtrage des flux réseau (loopback, ports locaux, ports distants).
/// </summary>
public sealed record NetworkFlowFilterOptions
{
    /// <summary>
    /// Exclut les flux locaux intra-machine (LocalIp == RemoteIp ou adresses loopback).
    /// </summary>
    public bool ExcludeLoopbackFlows { get; init; } = true;

    /// <summary>
    /// Ports locaux à exclure de l'audit.
    /// </summary>
    public IReadOnlySet<int> ExcludedLocalPorts { get; init; } = new HashSet<int>();

    /// <summary>
    /// Ports distants à exclure de l'audit.
    /// </summary>
    public IReadOnlySet<int> ExcludedRemotePorts { get; init; } = new HashSet<int>();
}
