using System.CommandLine;
using NetFlowAudit.Cli.Configuration;

namespace NetFlowAudit.Cli.CommandLine.Commands;

/// <summary>
/// <c>netflowaudit config</c>: configuration file commands.
/// </summary>
public sealed class ConfigCommand : Command
{
    /// <summary>
    /// Creates the command and its <c>init</c> subcommand.
    /// </summary>
    public ConfigCommand()
        : base("config", "Manage the netflowaudit.json configuration file.")
    {
        var path = new Argument<string?>("path")
        {
            Description = "File to create. Default: netflowaudit.json.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var init = new Command("init", "Write a configuration file with every key and its default value.");
        init.Arguments.Add(path);
        init.SetAction(parseResult =>
        {
            var generatedPath = ConfigurationTemplateGenerator.GenerateDefaultConfigFile(parseResult.GetValue(path));
            Console.WriteLine($"Configuration template generated at: {generatedPath}");
            return 0;
        });

        Subcommands.Add(init);
    }
}
