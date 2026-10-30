using AwesomeAssertions;
using PortAtlas.Cli.Configuration;
using Xunit;

namespace PortAtlas.Tests.CommandLine;

public sealed class MapCommandTests
{
    [Fact]
    public void BuildConfiguration_WhenMapArgumentsProvided_UpdatesAppConfiguration()
    {
        // Act
        var updated = MapCommandConfiguration.Build(
            new AppConfiguration(),
            "map",
            "--html", "custom_map.html",
            "--include-ports", "443,1433",
            "--exclude-ports", "53,123",
            "--include-hosts", "SRV-*",
            "--exclude-hosts", "*TEST*",
            "--include-ips", "10.0.0.0/8",
            "--exclude-ips", "10.99.0.0/16",
            "--include-processes", "sqlservr",
            "--exclude-processes", "svchost",
            "--layout", "organic",
            "--sort", "alpha",
            "--routing", "orthogonal",
            "--spacing", "compact",
            "--listeners-only",
            "--include-ephemeral-ports");

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
    public void BuildConfiguration_WhenListOptionIsRepeated_ConcatenatesValues()
    {
        // Act
        var updated = MapCommandConfiguration.Build(
            new AppConfiguration(), "map", "--exclude-ports", "53", "--exclude-ports", "123,135-139", "--direction", "out");

        // Assert
        updated.MapExcludedPorts.Should().Equal("53", "123", "135-139");
        updated.MapDirection.Should().Be("out");
    }

    [Fact]
    public void BuildConfiguration_WhenChoiceValueDiffersOnlyByCase_StoresLowercaseValue()
    {
        // Act
        var updated = MapCommandConfiguration.Build(new AppConfiguration(), "map", "--direction", "OUT", "--layout", "Organic");

        // Assert
        updated.MapDirection.Should().Be("out");
        updated.MapLayoutMode.Should().Be("organic");
    }

    [Fact]
    public void BuildConfiguration_WhenDrawioOptionsProvided_SetsDiagramPathAndStyle()
    {
        // Act
        var updated = MapCommandConfiguration.Build(
            new AppConfiguration(), "map", "--drawio", "fleet.drawio", "--drawio-edges", "direction", "--drawio-theme", "light");

        // Assert
        updated.OutputDrawioPath.Should().Be("fleet.drawio");
        updated.DrawioEdgeMode.Should().Be("direction");
        updated.DrawioTheme.Should().Be("light");
    }

    [Fact]
    public void BuildConfiguration_WhenMapLighteningValuesProvided_StoresBusRoutingHiddenLabelsAndPairEdges()
    {
        // Act
        var updated = MapCommandConfiguration.Build(
            new AppConfiguration(), "map", "--routing", "bus", "--edge-labels", "none", "--drawio-edges", "pair");

        // Assert
        updated.MapRoutingMode.Should().Be("bus");
        updated.MapEdgeLabels.Should().Be("none");
        updated.DrawioEdgeMode.Should().Be("pair");
    }
    [Fact]
    public void BuildConfiguration_WhenDrawioSubnetsProvided_StoresTheSetting()
    {
        // Act
        var updated = MapCommandConfiguration.Build(new AppConfiguration(), "map", "--drawio-subnets", "off");

        // Assert
        updated.DrawioSubnets.Should().Be("off");
    }

    [Fact]
    public void BuildConfiguration_WhenDrawioSubnetsOmitted_KeepsSubnetZonesOn()
    {
        // Act
        var updated = MapCommandConfiguration.Build(new AppConfiguration(), "map");

        // Assert
        updated.DrawioSubnets.Should().Be("on");
    }
}
