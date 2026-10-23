using System.Text.Json;
using NetFlowAudit.Core.Filtering;
using ProcessForge.Filtering;

namespace NetFlowAudit.Cli.Configuration;

/// <summary>
/// Gestionnaire d'exclusion dynamique de flux et de persistance des règles dans le fichier de configuration JSON.
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

            return $"Port local {port} exclu";
        }
    }

    /// <inheritdoc />
    public string ExcludeRemoteIp(string ipOrCidr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipOrCidr);

        lock (_syncLock)
        {
            var cleanIp = ipOrCidr.Trim();
            _networkFlowFilter.ExcludeRemoteIp(cleanIp);

            if (!_currentConfiguration.ExcludedRemoteIps.Contains(cleanIp, StringComparer.OrdinalIgnoreCase))
            {
                var updatedIps = new List<string>(_currentConfiguration.ExcludedRemoteIps) { cleanIp };
                _currentConfiguration = _currentConfiguration with { ExcludedRemoteIps = updatedIps };
                SaveConfigurationFile(_currentConfiguration);
            }

            return $"IP distante {cleanIp} exclue";
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

            return $"Port distant {port} exclu";
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

            if (!_currentConfiguration.ExcludedExactNames.Contains(cleanName, StringComparer.OrdinalIgnoreCase))
            {
                var updatedNames = new List<string>(_currentConfiguration.ExcludedExactNames) { cleanName };
                _currentConfiguration = _currentConfiguration with { ExcludedExactNames = updatedNames };
                SaveConfigurationFile(_currentConfiguration);
            }

            return $"Processus '{cleanName}' exclu";
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

            if (!_currentConfiguration.ExcludedExactNames.Contains(cleanTarget, StringComparer.OrdinalIgnoreCase))
            {
                var updatedNames = new List<string>(_currentConfiguration.ExcludedExactNames) { cleanTarget };
                _currentConfiguration = _currentConfiguration with { ExcludedExactNames = updatedNames };
                SaveConfigurationFile(_currentConfiguration);
            }

            return $"Cible '{cleanTarget}' exclue";
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
            // L'échec d'écriture sur disque (droits restreints, fichier verrouillé) ne doit pas interrompre
            // la surveillance continue en cours d'exécution ; l'exclusion demeure effective en mémoire vive.
        }
    }
}
