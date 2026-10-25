using System.Globalization;
using System.Text.Json;
using FileForge.IO;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Cli.Dashboard;
using NetFlowAudit.Cli.Help;
using NetFlowAudit.Cli.Terminal;
using NetFlowAudit.Core.Deduplication;
using NetFlowAudit.Core.Engine;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Filtering;
using NetFlowAudit.Core.Providers;
using NetFlowAudit.Core.Resolution;
using ProcessForge.Filtering;
using ProcessForge.Inspectors;
using SocketForge.Factories;
using TopologyForge.Models;

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

        ConsoleVirtualTerminalActivator.TryEnable();

        if (args.Any(a => a is "-h" or "--help" or "-?" or "/?" or "help"))
        {
            ConsoleHelpPrinter.PrintHelp();
            return;
        }

        if (args.Any(a => a.Equals("--init-config", StringComparison.OrdinalIgnoreCase) ||
                          a.Equals("--generate-config", StringComparison.OrdinalIgnoreCase)))
        {
            var targetPath = GetCommandLineArgumentValue(args, "--init-config") ??
                             GetCommandLineArgumentValue(args, "--generate-config");
            var generatedPath = ConfigurationTemplateGenerator.GenerateDefaultConfigFile(targetPath);
            Console.WriteLine($"Baseline configuration template successfully generated at: {generatedPath}");
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

        config = config with
        {
            OutputCsvPath = OutputFilePathResolver.Resolve(config.OutputCsvPath),
            OutputNdjsonPath = OutputFilePathResolver.Resolve(config.OutputNdjsonPath),
            OutputHtmlMapPath = OutputFilePathResolver.Resolve(config.OutputHtmlMapPath),
            DnsCacheFilePath = OutputFilePathResolver.Resolve(config.DnsCacheFilePath)
        };

        var delimiter = !string.IsNullOrEmpty(config.CsvDelimiter) ? config.CsvDelimiter[0] : ',';
        var topologyMapper = new AuditTopologyMapper();

        if (args.Any(a => a.Equals("--export-ndjson", StringComparison.OrdinalIgnoreCase)))
        {
            var targetNdjson = GetCommandLineArgumentValue(args, "--export-ndjson");
            if (string.IsNullOrWhiteSpace(targetNdjson))
            {
                targetNdjson = "netflow_audit.ndjson";
            }
            targetNdjson = OutputFilePathResolver.Resolve(targetNdjson);

            var singleInput = GetCommandLineArgumentValue(args, "--input") ??
                              GetCommandLineArgumentValue(args, "-i") ??
                              config.OutputCsvPath;

            var exporter = new AuditNdjsonExporter();
            Console.WriteLine($"Converting CSV audit log '{singleInput}' to NDJSON: '{targetNdjson}'...");
            var count = await exporter.ExportCsvToNdjsonAsync(singleInput, targetNdjson, delimiter, CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine($"Successfully exported {count} audit flow entries to NDJSON: {Path.GetFullPath(targetNdjson)}");
            return;
        }

        if (args.Any(a => a.Equals("--map-only", StringComparison.OrdinalIgnoreCase)))
        {
            var inputDir = GetCommandLineArgumentValue(args, "--input-dir") ??
                           GetCommandLineArgumentValue(args, "--dir");
            var inputFilter = GetCommandLineArgumentValue(args, "--input-filter") ??
                              GetCommandLineArgumentValue(args, "--pattern") ??
                              "*.csv";
            var inputsArg = GetCommandLineArgumentValue(args, "--inputs") ??
                            GetCommandLineArgumentValue(args, "--input-files");
            var singleInput = GetCommandLineArgumentValue(args, "--input") ??
                              GetCommandLineArgumentValue(args, "-i");

            var targetHtml = string.IsNullOrWhiteSpace(config.OutputHtmlMapPath)
                ? "netflow_topology.html"
                : config.OutputHtmlMapPath;
            var mapFilters = TopologyMapConfigurationBuilder.BuildFilterOptions(config);
            var mapRendering = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);

            NetworkTopologyGraph graph;
            if (!string.IsNullOrWhiteSpace(inputDir))
            {
                Console.WriteLine($"Reading audit logs from directory: {inputDir} (filter: {inputFilter})...");
                graph = await topologyMapper.GenerateHtmlMapFromDirectoryAsync(
                    inputDir,
                    inputFilter,
                    targetHtml,
                    mapFilters,
                    mapRendering,
                    delimiter,
                    "Network Flow & Server Topology Map",
                    CancellationToken.None).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(inputsArg))
            {
                var files = inputsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                Console.WriteLine($"Reading audit logs from {files.Length} source file(s)...");
                graph = await topologyMapper.GenerateHtmlMapFromFilesAsync(
                    files,
                    targetHtml,
                    mapFilters,
                    mapRendering,
                    delimiter,
                    "Network Flow & Server Topology Map",
                    CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                var sourceCsv = singleInput ?? config.OutputCsvPath;
                graph = await topologyMapper.GenerateHtmlMapFromCsvAsync(
                    sourceCsv,
                    targetHtml,
                    mapFilters,
                    mapRendering,
                    delimiter,
                    "Network Flow & Server Topology Map",
                    CancellationToken.None).ConfigureAwait(false);
            }

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
            if (!string.IsNullOrWhiteSpace(config.OutputNdjsonPath))
            {
                Console.WriteLine($"NDJSON output   : {config.OutputNdjsonPath}");
            }
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
            ? new CachedHostNameResolver(TimeSpan.FromMilliseconds(500), exclusionEvaluator, dnsCacheStore, orgResolver, config.CustomHostAliases)
            : new NoOpHostNameResolver();

        PersistentDelimitedFileWriter? ndjsonWriter = null;
        AuditNdjsonFormatter? ndjsonFormatter = null;

        if (!string.IsNullOrWhiteSpace(config.OutputNdjsonPath))
        {
            ndjsonFormatter = new AuditNdjsonFormatter();
            ndjsonWriter = new PersistentDelimitedFileWriter(config.OutputNdjsonPath, string.Empty);
        }

        var engine = new FlowAuditEngine(
            socketScanner,
            exclusionFilter,
            networkFlowFilter,
            deduplicationRegister,
            hostProvider,
            csvWriter,
            csvFormatter,
            hostNameResolver,
            TimeSpan.FromSeconds(config.ScanIntervalSeconds),
            ndjsonWriter,
            ndjsonFormatter);

        var exclusionConfigurator = new FlowExclusionConfigurator(config, configPath, networkFlowFilter, exclusionFilter);

        FlowAuditDashboardRenderer? dashboard = null;
        if (useDashboard)
        {
            dashboard = new FlowAuditDashboardRenderer(
                config.ScanIntervalSeconds,
                config.OutputCsvPath,
                networkFlowFilter,
                exclusionFilter,
                hostProvider.GetLocalIpAddresses(),
                config.MaskServerName,
                config.MaskLocalIp);
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

        if (config.ReloadHistoryLimit != 0 && File.Exists(config.OutputCsvPath))
        {
            var historyParser = new AuditCsvHistoryParser();
            var historicalEntries = await historyParser.ParseHistoryAsync(
                config.OutputCsvPath,
                config.ReloadHistoryLimit,
                delimiter,
                cts.Token).ConfigureAwait(false);

            if (historicalEntries.Count > 0)
            {
                deduplicationRegister.Hydrate(historicalEntries);
                dashboard?.HydrateDiscoveries(historicalEntries);
            }
        }

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
                            else if (key.KeyChar is 'v' or 'V' or 'f' or 'F')
                            {
                                dashboard?.ToggleViewMode();
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
            if (ndjsonWriter is not null)
            {
                await ndjsonWriter.DisposeAsync().ConfigureAwait(false);
            }
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

    internal static AppConfiguration ApplyCommandLineOverrides(AppConfiguration config, string[] args)
    {
        var output = GetCommandLineArgumentValue(args, "--output") ?? GetCommandLineArgumentValue(args, "-o");
        var ndjsonOutput = GetCommandLineArgumentValue(args, "--ndjson") ??
                           GetCommandLineArgumentValue(args, "--output-ndjson");
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

        var reloadHistoryStr = GetCommandLineArgumentValue(args, "--reload-history") ??
                               GetCommandLineArgumentValue(args, "--history-limit");
        var reloadHistoryLimit = config.ReloadHistoryLimit;
        if (!string.IsNullOrWhiteSpace(reloadHistoryStr))
        {
            if (reloadHistoryStr.Equals("all", StringComparison.OrdinalIgnoreCase) || reloadHistoryStr == "-1")
            {
                reloadHistoryLimit = null;
            }
            else if (int.TryParse(reloadHistoryStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLimit) && parsedLimit >= 0)
            {
                reloadHistoryLimit = parsedLimit;
            }
        }

        var maskServer = GetCommandLineArgumentValue(args, "--mask-server") ??
                         GetCommandLineArgumentValue(args, "--obfuscate-server");
        var maskIp = GetCommandLineArgumentValue(args, "--mask-ip") ??
                     GetCommandLineArgumentValue(args, "--obfuscate-ip");
        var isObfuscated = args.Any(a => a.Equals("--obfuscate", StringComparison.OrdinalIgnoreCase) ||
                                         a.Equals("--anonymize", StringComparison.OrdinalIgnoreCase));
        var mapLayout = GetCommandLineArgumentValue(args, "--map-layout");
        var mapSort = GetCommandLineArgumentValue(args, "--map-sort");
        var mapRouting = GetCommandLineArgumentValue(args, "--map-routing");
        var mapSpacing = GetCommandLineArgumentValue(args, "--map-spacing");

        var customAliases = new Dictionary<string, string>(config.CustomHostAliases, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--alias", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("--aliases", StringComparison.OrdinalIgnoreCase))
            {
                var pairList = args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var pair in pairList)
                {
                    var separatorIndex = pair.IndexOfAny(['=', ':']);
                    if (separatorIndex > 0 && separatorIndex < pair.Length - 1)
                    {
                        var ip = pair[..separatorIndex].Trim();
                        var host = pair[(separatorIndex + 1)..].Trim();
                        if (!string.IsNullOrWhiteSpace(ip) && !string.IsNullOrWhiteSpace(host))
                        {
                            customAliases[ip] = host;
                        }
                    }
                }
            }
        }

        if (isObfuscated)
        {
            maskServer ??= "SRV-AUDIT-01";
            maskIp ??= new System.Net.IPAddress([10, 0, 0, 10]).ToString();
        }

        return config with
        {
            OutputCsvPath = output ?? config.OutputCsvPath,
            OutputNdjsonPath = ndjsonOutput ?? config.OutputNdjsonPath,
            ScanIntervalSeconds = interval,
            CsvColumns = columns,
            ResolveRemoteHostNames = resolveDns,
            ResolveIpOwners = resolveIpOwners,
            IsolatedMode = isIsolated || config.IsolatedMode,
            ExcludedRemoteIps = excludedRemoteIps,
            MaskServerName = maskServer ?? config.MaskServerName,
            MaskLocalIp = maskIp ?? config.MaskLocalIp,
            CustomHostAliases = customAliases,
            ReloadHistoryLimit = reloadHistoryLimit,
            MapLayoutMode = mapLayout ?? config.MapLayoutMode,
            MapSortMethod = mapSort ?? config.MapSortMethod,
            MapRoutingMode = mapRouting ?? config.MapRoutingMode,
            MapSpacingProfile = mapSpacing ?? config.MapSpacingProfile
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
