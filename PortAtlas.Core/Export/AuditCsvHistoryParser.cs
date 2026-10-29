using System.Globalization;
using System.Net;
using System.Text;
using PortAtlas.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;

namespace PortAtlas.Core.Export;

/// <summary>
/// Default implementation of <see cref="IAuditCsvHistoryParser"/> that parses historical RFC 4180 audit CSV records.
/// </summary>
public sealed class AuditCsvHistoryParser : IAuditCsvHistoryParser
{
    private static readonly Dictionary<string, int> StandardPositionalColumnMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FirstSeenUtc"] = 0,
        ["LastSeenUtc"] = 1,
        ["ObservationCount"] = 2,
        ["ServerName"] = 3,
        ["ServerOsVersion"] = 4,
        ["ServerPrimaryIp"] = 5,
        ["Protocol"] = 6,
        ["Direction"] = 7,
        ["TcpState"] = 8,
        ["LocalIp"] = 9,
        ["LocalPort"] = 10,
        ["RemoteIp"] = 11,
        ["RemotePort"] = 12,
        ["RemoteHostName"] = 13,
        ["ProcessId"] = 14,
        ["ProcessName"] = 15,
        ["ExecutablePath"] = 16,
        ["CommandLine"] = 17,
        ["TargetExecutable"] = 18,
        ["UserName"] = 19
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<NetworkAuditEntry>> ParseHistoryAsync(
        string csvFilePath,
        int? maxRows = null,
        char delimiter = ',',
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvFilePath);

        if (maxRows.HasValue && maxRows.Value == 0)
        {
            return [];
        }

        if (!File.Exists(csvFilePath))
        {
            return [];
        }

        var lines = await ReadAllLinesAsync(csvFilePath, cancellationToken).ConfigureAwait(false);
        if (lines.Count == 0)
        {
            return [];
        }

        var firstLine = lines[0];
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return [];
        }

        var firstLineFields = SplitCsvLine(firstLine, delimiter);
        var columnMap = BuildColumnIndexMap(firstLineFields);
        var isHeaderPresent = columnMap.ContainsKey("ServerName") || columnMap.ContainsKey("LocalPort");

        int dataStartIndex;
        if (isHeaderPresent)
        {
            dataStartIndex = 1;
        }
        else
        {
            columnMap = StandardPositionalColumnMap;
            dataStartIndex = 0;
        }

        var dataLineCount = lines.Count - dataStartIndex;
        if (dataLineCount <= 0)
        {
            return [];
        }

        var startIndex = dataStartIndex;
        if (maxRows.HasValue && maxRows.Value > 0 && maxRows.Value < dataLineCount)
        {
            startIndex = lines.Count - maxRows.Value;
        }

        var results = new List<NetworkAuditEntry>(dataLineCount);

        for (var i = startIndex; i < lines.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rawLine = lines[i];
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var fields = SplitCsvLine(rawLine, delimiter);
            var entry = ConvertFieldsToAuditEntry(fields, columnMap);
            if (entry is not null)
            {
                results.Add(entry);
            }
        }

        return results;
    }

    private static async Task<List<string>> ReadAllLinesAsync(string path, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static Dictionary<string, int> BuildColumnIndexMap(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var clean = headers[i].Trim();
            if (!string.IsNullOrEmpty(clean) && !map.ContainsKey(clean))
            {
                map[clean] = i;
            }
        }

        return map;
    }

    private static NetworkAuditEntry? ConvertFieldsToAuditEntry(
        IReadOnlyList<string> fields,
        Dictionary<string, int> columnMap)
    {
        var firstSeenStr = GetField(fields, columnMap, "FirstSeenUtc");
        var lastSeenStr = GetField(fields, columnMap, "LastSeenUtc");
        var obsCountStr = GetField(fields, columnMap, "ObservationCount");
        var serverName = GetField(fields, columnMap, "ServerName");
        var serverOs = GetField(fields, columnMap, "ServerOsVersion");
        var serverPrimaryIp = GetField(fields, columnMap, "ServerPrimaryIp");
        var protocolStr = GetField(fields, columnMap, "Protocol");
        var directionStr = GetField(fields, columnMap, "Direction");
        var tcpState = GetField(fields, columnMap, "TcpState");
        var localIpStr = GetField(fields, columnMap, "LocalIp");
        var localPortStr = GetField(fields, columnMap, "LocalPort");
        var remoteIpStr = GetField(fields, columnMap, "RemoteIp");
        var remotePortStr = GetField(fields, columnMap, "RemotePort");
        var remoteHostName = GetField(fields, columnMap, "RemoteHostName");
        var pidStr = GetField(fields, columnMap, "ProcessId");
        var processName = GetField(fields, columnMap, "ProcessName");
        var execPath = GetField(fields, columnMap, "ExecutablePath");
        var commandLine = GetField(fields, columnMap, "CommandLine");
        var targetExec = GetField(fields, columnMap, "TargetExecutable");
        var userName = GetField(fields, columnMap, "UserName");

        if (string.IsNullOrWhiteSpace(serverName) && string.IsNullOrWhiteSpace(localPortStr))
        {
            return null;
        }

        var firstSeen = DateTimeOffset.TryParse(firstSeenStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedFirst)
            ? parsedFirst
            : DateTimeOffset.UtcNow;

        var lastSeen = DateTimeOffset.TryParse(lastSeenStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedLast)
            ? parsedLast
            : firstSeen;

        _ = long.TryParse(obsCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var obsCount);
        if (obsCount <= 0)
        {
            obsCount = 1;
        }

        var protocol = Enum.TryParse<SocketTransportProtocol>(protocolStr, ignoreCase: true, out var parsedProtocol)
            ? parsedProtocol
            : SocketTransportProtocol.Tcp;

        var direction = Enum.TryParse<SocketFlowDirection>(directionStr, ignoreCase: true, out var parsedDirection)
            ? parsedDirection
            : SocketFlowDirection.Outbound;

        var localAddress = IPAddress.TryParse(localIpStr, out var parsedLocalIp)
            ? parsedLocalIp
            : IPAddress.Loopback;

        _ = int.TryParse(localPortStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var localPort);

        var remoteAddress = IPAddress.TryParse(remoteIpStr, out var parsedRemoteIp)
            ? parsedRemoteIp
            : IPAddress.Any;

        _ = int.TryParse(remotePortStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var remotePort);
        _ = int.TryParse(pidStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid);

        var serverMetadata = new ServerHostMetadata(
            HostName: string.IsNullOrWhiteSpace(serverName) ? Environment.MachineName : serverName,
            OperatingSystemDescription: serverOs,
            PrimaryIpAddress: serverPrimaryIp);

        var processMetadata = new ProcessMetadata(
            ProcessId: pid,
            ProcessName: processName,
            ExecutablePath: execPath,
            CommandLine: commandLine,
            TargetExecutable: targetExec,
            UserName: userName);

        var socket = new ActiveSocketConnection(
            Protocol: protocol,
            Direction: direction,
            LocalEndpoint: new SocketEndpoint(localAddress, localPort),
            RemoteEndpoint: new SocketEndpoint(remoteAddress, remotePort),
            State: string.IsNullOrWhiteSpace(tcpState) ? "Established" : tcpState,
            Process: processMetadata);

        return new NetworkAuditEntry(
            FirstSeenUtc: firstSeen,
            LastSeenUtc: lastSeen,
            ObservationCount: obsCount,
            Server: serverMetadata,
            Socket: socket,
            RemoteHostName: remoteHostName);
    }

    private static string GetField(IReadOnlyList<string> fields, Dictionary<string, int> columnMap, string columnName)
    {
        if (columnMap.TryGetValue(columnName, out var index) && index >= 0 && index < fields.Count)
        {
            return fields[index];
        }

        return string.Empty;
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString());
        return result;
    }
}
