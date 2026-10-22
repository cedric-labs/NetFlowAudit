using System.Net;
using System.Runtime.InteropServices;
using NetFlowAudit.Core.Contracts;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Network.Windows;

/// <summary>
/// Scanneur de sockets TCP et UDP Windows utilisant les API Win32 natives IP Helper (iphlpapi.dll).
/// Ne requiert aucun driver tiers ni installation externe (zéro Npcap).
/// </summary>
public sealed class WindowsSocketScanner(IProcessInspector processInspector, IServerHostProvider hostProvider) : INetworkSocketScanner
{
    private readonly IProcessInspector _processInspector = processInspector ?? throw new ArgumentNullException(nameof(processInspector));
    private readonly IServerHostProvider _hostProvider = hostProvider ?? throw new ArgumentNullException(nameof(hostProvider));

    private const int AfInet = 2; // AF_INET (IPv4)
    private const int TcpTableOwnerPidAll = 5; // TCP_TABLE_OWNER_PID_ALL
    private const int UdpTableOwnerPid = 1; // UDP_TABLE_OWNER_PID

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int pdwSize,
        bool bOrder,
        int ulAf,
        int tableClass,
        uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable,
        ref int pdwSize,
        bool bOrder,
        int ulAf,
        int tableClass,
        uint reserved);

    /// <inheritdoc />
    public async Task<IReadOnlyList<NetworkConnectionRecord>> ScanActiveConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<NetworkConnectionRecord>();
        var host = _hostProvider.GetCurrentHostMetadata();
        var now = DateTimeOffset.UtcNow;

        var tcpRows = ReadNativeTcpTable();
        for (var i = 0; i < tcpRows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = tcpRows[i];
            var process = await _processInspector.ResolveProcessAsync(row.OwningPid, cancellationToken).ConfigureAwait(false);

            var localEp = new NetworkEndpoint(new IPAddress(row.LocalAddr), ConvertPort(row.LocalPort));
            var remoteEp = new NetworkEndpoint(new IPAddress(row.RemoteAddr), ConvertPort(row.RemotePort));

            var direction = DetermineDirection(localEp, remoteEp, row.State);

            records.Add(new NetworkConnectionRecord(
                FirstSeenUtc: now,
                LastSeenUtc: now,
                ObservationCount: 1,
                Server: host,
                Protocol: NetworkTransportProtocol.Tcp,
                Direction: direction,
                LocalEndpoint: localEp,
                RemoteEndpoint: remoteEp,
                TcpState: MapTcpState(row.State),
                Process: process));
        }

        var udpRows = ReadNativeUdpTable();
        for (var i = 0; i < udpRows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = udpRows[i];
            var process = await _processInspector.ResolveProcessAsync(row.OwningPid, cancellationToken).ConfigureAwait(false);

            var localEp = new NetworkEndpoint(new IPAddress(row.LocalAddr), ConvertPort(row.LocalPort));
            var remoteEp = new NetworkEndpoint(IPAddress.Any, 0);

            records.Add(new NetworkConnectionRecord(
                FirstSeenUtc: now,
                LastSeenUtc: now,
                ObservationCount: 1,
                Server: host,
                Protocol: NetworkTransportProtocol.Udp,
                Direction: FlowDirection.Listening,
                LocalEndpoint: localEp,
                RemoteEndpoint: remoteEp,
                TcpState: "UDP",
                Process: process));
        }

        return records;
    }

    private static FlowDirection DetermineDirection(NetworkEndpoint local, NetworkEndpoint remote, uint state)
    {
        // 2 = MIB_TCP_STATE_LISTEN
        if (state == 2 || remote.Address.Equals(IPAddress.Any) || remote.Port == 0)
        {
            return FlowDirection.Listening;
        }

        // Si le port local est éphémère (> 49151) et distant est un port de service usuel (< 49152) => Outbound
        if (local.Port > 1024 && remote.Port < 1024)
        {
            return FlowDirection.Outbound;
        }

        if (local.Port < 1024 && remote.Port > 1024)
        {
            return FlowDirection.Inbound;
        }

        return FlowDirection.Outbound;
    }

    private static int ConvertPort(uint port)
    {
        return (int)(((port & 0xFF) << 8) | ((port >> 8) & 0xFF));
    }

    private static string MapTcpState(uint state)
    {
        return state switch
        {
            1 => "Closed",
            2 => "Listen",
            3 => "SynSent",
            4 => "SynReceived",
            5 => "Established",
            6 => "FinWait1",
            7 => "FinWait2",
            8 => "CloseWait",
            9 => "Closing",
            10 => "LastAck",
            11 => "TimeWait",
            12 => "DeleteTCB",
            _ => $"Unknown({state})"
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MibTcpRowOwnerPid
    {
        public readonly uint State;
        public readonly uint LocalAddr;
        public readonly uint LocalPort;
        public readonly uint RemoteAddr;
        public readonly uint RemotePort;
        public readonly int OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MibUdpRowOwnerPid
    {
        public readonly uint LocalAddr;
        public readonly uint LocalPort;
        public readonly int OwningPid;
    }

    private static List<MibTcpRowOwnerPid> ReadNativeTcpTable()
    {
        var list = new List<MibTcpRowOwnerPid>();
        var size = 0;

        _ = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
        if (size <= 0)
        {
            return list;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var ret = GetExtendedTcpTable(buffer, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
            if (ret != 0)
            {
                return list;
            }

            var numEntries = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();

            for (var i = 0; i < numEntries; i++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr);
                list.Add(row);
                rowPtr = IntPtr.Add(rowPtr, rowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return list;
    }

    private static List<MibUdpRowOwnerPid> ReadNativeUdpTable()
    {
        var list = new List<MibUdpRowOwnerPid>();
        var size = 0;

        _ = GetExtendedUdpTable(IntPtr.Zero, ref size, true, AfInet, UdpTableOwnerPid, 0);
        if (size <= 0)
        {
            return list;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var ret = GetExtendedUdpTable(buffer, ref size, true, AfInet, UdpTableOwnerPid, 0);
            if (ret != 0)
            {
                return list;
            }

            var numEntries = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibUdpRowOwnerPid>();

            for (var i = 0; i < numEntries; i++)
            {
                var row = Marshal.PtrToStructure<MibUdpRowOwnerPid>(rowPtr);
                list.Add(row);
                rowPtr = IntPtr.Add(rowPtr, rowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return list;
    }
}
