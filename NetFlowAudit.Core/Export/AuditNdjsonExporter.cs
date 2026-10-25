using System.Text;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Export;

/// <summary>
/// Exports network audit entries or converts existing CSV logs into NDJSON files.
/// </summary>
public sealed class AuditNdjsonExporter(AuditNdjsonFormatter formatter, IAuditCsvHistoryParser csvHistoryParser)
{
    private readonly AuditNdjsonFormatter _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    private readonly IAuditCsvHistoryParser _csvHistoryParser = csvHistoryParser ?? throw new ArgumentNullException(nameof(csvHistoryParser));

    /// <summary>
    /// Initializes an exporter with default formatter and CSV parser components.
    /// </summary>
    public AuditNdjsonExporter() : this(new AuditNdjsonFormatter(), new AuditCsvHistoryParser())
    {
    }

    /// <summary>
    /// Exports a collection of network audit entries to an NDJSON file.
    /// </summary>
    /// <param name="entries">Network audit entries to export.</param>
    /// <param name="outputNdjsonFilePath">Target NDJSON file path.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>The total number of entries written.</returns>
    public async Task<int> ExportEntriesAsync(
        IEnumerable<NetworkAuditEntry> entries,
        string outputNdjsonFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputNdjsonFilePath);

        var directory = Path.GetDirectoryName(outputNdjsonFilePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var count = 0;
        await using var fileStream = new FileStream(outputNdjsonFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await using var writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = _formatter.FormatEntry(entry);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            count++;
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return count;
    }

    /// <summary>
    /// Reads an existing CSV audit file and converts all valid entries to an NDJSON file.
    /// </summary>
    /// <param name="inputCsvFilePath">Source CSV file path.</param>
    /// <param name="outputNdjsonFilePath">Target NDJSON file path.</param>
    /// <param name="delimiter">CSV delimiter character (default: ',').</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>The total number of entries converted and written.</returns>
    public async Task<int> ExportCsvToNdjsonAsync(
        string inputCsvFilePath,
        string outputNdjsonFilePath,
        char delimiter = ',',
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputCsvFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputNdjsonFilePath);

        var entries = await _csvHistoryParser.ParseHistoryAsync(
            inputCsvFilePath,
            maxRows: null,
            delimiter: delimiter,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return await ExportEntriesAsync(entries, outputNdjsonFilePath, cancellationToken).ConfigureAwait(false);
    }
}
