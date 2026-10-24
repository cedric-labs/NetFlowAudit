using NetFlowAudit.Core.Models;
using SocketForge.Models;
using TopologyForge.Builders;
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
        var effectiveOptions = renderingOptions with { Title = title };

        await _htmlRenderer.WriteHtmlFileAsync(outputHtmlPath, graph, effectiveOptions, cancellationToken).ConfigureAwait(false);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(csvFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputHtmlPath);
        ArgumentNullException.ThrowIfNull(filterOptions);
        ArgumentNullException.ThrowIfNull(renderingOptions);

        var flowRecords = await _csvParser.ParseFileAsync(csvFilePath, csvDelimiter, cancellationToken).ConfigureAwait(false);
        var evaluator = new TopologyFlowEvaluator(filterOptions);
        var builder = new TopologyGraphBuilder(evaluator);
        var graph = builder.BuildGraph(flowRecords);
        var effectiveOptions = renderingOptions with { Title = title };

        await _htmlRenderer.WriteHtmlFileAsync(outputHtmlPath, graph, effectiveOptions, cancellationToken).ConfigureAwait(false);
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
        ArgumentNullException.ThrowIfNull(csvFilePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputHtmlPath);
        ArgumentNullException.ThrowIfNull(filterOptions);
        ArgumentNullException.ThrowIfNull(renderingOptions);

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
        var graph = builder.BuildGraph(allFlowRecords);
        var effectiveOptions = renderingOptions with { Title = title };

        await _htmlRenderer.WriteHtmlFileAsync(outputHtmlPath, graph, effectiveOptions, cancellationToken).ConfigureAwait(false);
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
}
