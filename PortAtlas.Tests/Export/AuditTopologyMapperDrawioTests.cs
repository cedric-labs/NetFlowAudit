namespace PortAtlas.Tests.Export;

using System.Net;
using AwesomeAssertions;
using PortAtlas.Core.Export;
using PortAtlas.Core.Models;
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
                cancellationToken: CancellationToken.None);

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
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ExportToDrawioAsync_WhenSubnetGroupingChosen_WritesSubnetZonesOnlyWhenOn(bool groupBySubnets, bool expectZone)
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"drawio_subnets_{Guid.NewGuid():N}.drawio");
        var graph = new NetworkTopologyGraph(
            DateTimeOffset.UtcNow,
            [
                new TopologyServerNode("srv_local", "LOCAL-AUDIT", "192.168.1.10", true, [], 0, 1),
                new TopologyServerNode("srv_nas", "NAS", "192.168.1.50", false, [], 1, 0)
            ],
            [],
            0,
            0);

        try
        {
            // Act
            await AuditTopologyMapper.ExportToDrawioAsync(
                graph,
                tempFile,
                layoutOptions: new TopologyDrawioLayoutOptions { GroupBySubnets = groupBySubnets });

            // Assert
            var content = await File.ReadAllTextAsync(tempFile);
            content.Contains("Subnet 192.168.1.0/24", StringComparison.Ordinal).Should().Be(expectZone);
            content.Should().Contain("NAS");
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
