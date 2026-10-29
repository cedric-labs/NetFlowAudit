using System.Net;

namespace PortAtlas.Core.Resolution;

/// <summary>
/// Event arguments raised when a background resolution task finishes resolving an IP address.
/// </summary>
/// <param name="address">Resolved IP address.</param>
/// <param name="hostName">Corresponding host name (or empty string if no PTR/RDAP record exists).</param>
public sealed class HostNameResolvedEventArgs(IPAddress address, string hostName) : EventArgs
{
    /// <summary>
    /// Resolved IP address.
    /// </summary>
    public IPAddress Address { get; } = address ?? throw new ArgumentNullException(nameof(address));

    /// <summary>
    /// Corresponding resolved host name.
    /// </summary>
    public string HostName { get; } = hostName ?? throw new ArgumentNullException(nameof(hostName));
}
