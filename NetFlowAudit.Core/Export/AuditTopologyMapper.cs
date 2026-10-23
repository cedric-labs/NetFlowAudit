using NetFlowAudit.Core.Models;
using SocketForge.Models;
using TopologyForge.Builders;
using TopologyForge.Filtering;
using TopologyForge.Models;
using TopologyForge.Parsers;
using TopologyForge.Rendering;

namespace NetFlowAudit.Core.Export;

/// <summary>
/// Adaptateur et orchestrateur reliant les enregistrements d'audit <see cref="NetFlowAudit"/> au moteur de cartographie <see cref="TopologyForge"/>.
/// </summary>
/// <param name="csvParser">Analyseur de fichiers CSV topologiques (optionnel).</param>
/// <param name="htmlRenderer">Générateur de cartes HTML autonomes (optionnel).</param>
public sealed class AuditTopologyMapper(
    ITopologyCsvParser? csvParser = null,
    ITopologyHtmlRenderer? htmlRenderer = null)
{
    private readonly ITopologyCsvParser _csvParser = csvParser ?? new TopologyCsvParser();
    private readonly ITopologyHtmlRenderer _htmlRenderer = htmlRenderer ?? new TopologyHtmlRenderer();

    /// <summary>
    /// Convertit une entrée d'audit consolidée en enregistrement de flux topologique.
    /// </summary>
    /// <param name="entry">Entrée d'audit réseau.</param>
    /// <returns>Enregistrement topologique normalisé.</returns>
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
    /// Construit le graphe topologique filtré et génère le fichier HTML autonome à partir d'une collection d'entrées d'audit.
    /// </summary>
    /// <param name="entries">Entrées d'audit collectées.</param>
    /// <param name="outputHtmlPath">Chemin du fichier HTML de sortie.</param>
    /// <param name="filterOptions">Options de filtrage d'inclusion et d'exclusion.</param>
    /// <param name="title">Titre de la cartographie.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopératif.</param>
    /// <returns>Graphe topologique généré.</returns>
    public async Task<NetworkTopologyGraph> GenerateHtmlMapFromEntriesAsync(
        IEnumerable<NetworkAuditEntry> entries,
        string outputHtmlPath,
        TopologyFilterOptions filterOptions,
        string title = "Cartographie NetFlowAudit",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputHtmlPath);
        ArgumentNullException.ThrowIfNull(filterOptions);

        var flowRecords = entries.Select(ToTopologyFlowRecord);
        var evaluator = new TopologyFlowEvaluator(filterOptions);
        var builder = new TopologyGraphBuilder(evaluator);
        var graph = builder.BuildGraph(flowRecords);

        await _htmlRenderer.WriteHtmlFileAsync(outputHtmlPath, graph, title, cancellationToken).ConfigureAwait(false);
        return graph;
    }

    /// <summary>
    /// Lit un fichier CSV d'audit existant, applique les filtres d'inclusion/exclusion et génère la cartographie HTML autonome.
    /// </summary>
    /// <param name="csvFilePath">Chemin du fichier CSV source.</param>
    /// <param name="outputHtmlPath">Chemin du fichier HTML de sortie.</param>
    /// <param name="filterOptions">Options de filtrage d'inclusion et d'exclusion.</param>
    /// <param name="csvDelimiter">Délimiteur CSV (virgule par défaut).</param>
    /// <param name="title">Titre de la cartographie.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopératif.</param>
    /// <returns>Graphe topologique généré.</returns>
    public async Task<NetworkTopologyGraph> GenerateHtmlMapFromCsvAsync(
        string csvFilePath,
        string outputHtmlPath,
        TopologyFilterOptions filterOptions,
        char csvDelimiter = ',',
        string title = "Cartographie NetFlowAudit",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputHtmlPath);
        ArgumentNullException.ThrowIfNull(filterOptions);

        var flowRecords = await _csvParser.ParseFileAsync(csvFilePath, csvDelimiter, cancellationToken).ConfigureAwait(false);
        var evaluator = new TopologyFlowEvaluator(filterOptions);
        var builder = new TopologyGraphBuilder(evaluator);
        var graph = builder.BuildGraph(flowRecords);

        await _htmlRenderer.WriteHtmlFileAsync(outputHtmlPath, graph, title, cancellationToken).ConfigureAwait(false);
        return graph;
    }
}
