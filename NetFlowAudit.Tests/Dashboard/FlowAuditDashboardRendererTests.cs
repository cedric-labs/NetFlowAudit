using System.Net;
using System.Text.RegularExpressions;
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
        visibleLen.Should().Be(16);
    }

    [Fact]
    public void BuildWindowTooSmallFrame_WhenWindowWidthIsNarrow_RendersFittedBoxWithoutWrapping()
    {
        // Arrange & Act
        var frame = FlowAuditDashboardRenderer.BuildWindowTooSmallFrame(70);

        // Assert
        var lines = frame.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        lines.Should().NotBeEmpty();

        foreach (var line in lines)
        {
            var visibleWidth = FlowAuditDashboardRenderer.GetVisibleWidth(line);
            visibleWidth.Should().BeLessThanOrEqualTo(70);
        }
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

        lines.Length.Should().BeGreaterThan(15);

        for (var i = 0; i < lines.Length - 1; i++) // Excluding footer command line
        {
            var line = lines[i];
            var visibleWidth = FlowAuditDashboardRenderer.GetVisibleWidth(line);
            visibleWidth.Should().Be(102, $"Line index {i} ('{line}') must have exact visible width of 102 characters");
        }
    }

    [Fact]
    public void BuildDashboardFrame_WhenRendered_EnsuresInternalSeparatorsInHeaderAreStrictlyAligned()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var result = new FlowAuditCycleResult(
            DateTimeOffset.UtcNow,
            TotalSocketsScanned: 10,
            ExcludedSocketsCount: 5,
            NewlyDiscoveredCount: 2,
            TotalUniqueFlowsTracked: 8,
            NewlyDiscoveredEntries: [],
            ActiveSockets: []);

        // Act
        var frame = renderer.BuildDashboardFrame(result, SampleHost);
        var lines = frame.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        var row1Stripped = StripAnsi(lines[1]);
        var row2Stripped = StripAnsi(lines[2]);
        var row3Stripped = StripAnsi(lines[3]);

        var indices1 = FindPipeIndices(row1Stripped);
        var indices2 = FindPipeIndices(row2Stripped);
        var indices3 = FindPipeIndices(row3Stripped);

        // Assert
        indices1.Should().Equal(indices2, "Header row 1 and row 2 must have identical separator indices");
        indices2.Should().Equal(indices3, "Header row 2 and row 3 must have identical separator indices");
    }

    private static string StripAnsi(string text)
    {
        return Regex.Replace(text, @"\x1B\[[0-?]*[ -/]*[@-~]", string.Empty);
    }

    private static List<int> FindPipeIndices(string text)
    {
        var indices = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '│')
            {
                indices.Add(i);
            }
        }

        return indices;
    }
}
