using System.Globalization;
using System.Net;
using NetFlowAudit.Core.Contracts;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Network.Linux;

/// <summary>
/// Scanneur de sockets Linux basé sur l'analyse native du pseudo-système de fichiers /proc/net.
/// Ne nécessite aucune bibliothèque externe ni droit d'installation spécifique.
/// </summary>
public sealed class LinuxProcSocketScanner(IProcessInspector processInspector, IServerHostProvider hostProvider) : INetworkSocketScanner
{
    private readonly IProcessInspector _processInspector = processInspector ?? throw new ArgumentNullException(nameof(processInspector));
    private readonly IServerHostProvider _hostProvider = hostProvider ?? throw new ArgumentNullException(nameof(hostProvider));

    /// <inheritdoc />
    public async Task<IReadOnlyList<NetworkConnectionRecord>> ScanActiveConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<NetworkConnectionRecord>();
        var host = _hostProvider.GetCurrentHostMetadata();
        var now = DateTimeOffset.UtcNow;

        var inodeToPid = BuildInodeToPidMap();

        if (File.Exists("/proc/net/tcp"))
        {
            var tcpLines = await File.ReadAllLinesAsync("/proc/net/tcp", cancellationToken).ConfigureAwait(false);
            ParseProcNetLines(tcpLines, NetworkTransportProtocol.Tcp, inodeToPid, records, host, now, cancellationToken);
        }

        if (File.Exists("/proc/net/udp"))
        {
            var udpLines = await File.ReadAllLinesAsync("/proc/net/udp", cancellationToken).ConfigureAwait(false);
            ParseProcNetLines(udpLines, NetworkTransportProtocol.Udp, inodeToPid, records, host, now, cancellationToken);
        }

        return records;
    }

    private void ParseProcNetLines(
        string[] lines,
        NetworkTransportProtocol protocol,
        Dictionary<string, int> inodeToPid,
        List<NetworkConnectionRecord> records,
        ServerHostMetadata host,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        for (var i = 1; i < lines.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 10)
            {
                continue;
            }

            var localEp = ParseEndpoint(parts[1]);
            var remoteEp = ParseEndpoint(parts[2]);
            var stateHex = parts[3];
            var inode = parts[9];

            _ = inodeToPid.TryGetValue(inode, out var pid);
            var process = _processInspector.ResolveProcessAsync(pid, cancellationToken).GetAwaiter().GetResult();

            var direction = remoteEp.Port == 0 || remoteEp.Address.Equals(IPAddress.Any)
                ? FlowDirection.Listening
                : FlowDirection.Outbound;

            records.Add(new NetworkConnectionRecord(
                FirstSeenUtc: now,
                LastSeenUtc: now,
                ObservationCount: 1,
                Server: host,
                Protocol: protocol,
                Direction: direction,
                LocalEndpoint: localEp,
                RemoteEndpoint: remoteEp,
                TcpState: MapLinuxState(stateHex),
                Process: process));
        }
    }

    private static NetworkEndpoint ParseEndpoint(string hexEndpoint)
    {
        var tokens = hexEndpoint.Split(':');
        if (tokens.Length != 2)
        {
            return new NetworkEndpoint(IPAddress.Any, 0);
        }

        var ipHex = tokens[0];
        var portHex = tokens[1];

        var ipInt = uint.Parse(ipHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var port = int.Parse(portHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        var ip = new IPAddress(ipInt);
        return new NetworkEndpoint(ip, port);
    }

    private static string MapLinuxState(string stateHex)
    {
        return stateHex switch
        {
            "0A" => "Listen",
            "01" => "Established",
            "02" => "SynSent",
            "03" => "SynRecv",
            "04" => "FinWait1",
            "05" => "FinWait2",
            "06" => "TimeWait",
            "07" => "Close",
            "08" => "CloseWait",
            "09" => "LastAck",
            _ => $"State({stateHex})"
        };
    }

    private static Dictionary<string, int> BuildInodeToPidMap()
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!Directory.Exists("/proc"))
        {
            return map;
        }

        try
        {
            var procDirs = Directory.GetDirectories("/proc");
            foreach (var dir in procDirs)
            {
                var dirName = Path.GetFileName(dir);
                if (!int.TryParse(dirName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                {
                    continue;
                }

                var fdDir = Path.Combine(dir, "fd");
                if (!Directory.Exists(fdDir))
                {
                    continue;
                }

                try
                {
                    var links = Directory.GetFiles(fdDir);
                    foreach (var link in links)
                    {
                        var target = File.ResolveLinkTarget(link, returnFinalTarget: false);
                        if (target is not null && target.FullName.StartsWith("socket:[", StringComparison.Ordinal))
                        {
                            var inode = target.FullName.Replace("socket:[", "", StringComparison.Ordinal).Replace("]", "", StringComparison.Ordinal);
                            map[inode] = pid;
                        }
                    }
                }
                catch (IOException)
                {
                    // Erreur lecture fd
                }
                catch (UnauthorizedAccessException)
                {
                    // Droits insuffisants
                }
            }
        }
        catch (IOException)
        {
            // Erreur globale /proc
        }

        return map;
    }
}
