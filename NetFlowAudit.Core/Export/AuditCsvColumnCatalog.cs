namespace NetFlowAudit.Core.Export;

/// <summary>
/// Descriptive metadata for an audit CSV column used in CLI help and documentation.
/// </summary>
/// <param name="Column">Enumerated column identifier.</param>
/// <param name="Name">Technical column name used in configuration and CLI arguments.</param>
/// <param name="Category">Thematic category (Timestamp, Server, Network, Process).</param>
/// <param name="Description">Detailed description of the column purpose and content.</param>
/// <param name="Example">Typical observed value example.</param>
public sealed record AuditCsvColumnDescriptor(
    AuditCsvColumn Column,
    string Name,
    string Category,
    string Description,
    string Example);

/// <summary>
/// Catalog providing the complete list of CSV column descriptors and column name resolution.
/// </summary>
public static class AuditCsvColumnCatalog
{
    private static readonly IReadOnlyList<AuditCsvColumnDescriptor> Descriptors =
    [
        new(
            AuditCsvColumn.FirstSeenUtc,
            nameof(AuditCsvColumn.FirstSeenUtc),
            "Timestamp",
            "UTC date and time when the network flow was first observed.",
            "2026-10-23T07:15:00.0000000Z"),
        new(
            AuditCsvColumn.LastSeenUtc,
            nameof(AuditCsvColumn.LastSeenUtc),
            "Timestamp",
            "UTC date and time when the network flow was most recently observed.",
            "2026-10-23T07:20:00.0000000Z"),
        new(
            AuditCsvColumn.ObservationCount,
            nameof(AuditCsvColumn.ObservationCount),
            "Metrics",
            "Cumulative number of scan cycles where this flow was seen active.",
            "42"),
        new(
            AuditCsvColumn.ServerName,
            nameof(AuditCsvColumn.ServerName),
            "Server",
            "Hostname of the local server running the audit agent.",
            "SRV-PROD-APP01"),
        new(
            AuditCsvColumn.ServerOsVersion,
            nameof(AuditCsvColumn.ServerOsVersion),
            "Server",
            "Operating system description and version of the local host.",
            "Microsoft Windows 10.0.26100"),
        new(
            AuditCsvColumn.ServerPrimaryIp,
            nameof(AuditCsvColumn.ServerPrimaryIp),
            "Server",
            "Primary routable IP address assigned to the local host.",
            "192.168.1.50"),
        new(
            AuditCsvColumn.Protocol,
            nameof(AuditCsvColumn.Protocol),
            "Network",
            "Socket transport protocol (Tcp or Udp).",
            "Tcp"),
        new(
            AuditCsvColumn.Direction,
            nameof(AuditCsvColumn.Direction),
            "Network",
            "Inferred network flow direction (Listening, Inbound, Outbound).",
            "Outbound"),
        new(
            AuditCsvColumn.TcpState,
            nameof(AuditCsvColumn.TcpState),
            "Network",
            "TCP state machine status (Established, Listen, TimeWait, CloseWait, etc.).",
            "Established"),
        new(
            AuditCsvColumn.LocalIp,
            nameof(AuditCsvColumn.LocalIp),
            "Network",
            "Local IP address bound to the socket.",
            "192.168.1.50"),
        new(
            AuditCsvColumn.LocalPort,
            nameof(AuditCsvColumn.LocalPort),
            "Network",
            "Local port number bound to the socket.",
            "52431"),
        new(
            AuditCsvColumn.RemoteIp,
            nameof(AuditCsvColumn.RemoteIp),
            "Network",
            "Remote peer IP address (0.0.0.0 when listening).",
            "140.82.121.4"),
        new(
            AuditCsvColumn.RemotePort,
            nameof(AuditCsvColumn.RemotePort),
            "Network",
            "Remote peer port number (0 when listening).",
            "443"),
        new(
            AuditCsvColumn.RemoteHostName,
            nameof(AuditCsvColumn.RemoteHostName),
            "Network",
            "Remote hostname resolved via reverse DNS or RDAP organization fallback.",
            "lb-140-82-121-4-iad.github.com"),
        new(
            AuditCsvColumn.ProcessId,
            nameof(AuditCsvColumn.ProcessId),
            "Process",
            "Numeric process identifier (PID) owning the socket.",
            "4812"),
        new(
            AuditCsvColumn.ProcessName,
            nameof(AuditCsvColumn.ProcessName),
            "Process",
            "Short executable name of the owning process.",
            "git"),
        new(
            AuditCsvColumn.ExecutablePath,
            nameof(AuditCsvColumn.ExecutablePath),
            "Process",
            "Full absolute path to the executable binary on disk.",
            "C:\\Program Files\\Git\\cmd\\git.exe"),
        new(
            AuditCsvColumn.CommandLine,
            nameof(AuditCsvColumn.CommandLine),
            "Process",
            "Full command line with arguments used to launch the process.",
            "git fetch --all"),
        new(
            AuditCsvColumn.TargetExecutable,
            nameof(AuditCsvColumn.TargetExecutable),
            "Process",
            "Underlying application target extracted from wrappers (e.g. .jar, .py, .ps1, .sh).",
            "app.jar"),
        new(
            AuditCsvColumn.UserName,
            nameof(AuditCsvColumn.UserName),
            "Process",
            "User or service account running the owning process.",
            "NT AUTHORITY\\SYSTEM")
    ];

    private static readonly Dictionary<string, AuditCsvColumn> Lookup = Descriptors
        .ToDictionary(d => d.Name, d => d.Column, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns all available column descriptors for help output and documentation.
    /// </summary>
    public static IReadOnlyList<AuditCsvColumnDescriptor> GetAllDescriptors() => Descriptors;

    /// <summary>
    /// Resolves a list of requested column names into <see cref="AuditCsvColumn"/> values.
    /// If the list is null or empty, returns the default selection of all 20 columns.
    /// </summary>
    /// <param name="requestedColumnNames">Optional case-insensitive column names.</param>
    /// <returns>Ordered list of resolved columns.</returns>
    /// <exception cref="ArgumentException">Thrown when an unknown column name is provided.</exception>
    public static IReadOnlyList<AuditCsvColumn> ResolveColumns(IEnumerable<string>? requestedColumnNames)
    {
        if (requestedColumnNames is null)
        {
            return new AuditCsvOptions().SelectedColumns;
        }

        var list = requestedColumnNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToList();

        if (list.Count == 0)
        {
            return new AuditCsvOptions().SelectedColumns;
        }

        var resolved = new List<AuditCsvColumn>(list.Count);
        foreach (var name in list)
        {
            if (Lookup.TryGetValue(name, out var col))
            {
                resolved.Add(col);
            }
            else
            {
                var availableNames = string.Join(", ", Descriptors.Select(d => d.Name));
                throw new ArgumentException(
                    $"Unknown audit column '{name}'. Available columns: {availableNames}",
                    nameof(requestedColumnNames));
            }
        }

        return resolved;
    }
}
