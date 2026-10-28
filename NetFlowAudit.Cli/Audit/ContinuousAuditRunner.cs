using FileForge.IO;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Cli.Dashboard;
using NetFlowAudit.Core.Deduplication;
using NetFlowAudit.Core.Engine;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Filtering;
using NetFlowAudit.Core.Providers;
using NetFlowAudit.Core.Resolution;
using ProcessForge.Filtering;
using ProcessForge.Inspectors;
using SocketForge.Factories;

namespace NetFlowAudit.Cli.Audit;

/// <summary>
/// Runs the <c>audit</c> command: scans sockets continuously, writes deduplicated flows and drives the terminal dashboard.
/// </summary>
public static class ContinuousAuditRunner
{
    /// <summary>
    /// Runs the audit loop until the user presses Q, Ctrl+C or the process receives SIGTERM.
    /// </summary>
    /// <param name="config">Effective configuration (file values overridden by command-line options).</param>
    /// <param name="configPath">Configuration file path, updated when an exclusion is added from the dashboard.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Exit code (0 on graceful shutdown, 1 on configuration error).</returns>
    /// <exception cref="ArgumentNullException">When an argument is null.</exception>
    public static async Task<int> RunAsync(AppConfiguration config, string configPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(configPath);

        var delimiter = !string.IsNullOrEmpty(config.CsvDelimiter) ? config.CsvDelimiter[0] : ',';
        var topologyMapper = new AuditTopologyMapper();

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
            return 1;
        }

        var useDashboard = config.EnableDashboard && !Console.IsInputRedirected && !Console.IsOutputRedirected;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

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
            Console.WriteLine($"IP owners (RDAP): {(config.ResolveIpOwners ? "Enabled (RDAP with circuit breaker)" : "Disabled")}");
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

        using var orgResolver = config.ResolveIpOwners
            ? new RdapIpOrganizationResolver()
            : (IIpOrganizationResolver)new NoOpIpOrganizationResolver();

        IHostNameResolver hostNameResolver = config.ResolveRemoteHostNames
            ? new CachedHostNameResolver(TimeSpan.FromMilliseconds(config.DnsResolutionTimeoutMs), exclusionEvaluator, dnsCacheStore, orgResolver, config.CustomHostAliases)
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
                                var graph = await topologyMapper.GenerateHtmlMapFromEntriesAsync(
                                    deduplicationRegister.GetAllEntries(),
                                    targetHtml,
                                    mapFilters,
                                    mapRendering,
                                    $"Network Topology Map - {host.HostName}",
                                    cts.Token).ConfigureAwait(false);

                                if (!string.IsNullOrWhiteSpace(config.OutputDrawioPath))
                                {
                                    await AuditTopologyMapper.ExportToDrawioAsync(
                                        graph,
                                        config.OutputDrawioPath,
                                        $"NetFlowAudit Network Topology - {host.HostName}",
                                        TopologyMapConfigurationBuilder.BuildDrawioEdgeMode(config),
                                        TopologyMapConfigurationBuilder.BuildDrawioTheme(config),
                                        cts.Token).ConfigureAwait(false);
                                }
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
                    // Console.KeyAvailable throws when stdin is not a console (service host, piped input). The keyboard
                    // loop stops; the audit keeps running and is stopped by Ctrl+C or SIGTERM instead of the Q key.
                }
                catch (OperationCanceledException)
                {
                    // The audit is stopping (Q, Ctrl+C or SIGTERM): the keyboard loop ends with it, and the main task
                    // observes the same cancellation to flush the writers.
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
        return 0;
    }
}
