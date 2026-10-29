using System.Net;
using PortAtlas.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;

namespace PortAtlas.Core.Export;

/// <summary>
/// Data transfer record for serializing network flow entries to and from NDJSON (JSON Lines).
/// </summary>
public sealed record AuditNdjsonRecord
{
    /// <summary>
    /// UTC timestamp when this flow was first detected.
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; init; }

    /// <summary>
    /// UTC timestamp of the most recent observation.
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; init; }

    /// <summary>
    /// Cumulative count of scan intervals where the flow was active.
    /// </summary>
    public long ObservationCount { get; init; }

    /// <summary>
    /// Host name of the auditing server.
    /// </summary>
    public string ServerName { get; init; } = string.Empty;

    /// <summary>
    /// Operating system description of the host server.
    /// </summary>
    public string ServerOsVersion { get; init; } = string.Empty;

    /// <summary>
    /// Primary IP address of the host server.
    /// </summary>
    public string ServerPrimaryIp { get; init; } = string.Empty;

    /// <summary>
    /// Transport layer protocol (Tcp or Udp).
    /// </summary>
    public string Protocol { get; init; } = string.Empty;

    /// <summary>
    /// Flow direction (Inbound, Outbound, Listening).
    /// </summary>
    public string Direction { get; init; } = string.Empty;

    /// <summary>
    /// TCP connection state or socket status.
    /// </summary>
    public string TcpState { get; init; } = string.Empty;

    /// <summary>
    /// Local IP address of the socket endpoint.
    /// </summary>
    public string LocalIp { get; init; } = string.Empty;

    /// <summary>
    /// Local port number of the socket endpoint.
    /// </summary>
    public int LocalPort { get; init; }

    /// <summary>
    /// Remote IP address of the socket endpoint.
    /// </summary>
    public string RemoteIp { get; init; } = string.Empty;

    /// <summary>
    /// Remote port number of the socket endpoint.
    /// </summary>
    public int RemotePort { get; init; }

    /// <summary>
    /// Resolved remote host name or FQDN.
    /// </summary>
    public string RemoteHostName { get; init; } = string.Empty;

    /// <summary>
    /// Process identifier (PID) owning the socket.
    /// </summary>
    public int ProcessId { get; init; }

    /// <summary>
    /// Base process executable name.
    /// </summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>
    /// Full path to the executable image on disk.
    /// </summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>
    /// Full command line arguments invoked for the process.
    /// </summary>
    public string CommandLine { get; init; } = string.Empty;

    /// <summary>
    /// Unwrapped target payload file path or underlying application executable.
    /// </summary>
    public string TargetExecutable { get; init; } = string.Empty;

    /// <summary>
    /// User account name executing the process.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Converts a <see cref="NetworkAuditEntry"/> into an <see cref="AuditNdjsonRecord"/>.
    /// </summary>
    /// <param name="entry">Source network audit entry.</param>
    /// <returns>Formatted NDJSON data record.</returns>
    public static AuditNdjsonRecord FromEntry(NetworkAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new AuditNdjsonRecord
        {
            FirstSeenUtc = entry.FirstSeenUtc,
            LastSeenUtc = entry.LastSeenUtc,
            ObservationCount = entry.ObservationCount,
            ServerName = entry.Server.HostName,
            ServerOsVersion = entry.Server.OperatingSystemDescription,
            ServerPrimaryIp = entry.Server.PrimaryIpAddress,
            Protocol = entry.Socket.Protocol.ToString(),
            Direction = entry.Socket.Direction.ToString(),
            TcpState = entry.Socket.State,
            LocalIp = entry.Socket.LocalEndpoint.Address.ToString(),
            LocalPort = entry.Socket.LocalEndpoint.Port,
            RemoteIp = entry.Socket.RemoteEndpoint.Address.ToString(),
            RemotePort = entry.Socket.RemoteEndpoint.Port,
            RemoteHostName = entry.RemoteHostName,
            ProcessId = entry.Socket.Process.ProcessId,
            ProcessName = entry.Socket.Process.ProcessName,
            ExecutablePath = entry.Socket.Process.ExecutablePath,
            CommandLine = entry.Socket.Process.CommandLine,
            TargetExecutable = entry.Socket.Process.TargetExecutable,
            UserName = entry.Socket.Process.UserName
        };
    }

    /// <summary>
    /// Converts this record back into a strongly-typed <see cref="NetworkAuditEntry"/>.
    /// </summary>
    /// <returns>Constructed network audit entry.</returns>
    public NetworkAuditEntry ToEntry()
    {
        var localAddress = IPAddress.TryParse(LocalIp, out var parsedLocal) ? parsedLocal : IPAddress.Loopback;
        var remoteAddress = IPAddress.TryParse(RemoteIp, out var parsedRemote) ? parsedRemote : IPAddress.Any;

        var protocol = Enum.TryParse<SocketTransportProtocol>(Protocol, ignoreCase: true, out var parsedProto)
            ? parsedProto
            : SocketTransportProtocol.Tcp;

        var direction = Enum.TryParse<SocketFlowDirection>(Direction, ignoreCase: true, out var parsedDir)
            ? parsedDir
            : SocketFlowDirection.Outbound;

        var serverMetadata = new ServerHostMetadata(
            HostName: string.IsNullOrWhiteSpace(ServerName) ? Environment.MachineName : ServerName,
            OperatingSystemDescription: ServerOsVersion,
            PrimaryIpAddress: ServerPrimaryIp);

        var processMetadata = new ProcessMetadata(
            ProcessId: ProcessId,
            ProcessName: ProcessName,
            ExecutablePath: ExecutablePath,
            CommandLine: CommandLine,
            TargetExecutable: TargetExecutable,
            UserName: UserName);

        var socket = new ActiveSocketConnection(
            Protocol: protocol,
            Direction: direction,
            LocalEndpoint: new SocketEndpoint(localAddress, LocalPort),
            RemoteEndpoint: new SocketEndpoint(remoteAddress, RemotePort),
            State: TcpState,
            Process: processMetadata);

        return new NetworkAuditEntry(
            FirstSeenUtc: FirstSeenUtc,
            LastSeenUtc: LastSeenUtc,
            ObservationCount: ObservationCount > 0 ? ObservationCount : 1,
            Server: serverMetadata,
            Socket: socket,
            RemoteHostName: RemoteHostName);
    }
}
