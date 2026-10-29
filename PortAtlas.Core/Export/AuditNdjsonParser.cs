using System.Text;
using System.Text.Json;
using PortAtlas.Core.Models;

namespace PortAtlas.Core.Export;

/// <summary>
/// Parses NDJSON (newline-delimited JSON) lines and files into strongly-typed network audit records and entries.
/// </summary>
public sealed class AuditNdjsonParser
{
    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parses a single NDJSON line into a network audit entry.
    /// </summary>
    /// <param name="ndjsonLine">Raw single-line JSON string.</param>
    /// <returns>Parsed <see cref="NetworkAuditEntry"/>, or null if empty or malformed.</returns>
    public NetworkAuditEntry? ParseLine(string ndjsonLine)
    {
        if (string.IsNullOrWhiteSpace(ndjsonLine))
        {
            return null;
        }

        try
        {
            var record = JsonSerializer.Deserialize<AuditNdjsonRecord>(ndjsonLine, DeserializerOptions);
            return record?.ToEntry();
        }
        catch (JsonException)
        {
            // Malformed or truncated NDJSON records are skipped to preserve streaming reader resiliency.
            return null;
        }
    }

    /// <summary>
    /// Asynchronously parses historical audit entries from an NDJSON file.
    /// </summary>
    /// <param name="ndjsonFilePath">Path to the NDJSON file on disk.</param>
    /// <param name="maxRows">Maximum number of recent entries to read (null or negative for all rows, 0 for none).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Read-only list of parsed network audit entries.</returns>
    public async Task<IReadOnlyList<NetworkAuditEntry>> ParseHistoryAsync(
        string ndjsonFilePath,
        int? maxRows = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ndjsonFilePath);

        if (maxRows.HasValue && maxRows.Value == 0)
        {
            return [];
        }

        if (!File.Exists(ndjsonFilePath))
        {
            return [];
        }

        var results = new List<NetworkAuditEntry>();
        await using var stream = new FileStream(ndjsonFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = ParseLine(line);
            if (entry is not null)
            {
                results.Add(entry);
            }
        }

        if (maxRows.HasValue && maxRows.Value > 0 && results.Count > maxRows.Value)
        {
            return results.TakeLast(maxRows.Value).ToList();
        }

        return results;
    }
}
