using AwesomeAssertions;
using PortAtlas.Cli.Configuration;
using TopologyForge.Export.Drawio.Adapters;
using TopologyForge.Models;
using TopologyForge.Rendering;

namespace PortAtlas.Tests.Configuration;

public sealed class TopologyMapConfigurationBuilderTests
{
    [Fact]
    public void BuildRenderingOptions_WhenBuilt_ProposesTheMapCommandSyntaxForCopy()
    {
        // Act
        var format = TopologyMapConfigurationBuilder.BuildRenderingOptions(new AppConfiguration()).CliCommandFormat;

        // Assert
        format.Prefix.Should().Be("PortAtlas map");
        format.LayoutOption.Should().Be("--layout");
        format.SortOption.Should().Be("--sort");
        format.RoutingOption.Should().Be("--routing");
        format.SpacingOption.Should().Be("--spacing");
        format.EdgeLabelsOption.Should().Be("--edge-labels");
    }

    [Fact]
    public void BuildRenderingOptions_WhenRoutingIsBus_MapsToBusRouting()
    {
        // Act
        var renderingOptions = TopologyMapConfigurationBuilder.BuildRenderingOptions(new AppConfiguration { MapRoutingMode = "bus" });

        // Assert
        renderingOptions.DefaultRoutingMode.Should().Be(TopologyEdgeRoutingMode.Bus);
    }

    [Theory]
    [InlineData("none", TopologyEdgeLabelMode.None)]
    [InlineData("ports", TopologyEdgeLabelMode.Ports)]
    [InlineData("unknown", TopologyEdgeLabelMode.Ports)]
    public void BuildRenderingOptions_WhenEdgeLabelsConfigured_MapsToEdgeLabelMode(string edgeLabels, TopologyEdgeLabelMode expected)
    {
        // Act
        var renderingOptions = TopologyMapConfigurationBuilder.BuildRenderingOptions(new AppConfiguration { MapEdgeLabels = edgeLabels });

        // Assert
        renderingOptions.DefaultEdgeLabelMode.Should().Be(expected);
    }

    [Theory]
    [InlineData("pair", EdgeAggregationMode.ByServerPair)]
    [InlineData("direction", EdgeAggregationMode.ByDirection)]
    [InlineData("none", EdgeAggregationMode.None)]
    [InlineData("adaptive", EdgeAggregationMode.AdaptiveThreshold)]
    public void BuildDrawioEdgeMode_WhenModeConfigured_MapsToAggregationMode(string drawioEdges, EdgeAggregationMode expected)
    {
        // Act
        var mode = TopologyMapConfigurationBuilder.BuildDrawioEdgeMode(new AppConfiguration { DrawioEdgeMode = drawioEdges });

        // Assert
        mode.Should().Be(expected);
    }

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
    [Fact]
    public void BuildRenderingOptions_WhenBuilt_NamesTheDefaultSortAsTheCliAcceptsIt()
    {
        // Act
        var format = TopologyMapConfigurationBuilder.BuildRenderingOptions(new AppConfiguration()).CliCommandFormat;

        // Assert
        format.SugiyamaSortValue.Should().Be("barycentric");
    }

    [Theory]
    [InlineData("on", true)]
    [InlineData("ON", true)]
    [InlineData("off", false)]
    [InlineData("Off", false)]
    [InlineData("", true)]
    public void BuildDrawioLayoutOptions_WhenSubnetsConfigured_MapsToSubnetGrouping(string drawioSubnets, bool expected)
    {
        // Act
        var options = TopologyMapConfigurationBuilder.BuildDrawioLayoutOptions(new AppConfiguration { DrawioSubnets = drawioSubnets });

        // Assert
        options.GroupBySubnets.Should().Be(expected);
    }
}
