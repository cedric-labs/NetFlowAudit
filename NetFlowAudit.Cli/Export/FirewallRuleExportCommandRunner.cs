using System.Globalization;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Core.Export;

namespace NetFlowAudit.Cli.Export;

/// <summary>
/// CLI command runner executing the '--export-rules' workflow to consolidate historical audits into a network flow matrix CSV.
/// </summary>
/// <param name="exporter">Underlying exporter consolidating flows and formatting the CSV matrix.</param>
public sealed class FirewallRuleExportCommandRunner(FirewallRuleExporter? exporter = null)
{
    private readonly FirewallRuleExporter _exporter = exporter ?? new FirewallRuleExporter(logger: Console.WriteLine);

    /// <summary>
    /// Executes the firewall rule matrix export workflow.
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
        Console.WriteLine("  NetFlowAudit - Export de la Matrice de Flux Réseau (--export-rules)");
        Console.WriteLine("========================================================================");

        var outputPath = GetArgumentValue(args, ["--export-rules", "--firewall-matrix", "--rules-csv"]);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = "firewall_rules.csv";
        }

        var auditInputs = ExtractArgumentValues(args, ["-i", "--input", "--inputs", "--input-audits"]);
        if (auditInputs.Count == 0 && !string.IsNullOrWhiteSpace(config.OutputCsvPath))
        {
            auditInputs.Add(config.OutputCsvPath);
        }

        var excludeEphemeral = !HasArgumentFlag(args, ["--include-ephemeral"]);
        if (HasArgumentFlag(args, ["--exclude-ephemeral", "--exclude-ephemeral-ports"]))
        {
            excludeEphemeral = true;
        }

        var minObservations = 1;
        var minObsRaw = GetArgumentValue(args, ["--min-observations", "--min-obs"]);
        if (int.TryParse(minObsRaw, CultureInfo.InvariantCulture, out var parsedMinObs) && parsedMinObs >= 1)
        {
            minObservations = parsedMinObs;
        }

        int? activeWithinDays = null;
        var activeDaysRaw = GetArgumentValue(args, ["--active-within-days", "--seen-since", "--days"]);
        if (int.TryParse(activeDaysRaw, CultureInfo.InvariantCulture, out var parsedDays) && parsedDays > 0)
        {
            activeWithinDays = parsedDays;
        }

        var destinationZone = "all";
        if (HasArgumentFlag(args, ["--internal-only", "--internal"]))
        {
            destinationZone = "internal";
        }
        else if (HasArgumentFlag(args, ["--public-only", "--internet-only", "--external-only"]))
        {
            destinationZone = "public";
        }
        else
        {
            var zoneArg = GetArgumentValue(args, ["--destination-zone", "--zone"]);
            if (!string.IsNullOrWhiteSpace(zoneArg))
            {
                destinationZone = zoneArg;
            }
        }

        var direction = GetArgumentValue(args, ["--direction"]) ?? config.MapDirection;
        var listenersOnly = config.MapListenersOnly || HasArgumentFlag(args, ["--listeners-only"]);

        var includedPorts = ParseCsvArgument(args, ["--include-ports", "--include-port"], config.MapIncludedPorts);
        var excludedPorts = ParseCsvArgument(args, ["--exclude-ports", "--exclude-port"], config.MapExcludedPorts);
        var includedHosts = ParseCsvArgument(args, ["--include-hosts", "--include-host"], config.MapIncludedHosts);
        var excludedHosts = ParseCsvArgument(args, ["--exclude-hosts", "--exclude-host"], config.MapExcludedHosts);
        var includedIps = ParseCsvArgument(args, ["--include-ips", "--include-ip"], config.MapIncludedIps);
        var excludedIps = ParseCsvArgument(args, ["--exclude-ips", "--exclude-ip"], config.MapExcludedIps);
        var includedProcesses = ParseCsvArgument(args, ["--include-processes", "--include-process"], config.MapIncludedProcesses);
        var excludedProcesses = ParseCsvArgument(args, ["--exclude-processes", "--exclude-process"], config.MapExcludedProcesses);

        var options = new FirewallRuleExportOptions
        {
            ExcludeEphemeralPorts = excludeEphemeral,
            MinObservations = minObservations,
            ActiveWithinDays = activeWithinDays,
            DestinationZoneFilter = destinationZone,
            ListenersOnly = listenersOnly,
            Direction = direction,
            IncludedPorts = includedPorts,
            ExcludedPorts = excludedPorts,
            IncludedHosts = includedHosts,
            ExcludedHosts = excludedHosts,
            IncludedIps = includedIps,
            ExcludedIps = excludedIps,
            IncludedProcesses = includedProcesses,
            ExcludedProcesses = excludedProcesses,
            Delimiter = !string.IsNullOrEmpty(config.CsvDelimiter) ? config.CsvDelimiter[0] : ','
        };

        Console.WriteLine($"Exclusion des ports éphémères destination : {(excludeEphemeral ? "Oui (>= 49152 exclus)" : "Non")}");
        Console.WriteLine($"Seuil d'activité minimum : {minObservations} observation(s)");
        if (activeWithinDays.HasValue)
        {
            Console.WriteLine($"Filtre de fraîcheur : Observé au cours des {activeWithinDays.Value} derniers jours");
        }
        Console.WriteLine($"Filtrage de zone cible : {destinationZone.ToUpperInvariant()}");
        Console.WriteLine($"Fichier cible : {Path.GetFullPath(outputPath)}");
        Console.WriteLine();

        var report = await _exporter.ExportRulesFromPathsAsync(auditInputs, outputPath, options, cancellationToken).ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine("------------------------------------------------------------------------");
        Console.WriteLine("  Bilan de la génération de la matrice de flux");
        Console.WriteLine("------------------------------------------------------------------------");
        Console.WriteLine($"  * Fichiers sources ingérés : {report.SourceFiles.Count}");
        Console.WriteLine($"  * Flux bruts analysés : {report.TotalRawFlowsScanned}");
        Console.WriteLine($"  * Règles consolidées générées : {report.TotalRulesExported}");
        Console.WriteLine($"  * Matrice enregistrée dans : {report.DestinationCsvPath}");
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

    private static List<string> ParseCsvArgument(string[] args, string[] argumentNames, List<string> defaultValues)
    {
        var raw = GetArgumentValue(args, argumentNames);
        if (raw is null)
        {
            return defaultValues;
        }

        if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "none", StringComparison.OrdinalIgnoreCase) || string.Equals(raw, "clear", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
