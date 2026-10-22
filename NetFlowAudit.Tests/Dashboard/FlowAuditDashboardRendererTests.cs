using System.Net;
using FluentAssertions;
using NetFlowAudit.Cli.Dashboard;
using NetFlowAudit.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace NetFlowAudit.Tests.Dashboard;

public sealed class FlowAuditDashboardRendererTests
{
    private static readonly ServerHostMetadata SampleHost = new(
        "HOMELAB-PC",
        "Microsoft Windows 11 Pro",
        "192.168.1.10");

    [Fact]
    public void GetVisibleWidth_WhenStringContainsAnsiEscapeCodes_ReturnsOnlyVisibleCharactersCount()
    {
        // Arrange
        const string ansiText = "\x1b[36m│\x1b[0m \x1b[1m\x1b[97mNetFlowAudit\x1b[0m \x1b[36m│\x1b[0m";

        // Act
        var visibleLen = FlowAuditDashboardRenderer.GetVisibleWidth(ansiText);

        // Assert
        // Visible text is "│ NetFlowAudit │" -> 1 + 1 + 12 + 1 + 1 = 16 characters
        visibleLen.Should().Be(16);
    }

    [Fact]
    public void BuildDashboardFrame_WhenRendered_EnsuresEveryBoxLineHasExactTargetWidthOf102Characters()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var activeSockets = new List<ActiveSocketConnection>
        {
            new(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Listening,
                new SocketEndpoint(IPAddress.Any, 80),
                new SocketEndpoint(IPAddress.Any, 0),
                "Listen",
                new ProcessMetadata(100, "nginx", "/usr/sbin/nginx", "", "", "root")),
            new(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 52140),
                new SocketEndpoint(IPAddress.Parse("93.184.216.34"), 443),
                "Established",
                new ProcessMetadata(200, "curl", "/usr/bin/curl", "curl https://example.com", "", "user")),
            new(
                SocketTransportProtocol.Udp,
                SocketFlowDirection.Listening,
                new SocketEndpoint(IPAddress.Any, 53),
                new SocketEndpoint(IPAddress.Any, 0),
                "Listen",
                new ProcessMetadata(300, "dnsmasq", "/usr/sbin/dnsmasq", "", "", "system"))
        };

        var entry = new NetworkAuditEntry(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            SampleHost,
            activeSockets[1]);

        var result = new FlowAuditCycleResult(
            DateTimeOffset.UtcNow,
            TotalSocketsScanned: 3,
            ExcludedSocketsCount: 1,
            NewlyDiscoveredCount: 1,
            TotalUniqueFlowsTracked: 1,
            NewlyDiscoveredEntries: [entry],
            ActiveSockets: activeSockets);

        // Act
        renderer.Render(result, SampleHost);
        var frame = renderer.BuildDashboardFrame(result, SampleHost);

        // Assert
        var lines = frame.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        // All box lines from top border to bottom border must have visible width of 102
        lines.Length.Should().BeGreaterThan(15);

        for (var i = 0; i < lines.Length - 1; i++) // Excluding footer command line
        {
            var line = lines[i];
            var visibleWidth = FlowAuditDashboardRenderer.GetVisibleWidth(line);
            visibleWidth.Should().Be(102, $"Line index {i} ('{line}') must have exact visible width of 102 characters");
        }
    }
}
