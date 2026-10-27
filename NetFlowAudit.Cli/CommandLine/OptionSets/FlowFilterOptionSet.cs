using System.CommandLine;
using NetFlowAudit.Cli.Configuration;

namespace NetFlowAudit.Cli.CommandLine.OptionSets;

/// <summary>
/// Flow filters shared by the topology map (<c>audit</c>, <c>map</c>) and the rule matrix (<c>export rules</c>).
/// Each option overrides the matching <c>Map*</c> key of the configuration file.
/// </summary>
internal sealed class FlowFilterOptionSet
{
    private readonly Option<string[]> _includePorts = OptionFactory.CreateList("--include-ports", "ports", "Keep only these service ports or ranges (80,443,8000-8080).");
    private readonly Option<string[]> _excludePorts = OptionFactory.CreateList("--exclude-ports", "ports", "Remove these service ports or ranges (53,123,135-139).");
    private readonly Option<string[]> _includeHosts = OptionFactory.CreateList("--include-hosts", "patterns", "Keep only servers whose name matches one of these wildcard patterns (SRV-*,*SQL*).");
    private readonly Option<string[]> _excludeHosts = OptionFactory.CreateList("--exclude-hosts", "patterns", "Remove servers whose name matches one of these wildcard patterns.");
    private readonly Option<string[]> _includeIps = OptionFactory.CreateList("--include-ips", "addresses", "Keep only these IP addresses or CIDR subnets (10.0.0.0/8).");
    private readonly Option<string[]> _excludeIps = OptionFactory.CreateList("--exclude-ips", "addresses", "Remove these IP addresses or CIDR subnets.");
    private readonly Option<string[]> _includeProcesses = OptionFactory.CreateList("--include-processes", "names", "Keep only these process names or application targets.");
    private readonly Option<string[]> _excludeProcesses = OptionFactory.CreateList("--exclude-processes", "names", "Remove these process names or application targets.");
    private readonly Option<string?> _direction = OptionFactory.CreateChoice("--direction", "Flow direction to keep. Default: all.", "all", "in", "out");
    private readonly Option<bool> _listenersOnly = new("--listeners-only") { Description = "Keep only listening sockets." };
    public Option<bool> IncludeEphemeralPorts { get; } = new("--include-ephemeral-ports") { Description = "Keep ephemeral ports (49152-65535), excluded by default." };

    public void AddTo(Command command)
    {
        command.Options.Add(_includePorts);
        command.Options.Add(_excludePorts);
        command.Options.Add(_includeHosts);
        command.Options.Add(_excludeHosts);
        command.Options.Add(_includeIps);
        command.Options.Add(_excludeIps);
        command.Options.Add(_includeProcesses);
        command.Options.Add(_excludeProcesses);
        command.Options.Add(_direction);
        command.Options.Add(_listenersOnly);
        command.Options.Add(IncludeEphemeralPorts);
    }

    public AppConfiguration Apply(ParseResult parseResult, AppConfiguration config) =>
        config with
        {
            MapIncludedPorts = OptionFactory.ReadList(parseResult, _includePorts, config.MapIncludedPorts),
            MapExcludedPorts = OptionFactory.ReadList(parseResult, _excludePorts, config.MapExcludedPorts),
            MapIncludedHosts = OptionFactory.ReadList(parseResult, _includeHosts, config.MapIncludedHosts),
            MapExcludedHosts = OptionFactory.ReadList(parseResult, _excludeHosts, config.MapExcludedHosts),
            MapIncludedIps = OptionFactory.ReadList(parseResult, _includeIps, config.MapIncludedIps),
            MapExcludedIps = OptionFactory.ReadList(parseResult, _excludeIps, config.MapExcludedIps),
            MapIncludedProcesses = OptionFactory.ReadList(parseResult, _includeProcesses, config.MapIncludedProcesses),
            MapExcludedProcesses = OptionFactory.ReadList(parseResult, _excludeProcesses, config.MapExcludedProcesses),
            MapDirection = parseResult.GetValue(_direction) ?? config.MapDirection,
            MapListenersOnly = config.MapListenersOnly || parseResult.GetValue(_listenersOnly),
            MapExcludeEphemeralPorts = config.MapExcludeEphemeralPorts && !parseResult.GetValue(IncludeEphemeralPorts)
        };
}
