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
}
