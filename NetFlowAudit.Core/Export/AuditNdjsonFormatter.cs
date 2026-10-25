using System.Text.Encodings.Web;
using System.Text.Json;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Export;

/// <summary>
/// Formats network audit entries into single-line NDJSON (newline-delimited JSON) strings.
/// </summary>
public sealed class AuditNdjsonFormatter
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Formats a network audit entry into a single NDJSON line without trailing newline.
    /// </summary>
    /// <param name="entry">The network audit entry to serialize.</param>
    /// <returns>A compact single-line JSON string representation of the entry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
    public string FormatEntry(NetworkAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var record = AuditNdjsonRecord.FromEntry(entry);
        return JsonSerializer.Serialize(record, DefaultOptions);
    }

    /// <summary>
    /// Formats an <see cref="AuditNdjsonRecord"/> into a single NDJSON line.
    /// </summary>
    /// <param name="record">The audit record to serialize.</param>
    /// <returns>A compact single-line JSON string representation of the record.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is null.</exception>
    public string FormatRecord(AuditNdjsonRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return JsonSerializer.Serialize(record, DefaultOptions);
    }
}
