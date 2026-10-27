using AwesomeAssertions;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Cli.Diagnostics;
using Xunit;

namespace NetFlowAudit.Tests.Diagnostics;

public sealed class IpResolutionDiagnosticReporterTests
{
    [Fact]
    public Task RunDiagnosticAsync_WhenTargetIpStringIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var reporter = new IpResolutionDiagnosticReporter(TextWriter.Null);
        var config = new AppConfiguration();

        // Act
        var act = () => reporter.RunDiagnosticAsync(null!, config);

        // Assert
        return act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public Task RunDiagnosticAsync_WhenConfigIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var reporter = new IpResolutionDiagnosticReporter(TextWriter.Null);

        // Act
        var act = () => reporter.RunDiagnosticAsync("127.0.0.1", null!);

        // Assert
        return act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RunDiagnosticAsync_WhenInvalidIpString_OutputsErrorMessageWithoutThrowing()
    {
        // Arrange
        using var output = new StringWriter();
        var reporter = new IpResolutionDiagnosticReporter(output);

        // Act
        await reporter.RunDiagnosticAsync("not-an-ip", new AppConfiguration());

        // Assert
        output.ToString().Should().Contain("Invalid IP address format");
    }

    [Fact]
    public async Task RunDiagnosticAsync_WhenLoopbackIp_ExecutesAllStagesAndIdentifiesLocalSource()
    {
        // Arrange
        using var output = new StringWriter();
        var reporter = new IpResolutionDiagnosticReporter(output);

        // Act
        await reporter.RunDiagnosticAsync("127.0.0.1", new AppConfiguration());

        // Assert
        var report = output.ToString();
        report.Should().Contain("[1/6] Syntax Analysis & Classification");
        report.Should().Contain("Loopback / Local       : YES");
        report.Should().Contain("Synthesized Source     : Local");
    }

    [Fact]
    public async Task RunDiagnosticAsync_WhenUnspecifiedIp_SkipsDnsCallWithDefensiveExclusion()
    {
        // Arrange
        using var output = new StringWriter();
        var reporter = new IpResolutionDiagnosticReporter(output);

        // Act
        await reporter.RunDiagnosticAsync("0.0.0.0", new AppConfiguration());

        // Assert
        var report = output.ToString();
        report.Should().Contain("Unspecified / Any      : YES");
        report.Should().Contain("SKIPPED (Defensive guard)");
        report.Should().Contain("Synthesized Source     : Excluded");
    }
}
