using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Deduplication;
using NetFlowAudit.Core.Engine;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Filtering;
using NetFlowAudit.Core.Models;
using NetFlowAudit.Core.Providers;
using ProcessForge.Filtering;
using ProcessForge.Models;
using SocketForge.Contracts;
using SocketForge.Models;
using Xunit;

namespace NetFlowAudit.Tests.Engine;

public sealed class FlowAuditEngineTests : IAsyncLifetime
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), "NetFlowAudit_Engine_" + Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_testDir);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteScanCycleAsync_WhenNewFlowAppears_WritesToCsvAndReturnsCount()
    {
        // Arrange
        var testCsv = Path.Combine(_testDir, "output.csv");
        var csvFormatter = new AuditCsvFormatter(new AuditCsvOptions());
        await using var csvWriter = new FileForge.IO.PersistentDelimitedFileWriter(testCsv, csvFormatter.BuildHeaderLine());
        await csvWriter.InitializeAsync();

        var hostProvider = new LocalServerHostProvider();
        var deduplication = new ConnectionDeduplicationRegister();
        var exclusion = new ProcessExclusionFilter(new ProcessExclusionOptions { ExcludedProcessIds = [4] });

        var mockScanner = new FakeSocketScanner([
            new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("127.0.0.1"), 55000),
                new SocketEndpoint(IPAddress.Parse("93.184.216.34"), 80),
                "Established",
                new ProcessMetadata(100, "curl", "C:\\curl.exe", "curl http://example.com", "", "User"))
        ]);

        var engine = new FlowAuditEngine(
            mockScanner,
            exclusion,
            deduplication,
            hostProvider,
            csvWriter,
            csvFormatter,
            TimeSpan.FromSeconds(1));

        // Act
        var count = await engine.ExecuteScanCycleAsync();

        // Assert
        count.Should().Be(1);
        deduplication.UniqueCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteScanCycleAsync_WhenFlowIsExcluded_DoesNotRecord()
    {
        // Arrange
        var testCsv = Path.Combine(_testDir, "excluded.csv");
        var csvFormatter = new AuditCsvFormatter(new AuditCsvOptions());
        await using var csvWriter = new FileForge.IO.PersistentDelimitedFileWriter(testCsv, csvFormatter.BuildHeaderLine());
        await csvWriter.InitializeAsync();

        var hostProvider = new LocalServerHostProvider();
        var deduplication = new ConnectionDeduplicationRegister();
        var exclusion = new ProcessExclusionFilter(new ProcessExclusionOptions { ExcludedProcessIds = [4] });

        var mockScanner = new FakeSocketScanner([
            new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Listening,
                new SocketEndpoint(IPAddress.Any, 445),
                new SocketEndpoint(IPAddress.Any, 0),
                "Listen",
                new ProcessMetadata(4, "System", "", "", "", "SYSTEM"))
        ]);

        var engine = new FlowAuditEngine(
            mockScanner,
            exclusion,
            deduplication,
            hostProvider,
            csvWriter,
            csvFormatter,
            TimeSpan.FromSeconds(1));

        // Act
        var count = await engine.ExecuteScanCycleAsync();

        // Assert
        count.Should().Be(0);
        deduplication.UniqueCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteScanCycleAsync_WhenNetworkFlowFilterExcludesPort_OmitsFromCsvAndCountsExcluded()
    {
        // Arrange
        var testCsv = Path.Combine(_testDir, "network_filtered.csv");
        var csvFormatter = new AuditCsvFormatter(new AuditCsvOptions());
        await using var csvWriter = new FileForge.IO.PersistentDelimitedFileWriter(testCsv, csvFormatter.BuildHeaderLine());
        await csvWriter.InitializeAsync();

        var hostProvider = new LocalServerHostProvider();
        var deduplication = new ConnectionDeduplicationRegister();
        var exclusion = new ProcessExclusionFilter(new ProcessExclusionOptions());
        var networkFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions
        {
            ExcludedRemotePorts = new HashSet<int> { 443 }
        });

        var mockScanner = new FakeSocketScanner([
            new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.50"), 49152),
                new SocketEndpoint(IPAddress.Parse("93.184.216.34"), 443),
                "Established",
                new ProcessMetadata(200, "browser", "C:\\browser.exe", "", "", "User")),
            new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.50"), 49153),
                new SocketEndpoint(IPAddress.Parse("93.184.216.34"), 80),
                "Established",
                new ProcessMetadata(200, "browser", "C:\\browser.exe", "", "", "User"))
        ]);

        var engine = new FlowAuditEngine(
            mockScanner,
            exclusion,
            networkFilter,
            deduplication,
            hostProvider,
            csvWriter,
            csvFormatter,
            TimeSpan.FromSeconds(1));

        FlowAuditCycleResult? recordedResult = null;
        engine.ScanCycleCompleted += (_, e) => recordedResult = e.Result;

        // Act
        var count = await engine.ExecuteScanCycleAsync();

        // Assert
        count.Should().Be(1);
        deduplication.UniqueCount.Should().Be(1);
        recordedResult.Should().NotBeNull();
        recordedResult!.TotalSocketsScanned.Should().Be(2);
        recordedResult.ExcludedSocketsCount.Should().Be(1);
        recordedResult.NewlyDiscoveredCount.Should().Be(1);
        recordedResult.TotalUniqueFlowsTracked.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteScanCycleAsync_WhenLoopbackExcludedByDefault_DoesNotRecordLoopbackFlow()
    {
        // Arrange
        var testCsv = Path.Combine(_testDir, "loopback_filtered.csv");
        var csvFormatter = new AuditCsvFormatter(new AuditCsvOptions());
        await using var csvWriter = new FileForge.IO.PersistentDelimitedFileWriter(testCsv, csvFormatter.BuildHeaderLine());
        await csvWriter.InitializeAsync();

        var hostProvider = new LocalServerHostProvider();
        var deduplication = new ConnectionDeduplicationRegister();
        var exclusion = new ProcessExclusionFilter(new ProcessExclusionOptions());
        var networkFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions
        {
            ExcludeLoopbackFlows = true
        });

        var mockScanner = new FakeSocketScanner([
            new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Loopback, 5000),
                new SocketEndpoint(IPAddress.Loopback, 8080),
                "Established",
                new ProcessMetadata(300, "dotnet", "dotnet.exe", "", "", "User"))
        ]);

        var engine = new FlowAuditEngine(
            mockScanner,
            exclusion,
            networkFilter,
            deduplication,
            hostProvider,
            csvWriter,
            csvFormatter,
            TimeSpan.FromSeconds(1));

        // Act
        var count = await engine.ExecuteScanCycleAsync();

        // Assert
        count.Should().Be(0);
        deduplication.UniqueCount.Should().Be(0);
    }

    private sealed class FakeSocketScanner(IReadOnlyList<ActiveSocketConnection> connections) : ISocketScanner
    {
        public Task<IReadOnlyList<ActiveSocketConnection>> ScanSocketsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(connections);
        }
    }
}
