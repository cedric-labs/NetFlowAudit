using System.Net;
using SocketForge.Models;

namespace NetFlowAudit.Core.Filtering;

/// <summary>
/// Moteur de filtrage des flux réseau selon les ports configurés, les adresses IP distantes et les adresses intra-machine.
/// </summary>
public sealed class NetworkFlowFilter(NetworkFlowFilterOptions options) : INetworkFlowFilter
{
    private readonly NetworkFlowFilterOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IpAddressRangeMatcher _remoteIpMatcher = new(options?.ExcludedRemoteIps);

    /// <inheritdoc />
    public bool ShouldExclude(ActiveSocketConnection connection, IReadOnlySet<IPAddress> localHostAddresses)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (_options.ExcludedLocalPorts.Contains(connection.LocalEndpoint.Port))
        {
            return true;
        }

        if (connection.RemoteEndpoint.Port > 0 && _options.ExcludedRemotePorts.Contains(connection.RemoteEndpoint.Port))
        {
            return true;
        }

        if (_options.ExcludeLoopbackFlows && IsInternalFlow(connection, localHostAddresses))
        {
            return true;
        }

        if (_remoteIpMatcher.HasRules &&
            connection.Direction != SocketFlowDirection.Listening &&
            !connection.RemoteEndpoint.Address.Equals(IPAddress.Any) &&
            !connection.RemoteEndpoint.Address.Equals(IPAddress.IPv6Any) &&
            _remoteIpMatcher.IsMatch(connection.RemoteEndpoint.Address))
        {
            return true;
        }

        return false;
    }

    private static bool IsInternalFlow(ActiveSocketConnection connection, IReadOnlySet<IPAddress> localHostAddresses)
    {
        var localAddr = connection.LocalEndpoint.Address;
        var remoteAddr = connection.RemoteEndpoint.Address;

        // Si la socket écoute ou communique exclusivement sur loopback, il s'agit d'un flux interne
        if (IPAddress.IsLoopback(localAddr))
        {
            if (connection.Direction == SocketFlowDirection.Listening ||
                IPAddress.IsLoopback(remoteAddr) ||
                remoteAddr.Equals(IPAddress.Any) ||
                remoteAddr.Equals(IPAddress.IPv6Any) ||
                connection.RemoteEndpoint.Port == 0)
            {
                return true;
            }
        }

        // Si la socket écoute sur 0.0.0.0 ou toute autre interface publique, elle n'est pas interne
        if (connection.Direction == SocketFlowDirection.Listening ||
            remoteAddr.Equals(IPAddress.Any) ||
            remoteAddr.Equals(IPAddress.IPv6Any) ||
            connection.RemoteEndpoint.Port == 0)
        {
            return false;
        }

        if (localAddr.Equals(remoteAddr))
        {
            return true;
        }

        if (IPAddress.IsLoopback(remoteAddr))
        {
            return true;
        }

        if (localHostAddresses is not null && localHostAddresses.Contains(localAddr) && localHostAddresses.Contains(remoteAddr))
        {
            return true;
        }

        return false;
    }
}
