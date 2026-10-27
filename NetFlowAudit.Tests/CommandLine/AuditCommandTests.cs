using AwesomeAssertions;
using NetFlowAudit.Cli.Configuration;
using Xunit;

namespace NetFlowAudit.Tests.CommandLine;

public sealed class AuditCommandTests
{
    [Fact]
    public void BuildConfiguration_WhenObfuscateFlagProvided_SetsDefaultFictitiousServerAndIp()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(new AppConfiguration(), "audit", "--obfuscate");

        // Assert
        updated.MaskServerName.Should().Be("SRV-AUDIT-01");
        updated.MaskLocalIp.Should().Be("10.0.0.10");
    }

    [Fact]
    public void BuildConfiguration_WhenExplicitMaskServerAndIpProvided_SetsConfiguredValues()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(
            new AppConfiguration(), "audit", "--mask-server", "DEMO-SERVER", "--mask-ip", "172.16.0.5");

        // Assert
        updated.MaskServerName.Should().Be("DEMO-SERVER");
        updated.MaskLocalIp.Should().Be("172.16.0.5");
    }

    [Fact]
    public void BuildConfiguration_WhenSingleAliasProvided_PopulatesCustomHostAliases()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(new AppConfiguration(), "audit", "--alias", "203.0.113.20=cloud.example.test");

        // Assert
        updated.CustomHostAliases.Should().ContainKey("203.0.113.20")
            .WhoseValue.Should().Be("cloud.example.test");
    }

    [Fact]
    public void BuildConfiguration_WhenMultipleAliasesProvided_PopulatesAllAliases()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(
            new AppConfiguration(),
            "audit", "--alias", "192.168.1.10=srv-db", "--alias", "10.0.0.1=router.corp,10.0.0.2:gateway.corp");

        // Assert
        updated.CustomHostAliases.Should().HaveCount(3);
        updated.CustomHostAliases["192.168.1.10"].Should().Be("srv-db");
        updated.CustomHostAliases["10.0.0.1"].Should().Be("router.corp");
        updated.CustomHostAliases["10.0.0.2"].Should().Be("gateway.corp");
    }

    [Fact]
    public void BuildConfiguration_WhenOutputProvided_SetsOutputCsvPath()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(
            new AppConfiguration { OutputCsvPath = "default.csv" }, "audit", "--output", "custom_audit_{ServerName}.csv");

        // Assert
        updated.OutputCsvPath.Should().Be("custom_audit_{ServerName}.csv");
    }

    [Fact]
    public void BuildConfiguration_WhenShortOutputFlagProvided_SetsOutputCsvPath()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(
            new AppConfiguration { OutputCsvPath = "default.csv" }, "audit", "-o", "short_output.csv");

        // Assert
        updated.OutputCsvPath.Should().Be("short_output.csv");
    }

    [Fact]
    public void BuildConfiguration_WhenReloadHistoryAllProvided_SetsNullLimit()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(
            new AppConfiguration { ReloadHistoryLimit = 50 }, "audit", "--reload-history", "all");

        // Assert
        updated.ReloadHistoryLimit.Should().BeNull();
    }

    [Fact]
    public void BuildConfiguration_WhenReloadHistoryZeroProvided_SetsZeroLimit()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(
            new AppConfiguration { ReloadHistoryLimit = null }, "audit", "--reload-history", "0");

        // Assert
        updated.ReloadHistoryLimit.Should().Be(0);
    }

    [Fact]
    public void BuildConfiguration_WhenReloadHistoryPositiveIntegerProvided_SetsExplicitLimit()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(
            new AppConfiguration { ReloadHistoryLimit = null }, "audit", "--reload-history", "250");

        // Assert
        updated.ReloadHistoryLimit.Should().Be(250);
    }

    [Fact]
    public void BuildConfiguration_WhenNdjsonArgumentProvided_SetsOutputNdjsonPath()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(
            new AppConfiguration { OutputNdjsonPath = string.Empty }, "audit", "--ndjson", "audit_stream_{ServerName}.ndjson");

        // Assert
        updated.OutputNdjsonPath.Should().Be("audit_stream_{ServerName}.ndjson");
    }

    [Fact]
    public void BuildConfiguration_WhenNoTuiGiven_DisablesDashboard()
    {
        // Act
        var updated = AuditCommandConfiguration.Build(new AppConfiguration(), "audit", "--no-tui");

        // Assert
        updated.EnableDashboard.Should().BeFalse();
    }

    [Fact]
    public void BuildConfiguration_WhenNoOptionGiven_KeepsFileConfigurationValues()
    {
        // Arrange
        var fileConfiguration = new AppConfiguration
        {
            ScanIntervalSeconds = 12,
            OutputCsvPath = "from_file.csv",
            ResolveIpOwners = false,
            MapLayoutMode = "grid",
            MapIncludedPorts = ["443"]
        };

        // Act
        var updated = AuditCommandConfiguration.Build(fileConfiguration, "audit");

        // Assert
        updated.Should().BeEquivalentTo(fileConfiguration);
    }
}
