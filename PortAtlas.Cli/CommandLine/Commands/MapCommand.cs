using System.CommandLine;
using PortAtlas.Cli.CommandLine.OptionSets;
using PortAtlas.Cli.Configuration;
using PortAtlas.Cli.Export;
using PortAtlas.Core.Export;

namespace PortAtlas.Cli.CommandLine.Commands;

/// <summary>
/// <c>portatlas map</c>: HTML topology map and/or Draw.io diagram built from audit logs.
/// </summary>
public sealed class MapCommand : Command
{
    private const string DefaultHtmlPath = "netflow_topology.html";
    private static readonly string[] CsvExtension = [".csv"];

    private readonly AuditInputOptionSet _inputs = new();
    private readonly FlowFilterOptionSet _filters = new();
    private readonly MapRenderingOptionSet _rendering = new();
    private readonly Option<string?> _configFile;

    /// <summary>
    /// Creates the command.
    /// </summary>
    /// <param name="configFile">Global <c>--config</c> option.</param>
    public MapCommand(Option<string?> configFile)
        : base("map", "Render an HTML topology map and/or a Draw.io diagram from CSV audit logs. Without --html or --drawio, writes the outputs of the configuration (HTML map, default netflow_topology.html, and Draw.io diagram when OutputDrawioPath is set).")
    {
        _configFile = configFile;
        _inputs.AddTo(this);
        _filters.AddTo(this);
        _rendering.AddTo(this);

        SetAction(async (parseResult, cancellationToken) =>
        {
            var config = BuildConfiguration(parseResult, AppConfigurationFile.Load(parseResult.GetValue(_configFile)).Configuration);
            var files = _inputs.ResolveFiles(parseResult, config.OutputCsvPath, CsvExtension);
            if (files.Count == 0)
            {
                await Console.Error.WriteLineAsync($"No audit log found for: {_inputs.Describe(parseResult, config.OutputCsvPath)}").ConfigureAwait(false);
                return 1;
            }

            var htmlPath = parseResult.GetValue(_rendering.Html);
            var drawioPath = parseResult.GetValue(_rendering.Drawio);
            if (htmlPath is null && drawioPath is null)
            {
                htmlPath = string.IsNullOrWhiteSpace(config.OutputHtmlMapPath) ? DefaultHtmlPath : config.OutputHtmlMapPath;
                drawioPath = string.IsNullOrWhiteSpace(config.OutputDrawioPath) ? null : config.OutputDrawioPath;
            }

            return await new TopologyMapCommandRunner().RunAsync(
                files,
                htmlPath is null ? null : OutputFilePathResolver.Resolve(htmlPath),
                drawioPath is null ? null : OutputFilePathResolver.Resolve(drawioPath),
                config,
                cancellationToken).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Overrides the configuration file values with the options given on the command line.
    /// </summary>
    /// <param name="parseResult">Parsed command line of this command.</param>
    /// <param name="fileConfiguration">Values read from the configuration file.</param>
    /// <returns>The configuration the map is rendered with.</returns>
    /// <exception cref="ArgumentNullException">When an argument is null.</exception>
    public AppConfiguration BuildConfiguration(ParseResult parseResult, AppConfiguration fileConfiguration)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(fileConfiguration);

        return _rendering.Apply(parseResult, _filters.Apply(parseResult, fileConfiguration));
    }
}
