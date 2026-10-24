using System.Net;
using SocketForge.Models;

namespace NetFlowAudit.Core.Filtering;

/// <summary>
/// Evaluates and filters network flows based on configured ports, remote IP addresses, and intra-machine loopback traffic with thread-safe dynamic updates.
/// </summary>
public sealed class NetworkFlowFilter(NetworkFlowFilterOptions options) : INetworkFlowFilter
{
    private readonly NetworkFlowFilterOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly HashSet<int> _excludedLocalPorts = options?.ExcludedLocalPorts is not null ? new(options.ExcludedLocalPorts) : [];
    private readonly HashSet<int> _excludedRemotePorts = options?.ExcludedRemotePorts is not null ? new(options.ExcludedRemotePorts) : [];
    private readonly List<string> _currentRemoteIps = options?.ExcludedRemoteIps is not null ? new(options.ExcludedRemoteIps) : [];
    private readonly object _syncLock = new();
    private IpAddressRangeMatcher _remoteIpMatcher = new(options?.ExcludedRemoteIps);

    /// <inheritdoc />
    public void ExcludeLocalPort(int port)
    {
        lock (_syncLock)
        {
            _excludedLocalPorts.Add(port);
        }
    }

    /// <inheritdoc />
    public void ExcludeRemotePort(int port)
    {
        lock (_syncLock)
        {
            _excludedRemotePorts.Add(port);
        }
    }

    /// <inheritdoc />
    public void ExcludeRemoteIp(string ipOrCidr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipOrCidr);
        lock (_syncLock)
        {
            var trimmed = ipOrCidr.Trim();
            if (IPAddress.TryParse(trimmed, out var parsed) && parsed.IsIPv4MappedToIPv6)
            {
                trimmed = parsed.MapToIPv4().ToString();
            }

            if (!_currentRemoteIps.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                _currentRemoteIps.Add(trimmed);
                _remoteIpMatcher = new IpAddressRangeMatcher(_currentRemoteIps);
            }
        }
    }

    /// <inheritdoc />
    public bool ShouldExclude(ActiveSocketConnection connection, IReadOnlySet<IPAddress> localHostAddresses)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_syncLock)
        {
            if (_excludedLocalPorts.Contains(connection.LocalEndpoint.Port))
            {
                return true;
            }

            if (_excludedRemotePorts.Contains(connection.RemoteEndpoint.Port))
            {
                return true;
            }

            if (_options.ExcludeLoopbackFlows && IsInternalFlow(connection, localHostAddresses))
            {
                return true;
            }

            if (_remoteIpMatcher.HasRules && _remoteIpMatcher.IsMatch(connection.RemoteEndpoint.Address))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInternalFlow(ActiveSocketConnection connection, IReadOnlySet<IPAddress> localHostAddresses)
    {
        var localAddr = connection.LocalEndpoint.Address;
        var remoteAddr = connection.RemoteEndpoint.Address;

        // If the socket listens or communicates exclusively on loopback, it is an internal host flow
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

        // If the socket listens on 0.0.0.0 or any public interface, it is an exposed service
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
