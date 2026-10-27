using FluentAssertions;
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
        var reporter = new IpResolutionDiagnosticReporter();
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
        var reporter = new IpResolutionDiagnosticReporter();

        // Act
        var act = () => reporter.RunDiagnosticAsync("127.0.0.1", null!);

        // Assert
        return act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RunDiagnosticAsync_WhenInvalidIpString_OutputsErrorMessageWithoutThrowing()
    {
        // Arrange
        var reporter = new IpResolutionDiagnosticReporter();
        var config = new AppConfiguration();

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            // Act
            await reporter.RunDiagnosticAsync("not-an-ip", config);

            // Assert
            var output = sw.ToString();
            output.Should().Contain("Invalid IP address format");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunDiagnosticAsync_WhenLoopbackIp_ExecutesAllStagesAndIdentifiesLocalSource()
    {
        // Arrange
        var reporter = new IpResolutionDiagnosticReporter();
        var config = new AppConfiguration();

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            // Act
            await reporter.RunDiagnosticAsync("127.0.0.1", config);

            // Assert
            var output = sw.ToString();
            output.Should().Contain("[1/6] Syntax Analysis & Classification");
            output.Should().Contain("Loopback / Local       : YES");
            output.Should().Contain("Synthesized Source     : Local");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunDiagnosticAsync_WhenUnspecifiedIp_SkipsDnsCallWithDefensiveExclusion()
    {
        // Arrange
        var reporter = new IpResolutionDiagnosticReporter();
        var config = new AppConfiguration();

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            // Act
            await reporter.RunDiagnosticAsync("0.0.0.0", config);

            // Assert
            var output = sw.ToString();
            output.Should().Contain("Unspecified / Any      : YES");
            output.Should().Contain("SKIPPED (Defensive guard)");
            output.Should().Contain("Synthesized Source     : Excluded");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
