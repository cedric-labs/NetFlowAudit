using System.Net;
using System.Text.Json;
using PortAtlas.Core.Filtering;
using ProcessForge.Filtering;

namespace PortAtlas.Cli.Configuration;

/// <summary>
/// Dynamic flow exclusion configurator that updates in-memory filters and persists rules to JSON configuration.
/// </summary>
public sealed class FlowExclusionConfigurator(
    AppConfiguration initialConfiguration,
    string configurationFilePath,
    INetworkFlowFilter networkFlowFilter,
    ProcessExclusionFilter processExclusionFilter) : IFlowExclusionConfigurator
{
    private readonly object _syncLock = new();
    private readonly string _configurationFilePath = configurationFilePath ?? throw new ArgumentNullException(nameof(configurationFilePath));
    private readonly INetworkFlowFilter _networkFlowFilter = networkFlowFilter ?? throw new ArgumentNullException(nameof(networkFlowFilter));
    private readonly ProcessExclusionFilter _processExclusionFilter = processExclusionFilter ?? throw new ArgumentNullException(nameof(processExclusionFilter));

    private AppConfiguration _currentConfiguration = initialConfiguration ?? throw new ArgumentNullException(nameof(initialConfiguration));

    /// <inheritdoc />
    public AppConfiguration CurrentConfiguration
    {
        get
        {
            lock (_syncLock)
            {
                return _currentConfiguration;
            }
        }
    }

    /// <inheritdoc />
    public string ExcludeLocalPort(int port)
    {
        lock (_syncLock)
        {
            _networkFlowFilter.ExcludeLocalPort(port);

            if (!_currentConfiguration.ExcludedLocalPorts.Contains(port))
            {
                var updatedPorts = new List<int>(_currentConfiguration.ExcludedLocalPorts) { port };
                _currentConfiguration = _currentConfiguration with { ExcludedLocalPorts = updatedPorts };
                SaveConfigurationFile(_currentConfiguration);
            }

            return $"Excluded local port {port}";
        }
    }

    /// <inheritdoc />
    public string ExcludeRemoteIp(string ipOrCidr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipOrCidr);

        lock (_syncLock)
        {
            var cleanIp = ipOrCidr.Trim();
            if (IPAddress.TryParse(cleanIp, out var parsedIp) && parsedIp.IsIPv4MappedToIPv6)
            {
                cleanIp = parsedIp.MapToIPv4().ToString();
            }

            _networkFlowFilter.ExcludeRemoteIp(cleanIp);

            if (!_currentConfiguration.ExcludedRemoteIps.Contains(cleanIp, StringComparer.OrdinalIgnoreCase))
            {
                var updatedIps = new List<string>(_currentConfiguration.ExcludedRemoteIps) { cleanIp };
                _currentConfiguration = _currentConfiguration with { ExcludedRemoteIps = updatedIps };
                SaveConfigurationFile(_currentConfiguration);
            }

            return $"Excluded remote IP {cleanIp}";
        }
    }

    /// <inheritdoc />
    public string ExcludeRemotePort(int port)
    {
        lock (_syncLock)
        {
            _networkFlowFilter.ExcludeRemotePort(port);

            if (!_currentConfiguration.ExcludedRemotePorts.Contains(port))
            {
                var updatedPorts = new List<int>(_currentConfiguration.ExcludedRemotePorts) { port };
                _currentConfiguration = _currentConfiguration with { ExcludedRemotePorts = updatedPorts };
                SaveConfigurationFile(_currentConfiguration);
            }

            return $"Excluded remote port {port}";
        }
    }

    /// <inheritdoc />
    public string ExcludeProcess(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        lock (_syncLock)
        {
            var cleanName = processName.Trim();
            _processExclusionFilter.ExcludeExactName(cleanName);

            var updatedNames = new List<string>(_currentConfiguration.ExcludedExactNames);
            var changed = false;

            if (!updatedNames.Contains(cleanName, StringComparer.OrdinalIgnoreCase))
            {
                updatedNames.Add(cleanName);
                changed = true;
            }

            var baseName = Path.GetFileNameWithoutExtension(cleanName);
            if (!string.IsNullOrWhiteSpace(baseName) && !baseName.Equals(cleanName, StringComparison.OrdinalIgnoreCase))
            {
                _processExclusionFilter.ExcludeExactName(baseName);
                if (!updatedNames.Contains(baseName, StringComparer.OrdinalIgnoreCase))
                {
                    updatedNames.Add(baseName);
                    changed = true;
                }
            }

            if (changed)
            {
                _currentConfiguration = _currentConfiguration with { ExcludedExactNames = updatedNames };
                SaveConfigurationFile(_currentConfiguration);
            }

            return $"Excluded process '{cleanName}'";
        }
    }

    /// <inheritdoc />
    public string ExcludeTarget(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        lock (_syncLock)
        {
            var cleanTarget = target.Trim().Trim('"', '\'');
            if (cleanTarget.Contains('/') || cleanTarget.Contains('\\'))
            {
                var extracted = Path.GetFileName(cleanTarget);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    cleanTarget = extracted;
                }
            }

            _processExclusionFilter.ExcludeExactName(cleanTarget);

            var updatedNames = new List<string>(_currentConfiguration.ExcludedExactNames);
            var changed = false;

            if (!updatedNames.Contains(cleanTarget, StringComparer.OrdinalIgnoreCase))
            {
                updatedNames.Add(cleanTarget);
                changed = true;
            }

            var baseName = Path.GetFileNameWithoutExtension(cleanTarget);
            if (!string.IsNullOrWhiteSpace(baseName) && !baseName.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase))
            {
                _processExclusionFilter.ExcludeExactName(baseName);
                if (!updatedNames.Contains(baseName, StringComparer.OrdinalIgnoreCase))
                {
                    updatedNames.Add(baseName);
                    changed = true;
                }
            }

            if (changed)
            {
                _currentConfiguration = _currentConfiguration with { ExcludedExactNames = updatedNames };
                SaveConfigurationFile(_currentConfiguration);
            }

            return $"Excluded target '{cleanTarget}'";
        }
    }

    private void SaveConfigurationFile(AppConfiguration configurationToPersist)
    {
        try
        {
            var serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(configurationToPersist, serializerOptions);
            File.WriteAllText(_configurationFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Disk write failure (restricted permissions or locked file) must not interrupt live monitoring;
            // the exclusion remains active in memory.
        }
    }
}
