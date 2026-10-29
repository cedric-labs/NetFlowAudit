using System.Globalization;
using TopologyForge.Export.Drawio.Adapters;
using TopologyForge.Export.Drawio.Styling;
using TopologyForge.Filtering;
using TopologyForge.Models;
using TopologyForge.Rendering;

namespace PortAtlas.Cli.Configuration;

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

        var includeInbound = !config.MapListenersOnly && !string.Equals(config.MapDirection, "out", StringComparison.OrdinalIgnoreCase);
        var includeOutbound = !config.MapListenersOnly && !string.Equals(config.MapDirection, "in", StringComparison.OrdinalIgnoreCase);

        return new TopologyFilterOptions
        {
            ExcludeEphemeralPorts = config.MapExcludeEphemeralPorts,
            IncludeListeners = true,
            IncludeInboundFlows = includeInbound,
            IncludeOutboundFlows = includeOutbound,
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
            "CLUSTER" or "SUBNETCLUSTER" or "ZONE" or "ZONES" or "SUBNET" => TopologyNodeSortMethod.SubnetCluster,
            "IP" or "SUBNETIP" => TopologyNodeSortMethod.SubnetIp,
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

        var initialIncludePorts = config.MapIncludedPorts.Count > 0 ? string.Join(", ", config.MapIncludedPorts) : string.Empty;
        var initialExcludePorts = config.MapExcludedPorts.Count > 0 ? string.Join(", ", config.MapExcludedPorts) : string.Empty;
        var initialExcludeHosts = config.MapExcludedHosts.Count > 0 ? string.Join(", ", config.MapExcludedHosts) : string.Empty;
        var initialDirection = config.MapListenersOnly
            ? "IN"
            : (string.Equals(config.MapDirection, "out", StringComparison.OrdinalIgnoreCase)
                ? "OUT"
                : (string.Equals(config.MapDirection, "in", StringComparison.OrdinalIgnoreCase)
                    ? "IN"
                    : "all"));

        return new TopologyRenderingOptions
        {
            DefaultLayoutMode = layoutMode,
            DefaultSortMethod = sortMethod,
            DefaultRoutingMode = routingMode,
            DefaultSpacingProfile = spacingProfile,
            InitialIncludePorts = initialIncludePorts,
            InitialExcludePorts = initialExcludePorts,
            InitialExcludeHosts = initialExcludeHosts,
            InitialDirection = initialDirection,
            CliCommandFormat = MapCommandFormat
        };
    }

    // Syntax of the "map" command (ADR-0001), copied from the HTML map to reproduce the displayed view.
    private static readonly TopologyCliCommandFormat MapCommandFormat = new()
    {
        Prefix = "PortAtlas map",
        LayoutOption = "--layout",
        SortOption = "--sort",
        RoutingOption = "--routing",
        SpacingOption = "--spacing"
    };

    /// <summary>
    /// Converts the configured Draw.io edge mode (<c>adaptive</c>, <c>direction</c>, <c>none</c>) into the exporter setting.
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <returns>The edge aggregation mode; <see cref="EdgeAggregationMode.AdaptiveThreshold"/> for any other value.</returns>
    public static EdgeAggregationMode BuildDrawioEdgeMode(AppConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.DrawioEdgeMode.Trim().ToUpperInvariant() switch
        {
            "DIRECTION" => EdgeAggregationMode.ByDirection,
            "NONE" => EdgeAggregationMode.None,
            _ => EdgeAggregationMode.AdaptiveThreshold
        };
    }

    /// <summary>
    /// Converts the configured Draw.io theme (<c>dark</c>, <c>light</c>) into the exporter theme.
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <returns><see cref="DiagramTheme.EnterpriseLight"/> for <c>light</c>, otherwise <see cref="DiagramTheme.DarkModern"/>.</returns>
    public static DiagramTheme BuildDrawioTheme(AppConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.DrawioTheme.Equals("light", StringComparison.OrdinalIgnoreCase)
            ? DiagramTheme.EnterpriseLight
            : DiagramTheme.DarkModern;
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
}
