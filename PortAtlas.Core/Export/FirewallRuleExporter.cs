using PortAtlas.Core.Models;

namespace PortAtlas.Core.Export;

/// <summary>
/// Ingests raw historical network audits and exports consolidated network flow matrices to CSV.
/// </summary>
/// <param name="csvParser">Parser for historical CSV audit logs.</param>
/// <param name="ndjsonParser">Parser for NDJSON streaming logs.</param>
/// <param name="aggregator">Aggregator consolidating flows into firewall rules.</param>
/// <param name="formatter">Formatter writing RFC 4180 firewall rule rows.</param>
/// <param name="logger">Optional diagnostic reporting callback.</param>
/// <param name="pairAggregator">Aggregator merging the rules into source and destination pairs.</param>
/// <param name="pairFormatter">Formatter writing RFC 4180 pair rows.</param>
public sealed class FirewallRuleExporter(
    IAuditCsvHistoryParser? csvParser = null,
    AuditNdjsonParser? ndjsonParser = null,
    FirewallRuleAggregator? aggregator = null,
    FirewallRuleCsvFormatter? formatter = null,
    Action<string>? logger = null,
    FirewallRulePairAggregator? pairAggregator = null,
    FirewallRulePairCsvFormatter? pairFormatter = null)
{
    private readonly IAuditCsvHistoryParser _csvParser = csvParser ?? new AuditCsvHistoryParser();
    private readonly AuditNdjsonParser _ndjsonParser = ndjsonParser ?? new AuditNdjsonParser();
    private readonly FirewallRuleAggregator _aggregator = aggregator ?? new FirewallRuleAggregator();
    private readonly FirewallRuleCsvFormatter _formatter = formatter ?? new FirewallRuleCsvFormatter();
    private readonly Action<string>? _logger = logger;
    private readonly FirewallRulePairAggregator _pairAggregator = pairAggregator ?? new FirewallRulePairAggregator();
    private readonly FirewallRulePairCsvFormatter _pairFormatter = pairFormatter ?? new FirewallRulePairCsvFormatter();

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

        await WriteCsvAsync(
            outputPath,
            _formatter.BuildHeader(options.Delimiter),
            rules.Select(rule => _formatter.FormatRow(rule, options.Delimiter)),
            cancellationToken).ConfigureAwait(false);
        _logger?.Invoke($"Firewall rule matrix with {rules.Count} rule(s) successfully generated: {Path.GetFullPath(outputPath)}");

        if (string.IsNullOrWhiteSpace(options.PairsOutputPath))
        {
            return new FirewallRuleExportReport(validFiles, Path.GetFullPath(outputPath), rules.Count, allEntries.Count);
        }

        var pairs = _pairAggregator.Aggregate(rules);
        await WriteCsvAsync(
            options.PairsOutputPath,
            _pairFormatter.BuildHeader(options.Delimiter),
            pairs.Select(pair => _pairFormatter.FormatRow(pair, options.Delimiter)),
            cancellationToken).ConfigureAwait(false);
        _logger?.Invoke($"Pair matrix with {pairs.Count} source/destination pair(s) successfully generated: {Path.GetFullPath(options.PairsOutputPath)}");

        return new FirewallRuleExportReport(
            validFiles,
            Path.GetFullPath(outputPath),
            rules.Count,
            allEntries.Count,
            Path.GetFullPath(options.PairsOutputPath),
            pairs.Count);
    }

    private static async Task WriteCsvAsync(string path, string header, IEnumerable<string> rows, CancellationToken cancellationToken)
    {
        var outputDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        await using var writer = new StreamWriter(path, append: false);
        await writer.WriteLineAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            await writer.WriteLineAsync(row.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
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

        var matchingFiles = AuditInputPathResolver.Resolve(auditPathsOrPatterns, AuditInputPathResolver.AuditLogExtensions);
        return ExportRulesAsync(matchingFiles, outputPath, options, cancellationToken);
    }
}
