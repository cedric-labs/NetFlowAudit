using System.CommandLine;
using System.CommandLine.Help;
using PortAtlas.Cli.CommandLine.Commands;
using PortAtlas.Cli.Configuration;

namespace PortAtlas.Cli.CommandLine;

/// <summary>
/// Command tree of PortAtlas: the single definition of commands and options used for parsing and for the help text (ADR-0001).
/// </summary>
public static class PortAtlasCommandLine
{
    /// <summary>
    /// Builds the root command with every subcommand.
    /// </summary>
    /// <returns>The root command; invoked without a subcommand, it prints the help.</returns>
    public static RootCommand CreateRootCommand()
    {
        var configFile = new Option<string?>("--config")
        {
            Description = $"Configuration file. Default: {AppConfigurationFile.DefaultPath} in the working directory, then next to the executable.",
            HelpName = "path",
            Recursive = true
        };

        var export = new Command("export", "Export audit logs to other formats.");
        export.Subcommands.Add(new ExportRulesCommand(configFile));
        export.Subcommands.Add(new ExportNdjsonCommand(configFile));

        var root = new RootCommand("PortAtlas - network flow audit agent for Windows and Linux, without packet capture driver.");
        root.Options.Add(configFile);
        root.Subcommands.Add(new AuditCommand(configFile));
        root.Subcommands.Add(new MapCommand(configFile));
        root.Subcommands.Add(export);
        root.Subcommands.Add(new ResolveCommand(configFile));
        root.Subcommands.Add(new LookupCommand(configFile));
        root.Subcommands.Add(new ConfigCommand());
        root.SetAction(parseResult => new HelpAction().Invoke(parseResult));
        return root;
    }
}
