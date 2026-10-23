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
    public void BuildDashboardFrame_WhenEntryHasRemoteHostName_RendersHostNameInRemoteColumn()
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
        headerClean.IndexOf("REMOTE").Should().Be(dataClean.IndexOf("github.com:443"), "REMOTE header must align with remote endpoint");
        headerClean.IndexOf("PROCESS").Should().Be(dataClean.IndexOf("sampleproc"), "PROCESS header must align with process name");
        headerClean.IndexOf("TARGET / CMD").Should().Be(dataClean.IndexOf("targetapp.dll"), "TARGET / CMD header must align with target executable");
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

        // Act - 1st down arrow activates selection on row 0
        renderer.MoveSelectionDown();
        renderer.IsSelectionActive.Should().BeTrue();
        renderer.SelectedRowIndex.Should().Be(0);

        // Act - 2nd down arrow moves to row 1
        renderer.MoveSelectionDown();
        renderer.SelectedRowIndex.Should().Be(1);

        // Act - 3rd down arrow stops at last row (index 1)
        renderer.MoveSelectionDown();
        renderer.SelectedRowIndex.Should().Be(1);

        // Act - Up arrow moves back to row 0
        renderer.MoveSelectionUp();
        renderer.SelectedRowIndex.Should().Be(0);

        // Act - Esc cancels selection
        renderer.CancelSelection();
        renderer.IsSelectionActive.Should().BeFalse();
    }

    [Fact]
    public void MoveSelectionDown_WhenAtBottomOfVisiblePage_StopsAtLastVisibleRow()
    {
        // Arrange: 25 entries, default fallback height 30 -> 15 visible rows per page (indices 0..14)
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var entries = new List<NetworkAuditEntry>();
        var sockets = new List<ActiveSocketConnection>();

        for (var i = 0; i < 25; i++)
        {
            var sock = new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50000 + i),
                new SocketEndpoint(IPAddress.Parse("1.1.1.1"), 443),
                "Established",
                new ProcessMetadata(100 + i, $"proc-{i}", "", "", "", "user"));
            sockets.Add(sock);
            entries.Add(new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, sock));
        }

        var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 25, 0, 25, 25, entries, sockets);
        renderer.Render(result, SampleHost);

        // Act: press down 40 times
        for (var i = 0; i < 40; i++)
        {
            renderer.MoveSelectionDown();
        }

        // Assert: stopped strictly at last visible row on page 1 (index 14 for 15 visible rows)
        renderer.SelectedRowIndex.Should().Be(14);

        // Pressing up once immediately goes to row 13 without lag
        renderer.MoveSelectionUp();
        renderer.SelectedRowIndex.Should().Be(13);
    }

    [Fact]
    public void NextPage_WhenMultiplePagesExist_NavigatesBetweenPagesAndClampsSelection()
    {
        // Arrange: 20 entries, 15 visible per page -> Page 1 has 15 rows (0..14), Page 2 has 5 rows (0..4)
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var entries = new List<NetworkAuditEntry>();
        var sockets = new List<ActiveSocketConnection>();

        for (var i = 0; i < 20; i++)
        {
            var sock = new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 51000 + i),
                new SocketEndpoint(IPAddress.Parse("8.8.8.8"), 443),
                "Established",
                new ProcessMetadata(200 + i, $"app-{i}", "", "", "", "user"));
            sockets.Add(sock);
            entries.Add(new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, sock));
        }

        var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 20, 0, 20, 20, entries, sockets);
        renderer.Render(result, SampleHost);

        // Act & Assert
        renderer.CurrentPageIndex.Should().Be(0);
        renderer.TotalPages.Should().Be(2);

        renderer.NextPage();
        renderer.CurrentPageIndex.Should().Be(1);

        // On page 2, there are only 5 items (indices 0..4)
        for (var i = 0; i < 10; i++)
        {
            renderer.MoveSelectionDown();
        }

        renderer.SelectedRowIndex.Should().Be(4);

        renderer.PreviousPage();
        renderer.CurrentPageIndex.Should().Be(0);
    }

    [Fact]
    public void LoadNextBatch_WhenInvoked_IncreasesMaxStoredDiscoveriesBy100AndAllowsAll()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        renderer.MaxStoredDiscoveries.Should().Be(100);

        // Act
        renderer.LoadNextBatch();

        // Assert
        renderer.MaxStoredDiscoveries.Should().Be(200);

        // Act - Show all
        renderer.ShowAllDiscoveries();
        renderer.MaxStoredDiscoveries.Should().BeGreaterThanOrEqualTo(10000);
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

    [Fact]
    public void Render_WhenFlowMatchesNetworkFlowFilterRemoteIp_DoesNotAddToRecentDiscoveries()
    {
        // Arrange
        var flowFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions
        {
            ExcludedRemoteIps = ["52.123.242.181"]
        });
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv", flowFilter, null, null);

        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50001),
            new SocketEndpoint(IPAddress.Parse("52.123.242.181"), 443),
            "Established",
            new ProcessMetadata(100, "code", "", "", "", "user"));

        var entry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket);
        var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 1, 1, [entry], [socket]);

        // Act
        renderer.Render(result, SampleHost);

        // Assert
        var frame = renderer.BuildDashboardFrame(result, SampleHost);
        frame.Should().NotContain("52.123.242.181");
    }

    [Fact]
    public void Render_WhenFlowMatchesProcessExclusionFilter_DoesNotAddToRecentDiscoveries()
    {
        // Arrange
        var procFilter = new ProcessExclusionFilter(new ProcessExclusionOptions
        {
            ExcludedExactNames = ["excluded_proc"]
        });
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv", null, procFilter, null);

        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50001),
            new SocketEndpoint(IPAddress.Parse("8.8.8.8"), 443),
            "Established",
            new ProcessMetadata(100, "excluded_proc", "", "", "", "user"));

        var entry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket);
        var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 1, 1, [entry], [socket]);

        // Act
        renderer.Render(result, SampleHost);

        // Assert
        var frame = renderer.BuildDashboardFrame(result, SampleHost);
        frame.Should().NotContain("excluded_proc");
    }

    [Fact]
    public void ApplyExclusionAtSelection_WhenRemoteIpSelectedWithIpv4MappedIpv6_NormalizesAndPurgesAllMatchingEntries()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_render_mapped_{Guid.NewGuid():N}.json");
        try
        {
            var flowFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
            var procFilter = new ProcessExclusionFilter(new ProcessExclusionOptions());
            var configurator = new FlowExclusionConfigurator(new AppConfiguration(), tempFile, flowFilter, procFilter);

            var renderer = new FlowAuditDashboardRenderer(5, "output.csv", flowFilter, procFilter, null);

            var mappedIp = IPAddress.Parse("::ffff:52.123.242.181");
            var socket = new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50001),
                new SocketEndpoint(mappedIp, 443),
                "Established",
                new ProcessMetadata(100, "code", "", "", "", "user"));

            var entry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket);
            var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 1, 1, [entry], [socket]);

            renderer.Render(result, SampleHost);

            // Select Remote column (Col index 1)
            renderer.MoveSelectionDown();
            renderer.MoveSelectionRight();
            renderer.SelectedColumn.Should().Be(InteractiveExclusionColumn.Remote);

            // Act
            var applied = renderer.ApplyExclusionAtSelection(configurator);

            // Assert
            applied.Should().BeTrue();
            configurator.CurrentConfiguration.ExcludedRemoteIps.Should().Contain("52.123.242.181");

            // Subsequent render with matching pure IPv4 or mapped IPv6 should be dropped
            var pureIpSocket = new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50002),
                new SocketEndpoint(IPAddress.Parse("52.123.242.181"), 443),
                "Established",
                new ProcessMetadata(100, "code", "", "", "", "user"));
            var nextEntry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 2, SampleHost, pureIpSocket);
            var nextResult = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 1, 2, [nextEntry], [pureIpSocket]);

            renderer.Render(nextResult, SampleHost);
            var frame = renderer.BuildDashboardFrame(nextResult, SampleHost);
            frame.Should().Contain("52.123.242.181");
            frame.Should().NotContain("50002");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch (IOException) { }
            }
        }
    }

    [Fact]
    public void ApplyExclusionAtSelection_WhenTargetSelected_PurgesMatchingEntriesAndFiltersSubsequentRenderCycles()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_render_target_{Guid.NewGuid():N}.json");
        try
        {
            var flowFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
            var procFilter = new ProcessExclusionFilter(new ProcessExclusionOptions());
            var configurator = new FlowExclusionConfigurator(new AppConfiguration(), tempFile, flowFilter, procFilter);

            var renderer = new FlowAuditDashboardRenderer(5, "output.csv", flowFilter, procFilter, null);

            var socket = new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50001),
                new SocketEndpoint(IPAddress.Parse("1.1.1.1"), 443),
                "Established",
                new ProcessMetadata(100, "node", "C:\\node.exe", "node server.js", "server.js", "user"));

            var entry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket);
            var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 1, 1, [entry], [socket]);

            renderer.Render(result, SampleHost);

            // Select Target column (Col index 3)
            renderer.MoveSelectionDown();
            renderer.MoveSelectionRight();
            renderer.MoveSelectionRight();
            renderer.MoveSelectionRight();
            renderer.SelectedColumn.Should().Be(InteractiveExclusionColumn.Target);

            // Act
            var applied = renderer.ApplyExclusionAtSelection(configurator);

            // Assert
            applied.Should().BeTrue();
            configurator.CurrentConfiguration.ExcludedExactNames.Should().Contain("server.js");

            // Subsequent render with matching target should be dropped
            var nextSocket = new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50002),
                new SocketEndpoint(IPAddress.Parse("1.1.1.1"), 443),
                "Established",
                new ProcessMetadata(101, "node", "C:\\node.exe", "node server.js", "server.js", "user"));
            var nextEntry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 2, SampleHost, nextSocket);
            var nextResult = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 1, 2, [nextEntry], [nextSocket]);

            renderer.Render(nextResult, SampleHost);
            var frame = renderer.BuildDashboardFrame(nextResult, SampleHost);
            frame.Should().Contain("server.js");
            frame.Should().NotContain("50002");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch (IOException) { }
            }
        }
    }

    [Fact]
    public void BuildDashboardFrame_WhenRowIsSelected_MaintainsIdenticalLeftPaddingAndColumnAlignment()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var socket1 = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.68.81"), 64026),
            new SocketEndpoint(IPAddress.Parse("172.217.118.4"), 443),
            "Established",
            new ProcessMetadata(100, "language_server", "", "", "", "user"));

        var socket2 = new ActiveSocketConnection(
            SocketTransportProtocol.Udp,
            SocketFlowDirection.Listening,
            new SocketEndpoint(IPAddress.Parse("0.0.0.0"), 61664),
            new SocketEndpoint(IPAddress.Parse("0.0.0.0"), 0),
            "Listen",
            new ProcessMetadata(200, "dasHost", "", "", "", "system"));

        var entry1 = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket1);
        var entry2 = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket2);

        var result = new FlowAuditCycleResult(
            DateTimeOffset.UtcNow,
            TotalSocketsScanned: 2,
            ExcludedSocketsCount: 0,
            NewlyDiscoveredCount: 2,
            TotalUniqueFlowsTracked: 2,
            NewlyDiscoveredEntries: [entry1, entry2],
            ActiveSockets: [socket1, socket2]);

        renderer.Render(result, SampleHost);

        // Frame unselected
        var unselectedFrame = renderer.BuildDashboardFrame(result, SampleHost);
        var unselectedLines = unselectedFrame.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        var unselectedRow1 = StripAnsi(unselectedLines.First(l => l.Contains("192.168.68.81:64026", StringComparison.Ordinal)));
        var unselectedRow2 = StripAnsi(unselectedLines.First(l => l.Contains("0.0.0.0:61664", StringComparison.Ordinal)));

        // Act - Select second row
        renderer.MoveSelectionDown();
        renderer.MoveSelectionDown();
        var selectedFrame = renderer.BuildDashboardFrame(result, SampleHost);
        var selectedLines = selectedFrame.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        var selectedRow1 = StripAnsi(selectedLines.First(l => l.Contains("192.168.68.81:64026", StringComparison.Ordinal)));
        var selectedRow2 = StripAnsi(selectedLines.First(l => l.Contains("0.0.0.0:61664", StringComparison.Ordinal)));

        // Assert - Both rows must start with box character followed by exact 1 space
        selectedRow2.Should().StartWith("│ ");
        selectedRow1.Should().StartWith("│ ");
        unselectedRow2.Should().StartWith("│ ");

        // The column positions of LOCAL endpoint must be strictly identical between unselected and selected
        selectedRow2.IndexOf("0.0.0.0:61664", StringComparison.Ordinal)
            .Should().Be(unselectedRow2.IndexOf("0.0.0.0:61664", StringComparison.Ordinal));

        // The column positions of the process name must be strictly identical
        selectedRow2.IndexOf("dasHost", StringComparison.Ordinal)
            .Should().Be(unselectedRow2.IndexOf("dasHost", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyExclusionAtSelection_WhenRemoteIpIsAny_ExcludesRemoteIp0000AndPurgesMatchingEntries()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_render_any_{Guid.NewGuid():N}.json");
        try
        {
            var flowFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
            var procFilter = new ProcessExclusionFilter(new ProcessExclusionOptions());
            var configurator = new FlowExclusionConfigurator(new AppConfiguration(), tempFile, flowFilter, procFilter);

            var renderer = new FlowAuditDashboardRenderer(5, "output.csv", flowFilter, procFilter, null);

            var socket = new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Listening,
                new SocketEndpoint(IPAddress.Parse("0.0.0.0"), 50001),
                new SocketEndpoint(IPAddress.Any, 0),
                "Listen",
                new ProcessMetadata(100, "spoolsv", "", "", "", "user"));

            var entry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket);
            var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 1, 1, [entry], [socket]);

            renderer.Render(result, SampleHost);

            // Select Remote column (Col index 1)
            renderer.MoveSelectionDown();
            renderer.MoveSelectionRight();
            renderer.SelectedColumn.Should().Be(InteractiveExclusionColumn.Remote);

            // Act
            var applied = renderer.ApplyExclusionAtSelection(configurator);

            // Assert
            applied.Should().BeTrue();
            configurator.CurrentConfiguration.ExcludedRemoteIps.Should().Contain("0.0.0.0");

            // Subsequent render with 0.0.0.0 should be dropped
            var nextSocket = new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Listening,
                new SocketEndpoint(IPAddress.Parse("0.0.0.0"), 50002),
                new SocketEndpoint(IPAddress.Any, 0),
                "Listen",
                new ProcessMetadata(100, "services", "", "", "", "user"));
            var nextEntry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 2, SampleHost, nextSocket);
            var nextResult = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 1, 2, [nextEntry], [nextSocket]);

            renderer.Render(nextResult, SampleHost);
            var frame = renderer.BuildDashboardFrame(nextResult, SampleHost);
            frame.Should().NotContain("50002");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch (IOException) { }
            }
        }
    }

    [Fact]
    public void BuildDashboardFrame_WhenDefaultFooterRendered_DisplaysMapRefreshShortcutHint()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 1, 0, 0, 1, [], []);

        // Act
        var frame = renderer.BuildDashboardFrame(result, SampleHost);
        var cleanFooter = StripAnsi(frame);

        // Assert
        cleanFooter.Should().Contain("[M] Refresh Map");
    }

    [Fact]
    public void BuildDashboardFrame_WhenActiveProcessesRendered_DisplaysProgressBarAndYellowCellSelection()
    {
        // Arrange
        var renderer = new FlowAuditDashboardRenderer(5, "output.csv");
        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50000),
            new SocketEndpoint(IPAddress.Parse("10.0.0.1"), 443),
            "Established",
            new ProcessMetadata(100, "thunderbird", "/bin/thunderbird", "", "", "user"));
        var entry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, SampleHost, socket);
        var result = new FlowAuditCycleResult(DateTimeOffset.UtcNow, 10, 0, 1, 1, [entry], [socket]);

        renderer.Render(result, SampleHost);
        renderer.MoveSelectionDown(); // Activate selection on row 0, column Local

        // Act
        var frame = renderer.BuildDashboardFrame(result, SampleHost);

        // Assert
        // 1. Process progress bar characters are rendered in top processes pane
        frame.Should().Contain("█");
        frame.Should().Contain("─");
        // 2. Yellow selection highlight ANSI code is used for the selected cell
        frame.Should().Contain("\x1b[30;43;1m");
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
