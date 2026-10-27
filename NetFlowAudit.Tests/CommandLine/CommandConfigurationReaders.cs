using AwesomeAssertions;
using NetFlowAudit.Cli.CommandLine;
using NetFlowAudit.Cli.CommandLine.Commands;
using NetFlowAudit.Cli.Configuration;

namespace NetFlowAudit.Tests.CommandLine;

/// <summary>
/// Parses a command line with the real root command and returns the configuration the audit command would run with.
/// </summary>
internal static class AuditCommandConfiguration
{
    public static AppConfiguration Build(AppConfiguration fileConfiguration, params string[] args)
    {
        var parseResult = NetFlowAuditCommandLine.CreateRootCommand().Parse(args);
        parseResult.Errors.Should().BeEmpty();
        var command = parseResult.CommandResult.Command.Should().BeOfType<AuditCommand>().Subject;
        return command.BuildConfiguration(parseResult, fileConfiguration);
    }
}

/// <summary>
/// Parses a command line with the real root command and returns the configuration the map command would run with.
/// </summary>
internal static class MapCommandConfiguration
{
    public static AppConfiguration Build(AppConfiguration fileConfiguration, params string[] args)
    {
        var parseResult = NetFlowAuditCommandLine.CreateRootCommand().Parse(args);
        parseResult.Errors.Should().BeEmpty();
        var command = parseResult.CommandResult.Command.Should().BeOfType<MapCommand>().Subject;
        return command.BuildConfiguration(parseResult, fileConfiguration);
    }
}
