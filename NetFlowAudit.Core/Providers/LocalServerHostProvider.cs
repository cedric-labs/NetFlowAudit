using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Providers;

/// <summary>
/// Fournisseur d'informations sur l'hôte local et ses adresses réseau.
/// </summary>
public sealed class LocalServerHostProvider
{
    private readonly Lazy<ServerHostMetadata> _cachedMetadata = new(ResolveMetadata);
    private readonly Lazy<HashSet<IPAddress>> _cachedAddresses = new(ResolveAllLocalAddresses);

    /// <summary>
    /// Récupère les métadonnées du serveur local.
    /// </summary>
    public ServerHostMetadata GetCurrentHostMetadata() => _cachedMetadata.Value;

    /// <summary>
    /// Récupère la liste de toutes les adresses IP associées à la machine hôte.
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
            // Toléré lors de l'absence de droits ou carte réseau indisponible
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
            // Toléré lors de l'absence de droits
        }

        return set;
    }
}
