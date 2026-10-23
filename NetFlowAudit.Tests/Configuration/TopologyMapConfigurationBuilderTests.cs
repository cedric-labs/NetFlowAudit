using FluentAssertions;
using NetFlowAudit.Cli.Configuration;
using TopologyForge.Models;

namespace NetFlowAudit.Tests.Configuration;

public sealed class TopologyMapConfigurationBuilderTests
{
    [Fact]
    public void BuildFilterOptions_WhenDefaultAppConfiguration_EnablesEphemeralPortExclusionAndAllDirections()
    {
        // Arrange
        var config = new AppConfiguration();

        // Act
        var options = TopologyMapConfigurationBuilder.BuildFilterOptions(config);

        // Assert
        options.ExcludeEphemeralPorts.Should().BeTrue();
        options.IncludeListeners.Should().BeTrue();
        options.IncludeInboundFlows.Should().BeTrue();
        options.IncludeOutboundFlows.Should().BeTrue();
    }

    [Fact]
    public void BuildFilterOptions_WhenInclusionsAndExclusionsConfigured_PopulatesAllFilterLists()
    {
        // Arrange
        var config = new AppConfiguration
        {
            MapExcludeEphemeralPorts = false,
            MapListenersOnly = true,
            MapIncludedPorts = ["80", "443", "8000-8080"],
            MapExcludedPorts = ["53", "135-139"],
            MapIncludedHosts = ["SRV-WEB-*"],
            MapExcludedHosts = ["*.local"],
            MapIncludedIps = ["10.0.0.0/8"],
            MapExcludedIps = ["169.254.0.0/16"],
            MapIncludedProcesses = ["nginx", "w3wp"],
            MapExcludedProcesses = ["chrome"]
        };

        // Act
        var options = TopologyMapConfigurationBuilder.BuildFilterOptions(config);

        // Assert
        options.ExcludeEphemeralPorts.Should().BeFalse();
        options.IncludeListeners.Should().BeTrue();
        options.IncludeInboundFlows.Should().BeFalse();
        options.IncludeOutboundFlows.Should().BeFalse();
        options.IncludedPorts.Should().BeEquivalentTo([
            NetworkPortRange.Single(80),
            NetworkPortRange.Single(443),
            new NetworkPortRange(8000, 8080)
        ]);
        options.ExcludedPorts.Should().BeEquivalentTo([
            NetworkPortRange.Single(53),
            new NetworkPortRange(135, 139)
        ]);
        options.IncludedHostPatterns.Should().ContainSingle().Which.Should().Be("SRV-WEB-*");
        options.ExcludedHostPatterns.Should().ContainSingle().Which.Should().Be("*.local");
        options.IncludedIpRanges.Should().ContainSingle().Which.Should().Be("10.0.0.0/8");
        options.ExcludedIpRanges.Should().Contain("169.254.0.0/16");
        options.IncludedProcesses.Should().Equal("nginx", "w3wp");
        options.ExcludedProcesses.Should().ContainSingle().Which.Should().Be("chrome");
    }

    [Fact]
    public void ApplyCommandLineOverrides_WhenMapArgumentsProvided_UpdatesAppConfiguration()
    {
        // Arrange
        var baseConfig = new AppConfiguration();
        var args = new[]
        {
            "--map", "custom_map.html",
            "--include-ports", "443,1433",
            "--exclude-ports", "53,123",
            "--include-hosts", "SRV-*",
            "--exclude-hosts", "*TEST*",
            "--include-ips", "10.0.0.0/8",
            "--exclude-ips", "10.99.0.0/16",
            "--include-processes", "sqlservr",
            "--exclude-processes", "svchost",
            "--listeners-only",
            "--include-ephemeral-ports"
        };

        // Act
        var updated = TopologyMapConfigurationBuilder.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.OutputHtmlMapPath.Should().Be("custom_map.html");
        updated.MapIncludedPorts.Should().Equal("443", "1433");
        updated.MapExcludedPorts.Should().Equal("53", "123");
        updated.MapIncludedHosts.Should().Equal("SRV-*");
        updated.MapExcludedHosts.Should().Equal("*TEST*");
        updated.MapIncludedIps.Should().Equal("10.0.0.0/8");
        updated.MapExcludedIps.Should().Equal("10.99.0.0/16");
        updated.MapIncludedProcesses.Should().Equal("sqlservr");
        updated.MapExcludedProcesses.Should().Equal("svchost");
        updated.MapListenersOnly.Should().BeTrue();
        updated.MapExcludeEphemeralPorts.Should().BeFalse();
    }
}
