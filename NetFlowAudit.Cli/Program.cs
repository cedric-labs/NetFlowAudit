using System.Text.Json;
using FileForge.IO;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Core.Deduplication;
using NetFlowAudit.Core.Engine;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Providers;
using ProcessForge.Filtering;
using ProcessForge.Inspectors;
using SocketForge.Factories;

namespace NetFlowAudit.Cli;

/// <summary>
/// Point d'entrée principal de l'utilitaire d'audit réseau NetFlowAudit.
/// </summary>
public static class Program
{
    /// <summary>
    /// Fonction principale démarrant le scanneur de flux.
    /// </summary>
    public static async Task Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("  NetFlowAudit - Agent d'audit des flux réseau");
        Console.WriteLine("==================================================");

        var config = LoadConfiguration();

        using var cts = new CancellationTokenSource();

        var hostProvider = new LocalServerHostProvider();
        var host = hostProvider.GetCurrentHostMetadata();
        Console.WriteLine($"Serveur hôte    : {host.HostName} ({host.OperatingSystemDescription})");
        Console.WriteLine($"IP principale   : {host.PrimaryIpAddress}");
        Console.WriteLine($"Fichier CSV     : {config.OutputCsvPath}");
        Console.WriteLine($"Fréquence scan  : toutes les {config.ScanIntervalSeconds} seconde(s)");
        Console.WriteLine("Démarrage de l'audit continu... (Appuyez sur 'q' pour stopper)\n");

        var processInspector = new CrossPlatformProcessInspector();
        var socketScanner = SocketScannerFactory.Create(processInspector);

        var filterOptions = new ProcessExclusionOptions
        {
            ExcludedProcessIds = config.ExcludedPids,
            ExcludedExactNames = config.ExcludedExactNames,
            ExcludedNamePrefixes = config.ExcludedPrefixes,
            ExcludedNameSuffixes = config.ExcludedSuffixes,
            ExcludedNameContains = config.ExcludedContains,
            ExcludeSystemAccounts = config.ExcludeSystemAccounts
        };

        var exclusionFilter = new ProcessExclusionFilter(filterOptions);
        var deduplicationRegister = new ConnectionDeduplicationRegister();
        var csvFormatter = new AuditCsvFormatter(new AuditCsvOptions());

        await using var csvWriter = new PersistentDelimitedFileWriter(config.OutputCsvPath, csvFormatter.BuildHeaderLine());

        var engine = new FlowAuditEngine(
            socketScanner,
            exclusionFilter,
            deduplicationRegister,
            hostProvider,
            csvWriter,
            csvFormatter,
            TimeSpan.FromSeconds(config.ScanIntervalSeconds));

        var auditTask = engine.RunContinuousAuditAsync(cts.Token);

        var cancellationMonitorTask = Task.Run(async () =>
        {
            while (!Console.KeyAvailable)
            {
                await Task.Delay(500, cts.Token).ConfigureAwait(false);
            }

            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Q)
            {
                await cts.CancelAsync().ConfigureAwait(false);
            }
        }, cts.Token);

        try
        {
            await Task.WhenAny(auditTask, cancellationMonitorTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Arrêt propre de l'audit de flux.");
        }
    }

    private static AppConfiguration LoadConfiguration()
    {
        const string configFile = "netflowaudit.json";
        if (File.Exists(configFile))
        {
            try
            {
                var json = File.ReadAllText(configFile);
                var loaded = JsonSerializer.Deserialize<AppConfiguration>(json);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                Console.WriteLine($"Avertissement: Impossible de charger {configFile}, utilisation des valeurs par défaut.");
            }
        }

        return new AppConfiguration();
    }
}
