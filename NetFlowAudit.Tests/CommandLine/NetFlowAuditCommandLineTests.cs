using AwesomeAssertions;
using NetFlowAudit.Cli;
using NetFlowAudit.Cli.CommandLine;
using Xunit;

namespace NetFlowAudit.Tests.CommandLine;

[Collection(ConsoleOutputCollection.Name)]
public sealed class NetFlowAuditCommandLineTests
{
    [Fact]
    public async Task Main_WhenNoSubcommandGiven_PrintsHelpAndSucceeds()
    {
        // Act
        var exitCode = await Program.Main([]);

        // Assert
        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task Main_WhenArgumentsAreInvalid_ReturnsNonZeroExitCode()
    {
        // Act
        var exitCode = await Program.Main(["map", "--layout", "unknown-layout"]);

        // Assert
        exitCode.Should().NotBe(0);
    }

    [Theory]
    [InlineData("audit", "map", "export", "resolve", "lookup", "config")]
    public void CreateRootCommand_WhenBuilt_ExposesOneSubcommandPerMode(params string[] expectedNames)
    {
        // Act
        var root = NetFlowAuditCommandLine.CreateRootCommand();

        // Assert
        root.Subcommands.Select(c => c.Name).Should().BeEquivalentTo(expectedNames);
    }

    [Theory]
    [InlineData("export", "ndjson", "--layout", "grid")]
    [InlineData("export", "rules", "--html", "map.html")]
    [InlineData("resolve", "--include-ports", "443")]
    [InlineData("lookup", "192.0.2.1", "--cache-only")]
    [InlineData("audit", "--cache-only")]
    public void Parse_WhenOptionIsNotReadByTheCommand_ReportsError(params string[] args)
    {
        // Act
        var parseResult = NetFlowAuditCommandLine.CreateRootCommand().Parse(args);

        // Assert
        parseResult.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("--map-only")]
    [InlineData("--export-rules", "rules.csv")]
    [InlineData("--resolve-offline")]
    [InlineData("--test-ip", "192.0.2.1")]
    [InlineData("--init-config")]
    [InlineData("audit", "--daemon")]
    [InlineData("audit", "--isolated")]
    [InlineData("map", "--exclude-host", "SRV-MON")]
    [InlineData("map", "--map-layout", "grid")]
    [InlineData("map", "--input-dir", "audits")]
    [InlineData("export", "rules", "--internal-only")]
    [InlineData("export", "rules", "--min-obs", "3")]
    public void Parse_WhenRemovedFlagOrAliasGiven_ReportsError(params string[] args)
    {
        // Act
        var parseResult = NetFlowAuditCommandLine.CreateRootCommand().Parse(args);

        // Assert
        parseResult.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("map", "--layout", "arbre")]
    [InlineData("map", "--sort", "name")]
    [InlineData("map", "--routing", "direct")]
    [InlineData("map", "--spacing", "wide")]
    [InlineData("map", "--direction", "listening")]
    [InlineData("map", "--drawio-edges", "port")]
    [InlineData("map", "--drawio-theme", "blue")]
    [InlineData("export", "rules", "--zone", "wan")]
    public void Parse_WhenValueIsOutsideAcceptedValues_ReportsError(params string[] args)
    {
        // Act
        var parseResult = NetFlowAuditCommandLine.CreateRootCommand().Parse(args);

        // Assert
        parseResult.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("audit", "--interval", "0")]
    [InlineData("audit", "--reload-history", "-1")]
    [InlineData("audit", "--reload-history", "some")]
    [InlineData("export", "rules", "--min-observations", "0")]
    [InlineData("export", "rules", "--active-within-days", "0")]
    [InlineData("resolve", "--rdap-delay-ms", "-5")]
    [InlineData("lookup", "not-an-ip-address")]
    public void Parse_WhenNumericOrAddressValueIsInvalid_ReportsError(params string[] args)
    {
        // Act
        var parseResult = NetFlowAuditCommandLine.CreateRootCommand().Parse(args);

        // Assert
        parseResult.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("audit", "--no-tui", "--no-dns", "--no-rdap", "--html", "live.html", "--drawio", "live.drawio", "--layout", "grid")]
    [InlineData("map", "-i", "a.csv", "-i", "b.csv", "--html", "m.html", "--direction", "out", "--include-ephemeral-ports")]
    [InlineData("export", "rules", "-i", "audits", "-o", "rules.csv", "--zone", "internal", "--listeners-only")]
    [InlineData("export", "ndjson", "-i", "audit.csv", "-o", "audit.ndjson")]
    [InlineData("resolve", "-i", "audit.csv", "--cache", "a.json", "--cache", "b.json", "--in-place", "--force-resolve", "--rdap-delay-ms", "0")]
    [InlineData("lookup", "2001:db8::1")]
    [InlineData("config", "init", "netflowaudit.json")]
    [InlineData("audit", "--config", "custom.json")]
    public void Parse_WhenCommandLineIsValid_ReportsNoError(params string[] args)
    {
        // Act
        var parseResult = NetFlowAuditCommandLine.CreateRootCommand().Parse(args);

        // Assert
        parseResult.Errors.Should().BeEmpty();
    }
}
