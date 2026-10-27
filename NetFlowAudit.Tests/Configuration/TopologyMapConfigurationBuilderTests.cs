using AwesomeAssertions;
using NetFlowAudit.Cli.Configuration;
using TopologyForge.Models;
using TopologyForge.Rendering;

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
    public void BuildRenderingOptions_WhenSortRoutingAndSpacingConfigured_MapsToTopologyRenderingOptions()
    {
        // Arrange
        var config = new AppConfiguration
        {
            MapSortMethod = "degree",
            MapRoutingMode = "orthogonal",
            MapSpacingProfile = "spacious"
        };

        // Act
        var renderingOptions = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);

        // Assert
        renderingOptions.DefaultSortMethod.Should().Be(TopologyNodeSortMethod.ConnectionDegree);
        renderingOptions.DefaultRoutingMode.Should().Be(TopologyEdgeRoutingMode.OrthogonalCorridors);
        renderingOptions.DefaultSpacingProfile.Should().Be(TopologySpacingProfile.Spacious);
    }

    [Fact]
    public void BuildRenderingOptions_WhenOrganicLayoutAndFunctionalTierConfigured_MapsCorrectly()
    {
        // Arrange
        var config = new AppConfiguration
        {
            MapLayoutMode = "organic",
            MapSortMethod = "tiers"
        };

        // Act
        var renderingOptions = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);

        // Assert
        renderingOptions.DefaultLayoutMode.Should().Be(TopologyLayoutMode.ForceDirectedOrganic);
        renderingOptions.DefaultSortMethod.Should().Be(TopologyNodeSortMethod.FunctionalTier);
    }

    [Fact]
    public void BuildRenderingOptions_WhenSubnetClusterConfigured_MapsToSubnetCluster()
    {
        // Arrange
        var config = new AppConfiguration
        {
            MapSortMethod = "cluster"
        };

        // Act
        var renderingOptions = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);

        // Assert
        renderingOptions.DefaultSortMethod.Should().Be(TopologyNodeSortMethod.SubnetCluster);
    }

    [Fact]
    public void BuildRenderingOptions_WhenTreeLayoutConfigured_MapsToTree()
    {
        // Arrange
        var config = new AppConfiguration
        {
            MapLayoutMode = "tree"
        };

        // Act
        var renderingOptions = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);

        // Assert
        renderingOptions.DefaultLayoutMode.Should().Be(TopologyLayoutMode.Tree);
    }

    [Fact]
    public void BuildRenderingOptions_WhenHorizontalTreeLayoutConfigured_MapsToHorizontalTree()
    {
        // Arrange
        var config = new AppConfiguration
        {
            MapLayoutMode = "horizontal-tree"
        };

        // Act
        var renderingOptions = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);

        // Assert
        renderingOptions.DefaultLayoutMode.Should().Be(TopologyLayoutMode.HorizontalTree);
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
            "--map-layout", "organic",
            "--map-sort", "alpha",
            "--map-routing", "orthogonal",
            "--map-spacing", "compact",
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
        updated.MapLayoutMode.Should().Be("organic");
        updated.MapSortMethod.Should().Be("alpha");
        updated.MapRoutingMode.Should().Be("orthogonal");
        updated.MapSpacingProfile.Should().Be("compact");
        updated.MapListenersOnly.Should().BeTrue();
        updated.MapExcludeEphemeralPorts.Should().BeFalse();
    }

    [Fact]
    public void BuildRenderingOptions_WhenSubnetConfigured_MapsToSubnetCluster()
    {
        // Arrange
        var config = new AppConfiguration
        {
            MapSortMethod = "subnet"
        };

        // Act
        var renderingOptions = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);

        // Assert
        renderingOptions.DefaultSortMethod.Should().Be(TopologyNodeSortMethod.SubnetCluster);
    }

    [Fact]
    public void BuildRenderingOptions_WhenFiltersConfigured_PopulatesInitialFilterProperties()
    {
        // Arrange
        var config = new AppConfiguration
        {
            MapIncludedPorts = ["80", "443"],
            MapExcludedPorts = ["53"],
            MapExcludedHosts = ["*.local", "SRV-MON-*"],
            MapDirection = "out"
        };

        // Act
        var renderingOptions = TopologyMapConfigurationBuilder.BuildRenderingOptions(config);

        // Assert
        renderingOptions.InitialIncludePorts.Should().Be("80, 443");
        renderingOptions.InitialExcludePorts.Should().Be("53");
        renderingOptions.InitialExcludeHosts.Should().Be("*.local, SRV-MON-*");
        renderingOptions.InitialDirection.Should().Be("OUT");
    }

    [Fact]
    public void ApplyCommandLineOverrides_WhenArgumentAliasesUsed_UpdatesAppConfiguration()
    {
        // Arrange
        var baseConfig = new AppConfiguration();
        var args = new[]
        {
            "--layout", "organic",
            "--sort", "degree",
            "--routing", "straight",
            "--spacing", "spacious",
            "--exclude-host", "SRV-MON",
            "--include-host", "SRV-WEB",
            "--exclude-port", "53",
            "--include-port", "443",
            "--exclude-ip", "10.99.0.0/16",
            "--include-ip", "10.0.0.0/8",
            "--exclude-process", "chrome",
            "--include-process", "dotnet",
            "--direction", "out"
        };

        // Act
        var updated = TopologyMapConfigurationBuilder.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.MapLayoutMode.Should().Be("organic");
        updated.MapSortMethod.Should().Be("degree");
        updated.MapRoutingMode.Should().Be("straight");
        updated.MapSpacingProfile.Should().Be("spacious");
        updated.MapExcludedHosts.Should().Equal("SRV-MON");
        updated.MapIncludedHosts.Should().Equal("SRV-WEB");
        updated.MapExcludedPorts.Should().Equal("53");
        updated.MapIncludedPorts.Should().Equal("443");
        updated.MapExcludedIps.Should().Equal("10.99.0.0/16");
        updated.MapIncludedIps.Should().Equal("10.0.0.0/8");
        updated.MapExcludedProcesses.Should().Equal("chrome");
        updated.MapIncludedProcesses.Should().Equal("dotnet");
        updated.MapDirection.Should().Be("out");
    }

    [Fact]
    public void BuildFilterOptions_WhenDirectionIsOut_ExcludesInboundFlows()
    {
        // Arrange
        var config = new AppConfiguration
        {
            MapDirection = "out"
        };

        // Act
        var options = TopologyMapConfigurationBuilder.BuildFilterOptions(config);

        // Assert
        options.IncludeInboundFlows.Should().BeFalse();
        options.IncludeOutboundFlows.Should().BeTrue();
    }
}
