namespace NetFlowAudit.Tests.Export;

using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Models;
using SocketForge.Models;
using TopologyForge.Export.Drawio.Adapters;
using TopologyForge.Export.Drawio.Styling;
using TopologyForge.Models;
using Xunit;

public sealed class AuditTopologyMapperDrawioTests
{
    [Fact]
    public async Task ExportToDrawioAsync_WhenGraphProvided_WritesValidDrawioXmlFile()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"drawio_test_{Guid.NewGuid():N}.drawio");

        try
        {
            var server = new TopologyServerNode(
                NodeId: "srv_local",
                HostName: "LOCAL-AUDIT",
                IpAddress: "192.168.1.10",
                IsLocalAuditedServer: true,
                ListeningPorts: [new ServerListenerDescriptor(80, TopologyTransportProtocol.Tcp, "nginx", "web")],
                InboundConnectionCount: 0,
                OutboundConnectionCount: 1);

            var target = new TopologyServerNode(
                NodeId: "srv_db",
                HostName: "POSTGRES-DB",
                IpAddress: "192.168.1.50",
                IsLocalAuditedServer: false,
                ListeningPorts: [new ServerListenerDescriptor(5432, TopologyTransportProtocol.Tcp, "postgres", "db")],
                InboundConnectionCount: 1,
                OutboundConnectionCount: 0);

            var edge = new TopologyServiceEdge(
                EdgeId: "e1",
                SourceNodeId: "srv_local",
                TargetNodeId: "srv_db",
                TargetPort: 5432,
                Protocol: TopologyTransportProtocol.Tcp,
                ProcessName: "app",
                ServiceTarget: "db",
                ObservationCount: 120,
                FlowDirection: TopologyFlowDirection.Outbound);

            var graph = new NetworkTopologyGraph(DateTimeOffset.UtcNow, [server, target], [edge], 1, 0);

            // Act
            await AuditTopologyMapper.ExportToDrawioAsync(
                graph,
                tempFile,
                "Audit Topology",
                EdgeAggregationMode.AdaptiveThreshold,
                DiagramTheme.DarkModern,
                CancellationToken.None);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var rawBytes = await File.ReadAllBytesAsync(tempFile);
            rawBytes.Should().StartWith([(byte)'<', (byte)'?']);
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Contain("<mxfile host=\"app.diagrams.net\"")
                .And.Contain("name=\"Audit Topology\"")
                .And.Contain("LOCAL-AUDIT")
                .And.Contain("POSTGRES-DB")
                .And.Contain(":5432/Tcp");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
