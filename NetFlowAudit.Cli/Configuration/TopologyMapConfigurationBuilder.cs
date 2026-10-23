using System.Globalization;
using TopologyForge.Filtering;
using TopologyForge.Models;

namespace NetFlowAudit.Cli.Configuration;

/// <summary>
/// Constructeur d'options de filtrage topologique à partir de la configuration applicative et des arguments CLI.
/// </summary>
public static class TopologyMapConfigurationBuilder
{
    /// <summary>
    /// Convertit la configuration globale en options de filtrage pour <see cref="TopologyForge"/>.
    /// </summary>
    /// <param name="config">Configuration applicative.</param>
    /// <returns>Options de filtrage topologique.</returns>
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
    /// Applique les surcharges de ligne de commande relatives à la cartographie et aux filtres d'inclusion/exclusion.
    /// </summary>
    /// <param name="config">Configuration initiale.</param>
    /// <param name="args">Arguments de ligne de commande.</param>
    /// <returns>Configuration mise à jour.</returns>
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

        var listenersOnly = config.MapListenersOnly ||
                            args.Any(a => a.Equals("--listeners-only", StringComparison.OrdinalIgnoreCase));

        var includeEphemeral = args.Any(a => a.Equals("--include-ephemeral-ports", StringComparison.OrdinalIgnoreCase));
        var excludeEphemeral = includeEphemeral ? false : config.MapExcludeEphemeralPorts;

        return config with
        {
            OutputHtmlMapPath = mapPath,
            MapIncludedPorts = includePorts,
            MapExcludedPorts = excludePorts,
            MapIncludedHosts = includeHosts,
            MapExcludedHosts = excludeHosts,
            MapIncludedIps = includeIps,
            MapExcludedIps = excludeIps,
            MapIncludedProcesses = includeProcesses,
            MapExcludedProcesses = excludeProcesses,
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
