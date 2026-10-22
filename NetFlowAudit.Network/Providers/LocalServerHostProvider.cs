using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetFlowAudit.Core.Contracts;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Network.Providers;

/// <summary>
/// Fournisseur d'informations et de métadonnées relatives au serveur local hôte.
/// </summary>
public sealed class LocalServerHostProvider : IServerHostProvider
{
    private readonly Lazy<ServerHostMetadata> _cachedMetadata = new(ResolveMetadata);

    /// <inheritdoc />
    public ServerHostMetadata GetCurrentHostMetadata()
    {
        return _cachedMetadata.Value;
    }

    private static ServerHostMetadata ResolveMetadata()
    {
        var hostName = Environment.MachineName;
        var osDescription = Environment.OSVersion.VersionString;

        var primaryIp = ResolvePrimaryIpAddress();

        return new ServerHostMetadata(
            HostName: hostName,
            OperatingSystemDescription: osDescription,
            PrimaryIpAddress: primaryIp);
    }

    private static string ResolvePrimaryIpAddress()
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in networkInterfaces)
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
                        return unicast.Address.ToString();
                    }
                }
            }
        }
        catch (NetworkInformationException)
        {
            // Erreur d'accès à l'interface réseau, repli sur IP locale par défaut
        }

        return "127.0.0.1";
    }
}
