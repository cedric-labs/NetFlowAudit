using NetFlowAudit.Core.Models;
using SocketForge.Models;
using TopologyForge.Builders;
using TopologyForge.Export.Drawio;
using TopologyForge.Export.Drawio.Adapters;
using TopologyForge.Export.Drawio.Styling;
using TopologyForge.Filtering;
using TopologyForge.Models;
using TopologyForge.Parsers;
using TopologyForge.Rendering;

namespace NetFlowAudit.Core.Export;

/// <summary>
/// Adapter and orchestrator mapping <see cref="NetFlowAudit"/> audit entries to the <see cref="TopologyForge"/> graph and HTML rendering pipeline.
/// </summary>
/// <param name="csvParser">Optional topology CSV parser.</param>
/// <param name="htmlRenderer">Optional standalone HTML map renderer.</param>
public sealed class AuditTopologyMapper(
    ITopologyCsvParser? csvParser = null,
    ITopologyHtmlRenderer? htmlRenderer = null)
{
    private readonly ITopologyCsvParser _csvParser = csvParser ?? new TopologyCsvParser();
    private readonly ITopologyHtmlRenderer _htmlRenderer = htmlRenderer ?? new TopologyHtmlRenderer();

    /// <summary>
    /// Converts a consolidated audit entry into a normalized topology flow record.
    /// </summary>
    /// <param name="entry">Network flow audit entry.</param>
    /// <returns>Normalized topology flow record.</returns>
    public static TopologyFlowRecord ToTopologyFlowRecord(NetworkAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var protocol = entry.Socket.Protocol == SocketTransportProtocol.Udp
            ? TopologyTransportProtocol.Udp
            : TopologyTransportProtocol.Tcp;

        var direction = entry.Socket.Direction switch
        {
            SocketFlowDirection.Listening => TopologyFlowDirection.Listening,
            SocketFlowDirection.Inbound => TopologyFlowDirection.Inbound,
            _ => TopologyFlowDirection.Outbound
        };

        var sourceIp = !string.IsNullOrWhiteSpace(entry.Server.PrimaryIpAddress)
            ? entry.Server.PrimaryIpAddress
            : entry.Socket.LocalEndpoint.Address.ToString();

        var serviceTarget = !string.IsNullOrWhiteSpace(entry.Socket.Process.TargetExecutable)
            ? entry.Socket.Process.TargetExecutable
            : entry.Socket.Process.ProcessName;

        return new TopologyFlowRecord(
            SourceServerName: entry.Server.HostName,
            SourceIp: sourceIp,
            LocalPort: entry.Socket.LocalEndpoint.Port,
            RemoteServerName: entry.RemoteHostName,
            RemoteIp: entry.Socket.RemoteEndpoint.Address.ToString(),
            RemotePort: entry.Socket.RemoteEndpoint.Port,
            Protocol: protocol,
            Direction: direction,
            ProcessName: entry.Socket.Process.ProcessName,
            ServiceTarget: serviceTarget,
            ObservationCount: entry.ObservationCount);
    }

    /// <summary>
    /// Writes a standalone HTML5 topology map file for an existing network topology graph.
    /// </summary>
    /// <param name="graph">Constructed network topology graph.</param>
    /// <param name="outputHtmlPath">Output HTML file path.</param>
    /// <param name="renderingOptions">Sorting, routing, and spacing rendering options.</param>
    /// <param name="title">Map document title.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    public Task WriteHtmlMapAsync(
        NetworkTopologyGraph graph,
        string outputHtmlPath,
        TopologyRenderingOptions renderingOptions,
        string title = "NetFlowAudit Network Topology Map",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputHtmlPath);
        ArgumentNullException.ThrowIfNull(renderingOptions);

        var effectiveOptions = renderingOptions with { Title = title };
        return _htmlRenderer.WriteHtmlFileAsync(outputHtmlPath, graph, effectiveOptions, cancellationToken);
    }

    /// <summary>
    /// Reads an existing audit CSV file, applies filtering rules, and builds a consolidated network topology graph without generating HTML.
    /// </summary>
    /// <param name="csvFilePath">Source audit CSV file path.</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="csvDelimiter">CSV delimiter character (comma by default).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated network topology graph.</returns>
    public async Task<NetworkTopologyGraph> BuildGraphFromCsvAsync(
        string csvFilePath,
        TopologyFilterOptions filterOptions,
        char csvDelimiter = ',',
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvFilePath);
        ArgumentNullException.ThrowIfNull(filterOptions);

        var flowRecords = await _csvParser.ParseFileAsync(csvFilePath, csvDelimiter, cancellationToken).ConfigureAwait(false);
        var evaluator = new TopologyFlowEvaluator(filterOptions);
        var builder = new TopologyGraphBuilder(evaluator);
        return builder.BuildGraph(flowRecords);
    }

    /// <summary>
    /// Reads multiple audit CSV files, consolidates flows across all monitored servers, applies filtering rules, and builds a unified network topology graph without generating HTML.
    /// </summary>
    /// <param name="csvFilePaths">Collection of source audit CSV file paths.</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="csvDelimiter">CSV delimiter character (comma by default).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated consolidated network topology graph.</returns>
    public async Task<NetworkTopologyGraph> BuildGraphFromFilesAsync(
        IEnumerable<string> csvFilePaths,
        TopologyFilterOptions filterOptions,
        char csvDelimiter = ',',
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(csvFilePaths);
        ArgumentNullException.ThrowIfNull(filterOptions);

        var allFlowRecords = new List<TopologyFlowRecord>();
        foreach (var filePath in csvFilePaths)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                continue;
            }

            var records = await _csvParser.ParseFileAsync(filePath, csvDelimiter, cancellationToken).ConfigureAwait(false);
            allFlowRecords.AddRange(records);
        }

        var evaluator = new TopologyFlowEvaluator(filterOptions);
        var builder = new TopologyGraphBuilder(evaluator);
        return builder.BuildGraph(allFlowRecords);
    }

    /// <summary>
    /// Discovers all audit CSV files matching a search pattern in a directory and builds a consolidated network topology graph without generating HTML.
    /// </summary>
    /// <param name="directoryPath">Directory path containing audit CSV files.</param>
    /// <param name="searchPattern">File search pattern (e.g. "*.csv").</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="csvDelimiter">CSV delimiter character (comma by default).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated consolidated network topology graph.</returns>
    public Task<NetworkTopologyGraph> BuildGraphFromDirectoryAsync(
        string directoryPath,
        string searchPattern,
        TopologyFilterOptions filterOptions,
        char csvDelimiter = ',',
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(filterOptions);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"The audit directory '{directoryPath}' does not exist.");
        }

        var pattern = string.IsNullOrWhiteSpace(searchPattern) ? "*.csv" : searchPattern;
        var files = Directory.GetFiles(directoryPath, pattern, SearchOption.TopDirectoryOnly);

        return BuildGraphFromFilesAsync(files, filterOptions, csvDelimiter, cancellationToken);
    }

    /// <summary>
    /// Builds a filtered topology graph and writes a standalone HTML5 map file from a collection of audit entries.
    /// </summary>
    /// <param name="entries">Collected audit entries.</param>
    /// <param name="outputHtmlPath">Output HTML file path.</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="title">Map document title.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated network topology graph.</returns>
    public Task<NetworkTopologyGraph> GenerateHtmlMapFromEntriesAsync(
        IEnumerable<NetworkAuditEntry> entries,
        string outputHtmlPath,
        TopologyFilterOptions filterOptions,
        string title = "NetFlowAudit Network Topology Map",
        CancellationToken cancellationToken = default)
        => GenerateHtmlMapFromEntriesAsync(entries, outputHtmlPath, filterOptions, new TopologyRenderingOptions(), title, cancellationToken);

    /// <summary>
    /// Builds a filtered topology graph and writes a standalone HTML5 map file with custom rendering and layout options.
    /// </summary>
    /// <param name="entries">Collected audit entries.</param>
    /// <param name="outputHtmlPath">Output HTML file path.</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="renderingOptions">Sorting, routing, and spacing rendering options.</param>
    /// <param name="title">Map document title.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated network topology graph.</returns>
    public async Task<NetworkTopologyGraph> GenerateHtmlMapFromEntriesAsync(
        IEnumerable<NetworkAuditEntry> entries,
        string outputHtmlPath,
        TopologyFilterOptions filterOptions,
        TopologyRenderingOptions renderingOptions,
        string title = "NetFlowAudit Network Topology Map",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputHtmlPath);
        ArgumentNullException.ThrowIfNull(filterOptions);
        ArgumentNullException.ThrowIfNull(renderingOptions);

        var flowRecords = entries.Select(ToTopologyFlowRecord);
        var evaluator = new TopologyFlowEvaluator(filterOptions);
        var builder = new TopologyGraphBuilder(evaluator);
        var graph = builder.BuildGraph(flowRecords);

        await WriteHtmlMapAsync(graph, outputHtmlPath, renderingOptions, title, cancellationToken).ConfigureAwait(false);
        return graph;
    }

    /// <summary>
    /// Reads an existing audit CSV file, applies filtering rules, and generates a standalone HTML5 topology map.
    /// </summary>
    /// <param name="csvFilePath">Source audit CSV file path.</param>
    /// <param name="outputHtmlPath">Output HTML file path.</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="csvDelimiter">CSV delimiter character (comma by default).</param>
    /// <param name="title">Map document title.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated network topology graph.</returns>
    public Task<NetworkTopologyGraph> GenerateHtmlMapFromCsvAsync(
        string csvFilePath,
        string outputHtmlPath,
        TopologyFilterOptions filterOptions,
        char csvDelimiter = ',',
        string title = "NetFlowAudit Network Topology Map",
        CancellationToken cancellationToken = default)
        => GenerateHtmlMapFromCsvAsync(csvFilePath, outputHtmlPath, filterOptions, new TopologyRenderingOptions(), csvDelimiter, title, cancellationToken);

    /// <summary>
    /// Reads an existing audit CSV file, applies filtering and rendering options, and generates a standalone HTML5 topology map.
    /// </summary>
    /// <param name="csvFilePath">Source audit CSV file path.</param>
    /// <param name="outputHtmlPath">Output HTML file path.</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="renderingOptions">Sorting, routing, and spacing rendering options.</param>
    /// <param name="csvDelimiter">CSV delimiter character (comma by default).</param>
    /// <param name="title">Map document title.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated network topology graph.</returns>
    public async Task<NetworkTopologyGraph> GenerateHtmlMapFromCsvAsync(
        string csvFilePath,
        string outputHtmlPath,
        TopologyFilterOptions filterOptions,
        TopologyRenderingOptions renderingOptions,
        char csvDelimiter = ',',
        string title = "NetFlowAudit Network Topology Map",
        CancellationToken cancellationToken = default)
    {
        var graph = await BuildGraphFromCsvAsync(csvFilePath, filterOptions, csvDelimiter, cancellationToken).ConfigureAwait(false);
        await WriteHtmlMapAsync(graph, outputHtmlPath, renderingOptions, title, cancellationToken).ConfigureAwait(false);
        return graph;
    }

    /// <summary>
    /// Reads multiple audit CSV files, consolidates flows across all monitored servers, applies filtering and rendering options, and generates a unified HTML5 topology map.
    /// </summary>
    /// <param name="csvFilePaths">Collection of source audit CSV file paths.</param>
    /// <param name="outputHtmlPath">Output HTML file path.</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="csvDelimiter">CSV delimiter character (comma by default).</param>
    /// <param name="title">Map document title.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated consolidated network topology graph.</returns>
    public Task<NetworkTopologyGraph> GenerateHtmlMapFromFilesAsync(
        IEnumerable<string> csvFilePaths,
        string outputHtmlPath,
        TopologyFilterOptions filterOptions,
        char csvDelimiter = ',',
        string title = "NetFlowAudit Network Topology Map",
        CancellationToken cancellationToken = default)
        => GenerateHtmlMapFromFilesAsync(csvFilePaths, outputHtmlPath, filterOptions, new TopologyRenderingOptions(), csvDelimiter, title, cancellationToken);

    /// <summary>
    /// Reads multiple audit CSV files, consolidates flows across all monitored servers, applies filtering and rendering options, and generates a unified HTML5 topology map.
    /// </summary>
    /// <param name="csvFilePaths">Collection of source audit CSV file paths.</param>
    /// <param name="outputHtmlPath">Output HTML file path.</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="renderingOptions">Sorting, routing, and spacing rendering options.</param>
    /// <param name="csvDelimiter">CSV delimiter character (comma by default).</param>
    /// <param name="title">Map document title.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated consolidated network topology graph.</returns>
    public async Task<NetworkTopologyGraph> GenerateHtmlMapFromFilesAsync(
        IEnumerable<string> csvFilePaths,
        string outputHtmlPath,
        TopologyFilterOptions filterOptions,
        TopologyRenderingOptions renderingOptions,
        char csvDelimiter = ',',
        string title = "NetFlowAudit Network Topology Map",
        CancellationToken cancellationToken = default)
    {
        var graph = await BuildGraphFromFilesAsync(csvFilePaths, filterOptions, csvDelimiter, cancellationToken).ConfigureAwait(false);
        await WriteHtmlMapAsync(graph, outputHtmlPath, renderingOptions, title, cancellationToken).ConfigureAwait(false);
        return graph;
    }

    /// <summary>
    /// Discovers all audit CSV files matching a search pattern in a directory and generates a consolidated HTML5 topology map.
    /// </summary>
    /// <param name="directoryPath">Directory path containing audit CSV files.</param>
    /// <param name="searchPattern">File search pattern (e.g. "*.csv").</param>
    /// <param name="outputHtmlPath">Output HTML file path.</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="csvDelimiter">CSV delimiter character (comma by default).</param>
    /// <param name="title">Map document title.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated consolidated network topology graph.</returns>
    public Task<NetworkTopologyGraph> GenerateHtmlMapFromDirectoryAsync(
        string directoryPath,
        string searchPattern,
        string outputHtmlPath,
        TopologyFilterOptions filterOptions,
        char csvDelimiter = ',',
        string title = "NetFlowAudit Network Topology Map",
        CancellationToken cancellationToken = default)
        => GenerateHtmlMapFromDirectoryAsync(directoryPath, searchPattern, outputHtmlPath, filterOptions, new TopologyRenderingOptions(), csvDelimiter, title, cancellationToken);

    /// <summary>
    /// Discovers all audit CSV files matching a search pattern in a directory and generates a consolidated HTML5 topology map.
    /// </summary>
    /// <param name="directoryPath">Directory path containing audit CSV files.</param>
    /// <param name="searchPattern">File search pattern (e.g. "*.csv").</param>
    /// <param name="outputHtmlPath">Output HTML file path.</param>
    /// <param name="filterOptions">Inclusion and exclusion filter options.</param>
    /// <param name="renderingOptions">Sorting, routing, and spacing rendering options.</param>
    /// <param name="csvDelimiter">CSV delimiter character (comma by default).</param>
    /// <param name="title">Map document title.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Generated consolidated network topology graph.</returns>
    public Task<NetworkTopologyGraph> GenerateHtmlMapFromDirectoryAsync(
        string directoryPath,
        string searchPattern,
        string outputHtmlPath,
        TopologyFilterOptions filterOptions,
        TopologyRenderingOptions renderingOptions,
        char csvDelimiter = ',',
        string title = "NetFlowAudit Network Topology Map",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputHtmlPath);
        ArgumentNullException.ThrowIfNull(filterOptions);
        ArgumentNullException.ThrowIfNull(renderingOptions);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"The audit directory '{directoryPath}' does not exist.");
        }

        var pattern = string.IsNullOrWhiteSpace(searchPattern) ? "*.csv" : searchPattern;
        var files = Directory.GetFiles(directoryPath, pattern, SearchOption.TopDirectoryOnly);

        return GenerateHtmlMapFromFilesAsync(files, outputHtmlPath, filterOptions, renderingOptions, csvDelimiter, title, cancellationToken);
    }

    /// <summary>
    /// Exports the specified network topology graph to an uncompressed Draw.io (.drawio) XML file.
    /// </summary>
    /// <param name="graph">Consolidated topology graph.</param>
    /// <param name="outputDrawioPath">Target Draw.io output file path.</param>
    /// <param name="title">Diagram title.</param>
    /// <param name="aggregationMode">Edge aggregation mode.</param>
    /// <param name="theme">Visual diagram theme (DarkModern by default).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    public static Task ExportToDrawioAsync(
        NetworkTopologyGraph graph,
        string outputDrawioPath,
        string title = "NetFlowAudit Network Topology",
        EdgeAggregationMode aggregationMode = EdgeAggregationMode.AdaptiveThreshold,
        DiagramTheme? theme = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDrawioPath);

        var adapter = new TopologyToDrawioAdapter(aggregationMode: aggregationMode);
        var diagram = adapter.Convert(graph, title);
        var renderer = new DrawioXmlRenderer();
        var xml = renderer.Render(diagram, theme ?? DiagramTheme.DarkModern);

        var dir = Path.GetDirectoryName(Path.GetFullPath(outputDrawioPath));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return File.WriteAllTextAsync(outputDrawioPath, xml, System.Text.Encoding.UTF8, cancellationToken);
    }
}
