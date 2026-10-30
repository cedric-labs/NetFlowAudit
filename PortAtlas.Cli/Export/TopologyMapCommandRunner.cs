using PortAtlas.Cli.Configuration;
using PortAtlas.Core.Export;

namespace PortAtlas.Cli.Export;

/// <summary>
/// Runs the <c>map</c> command: builds the topology graph from audit logs and writes the requested HTML map and Draw.io diagram.
/// </summary>
/// <param name="topologyMapper">Mapper converting audit logs into a topology graph and rendering it.</param>
public sealed class TopologyMapCommandRunner(AuditTopologyMapper? topologyMapper = null)
{
    private readonly AuditTopologyMapper _topologyMapper = topologyMapper ?? new AuditTopologyMapper();

    /// <summary>
    /// Builds the graph and writes the outputs.
    /// </summary>
    /// <param name="auditFiles">CSV audit logs to consolidate.</param>
    /// <param name="htmlPath">HTML map to write, or <see langword="null"/> for none.</param>
    /// <param name="drawioPath">Draw.io diagram to write, or <see langword="null"/> for none.</param>
    /// <param name="config">Configuration providing filters, rendering and Draw.io style.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Exit code (0 on success).</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="auditFiles"/> or <paramref name="config"/> is null.</exception>
    public async Task<int> RunAsync(
        IReadOnlyList<string> auditFiles,
        string? htmlPath,
        string? drawioPath,
        AppConfiguration config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditFiles);
        ArgumentNullException.ThrowIfNull(config);

        var delimiter = !string.IsNullOrEmpty(config.CsvDelimiter) ? config.CsvDelimiter[0] : ',';
        Console.WriteLine($"Reading {auditFiles.Count} audit log(s)...");
        var graph = await _topologyMapper.BuildGraphFromFilesAsync(
            auditFiles,
            TopologyMapConfigurationBuilder.BuildFilterOptions(config),
            delimiter,
            cancellationToken).ConfigureAwait(false);

        if (htmlPath is not null)
        {
            await _topologyMapper.WriteHtmlMapAsync(
                graph,
                htmlPath,
                TopologyMapConfigurationBuilder.BuildRenderingOptions(config),
                "Network Flow & Server Topology Map",
                cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"Topology map generated: {Path.GetFullPath(htmlPath)} ({graph.Nodes.Count} servers, {graph.Edges.Count} flows)");
        }

        if (drawioPath is not null)
        {
            await AuditTopologyMapper.ExportToDrawioAsync(
                graph,
                drawioPath,
                "PortAtlas Network Topology",
                TopologyMapConfigurationBuilder.BuildDrawioEdgeMode(config),
                TopologyMapConfigurationBuilder.BuildDrawioTheme(config),
                TopologyMapConfigurationBuilder.BuildDrawioLayoutOptions(config),
                cancellationToken).ConfigureAwait(false);
            Console.WriteLine($"Draw.io diagram generated: {Path.GetFullPath(drawioPath)} ({graph.Nodes.Count} servers, {graph.Edges.Count} flows)");
        }

        return 0;
    }
}
