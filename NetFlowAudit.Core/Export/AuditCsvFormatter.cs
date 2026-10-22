using System.Globalization;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Export;

/// <summary>
/// Options de configuration du formateur et de l'exportateur CSV.
/// </summary>
public sealed record AuditCsvOptions
{
    /// <summary>
    /// Liste ordonnée des colonnes à inclure dans l'export CSV.
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
    /// Séparateur de champ CSV (virgule par défaut).
    /// </summary>
    public string Delimiter { get; init; } = ",";
}

/// <summary>
/// Formateur de lignes CSV pour les enregistrements d'audit réseau.
/// </summary>
public sealed class AuditCsvFormatter(AuditCsvOptions options)
{
    private readonly AuditCsvOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Génère la ligne d'en-tête CSV selon les colonnes sélectionnées.
    /// </summary>
    public string BuildHeaderLine()
    {
        var headers = new string[_options.SelectedColumns.Count];
        for (var i = 0; i < _options.SelectedColumns.Count; i++)
        {
            headers[i] = EscapeField(_options.SelectedColumns[i].ToString());
        }

        return string.Join(_options.Delimiter, headers);
    }

    /// <summary>
    /// Formate un enregistrement de flux en une ligne CSV standard conforme RFC 4180.
    /// </summary>
    public string FormatRecordLine(NetworkConnectionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var values = new string[_options.SelectedColumns.Count];
        for (var i = 0; i < _options.SelectedColumns.Count; i++)
        {
            values[i] = EscapeField(ExtractFieldValue(_options.SelectedColumns[i], record));
        }

        return string.Join(_options.Delimiter, values);
    }

    private static string ExtractFieldValue(AuditCsvColumn column, NetworkConnectionRecord record)
    {
        return column switch
        {
            AuditCsvColumn.FirstSeenUtc => record.FirstSeenUtc.ToString("o", CultureInfo.InvariantCulture),
            AuditCsvColumn.LastSeenUtc => record.LastSeenUtc.ToString("o", CultureInfo.InvariantCulture),
            AuditCsvColumn.ObservationCount => record.ObservationCount.ToString(CultureInfo.InvariantCulture),
            AuditCsvColumn.ServerName => record.Server.HostName,
            AuditCsvColumn.ServerOsVersion => record.Server.OperatingSystemDescription,
            AuditCsvColumn.ServerPrimaryIp => record.Server.PrimaryIpAddress,
            AuditCsvColumn.Protocol => record.Protocol.ToString(),
            AuditCsvColumn.Direction => record.Direction.ToString(),
            AuditCsvColumn.TcpState => record.TcpState,
            AuditCsvColumn.LocalIp => record.LocalEndpoint.Address.ToString(),
            AuditCsvColumn.LocalPort => record.LocalEndpoint.Port.ToString(CultureInfo.InvariantCulture),
            AuditCsvColumn.RemoteIp => record.RemoteEndpoint.Address.ToString(),
            AuditCsvColumn.RemotePort => record.RemoteEndpoint.Port.ToString(CultureInfo.InvariantCulture),
            AuditCsvColumn.ProcessId => record.Process.ProcessId.ToString(CultureInfo.InvariantCulture),
            AuditCsvColumn.ProcessName => record.Process.ProcessName,
            AuditCsvColumn.ExecutablePath => record.Process.ExecutablePath,
            AuditCsvColumn.CommandLine => record.Process.CommandLine,
            AuditCsvColumn.TargetExecutable => record.Process.TargetExecutable,
            AuditCsvColumn.UserName => record.Process.UserName,
            _ => string.Empty
        };
    }

    private static string EscapeField(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var mustQuote = value.Contains(',') || value.Contains(';') || value.Contains('"') ||
                        value.Contains('\n') || value.Contains('\r');

        if (!mustQuote)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
