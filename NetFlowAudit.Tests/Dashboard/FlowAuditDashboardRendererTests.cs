using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Cli.Dashboard;
using NetFlowAudit.Core.Filtering;
using NetFlowAudit.Core.Models;
using ProcessForge.Filtering;
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
        var frame = FlowAuditDashboardRenderer.BuildWindowTooSmallFrame(70, 20);

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
    public void BuildDashboardFrame_WhenWindowIsWide_AdaptsAllLinesToTargetWidthOf130Characters()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var result = new FlowAuditCycleResult(
            DateTimeOffset.UtcNow,
            TotalSocketsScanned: 5,
            ExcludedSocketsCount: 2,
            NewlyDiscoveredCount: 1,
            TotalUniqueFlowsTracked: 3,
            NewlyDiscoveredEntries: [],
            ActiveSockets: []);

        const int wideWidth = 130;
        const int tallHeight = 25;

        // Act
        var frame = renderer.BuildDashboardFrame(result, SampleHost, wideWidth, tallHeight);

        // Assert
        var lines = frame.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        lines.Length.Should().BeGreaterThan(15);

        for (var i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i];
            var visibleWidth = FlowAuditDashboardRenderer.GetVisibleWidth(line);
            visibleWidth.Should().Be(wideWidth, $"Line index {i} must match expanded width of {wideWidth}");
        }
    }

    [Fact]
    public void BuildDashboardFrame_WhenWindowHeightIsLarge_ExpandsDiscoveriesRowsCount()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var result = new FlowAuditCycleResult(
            DateTimeOffset.UtcNow,
            TotalSocketsScanned: 5,
            ExcludedSocketsCount: 2,
            NewlyDiscoveredCount: 1,
            TotalUniqueFlowsTracked: 3,
            NewlyDiscoveredEntries: [],
            ActiveSockets: []);

        const int targetWidth = 110;
        const int targetHeight = 40;

        // Act
        var frame = renderer.BuildDashboardFrame(result, SampleHost, targetWidth, targetHeight);

        // Assert
        var lines = frame.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        // Fixed overhead is 15 lines (including footer). With targetHeight = 40, discoveries rows = 25.
        // Total lines in frame should be 40 (39 box lines + 1 footer line).
        lines.Length.Should().Be(40);
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

    [Fact]
    public void BuildDashboardFrame_WhenEntryHasRemoteHostName_RendersHostNameInDistantColumn()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 54321),
            new SocketEndpoint(IPAddress.Parse("140.82.121.4"), 443),
            "Established",
            new ProcessMetadata(1234, "git", "git.exe", "git fetch", "", "user"));

        var entry = new NetworkAuditEntry(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            SampleHost,
            socket,
            "github.com");

        var result = new FlowAuditCycleResult(
            DateTimeOffset.UtcNow,
            TotalSocketsScanned: 1,
            ExcludedSocketsCount: 0,
            NewlyDiscoveredCount: 1,
            TotalUniqueFlowsTracked: 1,
            NewlyDiscoveredEntries: [entry],
            ActiveSockets: [socket]);

        renderer.Render(result, SampleHost);

        // Act
        var frame = renderer.BuildDashboardFrame(result, SampleHost, 120, 25);

        // Assert
        frame.Should().Contain("github.com:443");
    }

    [Fact]
    public void BuildDashboardFrame_WhenDiscoveriesTableRendered_AlignsHeadersStrictlyWithRowValues()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 54321),
            new SocketEndpoint(IPAddress.Parse("140.82.121.4"), 443),
            "Established",
            new ProcessMetadata(1234, "sampleproc", "/usr/bin/sampleproc", "sampleproc run", "targetapp.dll", "user"));

        var entry = new NetworkAuditEntry(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            SampleHost,
            socket,
            "github.com");

        var result = new FlowAuditCycleResult(
            DateTimeOffset.UtcNow,
            TotalSocketsScanned: 1,
            ExcludedSocketsCount: 0,
            NewlyDiscoveredCount: 1,
            TotalUniqueFlowsTracked: 1,
            NewlyDiscoveredEntries: [entry],
            ActiveSockets: [socket]);

        renderer.Render(result, SampleHost);

        // Act
        var frame = renderer.BuildDashboardFrame(result, SampleHost, 110, 25);
        var lines = frame.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        var headerLineIndex = Array.FindIndex(lines, l => l.Contains("PROTO") && l.Contains("LOCAL"));
        headerLineIndex.Should().BeGreaterThan(-1);

        var dataLineIndex = headerLineIndex + 1;
        dataLineIndex.Should().BeLessThan(lines.Length);

        var headerClean = StripAnsi(lines[headerLineIndex]);
        var dataClean = StripAnsi(lines[dataLineIndex]);

        // Assert column alignment
        headerClean.IndexOf("LOCAL").Should().Be(dataClean.IndexOf("192.168.1.10:54321"), "LOCAL header must align with local endpoint");
        headerClean.IndexOf("DISTANT").Should().Be(dataClean.IndexOf("github.com:443"), "DISTANT header must align with remote endpoint");
        headerClean.IndexOf("PROCESSUS").Should().Be(dataClean.IndexOf("sampleproc"), "PROCESSUS header must align with process name");
        headerClean.IndexOf("CIBLE / CMD").Should().Be(dataClean.IndexOf("targetapp.dll"), "CIBLE / CMD header must align with target executable");
    }

    [Fact]
    public void MoveSelectionDown_WhenTriggered_ActivatesSelectionAndNavigatesRows()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var socket1 = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50001),
            new SocketEndpoint(IPAddress.Parse("1.1.1.1"), 443),
            "Established",
            new ProcessMetadata(100, "curl", "", "", "", "user"));
        var socket2 = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50002),
            new SocketEndpoint(IPAddress.Parse("8.8.8.8"), 443),
            "Established",
            new ProcessMetadata(200, "wget", "", "", "", "user"));

        var entry1 = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket1);
        var entry2 = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket2);

        var result = new FlowAuditCycleResult(
            DateTimeOffset.UtcNow,
            2, 0, 2, 2,
            [entry1, entry2],
            [socket1, socket2]);

        renderer.Render(result, SampleHost);
        renderer.IsSelectionActive.Should().BeFalse();

        // Act - 1ère pression flèche bas active la sélection sur ligne 0
        renderer.MoveSelectionDown();
        renderer.IsSelectionActive.Should().BeTrue();
        renderer.SelectedRowIndex.Should().Be(0);

        // Act - 2ème pression descend à la ligne 1
        renderer.MoveSelectionDown();
        renderer.SelectedRowIndex.Should().Be(1);

        // Act - Flèche haut remonte à la ligne 0
        renderer.MoveSelectionUp();
        renderer.SelectedRowIndex.Should().Be(0);

        // Act - Échap annule
        renderer.CancelSelection();
        renderer.IsSelectionActive.Should().BeFalse();
    }

    [Fact]
    public void MoveSelectionRight_WhenTriggered_CyclesThroughInteractiveColumns()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50001),
            new SocketEndpoint(IPAddress.Parse("1.1.1.1"), 443),
            "Established",
            new ProcessMetadata(100, "curl", "", "", "", "user"));

        var entry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket);
        var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 1, 1, [entry], [socket]);

        renderer.Render(result, SampleHost);

        // Act & Assert
        renderer.MoveSelectionRight();
        renderer.SelectedColumn.Should().Be(InteractiveExclusionColumn.Local);

        renderer.MoveSelectionRight();
        renderer.SelectedColumn.Should().Be(InteractiveExclusionColumn.Remote);

        renderer.MoveSelectionRight();
        renderer.SelectedColumn.Should().Be(InteractiveExclusionColumn.Process);

        renderer.MoveSelectionRight();
        renderer.SelectedColumn.Should().Be(InteractiveExclusionColumn.Target);

        // Cycle back to Local
        renderer.MoveSelectionRight();
        renderer.SelectedColumn.Should().Be(InteractiveExclusionColumn.Local);

        // Cycle left to Target
        renderer.MoveSelectionLeft();
        renderer.SelectedColumn.Should().Be(InteractiveExclusionColumn.Target);
    }

    [Fact]
    public void ApplyExclusionAtSelection_WhenProcessSelected_InvokesConfiguratorAndPurgesRow()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_render_ex_{Guid.NewGuid():N}.json");
        try
        {
            var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
            var socket = new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50001),
                new SocketEndpoint(IPAddress.Parse("1.1.1.1"), 443),
                "Established",
                new ProcessMetadata(100, "noisy-service", "", "", "", "user"));

            var entry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket);
            var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 1, 1, [entry], [socket]);

            renderer.Render(result, SampleHost);

            var configurator = new FlowExclusionConfigurator(
                new AppConfiguration(),
                tempFile,
                new NetworkFlowFilter(new NetworkFlowFilterOptions()),
                new ProcessExclusionFilter(new ProcessExclusionOptions()));

            // Act - Select Process column and apply
            renderer.MoveSelectionDown();
            renderer.MoveSelectionRight(); // Remote
            renderer.MoveSelectionRight(); // Process
            renderer.SelectedColumn.Should().Be(InteractiveExclusionColumn.Process);

            var applied = renderer.ApplyExclusionAtSelection(configurator);

            // Assert
            applied.Should().BeTrue();
            configurator.CurrentConfiguration.ExcludedExactNames.Should().Contain("noisy-service");

            // Frame should now be cleared of this entry and contain confirmation banner
            var frame = renderer.BuildDashboardFrame(result, SampleHost);
            frame.Should().Contain("noisy-service");
            frame.Should().Contain("✔");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch (IOException) { }
            }
        }
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
