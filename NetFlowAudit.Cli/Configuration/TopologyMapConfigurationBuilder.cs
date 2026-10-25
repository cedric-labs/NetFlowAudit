using System.Globalization;
using TopologyForge.Filtering;
using TopologyForge.Models;
using TopologyForge.Rendering;

namespace NetFlowAudit.Cli.Configuration;

/// <summary>
/// Builds topology filtering and rendering options from application configuration and CLI arguments.
/// </summary>
public static class TopologyMapConfigurationBuilder
{
    /// <summary>
    /// Converts global configuration into topology filter options for <see cref="TopologyForge"/>.
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <returns>Configured topology filter options.</returns>
    public static TopologyFilterOptions BuildFilterOptions(AppConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var includedPorts = ParsePortRanges(config.MapIncludedPorts);
        var excludedPorts = ParsePortRanges(config.MapExcludedPorts);

        foreach (var port in config.ExcludedLocalPorts.Concat(config.ExcludedRemotePorts))
        {
            if (port is > 0 and <= 65535)
            {
                excludedPorts.Add(NetworkPortRange.Single(port));
            }
        }

        var excludedIps = new List<string>(config.MapExcludedIps);
        foreach (var ip in config.ExcludedRemoteIps)
        {
            if (!string.IsNullOrWhiteSpace(ip))
            {
                excludedIps.Add(ip);
            }
        }

        return new TopologyFilterOptions
        {
            ExcludeEphemeralPorts = config.MapExcludeEphemeralPorts,
            IncludeListeners = true,
            IncludeInboundFlows = !config.MapListenersOnly,
            IncludeOutboundFlows = !config.MapListenersOnly,
            IncludedPorts = includedPorts,
            ExcludedPorts = excludedPorts,
            IncludedHostPatterns = config.MapIncludedHosts,
            ExcludedHostPatterns = config.MapExcludedHosts,
            IncludedIpRanges = config.MapIncludedIps,
            ExcludedIpRanges = excludedIps,
            IncludedProcesses = config.MapIncludedProcesses,
            ExcludedProcesses = config.MapExcludedProcesses
        };
    }

    /// <summary>
    /// Converts global configuration into layout, sorting, and routing options for <see cref="TopologyForge"/>.
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <returns>Configured topology rendering options.</returns>
    public static TopologyRenderingOptions BuildRenderingOptions(AppConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var layoutMode = config.MapLayoutMode.Trim().ToUpperInvariant() switch
        {
            "ORGANIC" or "FORCE" or "FORCEDIRECTED" => TopologyLayoutMode.ForceDirectedOrganic,
            "CIRCULAR" or "RING" => TopologyLayoutMode.Circular,
            "GRID" or "MATRIX" => TopologyLayoutMode.Grid,
            "TREE" or "TOPDOWN" or "ARBRE" => TopologyLayoutMode.Tree,
            "HORIZONTALTREE" or "HORIZONTAL-TREE" or "HORIZONTAL" or "LEFTTORIGHT" => TopologyLayoutMode.HorizontalTree,
            _ => TopologyLayoutMode.Layered
        };

        var sortMethod = config.MapSortMethod.Trim().ToUpperInvariant() switch
        {
            "DEGREE" or "CONNECTIONDEGREE" => TopologyNodeSortMethod.ConnectionDegree,
            "ALPHA" or "ALPHABETICAL" or "NAME" => TopologyNodeSortMethod.Alphabetical,
            "TIERS" or "TIER" or "FUNCTIONALTIER" => TopologyNodeSortMethod.FunctionalTier,
            "CLUSTER" or "SUBNETCLUSTER" or "ZONE" or "ZONES" => TopologyNodeSortMethod.SubnetCluster,
            "SUBNET" or "IP" or "SUBNETIP" => TopologyNodeSortMethod.SubnetIp,
            _ => TopologyNodeSortMethod.BarycentricSugiyama
        };

        var routingMode = config.MapRoutingMode.Trim().ToUpperInvariant() switch
        {
            "ORTHOGONAL" or "ORTHOGONALCORRIDORS" => TopologyEdgeRoutingMode.OrthogonalCorridors,
            "STRAIGHT" or "DIRECT" or "DIRECTSTRAIGHT" => TopologyEdgeRoutingMode.DirectStraight,
            _ => TopologyEdgeRoutingMode.BezierCurved
        };

        var spacingProfile = config.MapSpacingProfile.Trim().ToUpperInvariant() switch
        {
            "COMPACT" => TopologySpacingProfile.Compact,
            "SPACIOUS" or "LARGE" or "WIDE" => TopologySpacingProfile.Spacious,
            _ => TopologySpacingProfile.Balanced
        };

        return new TopologyRenderingOptions
        {
            DefaultLayoutMode = layoutMode,
            DefaultSortMethod = sortMethod,
            DefaultRoutingMode = routingMode,
            DefaultSpacingProfile = spacingProfile
        };
    }

    /// <summary>
    /// Applies CLI command-line overrides for topology map paths and inclusion/exclusion filters.
    /// </summary>
    /// <param name="config">Initial application configuration.</param>
    /// <param name="args">Command-line argument array.</param>
    /// <returns>Updated application configuration.</returns>
    public static AppConfiguration ApplyCommandLineOverrides(AppConfiguration config, string[] args)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(args);

        var mapPath = GetArgumentValue(args, "--map") ?? config.OutputHtmlMapPath;
        var includePorts = ParseCsvArgument(args, "--include-ports", config.MapIncludedPorts);
        var excludePorts = ParseCsvArgument(args, "--exclude-ports", config.MapExcludedPorts);
        var includeHosts = ParseCsvArgument(args, "--include-hosts", config.MapIncludedHosts);
        var excludeHosts = ParseCsvArgument(args, "--exclude-hosts", config.MapExcludedHosts);
        var includeIps = ParseCsvArgument(args, "--include-ips", config.MapIncludedIps);
        var excludeIps = ParseCsvArgument(args, "--exclude-ips", config.MapExcludedIps);
        var includeProcesses = ParseCsvArgument(args, "--include-processes", config.MapIncludedProcesses);
        var excludeProcesses = ParseCsvArgument(args, "--exclude-processes", config.MapExcludedProcesses);
        var layoutMode = GetArgumentValue(args, "--map-layout") ?? config.MapLayoutMode;
        var sortMethod = GetArgumentValue(args, "--map-sort") ?? config.MapSortMethod;
        var routingMode = GetArgumentValue(args, "--map-routing") ?? config.MapRoutingMode;
        var spacingProfile = GetArgumentValue(args, "--map-spacing") ?? config.MapSpacingProfile;
        var drawioPath = GetArgumentValue(args, "--export-drawio") ??
                         GetArgumentValue(args, "--drawio") ??
                         config.OutputDrawioPath;
        var drawioMode = GetArgumentValue(args, "--drawio-edge-mode") ??
                         GetArgumentValue(args, "--drawio-mode") ??
                         config.DrawioEdgeMode;
        var drawioTheme = GetArgumentValue(args, "--drawio-theme") ?? config.DrawioTheme;

        var listenersOnly = config.MapListenersOnly ||
                            args.Any(a => a.Equals("--listeners-only", StringComparison.OrdinalIgnoreCase));

        var includeEphemeral = args.Any(a => a.Equals("--include-ephemeral-ports", StringComparison.OrdinalIgnoreCase));
        var excludeEphemeral = includeEphemeral ? false : config.MapExcludeEphemeralPorts;

        return config with
        {
            OutputHtmlMapPath = mapPath,
            MapLayoutMode = layoutMode,
            MapIncludedPorts = includePorts,
            MapExcludedPorts = excludePorts,
            MapIncludedHosts = includeHosts,
            MapExcludedHosts = excludeHosts,
            MapIncludedIps = includeIps,
            MapExcludedIps = excludeIps,
            MapIncludedProcesses = includeProcesses,
            MapExcludedProcesses = excludeProcesses,
            MapSortMethod = sortMethod,
            MapRoutingMode = routingMode,
            MapSpacingProfile = spacingProfile,
            OutputDrawioPath = drawioPath,
            DrawioEdgeMode = drawioMode,
            DrawioTheme = drawioTheme,
            MapListenersOnly = listenersOnly,
            MapExcludeEphemeralPorts = excludeEphemeral
        };
    }

    private static List<NetworkPortRange> ParsePortRanges(IEnumerable<string> specifications)
    {
        var result = new List<NetworkPortRange>();
        foreach (var spec in specifications)
        {
            if (string.IsNullOrWhiteSpace(spec))
            {
                continue;
            }

            foreach (var range in NetworkPortRange.ParseList(spec.ToString(CultureInfo.InvariantCulture)))
            {
                result.Add(range);
            }
        }

        return result;
    }

    private static List<string> ParseCsvArgument(string[] args, string argName, List<string> fallback)
    {
        var raw = GetArgumentValue(args, argName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static string? GetArgumentValue(string[] args, string argumentName)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(argumentName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
