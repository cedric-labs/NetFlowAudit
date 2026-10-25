using FluentAssertions;
using NetFlowAudit.Cli;
using NetFlowAudit.Cli.Configuration;
using Xunit;

namespace NetFlowAudit.Tests.Configuration;

public sealed class ProgramCommandLineOverridesTests
{
    [Fact]
    public void ApplyCommandLineOverrides_WhenObfuscateFlagProvided_SetsDefaultFictitiousServerAndIp()
    {
        // Arrange
        var baseConfig = new AppConfiguration();
        var args = new[] { "--obfuscate" };

        // Act
        var updated = Program.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.MaskServerName.Should().Be("SRV-AUDIT-01");
        updated.MaskLocalIp.Should().Be("10.0.0.10");
    }

    [Fact]
    public void ApplyCommandLineOverrides_WhenExplicitMaskServerAndIpProvided_SetsConfiguredValues()
    {
        // Arrange
        var baseConfig = new AppConfiguration();
        var args = new[] { "--mask-server", "DEMO-SERVER", "--mask-ip", "172.16.0.5" };

        // Act
        var updated = Program.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.MaskServerName.Should().Be("DEMO-SERVER");
        updated.MaskLocalIp.Should().Be("172.16.0.5");
    }

    [Fact]
    public void ApplyCommandLineOverrides_WhenSingleAliasProvided_PopulatesCustomHostAliases()
    {
        // Arrange
        var baseConfig = new AppConfiguration();
        var args = new[] { "--alias", "203.0.113.30=nextcloud.home.arpa" };

        // Act
        var updated = Program.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.CustomHostAliases.Should().ContainKey("203.0.113.30")
            .WhoseValue.Should().Be("nextcloud.home.arpa");
    }

    [Fact]
    public void ApplyCommandLineOverrides_WhenMultipleAliasesProvided_PopulatesAllAliases()
    {
        // Arrange
        var baseConfig = new AppConfiguration();
        var args = new[]
        {
            "--alias", "192.168.1.10=srv-db",
            "--aliases", "10.0.0.1=router.corp,10.0.0.2:gateway.corp"
        };

        // Act
        var updated = Program.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.CustomHostAliases.Should().HaveCount(3);
        updated.CustomHostAliases["192.168.1.10"].Should().Be("srv-db");
        updated.CustomHostAliases["10.0.0.1"].Should().Be("router.corp");
        updated.CustomHostAliases["10.0.0.2"].Should().Be("gateway.corp");
    }

    [Fact]
    public void ApplyCommandLineOverrides_WhenOutputProvided_SetsOutputCsvPath()
    {
        // Arrange
        var baseConfig = new AppConfiguration { OutputCsvPath = "default.csv" };
        var args = new[] { "--output", "custom_audit_{ServerName}.csv" };

        // Act
        var updated = Program.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.OutputCsvPath.Should().Be("custom_audit_{ServerName}.csv");
    }

    [Fact]
    public void ApplyCommandLineOverrides_WhenShortOutputFlagProvided_SetsOutputCsvPath()
    {
        // Arrange
        var baseConfig = new AppConfiguration { OutputCsvPath = "default.csv" };
        var args = new[] { "-o", "short_output.csv" };

        // Act
        var updated = Program.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.OutputCsvPath.Should().Be("short_output.csv");
    }

    [Fact]
    public void ApplyCommandLineOverrides_WhenReloadHistoryAllProvided_SetsNullLimit()
    {
        // Arrange
        var baseConfig = new AppConfiguration { ReloadHistoryLimit = 50 };
        var args = new[] { "--reload-history", "all" };

        // Act
        var updated = Program.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.ReloadHistoryLimit.Should().BeNull();
    }

    [Fact]
    public void ApplyCommandLineOverrides_WhenReloadHistoryZeroProvided_SetsZeroLimit()
    {
        // Arrange
        var baseConfig = new AppConfiguration { ReloadHistoryLimit = null };
        var args = new[] { "--reload-history", "0" };

        // Act
        var updated = Program.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.ReloadHistoryLimit.Should().Be(0);
    }

    [Theory]
    [InlineData("--ndjson")]
    [InlineData("--output-ndjson")]
    public void ApplyCommandLineOverrides_WhenNdjsonArgumentProvided_SetsOutputNdjsonPath(string flag)
    {
        // Arrange
        var baseConfig = new AppConfiguration { OutputNdjsonPath = string.Empty };
        var args = new[] { flag, "audit_stream_{ServerName}.ndjson" };

        // Act
        var updated = Program.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.OutputNdjsonPath.Should().Be("audit_stream_{ServerName}.ndjson");
    }

    [Fact]
    public void ApplyCommandLineOverrides_WhenReloadHistoryPositiveIntegerProvided_SetsExplicitLimit()
    {
        // Arrange
        var baseConfig = new AppConfiguration { ReloadHistoryLimit = null };
        var args = new[] { "--history-limit", "250" };

        // Act
        var updated = Program.ApplyCommandLineOverrides(baseConfig, args);

        // Assert
        updated.ReloadHistoryLimit.Should().Be(250);
    }
}
