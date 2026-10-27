using NetFlowAudit.Core.Export;

namespace NetFlowAudit.Cli.Export;

/// <summary>
/// Runs the <c>export rules</c> command: consolidates audit logs into a network flow matrix CSV and reports the outcome on the console.
/// </summary>
/// <param name="exporter">Underlying exporter consolidating flows and formatting the CSV matrix.</param>
public sealed class FirewallRuleExportCommandRunner(FirewallRuleExporter? exporter = null)
{
    private readonly FirewallRuleExporter _exporter = exporter ?? new FirewallRuleExporter(logger: Console.WriteLine);

    /// <summary>
    /// Exports the firewall rule matrix.
    /// </summary>
    /// <param name="auditFiles">Audit log files (CSV or NDJSON) to consolidate.</param>
    /// <param name="outputPath">Destination path of the matrix CSV.</param>
    /// <param name="options">Consolidation and filtering options.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Exit code (0 on success).</returns>
    /// <exception cref="ArgumentNullException">When a reference argument is null.</exception>
    public async Task<int> RunAsync(
        IReadOnlyList<string> auditFiles,
        string outputPath,
        FirewallRuleExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(options);

        Console.WriteLine("========================================================================");
        Console.WriteLine("  NetFlowAudit - Network Flow Matrix Export (export rules)");
        Console.WriteLine("========================================================================");
        Console.WriteLine($"Exclude ephemeral destination ports: {(options.ExcludeEphemeralPorts ? "Yes (>= 49152 excluded)" : "No")}");
        Console.WriteLine($"Minimum observation threshold: {options.MinObservations} observation(s)");
        if (options.ActiveWithinDays.HasValue)
        {
            Console.WriteLine($"Freshness filter: Observed within the last {options.ActiveWithinDays.Value} day(s)");
        }
        Console.WriteLine($"Destination zone filter: {options.DestinationZoneFilter.ToUpperInvariant()}");
        Console.WriteLine($"Destination file: {Path.GetFullPath(outputPath)}");
        Console.WriteLine();

        var report = await _exporter.ExportRulesAsync(auditFiles, outputPath, options, cancellationToken).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine("------------------------------------------------------------------------");
        Console.WriteLine("  Network Flow Matrix Generation Summary");
        Console.WriteLine("------------------------------------------------------------------------");
        Console.WriteLine($"  * Source files ingested: {report.SourceFiles.Count}");
        Console.WriteLine($"  * Raw flows scanned: {report.TotalRawFlowsScanned}");
        Console.WriteLine($"  * Consolidated rules generated: {report.TotalRulesExported}");
        Console.WriteLine($"  * Matrix exported to: {report.DestinationCsvPath}");
        Console.WriteLine("------------------------------------------------------------------------");

        return 0;
    }
}
