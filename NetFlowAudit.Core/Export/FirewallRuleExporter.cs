using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Export;

/// <summary>
/// Ingests raw historical network audits and exports consolidated network flow matrices to CSV.
/// </summary>
/// <param name="csvParser">Parser for historical CSV audit logs.</param>
/// <param name="ndjsonParser">Parser for NDJSON streaming logs.</param>
/// <param name="aggregator">Aggregator consolidating flows into firewall rules.</param>
/// <param name="formatter">Formatter writing RFC 4180 firewall rule rows.</param>
/// <param name="logger">Optional diagnostic reporting callback.</param>
public sealed class FirewallRuleExporter(
    IAuditCsvHistoryParser? csvParser = null,
    AuditNdjsonParser? ndjsonParser = null,
    FirewallRuleAggregator? aggregator = null,
    FirewallRuleCsvFormatter? formatter = null,
    Action<string>? logger = null)
{
    private readonly IAuditCsvHistoryParser _csvParser = csvParser ?? new AuditCsvHistoryParser();
    private readonly AuditNdjsonParser _ndjsonParser = ndjsonParser ?? new AuditNdjsonParser();
    private readonly FirewallRuleAggregator _aggregator = aggregator ?? new FirewallRuleAggregator();
    private readonly FirewallRuleCsvFormatter _formatter = formatter ?? new FirewallRuleCsvFormatter();
    private readonly Action<string>? _logger = logger;

    /// <summary>
    /// Ingests audit files, aggregates firewall rules, and exports a formatted CSV matrix.
    /// </summary>
    /// <param name="auditFiles">Collection of source CSV or NDJSON audit file paths.</param>
    /// <param name="outputPath">Destination path for the generated firewall matrix CSV.</param>
    /// <param name="options">Consolidation and filtering options.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Execution report detailing processed files, row counts, and exported rule counts.</returns>
    public async Task<FirewallRuleExportReport> ExportRulesAsync(
        IReadOnlyList<string> auditFiles,
        string outputPath,
        FirewallRuleExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(options);

        var allEntries = new List<NetworkAuditEntry>();
        var validFiles = new List<string>();

        foreach (var file in auditFiles)
        {
            if (!File.Exists(file))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            validFiles.Add(file);

            var isNdjson = file.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase) ||
                           file.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);

            if (isNdjson)
            {
                var ndjsonEntries = await _ndjsonParser.ParseHistoryAsync(file, cancellationToken: cancellationToken).ConfigureAwait(false);
                allEntries.AddRange(ndjsonEntries);
            }
            else
            {
                var csvEntries = await _csvParser.ParseHistoryAsync(file, cancellationToken: cancellationToken).ConfigureAwait(false);
                allEntries.AddRange(csvEntries);
            }
        }

        _logger?.Invoke($"Consolidation de {allEntries.Count} flux bruts depuis {validFiles.Count} fichier(s)...");

        var rules = _aggregator.Aggregate(allEntries, options);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        await using (var writer = new StreamWriter(outputPath, append: false))
        {
            await writer.WriteLineAsync(_formatter.BuildHeader(options.Delimiter).AsMemory(), cancellationToken).ConfigureAwait(false);
            foreach (var rule in rules)
            {
                await writer.WriteLineAsync(_formatter.FormatRow(rule, options.Delimiter).AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        _logger?.Invoke($"Firewall rule matrix with {rules.Count} rule(s) successfully generated: {Path.GetFullPath(outputPath)}");

        return new FirewallRuleExportReport(
            validFiles,
            Path.GetFullPath(outputPath),
            rules.Count,
            allEntries.Count);
    }

    /// <summary>
    /// Resolves file paths, directories, or wildcards, aggregates rules, and exports the CSV matrix.
    /// </summary>
    /// <param name="auditPathsOrPatterns">Paths, directories, or glob patterns for audit files.</param>
    /// <param name="outputPath">Destination path for the generated firewall matrix CSV.</param>
    /// <param name="options">Consolidation and filtering options.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Execution report detailing processed files, row counts, and exported rule counts.</returns>
    public Task<FirewallRuleExportReport> ExportRulesFromPathsAsync(
        IReadOnlyList<string> auditPathsOrPatterns,
        string outputPath,
        FirewallRuleExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditPathsOrPatterns);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(options);

        var matchingFiles = ResolveMatchingFiles(auditPathsOrPatterns, [".csv", ".ndjson", ".jsonl"]);
        return ExportRulesAsync(matchingFiles, outputPath, options, cancellationToken);
    }

    private static IReadOnlyList<string> ResolveMatchingFiles(IEnumerable<string> pathsOrPatterns, string[] validExtensions)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in pathsOrPatterns)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            if (File.Exists(item))
            {
                resolved.Add(Path.GetFullPath(item));
                continue;
            }

            if (Directory.Exists(item))
            {
                foreach (var ext in validExtensions)
                {
                    foreach (var file in Directory.EnumerateFiles(item, $"*{ext}", SearchOption.TopDirectoryOnly))
                    {
                        resolved.Add(Path.GetFullPath(file));
                    }
                }
                continue;
            }

            var dir = Path.GetDirectoryName(item);
            var pattern = Path.GetFileName(item);
            if (string.IsNullOrEmpty(dir))
            {
                dir = Directory.GetCurrentDirectory();
            }

            if (Directory.Exists(dir) && !string.IsNullOrWhiteSpace(pattern))
            {
                foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                {
                    resolved.Add(Path.GetFullPath(file));
                }
            }
        }

        return resolved.ToList();
    }
}
