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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="outputNdjsonFilePath"/> is null or white-space.</exception>
    public async Task<int> ExportEntriesAsync(
        IEnumerable<NetworkAuditEntry> entries,
        string outputNdjsonFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputNdjsonFilePath);

        EnsureDirectoryExists(outputNdjsonFilePath);

        await using var fileStream = new FileStream(outputNdjsonFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await using var writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var count = await WriteEntriesAsync(writer, entries, cancellationToken).ConfigureAwait(false);
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
    /// <exception cref="ArgumentException">Thrown when <paramref name="inputCsvFilePath"/> or <paramref name="outputNdjsonFilePath"/> is null or white-space.</exception>
    public Task<int> ExportCsvToNdjsonAsync(
        string inputCsvFilePath,
        string outputNdjsonFilePath,
        char delimiter = ',',
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputCsvFilePath);
        return ExportCsvFilesToNdjsonAsync([inputCsvFilePath], outputNdjsonFilePath, delimiter, cancellationToken);
    }

    /// <summary>
    /// Reads multiple CSV audit files sequentially and exports all valid entries into a single NDJSON file.
    /// </summary>
    /// <param name="inputCsvFilePaths">Source CSV file paths to read in sequential order.</param>
    /// <param name="outputNdjsonFilePath">Target NDJSON file path.</param>
    /// <param name="delimiter">CSV delimiter character (default: ',').</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>The total number of entries converted and written across all input files.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inputCsvFilePaths"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="outputNdjsonFilePath"/> is null or white-space.</exception>
    public async Task<int> ExportCsvFilesToNdjsonAsync(
        IReadOnlyList<string> inputCsvFilePaths,
        string outputNdjsonFilePath,
        char delimiter = ',',
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputCsvFilePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputNdjsonFilePath);

        EnsureDirectoryExists(outputNdjsonFilePath);

        await using var fileStream = new FileStream(outputNdjsonFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await using var writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var totalCount = 0;
        foreach (var inputCsvFilePath in inputCsvFilePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entries = await _csvHistoryParser.ParseHistoryAsync(
                inputCsvFilePath,
                maxRows: null,
                delimiter: delimiter,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            totalCount += await WriteEntriesAsync(writer, entries, cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return totalCount;
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private async Task<int> WriteEntriesAsync(
        StreamWriter writer,
        IEnumerable<NetworkAuditEntry> entries,
        CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = _formatter.FormatEntry(entry);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            count++;
        }

        return count;
    }
}
