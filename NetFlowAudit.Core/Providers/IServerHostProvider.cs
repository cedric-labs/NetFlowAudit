using System.Net;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Providers;

/// <summary>
/// Abstraction contract providing local host machine metadata and network interface IP addresses.
/// </summary>
public interface IServerHostProvider
{
    /// <summary>
    /// Retrieves identification and environment metadata of the local server.
    /// </summary>
    /// <returns>Local host server metadata.</returns>
    ServerHostMetadata GetCurrentHostMetadata();

    /// <summary>
    /// Retrieves all IP addresses bound to active local network interfaces.
    /// </summary>
    /// <returns>Read-only set of local IP addresses.</returns>
    IReadOnlySet<IPAddress> GetLocalIpAddresses();
}
