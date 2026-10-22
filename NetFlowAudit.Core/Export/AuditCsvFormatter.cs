using System.Globalization;
using FileForge.IO;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Export;

/// <summary>
/// Options de sélection des colonnes pour l'export CSV NetFlowAudit.
/// </summary>
public sealed record AuditCsvOptions
{
    /// <summary>
    /// Colonnes ordonnées à inclure dans le journal CSV.
    /// </summary>
    public IReadOnlyList<AuditCsvColumn> SelectedColumns { get; init; } =
    [
        AuditCsvColumn.FirstSeenUtc,
        AuditCsvColumn.LastSeenUtc,
        AuditCsvColumn.ObservationCount,
        AuditCsvColumn.ServerName,
        AuditCsvColumn.ServerOsVersion,
        AuditCsvColumn.ServerPrimaryIp,
        AuditCsvColumn.Protocol,
        AuditCsvColumn.Direction,
        AuditCsvColumn.TcpState,
        AuditCsvColumn.LocalIp,
        AuditCsvColumn.LocalPort,
        AuditCsvColumn.RemoteIp,
        AuditCsvColumn.RemotePort,
        AuditCsvColumn.ProcessId,
        AuditCsvColumn.ProcessName,
        AuditCsvColumn.ExecutablePath,
        AuditCsvColumn.CommandLine,
        AuditCsvColumn.TargetExecutable,
        AuditCsvColumn.UserName
    ];

    /// <summary>
    /// Séparateur de champ CSV.
    /// </summary>
    public char Delimiter { get; init; } = ',';
}

/// <summary>
/// Formateur de lignes CSV d'audit s'appuyant sur les règles d'échappement de FileForge.
/// </summary>
public sealed class AuditCsvFormatter(AuditCsvOptions options)
{
    private readonly AuditCsvOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Génère la ligne d'en-tête du fichier CSV.
    /// </summary>
    public string BuildHeaderLine()
    {
        var headers = new string[_options.SelectedColumns.Count];
        for (var i = 0; i < _options.SelectedColumns.Count; i++)
        {
            headers[i] = PersistentDelimitedFileWriter.EscapeField(_options.SelectedColumns[i].ToString(), _options.Delimiter);
        }

        return string.Join(_options.Delimiter, headers);
    }

    /// <summary>
    /// Formate une entrée d'audit en ligne CSV.
    /// </summary>
    public string FormatEntry(NetworkAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var values = new string[_options.SelectedColumns.Count];
        for (var i = 0; i < _options.SelectedColumns.Count; i++)
        {
            values[i] = PersistentDelimitedFileWriter.EscapeField(ExtractFieldValue(_options.SelectedColumns[i], entry), _options.Delimiter);
        }

        return string.Join(_options.Delimiter, values);
    }

    private static string ExtractFieldValue(AuditCsvColumn column, NetworkAuditEntry entry)
    {
        return column switch
        {
            AuditCsvColumn.FirstSeenUtc => entry.FirstSeenUtc.ToString("o", CultureInfo.InvariantCulture),
            AuditCsvColumn.LastSeenUtc => entry.LastSeenUtc.ToString("o", CultureInfo.InvariantCulture),
            AuditCsvColumn.ObservationCount => entry.ObservationCount.ToString(CultureInfo.InvariantCulture),
            AuditCsvColumn.ServerName => entry.Server.HostName,
            AuditCsvColumn.ServerOsVersion => entry.Server.OperatingSystemDescription,
            AuditCsvColumn.ServerPrimaryIp => entry.Server.PrimaryIpAddress,
            AuditCsvColumn.Protocol => entry.Socket.Protocol.ToString(),
            AuditCsvColumn.Direction => entry.Socket.Direction.ToString(),
            AuditCsvColumn.TcpState => entry.Socket.State,
            AuditCsvColumn.LocalIp => entry.Socket.LocalEndpoint.Address.ToString(),
            AuditCsvColumn.LocalPort => entry.Socket.LocalEndpoint.Port.ToString(CultureInfo.InvariantCulture),
            AuditCsvColumn.RemoteIp => entry.Socket.RemoteEndpoint.Address.ToString(),
            AuditCsvColumn.RemotePort => entry.Socket.RemoteEndpoint.Port.ToString(CultureInfo.InvariantCulture),
            AuditCsvColumn.ProcessId => entry.Socket.Process.ProcessId.ToString(CultureInfo.InvariantCulture),
            AuditCsvColumn.ProcessName => entry.Socket.Process.ProcessName,
            AuditCsvColumn.ExecutablePath => entry.Socket.Process.ExecutablePath,
            AuditCsvColumn.CommandLine => entry.Socket.Process.CommandLine,
            AuditCsvColumn.TargetExecutable => entry.Socket.Process.TargetExecutable,
            AuditCsvColumn.UserName => entry.Socket.Process.UserName,
            _ => string.Empty
        };
    }
}
