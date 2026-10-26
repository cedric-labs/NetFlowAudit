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
        Console.WriteLine("  NetFlowAudit - Résolution & Consolidation Hors-Ligne (--resolve-offline)");
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

        Console.WriteLine($"Mode d'exécution : {(isolated ? "100% Hors-ligne (--isolated)" : "Enrichissement actif avec DNS/RDAP")}");
        Console.WriteLine($"Mise à jour des audits : {(inPlace ? "Directement sur place (--in-place)" : "Génération de nouveaux fichiers '*_enriched'")}");
        Console.WriteLine($"Remplacement forcé des noms : {(forceResolve ? "Oui (--force-resolve)" : "Non (conservation des noms existants)")}");
        Console.WriteLine();

        var report = await _enricher.EnrichPathsAsync(auditInputs, cacheInputs, options, cancellationToken).ConfigureAwait(false);

        // Display cache conflicts
        if (report.CacheReport.Conflicts.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[AVERTISSEMENT] {report.CacheReport.Conflicts.Count} incohérence(s) détectée(s) lors de la fusion des caches :");
            foreach (var conflict in report.CacheReport.Conflicts)
            {
                Console.WriteLine($"  * IP {conflict.IpAddress} : Nom conservé '{conflict.WinningHostName}', écarté '{conflict.ConflictingHostName}' ({conflict.Reason})");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("------------------------------------------------------------------------");
        Console.WriteLine("  Bilan de l'enrichissement et de la consolidation");
        Console.WriteLine("------------------------------------------------------------------------");
        Console.WriteLine($"  * Caches sources traités : {report.CacheReport.TotalCachesProcessed} ({report.CacheReport.TotalEntriesLoaded} entrées brutes)");
        Console.WriteLine($"  * Entrées de cache dédoublonnées : {report.CacheReport.MergedEntries.Count}");
        Console.WriteLine($"  * Résolutions en ligne effectuées : {report.OnlineResolutionsCount}");
        Console.WriteLine($"  * Fichiers d'audit traités : {report.ProcessedAuditFiles.Count}");
        Console.WriteLine($"  * Lignes d'audit examinées : {report.TotalRecordsScanned}");
        Console.WriteLine($"  * Lignes d'audit mises à jour : {report.TotalRecordsUpdated}");
        Console.WriteLine($"  * Adresses IP distinctes enrichies : {report.DistinctIpsEnriched}");
        Console.WriteLine($"  * Cache master persisté dans : {Path.GetFullPath(outputCachePath)}");

        if (report.GeneratedAuditFiles.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Fichier(s) d'audit enrichi(s) généré(s) :");
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
