using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PortAtlas.Core.Models;

namespace PortAtlas.Core.Providers;

/// <summary>
/// Provides host machine metadata and network interface IP addresses for the local runtime environment.
/// </summary>
public sealed class LocalServerHostProvider : IServerHostProvider
{
    private readonly Lazy<ServerHostMetadata> _cachedMetadata = new(ResolveMetadata);
    private readonly Lazy<HashSet<IPAddress>> _cachedAddresses = new(ResolveAllLocalAddresses);

    /// <summary>
    /// Retrieves identification and environment metadata of the local server.
    /// </summary>
    public ServerHostMetadata GetCurrentHostMetadata() => _cachedMetadata.Value;

    /// <summary>
    /// Retrieves all IP addresses bound to active local network interfaces.
    /// </summary>
    public IReadOnlySet<IPAddress> GetLocalIpAddresses() => _cachedAddresses.Value;

    private static ServerHostMetadata ResolveMetadata()
    {
        var hostName = Environment.MachineName;
        var osDescription = Environment.OSVersion.VersionString;
        var primaryIp = "127.0.0.1";

        try
        {
            var ifaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in ifaces)
            {
                if (iface.OperationalStatus != OperationalStatus.Up ||
                    iface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var ipProps = iface.GetIPProperties();
                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(unicast.Address))
                    {
                        primaryIp = unicast.Address.ToString();
                        break;
                    }
                }

                if (primaryIp != "127.0.0.1")
                {
                    break;
                }
            }
        }
        catch (NetworkInformationException)
        {
            // Tolerated on restricted security environments or unavailable network interfaces
        }

        return new ServerHostMetadata(hostName, osDescription, primaryIp);
    }

    private static HashSet<IPAddress> ResolveAllLocalAddresses()
    {
        var set = new HashSet<IPAddress>
        {
            IPAddress.Loopback,
            IPAddress.IPv6Loopback
        };

        try
        {
            var ifaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in ifaces)
            {
                if (iface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var ipProps = iface.GetIPProperties();
                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    set.Add(unicast.Address);
                }
            }
        }
        catch (NetworkInformationException)
        {
            // Tolerated on restricted security environments
        }

        return set;
    }
}
