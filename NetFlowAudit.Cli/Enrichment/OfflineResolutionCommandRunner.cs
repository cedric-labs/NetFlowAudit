using System.Globalization;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Core.Enrichment;
using NetFlowAudit.Core.Resolution;

namespace NetFlowAudit.Cli.Enrichment;

/// <summary>
/// CLI command runner executing the '--resolve-offline' workflow for cache consolidation and audit enrichment.
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
    /// Executes the offline resolution and consolidation command.
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Exit code (0 on success).</returns>
    public async Task<int> RunAsync(AppConfiguration config, string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(args);

        Console.WriteLine("========================================================================");
        Console.WriteLine("  NetFlowAudit - Offline DNS Resolution & Cache Consolidation (--resolve-offline)");
        Console.WriteLine("========================================================================");

        var auditInputs = ExtractArgumentValues(args, ["-i", "--input", "--inputs", "--input-audits"]);
        if (auditInputs.Count == 0 && !string.IsNullOrWhiteSpace(config.OutputCsvPath))
        {
            auditInputs.Add(config.OutputCsvPath);
        }

        var cacheInputs = ExtractArgumentValues(args, ["--cache", "--caches", "--input-cache", "--input-caches"]);
        if (cacheInputs.Count == 0 && !string.IsNullOrWhiteSpace(config.DnsCacheFilePath))
        {
            cacheInputs.Add(config.DnsCacheFilePath);
        }

        var outputCachePath = GetArgumentValue(args, ["--output-cache"]) ?? config.DnsCacheFilePath ?? "netflow_dns_cache.json";
        var inPlace = HasArgumentFlag(args, ["--in-place"]);
        var forceResolve = HasArgumentFlag(args, ["--force-resolve", "--force"]);
        var isolated = config.IsolatedMode || HasArgumentFlag(args, ["--isolated"]);

        var rdapDelayMs = 200;
        var rdapDelayRaw = GetArgumentValue(args, ["--rdap-delay-ms", "--delay"]);
        if (int.TryParse(rdapDelayRaw, CultureInfo.InvariantCulture, out var parsedDelay) && parsedDelay >= 0)
        {
            rdapDelayMs = parsedDelay;
        }

        var options = new OfflineAuditEnrichmentOptions
        {
            InPlace = inPlace,
            ForceResolve = forceResolve,
            Isolated = isolated,
            RdapThrottleDelay = TimeSpan.FromMilliseconds(rdapDelayMs),
            OutputCachePath = outputCachePath
        };

        Console.WriteLine($"Execution mode: {(isolated ? "100% Offline / Air-Gapped (--isolated)" : "Active enrichment with DNS & RDAP")}");
        Console.WriteLine($"Audit log updates: {(inPlace ? "Direct in-place overwrite (--in-place)" : "Generate new '*_enriched' files")}");
        Console.WriteLine($"Force hostname replacement: {(forceResolve ? "Yes (--force-resolve)" : "No (retain existing names)")}");
        Console.WriteLine();

        var report = await _enricher.EnrichPathsAsync(auditInputs, cacheInputs, options, cancellationToken).ConfigureAwait(false);

        // Display cache conflicts
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
        Console.WriteLine($"  * Master cache saved to: {Path.GetFullPath(outputCachePath)}");

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

    private static List<string> ExtractArgumentValues(string[] args, string[] argumentNames)
    {
        var results = new List<string>();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (argumentNames.Any(name => string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)))
            {
                var val = args[i + 1];
                if (!val.StartsWith('-'))
                {
                    var items = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    results.AddRange(items);
                }
            }
        }
        return results;
    }

    private static string? GetArgumentValue(string[] args, string[] argumentNames)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (argumentNames.Any(name => string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)))
            {
                var val = args[i + 1];
                if (!val.StartsWith('-'))
                {
                    return val;
                }
            }
        }
        return null;
    }

    private static bool HasArgumentFlag(string[] args, string[] argumentNames)
    {
        return args.Any(arg => argumentNames.Any(name => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)));
    }
}
