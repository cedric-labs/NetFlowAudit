using NetFlowAudit.Core.Enrichment;
using NetFlowAudit.Core.Resolution;

namespace NetFlowAudit.Cli.Enrichment;

/// <summary>
/// Runs the <c>resolve</c> command: consolidates DNS caches, enriches audit logs and reports the outcome on the console.
/// </summary>
/// <param name="enricher">Enricher orchestrating offline analysis and file updates.</param>
public sealed class OfflineResolutionCommandRunner(OfflineAuditEnricher? enricher = null)
{
    private readonly OfflineAuditEnricher _enricher = enricher ?? new OfflineAuditEnricher(
        offlineResolver: new OfflineDnsResolver(
            organizationResolver: new RdapIpOrganizationResolver(),
            logThrottlingNotice: Console.WriteLine),
        logger: Console.WriteLine);

    /// <summary>
    /// Consolidates the caches and enriches the audit logs.
    /// </summary>
    /// <param name="auditFiles">Audit log files (CSV or NDJSON) to enrich.</param>
    /// <param name="cacheFiles">DNS cache files to merge into the master cache.</param>
    /// <param name="options">Enrichment options, including the master cache path.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Exit code (0 on success).</returns>
    /// <exception cref="ArgumentNullException">When an argument is null.</exception>
    public async Task<int> RunAsync(
        IReadOnlyList<string> auditFiles,
        IReadOnlyList<string> cacheFiles,
        OfflineAuditEnrichmentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditFiles);
        ArgumentNullException.ThrowIfNull(cacheFiles);
        ArgumentNullException.ThrowIfNull(options);

        Console.WriteLine("========================================================================");
        Console.WriteLine("  NetFlowAudit - Offline DNS Resolution & Cache Consolidation (resolve)");
        Console.WriteLine("========================================================================");
        Console.WriteLine($"Execution mode: {(options.Isolated ? "Cache only, no DNS or RDAP query (--cache-only)" : "Active enrichment with DNS & RDAP")}");
        Console.WriteLine($"Audit log updates: {(options.InPlace ? "Direct in-place overwrite (--in-place)" : "Generate new '*_enriched' files")}");
        Console.WriteLine($"Force hostname replacement: {(options.ForceResolve ? "Yes (--force-resolve)" : "No (retain existing names)")}");
        Console.WriteLine();

        var report = await _enricher.EnrichPathsAsync(auditFiles, cacheFiles, options, cancellationToken).ConfigureAwait(false);

        if (report.CacheReport.Conflicts.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING] {report.CacheReport.Conflicts.Count} conflict(s) detected during cache merge:");
            foreach (var conflict in report.CacheReport.Conflicts)
            {
                Console.WriteLine($"  * IP {conflict.IpAddress}: Retained '{conflict.WinningHostName}', discarded '{conflict.ConflictingHostName}' ({conflict.Reason})");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("------------------------------------------------------------------------");
        Console.WriteLine("  Offline Consolidation & Enrichment Summary");
        Console.WriteLine("------------------------------------------------------------------------");
        Console.WriteLine($"  * Source caches processed: {report.CacheReport.TotalCachesProcessed} ({report.CacheReport.TotalEntriesLoaded} raw entries)");
        Console.WriteLine($"  * Deduplicated cache entries: {report.CacheReport.MergedEntries.Count}");
        Console.WriteLine($"  * Online resolutions completed: {report.OnlineResolutionsCount}");
        Console.WriteLine($"  * Audit files processed: {report.ProcessedAuditFiles.Count}");
        Console.WriteLine($"  * Audit records scanned: {report.TotalRecordsScanned}");
        Console.WriteLine($"  * Audit records updated: {report.TotalRecordsUpdated}");
        Console.WriteLine($"  * Distinct IP addresses enriched: {report.DistinctIpsEnriched}");
        Console.WriteLine($"  * Master cache saved to: {Path.GetFullPath(options.OutputCachePath)}");

        if (report.GeneratedAuditFiles.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Generated enriched audit file(s):");
            foreach (var file in report.GeneratedAuditFiles)
            {
                Console.WriteLine($"    -> {Path.GetFullPath(file)}");
            }
        }
        Console.WriteLine("------------------------------------------------------------------------");

        return 0;
    }
}
