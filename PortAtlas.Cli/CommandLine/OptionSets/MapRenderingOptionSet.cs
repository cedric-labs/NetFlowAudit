using System.CommandLine;
using PortAtlas.Cli.Configuration;

namespace PortAtlas.Cli.CommandLine.OptionSets;

/// <summary>
/// Topology map outputs and their initial rendering, shared by <c>audit</c> and <c>map</c>.
/// </summary>
internal sealed class MapRenderingOptionSet
{
    public Option<string?> Html { get; } = OptionFactory.CreatePath("--html", "HTML topology map to write. Tokens {ServerName}, {HostName}, {Date}, {Timestamp} are expanded.");

    public Option<string?> Drawio { get; } = OptionFactory.CreatePath("--drawio", "Draw.io diagram to write. Tokens are expanded as for --html.");

    private readonly Option<string?> _layout = OptionFactory.CreateChoice("--layout", "Initial layout of the HTML map. Default: layered.", "layered", "tree", "horizontal-tree", "organic", "circular", "grid");
    private readonly Option<string?> _sort = OptionFactory.CreateChoice("--sort", "Initial server ordering of the HTML map. Default: barycentric.", "barycentric", "tiers", "subnet", "degree", "alpha", "ip");
    private readonly Option<string?> _routing = OptionFactory.CreateChoice("--routing", "Initial arrow routing of the HTML map. Default: bezier.", "bezier", "orthogonal", "straight", "bus");
    private readonly Option<string?> _edgeLabels = OptionFactory.CreateChoice("--edge-labels", "Initial arrow labels of the HTML map; none merges the flows of a server pair into one arrow. Default: ports.", "ports", "none");
    private readonly Option<string?> _spacing = OptionFactory.CreateChoice("--spacing", "Initial spacing of the HTML map. Default: balanced.", "compact", "balanced", "spacious");
    private readonly Option<string?> _drawioEdges = OptionFactory.CreateChoice("--drawio-edges", "Grouping of parallel arrows in the Draw.io diagram. Default: adaptive.", "adaptive", "direction", "pair", "none");
    private readonly Option<string?> _drawioSubnets = OptionFactory.CreateChoice("--drawio-subnets", "Subnet zones of the Draw.io diagram; off lays the servers out as a plain grid without zones. Default: on.", "on", "off");
    private readonly Option<string?> _drawioTheme = OptionFactory.CreateChoice("--drawio-theme", "Theme of the Draw.io diagram. Default: dark.", "dark", "light");

    public void AddTo(Command command)
    {
        command.Options.Add(Html);
        command.Options.Add(Drawio);
        command.Options.Add(_layout);
        command.Options.Add(_sort);
        command.Options.Add(_routing);
        command.Options.Add(_edgeLabels);
        command.Options.Add(_spacing);
        command.Options.Add(_drawioEdges);
        command.Options.Add(_drawioSubnets);
        command.Options.Add(_drawioTheme);
    }

    public AppConfiguration Apply(ParseResult parseResult, AppConfiguration config) =>
        config with
        {
            OutputHtmlMapPath = parseResult.GetValue(Html) ?? config.OutputHtmlMapPath,
            OutputDrawioPath = parseResult.GetValue(Drawio) ?? config.OutputDrawioPath,
            MapLayoutMode = parseResult.GetValue(_layout) ?? config.MapLayoutMode,
            MapSortMethod = parseResult.GetValue(_sort) ?? config.MapSortMethod,
            MapRoutingMode = parseResult.GetValue(_routing) ?? config.MapRoutingMode,
            MapEdgeLabels = parseResult.GetValue(_edgeLabels) ?? config.MapEdgeLabels,
            MapSpacingProfile = parseResult.GetValue(_spacing) ?? config.MapSpacingProfile,
            DrawioEdgeMode = parseResult.GetValue(_drawioEdges) ?? config.DrawioEdgeMode,
            DrawioSubnets = parseResult.GetValue(_drawioSubnets) ?? config.DrawioSubnets,
            DrawioTheme = parseResult.GetValue(_drawioTheme) ?? config.DrawioTheme
        };
}
