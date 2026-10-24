using System.Net;
using System.Text.Json;
using FluentAssertions;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Core.Filtering;
using ProcessForge.Filtering;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace NetFlowAudit.Tests.Configuration;

public sealed class FlowExclusionConfiguratorTests : IDisposable
{
    private readonly string _tempConfigFile;

    public FlowExclusionConfiguratorTests()
    {
        _tempConfigFile = Path.Combine(Path.GetTempPath(), $"netflowaudit_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempConfigFile))
        {
            try
            {
                File.Delete(_tempConfigFile);
            }
            catch (IOException)
            {
                // Tolerated for test cleanup
            }
        }
    }

    [Fact]
    public void ExcludeLocalPort_WhenInvoked_UpdatesMemoryFilterAndPersistsConfig()
    {
        // Arrange
        var initialConfig = new AppConfiguration();
        var networkFlowFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
        var processFilter = new ProcessExclusionFilter(new ProcessExclusionOptions());
        var configurator = new FlowExclusionConfigurator(initialConfig, _tempConfigFile, networkFlowFilter, processFilter);

        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Listening,
            new SocketEndpoint(IPAddress.Any, 8088),
            new SocketEndpoint(IPAddress.Any, 0),
            "Listen",
            new ProcessMetadata(10, "web", "/usr/bin/web", "", "", "user"));

        // Act
        var resultMessage = configurator.ExcludeLocalPort(8088);

        // Assert
        resultMessage.Should().Contain("8088");
        networkFlowFilter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
        configurator.CurrentConfiguration.ExcludedLocalPorts.Should().Contain(8088);

        File.Exists(_tempConfigFile).Should().BeTrue();
        var persistedJson = File.ReadAllText(_tempConfigFile);
        var loaded = JsonSerializer.Deserialize<AppConfiguration>(persistedJson);
        loaded.Should().NotBeNull();
        loaded!.ExcludedLocalPorts.Should().Contain(8088);
    }

    [Fact]
    public void ExcludeRemoteIp_WhenInvoked_UpdatesMemoryFilterAndPersistsConfig()
    {
        // Arrange
        var initialConfig = new AppConfiguration();
        var networkFlowFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
        var processFilter = new ProcessExclusionFilter(new ProcessExclusionOptions());
        var configurator = new FlowExclusionConfigurator(initialConfig, _tempConfigFile, networkFlowFilter, processFilter);

        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50000),
            new SocketEndpoint(IPAddress.Parse("52.123.242.181"), 443),
            "Established",
            new ProcessMetadata(10, "curl", "/usr/bin/curl", "", "", "user"));

        // Act
        var resultMessage = configurator.ExcludeRemoteIp("52.123.242.181");

        // Assert
        resultMessage.Should().Contain("52.123.242.181");
        networkFlowFilter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
        configurator.CurrentConfiguration.ExcludedRemoteIps.Should().Contain("52.123.242.181");

        File.Exists(_tempConfigFile).Should().BeTrue();
        var persistedJson = File.ReadAllText(_tempConfigFile);
        var loaded = JsonSerializer.Deserialize<AppConfiguration>(persistedJson);
        loaded.Should().NotBeNull();
        loaded!.ExcludedRemoteIps.Should().Contain("52.123.242.181");
    }

    [Fact]
    public void ExcludeRemotePort_WhenInvoked_UpdatesMemoryFilterAndPersistsConfig()
    {
        // Arrange
        var initialConfig = new AppConfiguration();
        var networkFlowFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
        var processFilter = new ProcessExclusionFilter(new ProcessExclusionOptions());
        var configurator = new FlowExclusionConfigurator(initialConfig, _tempConfigFile, networkFlowFilter, processFilter);

        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50000),
            new SocketEndpoint(IPAddress.Parse("8.8.4.4"), 853),
            "Established",
            new ProcessMetadata(10, "curl", "/usr/bin/curl", "", "", "user"));

        // Act
        var resultMessage = configurator.ExcludeRemotePort(853);

        // Assert
        resultMessage.Should().Contain("853");
        networkFlowFilter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
        configurator.CurrentConfiguration.ExcludedRemotePorts.Should().Contain(853);

        File.Exists(_tempConfigFile).Should().BeTrue();
        var persistedJson = File.ReadAllText(_tempConfigFile);
        var loaded = JsonSerializer.Deserialize<AppConfiguration>(persistedJson);
        loaded.Should().NotBeNull();
        loaded!.ExcludedRemotePorts.Should().Contain(853);
    }

    [Fact]
    public void ExcludeProcess_WhenInvoked_UpdatesProcessFilterAndPersistsConfig()
    {
        // Arrange
        var initialConfig = new AppConfiguration();
        var networkFlowFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
        var processFilter = new ProcessExclusionFilter(new ProcessExclusionOptions());
        var configurator = new FlowExclusionConfigurator(initialConfig, _tempConfigFile, networkFlowFilter, processFilter);

        var proc = new ProcessMetadata(250, "telemetry-agent", "/opt/agent", "", "", "user");

        // Act
        var resultMessage = configurator.ExcludeProcess("telemetry-agent");

        // Assert
        resultMessage.Should().Contain("telemetry-agent");
        processFilter.ShouldExclude(proc).Should().BeTrue();
        configurator.CurrentConfiguration.ExcludedExactNames.Should().Contain("telemetry-agent");

        File.Exists(_tempConfigFile).Should().BeTrue();
        var persistedJson = File.ReadAllText(_tempConfigFile);
        var loaded = JsonSerializer.Deserialize<AppConfiguration>(persistedJson);
        loaded.Should().NotBeNull();
        loaded!.ExcludedExactNames.Should().Contain("telemetry-agent");
    }

    [Fact]
    public void ExcludeTarget_WhenTargetContainsFullPath_ExtractsFileNameAndPersistsConfig()
    {
        // Arrange
        var initialConfig = new AppConfiguration();
        var networkFlowFilter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
        var processFilter = new ProcessExclusionFilter(new ProcessExclusionOptions());
        var configurator = new FlowExclusionConfigurator(initialConfig, _tempConfigFile, networkFlowFilter, processFilter);

        var proc = new ProcessMetadata(250, "custom-daemon.exe", "C:\\Services\\custom-daemon.exe", "", "", "user");

        // Act
        var resultMessage = configurator.ExcludeTarget("\"C:\\Services\\custom-daemon.exe\"");

        // Assert
        resultMessage.Should().Contain("custom-daemon.exe");
        processFilter.ShouldExclude(proc).Should().BeTrue();
        configurator.CurrentConfiguration.ExcludedExactNames.Should().Contain("custom-daemon.exe");

        File.Exists(_tempConfigFile).Should().BeTrue();
        var persistedJson = File.ReadAllText(_tempConfigFile);
        var loaded = JsonSerializer.Deserialize<AppConfiguration>(persistedJson);
        loaded.Should().NotBeNull();
        loaded!.ExcludedExactNames.Should().Contain("custom-daemon.exe");
    }
}
