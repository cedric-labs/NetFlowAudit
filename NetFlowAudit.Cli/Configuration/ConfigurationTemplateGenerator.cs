using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetFlowAudit.Cli.Configuration;

/// <summary>
/// Generates standard baseline JSON configuration templates for NetFlowAudit.
/// </summary>
public static class ConfigurationTemplateGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Generates a standard baseline configuration file at the specified target path.
    /// </summary>
    /// <param name="targetPath">Target file path or directory (defaults to 'netflowaudit.json' in current directory).</param>
    /// <returns>Resolved absolute path of the generated configuration file.</returns>
    public static string GenerateDefaultConfigFile(string? targetPath = null)
    {
        var resolvedPath = ResolveTargetPath(targetPath);

        var defaultConfig = new AppConfiguration
        {
            ScanIntervalSeconds = 5,
            OutputCsvPath = "netflow_audit.csv",
            OutputNdjsonPath = string.Empty,
            OutputHtmlMapPath = "netflow_topology.html",
            MapExcludeEphemeralPorts = true,
            MapListenersOnly = false,
            MapIncludedPorts = [],
            MapExcludedPorts = ["53", "123"],
            MapIncludedHosts = [],
            MapExcludedHosts = [],
            MapIncludedIps = [],
            MapExcludedIps = [],
            MapIncludedProcesses = [],
            MapExcludedProcesses = [],
            MapLayoutMode = "layered",
            MapSortMethod = "barycentric",
            MapRoutingMode = "bezier",
            MapSpacingProfile = "balanced",
            EnableDashboard = true,
            ResolveRemoteHostNames = true,
            ResolveIpOwners = true,
            ReloadHistoryLimit = null,
            DnsCacheFilePath = "netflow_dns_cache.json",
            DnsExcludedNetworks =
            [
                "0.0.0.0",
                "255.255.255.255",
                "169.254.0.0/16",
                "224.0.0.0/4",
                "fe80::/10",
                "ff00::/8"
            ],
            ExcludedPids = [0, 4],
            ExcludedExactNames = ["System", "Idle"],
            ExcludedPrefixes = ["svchost"],
            ExcludedSuffixes = [],
            ExcludedContains = [],
            ExcludeSystemAccounts = false,
            ExcludeLoopbackFlows = true,
            ExcludedLocalPorts = [22, 3389],
            ExcludedRemotePorts = [53],
            ExcludedRemoteIps = ["192.168.1.50", "10.0.0.0/8"],
            MaskServerName = string.Empty,
            MaskLocalIp = string.Empty,
            CustomHostAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["10.0.0.5"] = "srv-oracle-prod"
            },
            CsvDelimiter = ",",
            CsvColumns =
            [
                "FirstSeenUtc",
                "LastSeenUtc",
                "ObservationCount",
                "ServerName",
                "ServerOsVersion",
                "ServerPrimaryIp",
                "Protocol",
                "Direction",
                "TcpState",
                "LocalIp",
                "LocalPort",
                "RemoteIp",
                "RemotePort",
                "RemoteHostName",
                "ProcessId",
                "ProcessName",
                "ExecutablePath",
                "CommandLine",
                "TargetExecutable",
                "UserName"
            ]
        };

        var json = JsonSerializer.Serialize(defaultConfig, JsonOptions);
        var dir = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(resolvedPath, json);
        return resolvedPath;
    }

    private static string ResolveTargetPath(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return Path.GetFullPath("netflowaudit.json");
        }

        var trimmed = targetPath.Trim();
        if (Directory.Exists(trimmed) || trimmed.EndsWith(Path.DirectorySeparatorChar) || trimmed.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return Path.GetFullPath(Path.Combine(trimmed, "netflowaudit.json"));
        }

        return Path.GetFullPath(trimmed);
    }
}
