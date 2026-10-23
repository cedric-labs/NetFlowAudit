using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
using TopologyForge.Filtering;
using TopologyForge.Models;
using TopologyForge.Rendering;

namespace NetFlowAudit.Tests.Export;

public sealed class AuditTopologyMapperTests
{
    private static NetworkAuditEntry CreateSampleAuditEntry(
        string serverName = "SRV-WEB-01",
        string serverIp = "10.0.1.10",
        int localPort = 52000,
        string remoteIp = "10.0.2.20",
        int remotePort = 1433,
        string remoteHost = "SRV-SQL-01",
        SocketFlowDirection direction = SocketFlowDirection.Outbound,
        string processName = "w3wp")
    {
        var server = new ServerHostMetadata(serverName, "Windows Server", serverIp);
        var process = new ProcessMetadata(1234, processName, @"C:\w3wp.exe", "w3wp.exe", "MyAppPool", "IIS APPPOOL");
        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            direction,
            new SocketEndpoint(IPAddress.Parse(serverIp), localPort),
            new SocketEndpoint(IPAddress.Parse(remoteIp), remotePort),
            "ESTABLISHED",
            process);

        return new NetworkAuditEntry(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow,
            15,
            server,
            socket,
            remoteHost);
    }

    [Fact]
    public void ToTopologyFlowRecord_WhenAuditEntryProvided_MapsAllPropertiesAccurately()
    {
        // Arrange
        var entry = CreateSampleAuditEntry();

        // Act
        var record = AuditTopologyMapper.ToTopologyFlowRecord(entry);

        // Assert
        record.SourceServerName.Should().Be("SRV-WEB-01");
        record.SourceIp.Should().Be("10.0.1.10");
        record.LocalPort.Should().Be(52000);
        record.RemoteServerName.Should().Be("SRV-SQL-01");
        record.RemoteIp.Should().Be("10.0.2.20");
        record.RemotePort.Should().Be(1433);
        record.Protocol.Should().Be(TopologyTransportProtocol.Tcp);
        record.Direction.Should().Be(TopologyFlowDirection.Outbound);
        record.ProcessName.Should().Be("w3wp");
        record.ServiceTarget.Should().Be("MyAppPool");
        record.ObservationCount.Should().Be(15);
    }

    [Fact]
    public async Task GenerateHtmlMapFromEntriesAsync_WhenFilteredEntriesProvided_WritesSelfContainedHtmlMap()
    {
        // Arrange
        var mapper = new AuditTopologyMapper();
        var entries = new[]
        {
            CreateSampleAuditEntry(localPort: 443, remoteIp: "0.0.0.0", remotePort: 0, remoteHost: "", direction: SocketFlowDirection.Listening, processName: "iis"),
            CreateSampleAuditEntry(localPort: 52100, remoteIp: "10.0.2.20", remotePort: 1433, remoteHost: "SRV-SQL-01", direction: SocketFlowDirection.Outbound, processName: "w3wp"),
            CreateSampleAuditEntry(localPort: 52101, remoteIp: "10.0.99.99", remotePort: 53, remoteHost: "SRV-DNS-01", direction: SocketFlowDirection.Outbound, processName: "dns")
        };

        var filterOptions = new TopologyFilterOptions
        {
            ExcludedPorts = [NetworkPortRange.Single(53)]
        };

        var tempHtml = Path.Combine(Path.GetTempPath(), $"netflow_map_{Guid.NewGuid():N}.html");

        try
        {
            // Act
            var graph = await mapper.GenerateHtmlMapFromEntriesAsync(entries, tempHtml, filterOptions, "Carte Audit Test");

            // Assert
            graph.Nodes.Should().HaveCount(2);
            graph.Edges.Should().ContainSingle();
            File.Exists(tempHtml).Should().BeTrue();
            var html = await File.ReadAllTextAsync(tempHtml);
            html.Should().Contain("SRV-WEB-01");
            html.Should().Contain("SRV-SQL-01");
            html.Should().NotContain("SRV-DNS-01");
        }
        finally
        {
            if (File.Exists(tempHtml))
            {
                File.Delete(tempHtml);
            }
        }
    }

    [Fact]
    public async Task GenerateHtmlMapFromEntriesAsync_WhenRenderingOptionsProvided_EmbedsSelectedOptionsInHtml()
    {
        // Arrange
        var mapper = new AuditTopologyMapper();
        var entries = new[]
        {
            CreateSampleAuditEntry(localPort: 52100, remoteIp: "10.0.2.20", remotePort: 1433, remoteHost: "SRV-SQL-01", direction: SocketFlowDirection.Outbound, processName: "w3wp")
        };

        var filterOptions = new TopologyFilterOptions();
        var renderingOptions = new TopologyRenderingOptions
        {
            DefaultSortMethod = TopologyNodeSortMethod.Alphabetical,
            DefaultRoutingMode = TopologyEdgeRoutingMode.OrthogonalCorridors,
            DefaultSpacingProfile = TopologySpacingProfile.Spacious
        };

        var tempHtml = Path.Combine(Path.GetTempPath(), $"netflow_map_opts_{Guid.NewGuid():N}.html");

        try
        {
            // Act
            await mapper.GenerateHtmlMapFromEntriesAsync(entries, tempHtml, filterOptions, renderingOptions, "Carte Options Test");

            // Assert
            var html = await File.ReadAllTextAsync(tempHtml);
            html.Should().Contain("\"defaultSort\":\"alpha\"");
            html.Should().Contain("\"defaultRouting\":\"orthogonal\"");
            html.Should().Contain("\"defaultSpacing\":\"spacious\"");
        }
        finally
        {
            if (File.Exists(tempHtml))
            {
                File.Delete(tempHtml);
            }
        }
    }

    [Fact]
    public async Task GenerateHtmlMapFromCsvAsync_WhenCsvFileProvided_ParsesFiltersAndWritesHtmlMap()
    {
        // Arrange
        var mapper = new AuditTopologyMapper();
        var tempCsv = Path.Combine(Path.GetTempPath(), $"netflow_input_{Guid.NewGuid():N}.csv");
        var tempHtml = Path.Combine(Path.GetTempPath(), $"netflow_csv_map_{Guid.NewGuid():N}.html");

        try
        {
            await File.WriteAllLinesAsync(tempCsv,
            [
                "ServerName,ServerPrimaryIp,Protocol,Direction,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessName,TargetExecutable,ObservationCount",
                "SRV-APP-01,10.1.0.10,Tcp,Outbound,10.1.0.10,55100,10.1.0.20,5432,SRV-PGSQL-01,dotnet,App.dll,20",
                "SRV-APP-01,10.1.0.10,Tcp,Outbound,10.1.0.10,55101,10.1.0.30,6379,SRV-REDIS-01,dotnet,App.dll,10"
            ]);

            var filterOptions = new TopologyFilterOptions
            {
                IncludedPorts = [NetworkPortRange.Single(5432)]
            };

            // Act
            var graph = await mapper.GenerateHtmlMapFromCsvAsync(tempCsv, tempHtml, filterOptions);

            // Assert
            graph.Nodes.Should().HaveCount(2);
            graph.Edges.Should().ContainSingle(e => e.TargetPort == 5432);
            File.Exists(tempHtml).Should().BeTrue();
            var html = await File.ReadAllTextAsync(tempHtml);
            html.Should().Contain("SRV-PGSQL-01");
            html.Should().NotContain("SRV-REDIS-01");
        }
        finally
        {
            if (File.Exists(tempCsv))
            {
                File.Delete(tempCsv);
            }

            if (File.Exists(tempHtml))
            {
                File.Delete(tempHtml);
            }
        }
    }
}
