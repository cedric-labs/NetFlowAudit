namespace NetFlowAudit.Core.Export;

/// <summary>
/// Column definitions available for RFC 4180 CSV flow audit log export.
/// </summary>
public enum AuditCsvColumn
{
    /// <summary>
    /// UTC timestamp of first flow observation.
    /// </summary>
    FirstSeenUtc,

    /// <summary>
    /// UTC timestamp of most recent flow observation.
    /// </summary>
    LastSeenUtc,

    /// <summary>
    /// Cumulative count of times this flow was observed.
    /// </summary>
    ObservationCount,

    /// <summary>
    /// Local server host name.
    /// </summary>
    ServerName,

    /// <summary>
    /// Local server operating system description and version.
    /// </summary>
    ServerOsVersion,

    /// <summary>
    /// Primary IP address of the local server.
    /// </summary>
    ServerPrimaryIp,

    /// <summary>
    /// Network transport protocol (TCP, UDP).
    /// </summary>
    Protocol,

    /// <summary>
    /// Flow direction (Inbound, Outbound, Listening).
    /// </summary>
    Direction,

    /// <summary>
    /// TCP socket state.
    /// </summary>
    TcpState,

    /// <summary>
    /// Local socket IP address.
    /// </summary>
    LocalIp,

    /// <summary>
    /// Local socket port number.
    /// </summary>
    LocalPort,

    /// <summary>
    /// Remote socket IP address.
    /// </summary>
    RemoteIp,

    /// <summary>
    /// Remote socket port number.
    /// </summary>
    RemotePort,

    /// <summary>
    /// Resolved remote host name or FQDN (empty if unresolved).
    /// </summary>
    RemoteHostName,

    /// <summary>
    /// Operating system process ID (PID).
    /// </summary>
    ProcessId,

    /// <summary>
    /// Executable binary short process name.
    /// </summary>
    ProcessName,

    /// <summary>
    /// Full absolute path of the executable binary.
    /// </summary>
    ExecutablePath,

    /// <summary>
    /// Full command line used to launch the process.
    /// </summary>
    CommandLine,

    /// <summary>
    /// Resolved application payload target (e.g. JAR, Python/PowerShell script, DLL).
    /// </summary>
    TargetExecutable,

    /// <summary>
    /// User account running the process.
    /// </summary>
    UserName
}
