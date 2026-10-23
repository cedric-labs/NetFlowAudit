using System.Net;

namespace NetFlowAudit.Core.Filtering;

/// <summary>
/// Évaluateur de correspondance d'adresses IP contre un ensemble de règles (notations CIDR ou adresses IP exactes).
/// </summary>
public sealed class IpAddressRangeMatcher
{
    private readonly List<IPNetwork> _networks = [];
    private readonly HashSet<IPAddress> _addresses = [];

    /// <summary>
    /// Initialise une nouvelle instance avec la liste des règles de filtrage (notations CIDR ou adresses IP exactes).
    /// </summary>
    /// <param name="rules">Règles d'exclusion (ex: "192.168.1.50", "10.0.0.0/8").</param>
    public IpAddressRangeMatcher(IEnumerable<string>? rules)
    {
        if (rules is null)
        {
            return;
        }

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule))
            {
                continue;
            }

            var trimmed = rule.Trim();
            if (trimmed.Contains('/', StringComparison.Ordinal))
            {
                if (IPNetwork.TryParse(trimmed, out var network))
                {
                    _networks.Add(network);
                }
            }
            else if (IPAddress.TryParse(trimmed, out var address))
            {
                _addresses.Add(address);
            }
        }
    }

    /// <summary>
    /// Indique si au moins une règle est configurée.
    /// </summary>
    public bool HasRules => _addresses.Count > 0 || _networks.Count > 0;

    /// <summary>
    /// Détermine si l'adresse IP spécifiée correspond à l'une des adresses ou plages réseau configurées.
    /// </summary>
    /// <param name="address">Adresse IP à évaluer.</param>
    /// <returns>True si l'adresse correspond à une règle, sinon False.</returns>
    public bool IsMatch(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (_addresses.Contains(address))
        {
            return true;
        }

        for (var i = 0; i < _networks.Count; i++)
        {
            if (_networks[i].Contains(address))
            {
                return true;
            }
        }

        return false;
    }
}
