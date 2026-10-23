using System.Text.Json;
using FileForge.IO;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Cli.Dashboard;
using NetFlowAudit.Cli.Help;
using NetFlowAudit.Core.Deduplication;
using NetFlowAudit.Core.Engine;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Filtering;
using NetFlowAudit.Core.Providers;
using NetFlowAudit.Core.Resolution;
using ProcessForge.Filtering;
using ProcessForge.Inspectors;
using SocketForge.Factories;

namespace NetFlowAudit.Cli;

/// <summary>
/// Main entry point for the NetFlowAudit network flow auditing utility.
/// </summary>
public static class Program
{
    /// <summary>
    /// Starts continuous network flow auditing or standalone topology map generation.
    /// </summary>
    public static async Task Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // Environment without terminal control
        }

        if (args.Any(a => a is "-h" or "--help" or "-?" or "/?" or "help"))
        {
            ConsoleHelpPrinter.PrintHelp();
            return;
        }

        var configPath = GetCommandLineArgumentValue(args, "--config") ?? "netflowaudit.json";
        if (!File.Exists(configPath))
        {
            var baseConfig = Path.Combine(AppContext.BaseDirectory, configPath);
            if (File.Exists(baseConfig))
            {
                configPath = baseConfig;
            }
        }

        var config = LoadConfiguration(configPath);
        config = ApplyCommandLineOverrides(config, args);
        config = TopologyMapConfigurationBuilder.ApplyCommandLineOverrides(config, args);

        var delimiter = !string.IsNullOrEmpty(config.CsvDelimiter) ? config.CsvDelimiter[0] : ',';
        var topologyMapper = new AuditTopologyMapper();

        if (args.Any(a => a.Equals("--map-only", StringComparison.OrdinalIgnoreCase)))
        {
            var targetHtml = string.IsNullOrWhiteSpace(config.OutputHtmlMapPath)
                ? "netflow_topology.html"
                : config.OutputHtmlMapPath;
            var mapFilters = TopologyMapConfigurationBuilder.BuildFilterOptions(config);
            var mapRendering = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);
            var graph = await topologyMapper.GenerateHtmlMapFromCsvAsync(
                config.OutputCsvPath,
                targetHtml,
                mapFilters,
                mapRendering,
                delimiter,
                "Network Flow & Server Topology Map",
                CancellationToken.None).ConfigureAwait(false);

            Console.WriteLine($"Topology map generated: {Path.GetFullPath(targetHtml)} ({graph.Nodes.Count} servers, {graph.Edges.Count} flows)");
            return;
        }

        IReadOnlyList<AuditCsvColumn> selectedColumns;
        try
        {
            selectedColumns = AuditCsvColumnCatalog.ResolveColumns(config.CsvColumns);
        }
        catch (ArgumentException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Column configuration error: {ex.Message}");
            Console.ResetColor();
            return;
        }

        var disableDashboardArg = args.Any(a => a.Equals("--no-tui", StringComparison.OrdinalIgnoreCase) ||
                                                a.Equals("--daemon", StringComparison.OrdinalIgnoreCase) ||
                                                a.Equals("--silent", StringComparison.OrdinalIgnoreCase));

        var useDashboard = config.EnableDashboard && !disableDashboardArg && !Console.IsInputRedirected && !Console.IsOutputRedirected;

        using var cts = new CancellationTokenSource();

        var hostProvider = new LocalServerHostProvider();
        var host = hostProvider.GetCurrentHostMetadata();

        if (!useDashboard)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("  NetFlowAudit - Continuous Network Flow Auditor");
            Console.WriteLine("==================================================");
            Console.WriteLine($"Host server     : {host.HostName} ({host.OperatingSystemDescription})");
            Console.WriteLine($"Primary IP      : {host.PrimaryIpAddress}");
            Console.WriteLine($"CSV output      : {config.OutputCsvPath}");
            if (!string.IsNullOrWhiteSpace(config.OutputHtmlMapPath))
            {
                Console.WriteLine($"HTML map        : {config.OutputHtmlMapPath}");
            }

            Console.WriteLine($"CSV columns     : {selectedColumns.Count} active columns");
            Console.WriteLine($"Reverse DNS     : {(config.ResolveRemoteHostNames ? $"Enabled (cache: {config.DnsCacheFilePath})" : "Disabled")}");
            Console.WriteLine($"IP owners (RDAP): {(config.ResolveIpOwners && !config.IsolatedMode ? "Enabled (RDAP with circuit breaker)" : (config.IsolatedMode ? "Disabled (Isolated mode)" : "Disabled"))}");
            Console.WriteLine($"Scan interval   : every {config.ScanIntervalSeconds} second(s)");
            Console.WriteLine($"Loopback filter : {(config.ExcludeLoopbackFlows ? "Enabled" : "Disabled")}");
            if (config.ExcludedRemoteIps.Count > 0)
            {
                Console.WriteLine($"Excluded IPs    : {string.Join(", ", config.ExcludedRemoteIps)}");
            }

            Console.WriteLine("Starting continuous audit... (Press 'm' to refresh topology map, 'q' to stop)\n");
        }

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

        var flowFilterOptions = new NetworkFlowFilterOptions
        {
            ExcludeLoopbackFlows = config.ExcludeLoopbackFlows,
            ExcludedLocalPorts = new HashSet<int>(config.ExcludedLocalPorts),
            ExcludedRemotePorts = new HashSet<int>(config.ExcludedRemotePorts),
            ExcludedRemoteIps = config.ExcludedRemoteIps
        };

        var networkFlowFilter = new NetworkFlowFilter(flowFilterOptions);

        var deduplicationRegister = new ConnectionDeduplicationRegister();

        var csvOptions = new AuditCsvOptions
        {
            SelectedColumns = selectedColumns,
            Delimiter = delimiter
        };
        var csvFormatter = new AuditCsvFormatter(csvOptions);

        await using var csvWriter = new PersistentDelimitedFileWriter(config.OutputCsvPath, csvFormatter.BuildHeaderLine());

        var exclusionEvaluator = new DnsAddressExclusionEvaluator(config.DnsExcludedNetworks);
        var dnsCacheStore = new PersistentDnsCacheStore(config.DnsCacheFilePath);

        using var orgResolver = config.ResolveIpOwners && !config.IsolatedMode
            ? new RdapIpOrganizationResolver()
            : (IIpOrganizationResolver)new NoOpIpOrganizationResolver();

        IHostNameResolver hostNameResolver = config.ResolveRemoteHostNames
            ? new CachedHostNameResolver(TimeSpan.FromMilliseconds(500), exclusionEvaluator, dnsCacheStore, orgResolver)
            : new NoOpHostNameResolver();

        var engine = new FlowAuditEngine(
            socketScanner,
            exclusionFilter,
            networkFlowFilter,
            deduplicationRegister,
            hostProvider,
            csvWriter,
            csvFormatter,
            hostNameResolver,
            TimeSpan.FromSeconds(config.ScanIntervalSeconds));

        var exclusionConfigurator = new FlowExclusionConfigurator(config, configPath, networkFlowFilter, exclusionFilter);

        FlowAuditDashboardRenderer? dashboard = null;
        if (useDashboard)
        {
            dashboard = new FlowAuditDashboardRenderer(
                config.ScanIntervalSeconds,
                config.OutputCsvPath,
                networkFlowFilter,
                exclusionFilter,
                hostProvider.GetLocalIpAddresses());
            dashboard.ClearScreen();
            engine.ScanCycleCompleted += (_, e) => dashboard.Render(e.Result, host);
            hostNameResolver.HostNameResolved += (_, e) => dashboard.UpdateRemoteHostName(e.Address, e.HostName);
        }

        if (!string.IsNullOrWhiteSpace(config.OutputHtmlMapPath))
        {
            engine.ScanCycleCompleted += (_, _) =>
            {
                var currentEntries = deduplicationRegister.GetAllEntries();
                var mapFilters = TopologyMapConfigurationBuilder.BuildFilterOptions(config);
                var mapRendering = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);
                _ = topologyMapper.GenerateHtmlMapFromEntriesAsync(
                    currentEntries,
                    config.OutputHtmlMapPath,
                    mapFilters,
                    mapRendering,
                    $"Network Topology Map - {host.HostName}",
                    cts.Token);
            };
        }

        System.Runtime.InteropServices.PosixSignalRegistration? posixSigInt = null;
        System.Runtime.InteropServices.PosixSignalRegistration? posixSigTerm = null;

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            posixSigInt = System.Runtime.InteropServices.PosixSignalRegistration.Create(
                System.Runtime.InteropServices.PosixSignal.SIGINT,
                ctx =>
                {
                    ctx.Cancel = true;
                    _ = cts.CancelAsync();
                });

            posixSigTerm = System.Runtime.InteropServices.PosixSignalRegistration.Create(
                System.Runtime.InteropServices.PosixSignal.SIGTERM,
                ctx =>
                {
                    ctx.Cancel = true;
                    _ = cts.CancelAsync();
                });
        }

        using var sigIntScope = posixSigInt;
        using var sigTermScope = posixSigTerm;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _ = cts.CancelAsync();
        };

        var auditTask = engine.RunContinuousAuditAsync(cts.Token);

        if (!Console.IsInputRedirected)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        dashboard?.CheckAndRedrawIfWindowChanged();

                        while (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(intercept: true);
                            if (key.KeyChar is 'q' or 'Q')
                            {
                                await cts.CancelAsync().ConfigureAwait(false);
                                return;
                            }

                            if (key.KeyChar is 'c' or 'C')
                            {
                                dashboard?.ForceRedraw();
                            }
                            else if (key.KeyChar is 'm' or 'M')
                            {
                                var targetHtml = string.IsNullOrWhiteSpace(config.OutputHtmlMapPath)
                                    ? "netflow_topology.html"
                                    : config.OutputHtmlMapPath;
                                var mapFilters = TopologyMapConfigurationBuilder.BuildFilterOptions(config);
                                var mapRendering = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);
                                await topologyMapper.GenerateHtmlMapFromEntriesAsync(
                                    deduplicationRegister.GetAllEntries(),
                                    targetHtml,
                                    mapFilters,
                                    mapRendering,
                                    $"Network Topology Map - {host.HostName}",
                                    cts.Token).ConfigureAwait(false);
                            }
                            else if (dashboard is not null)
                            {
                                switch (key.Key)
                                {
                                    case ConsoleKey.UpArrow:
                                        dashboard.MoveSelectionUp();
                                        break;

                                    case ConsoleKey.DownArrow:
                                        dashboard.MoveSelectionDown();
                                        break;

                                    case ConsoleKey.LeftArrow:
                                        dashboard.MoveSelectionLeft();
                                        break;

                                    case ConsoleKey.RightArrow:
                                        dashboard.MoveSelectionRight();
                                        break;

                                    case ConsoleKey.PageDown:
                                        dashboard.NextPage();
                                        break;

                                    case ConsoleKey.PageUp:
                                        dashboard.PreviousPage();
                                        break;

                                    case ConsoleKey.Enter:
                                        dashboard.ApplyExclusionAtSelection(exclusionConfigurator);
                                        break;

                                    case ConsoleKey.Escape:
                                        dashboard.CancelSelection();
                                        break;

                                    default:
                                        if (key.KeyChar is 'x' or 'X' or 'e' or 'E')
                                        {
                                            dashboard.ApplyExclusionAtSelection(exclusionConfigurator);
                                        }
                                        else if (key.KeyChar is 'n' or 'N')
                                        {
                                            dashboard.NextPage();
                                        }
                                        else if (key.KeyChar is 'p' or 'P')
                                        {
                                            dashboard.PreviousPage();
                                        }
                                        else if (key.KeyChar is 'l' or 'L' or '+')
                                        {
                                            dashboard.LoadNextBatch();
                                        }
                                        else if (key.KeyChar is 'a' or 'A')
                                        {
                                            dashboard.ShowAllDiscoveries();
                                        }

                                        break;
                                }
                            }
                        }

                        await Task.Delay(20, cts.Token).ConfigureAwait(false);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Non-interactive environment without terminal
                }
                catch (OperationCanceledException)
                {
                    // Cooperative cancellation
                }
            }, cts.Token);
        }

        try
        {
            await auditTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            dashboard?.RestoreCursor();
            if (!useDashboard)
            {
                Console.WriteLine("Graceful shutdown of network flow audit.");
            }
        }
        finally
        {
            dashboard?.RestoreCursor();
            if (hostNameResolver is IAsyncDisposable asyncResolver)
            {
                await asyncResolver.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static AppConfiguration LoadConfiguration(string configFile)
    {
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
                Console.WriteLine($"Warning: Could not load {configFile}, using default settings.");
            }
        }

        return new AppConfiguration();
    }

    private static AppConfiguration ApplyCommandLineOverrides(AppConfiguration config, string[] args)
    {
        var output = GetCommandLineArgumentValue(args, "--output");
        var intervalStr = GetCommandLineArgumentValue(args, "--interval");
        var columnsStr = GetCommandLineArgumentValue(args, "--columns");
        var dnsStr = GetCommandLineArgumentValue(args, "--resolve-dns");
        var ipOwnersStr = GetCommandLineArgumentValue(args, "--resolve-ip-owners");
        var isIsolated = args.Any(a => a.Equals("--isolated", StringComparison.OrdinalIgnoreCase) ||
                                       a.Equals("--offline", StringComparison.OrdinalIgnoreCase));
        var noIpOwners = args.Any(a => a.Equals("--no-ip-owners", StringComparison.OrdinalIgnoreCase));
        var excludeRemoteIpsStr = GetCommandLineArgumentValue(args, "--exclude-remote-ips") ??
                                  GetCommandLineArgumentValue(args, "--exclude-remote-ip");

        var interval = config.ScanIntervalSeconds;
        if (intervalStr is not null && int.TryParse(intervalStr, out var parsedInterval) && parsedInterval > 0)
        {
            interval = parsedInterval;
        }

        var columns = config.CsvColumns;
        if (!string.IsNullOrWhiteSpace(columnsStr))
        {
            columns = columnsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        var resolveDns = config.ResolveRemoteHostNames;
        if (dnsStr is not null && bool.TryParse(dnsStr, out var parsedDns))
        {
            resolveDns = parsedDns;
        }

        var resolveIpOwners = config.ResolveIpOwners;
        if (ipOwnersStr is not null && bool.TryParse(ipOwnersStr, out var parsedOwners))
        {
            resolveIpOwners = parsedOwners;
        }

        if (noIpOwners || isIsolated)
        {
            resolveIpOwners = false;
        }

        var excludedRemoteIps = config.ExcludedRemoteIps;
        if (!string.IsNullOrWhiteSpace(excludeRemoteIpsStr))
        {
            excludedRemoteIps = excludeRemoteIpsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        return config with
        {
            OutputCsvPath = output ?? config.OutputCsvPath,
            ScanIntervalSeconds = interval,
            CsvColumns = columns,
            ResolveRemoteHostNames = resolveDns,
            ResolveIpOwners = resolveIpOwners,
            IsolatedMode = isIsolated || config.IsolatedMode,
            ExcludedRemoteIps = excludedRemoteIps
        };
    }

    private static string? GetCommandLineArgumentValue(string[] args, string argumentName)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(argumentName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
