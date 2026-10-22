using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Filtering;

/// <summary>
/// Évaluateur de conformité des processus vis-à-vis des filtres et règles d'exclusion configurées.
/// </summary>
public sealed class ProcessExclusionFilter(ProcessFilterOptions options)
{
    private readonly ProcessFilterOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Détermine si le processus associé à la connexion doit être exclu de l'audit.
    /// </summary>
    /// <param name="process">Métadonnées du processus à évaluer.</param>
    /// <returns>True si le processus doit être ignoré, false sinon.</returns>
    public bool ShouldExclude(ProcessMetadata process)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (_options.ExcludedProcessIds.Contains(process.ProcessId))
        {
            return true;
        }

        if (_options.ExcludeSystemAccounts && IsSystemAccount(process.UserName))
        {
            return true;
        }

        var processName = process.ProcessName;
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        return MatchesNameRules(processName);
    }

    private static bool IsSystemAccount(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        return userName.Contains("NT AUTHORITY", StringComparison.OrdinalIgnoreCase) ||
               userName.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
               userName.Equals("root", StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesNameRules(string name)
    {
        for (var i = 0; i < _options.ExcludedExactNames.Count; i++)
        {
            if (name.Equals(_options.ExcludedExactNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        for (var i = 0; i < _options.ExcludedNamePrefixes.Count; i++)
        {
            if (name.StartsWith(_options.ExcludedNamePrefixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        for (var i = 0; i < _options.ExcludedNameSuffixes.Count; i++)
        {
            if (name.EndsWith(_options.ExcludedNameSuffixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        for (var i = 0; i < _options.ExcludedNameContains.Count; i++)
        {
            if (name.Contains(_options.ExcludedNameContains[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
