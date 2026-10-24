using System.Net;
using SocketForge.Models;

namespace NetFlowAudit.Core.Filtering;

/// <summary>
/// Filtering contract evaluating whether active network connections must be excluded from auditing.
/// </summary>
public interface INetworkFlowFilter
{
    /// <summary>
    /// Evaluates whether the given socket connection must be excluded from flow auditing.
    /// </summary>
    /// <param name="connection">Inspected active socket connection.</param>
    /// <param name="localHostAddresses">Set of IP addresses assigned to the local host machine.</param>
    /// <returns>True if the flow should be excluded; otherwise false.</returns>
    bool ShouldExclude(ActiveSocketConnection connection, IReadOnlySet<IPAddress> localHostAddresses);

    /// <summary>
    /// Dynamically registers a local port into the exclusion filter at runtime.
    /// </summary>
    /// <param name="port">Local port number to exclude.</param>
    void ExcludeLocalPort(int port);

    /// <summary>
    /// Dynamically registers a remote port into the exclusion filter at runtime.
    /// </summary>
    /// <param name="port">Remote port number to exclude.</param>
    void ExcludeRemotePort(int port);

    /// <summary>
    /// Dynamically registers a remote IP address or CIDR notation into the exclusion filter at runtime.
    /// </summary>
    /// <param name="ipOrCidr">Remote IP address or CIDR subnet notation to exclude.</param>
    void ExcludeRemoteIp(string ipOrCidr);
}
