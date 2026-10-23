using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Évaluateur vérifiant si une adresse IP correspond à une plage réseau ou une adresse exclue de la résolution DNS inverse.
/// </summary>
public sealed class DnsAddressExclusionEvaluator
{
    private readonly List<IPNetwork> _excludedNetworks = [];
    private readonly HashSet<IPAddress> _excludedAddresses = [];

    /// <summary>
    /// Initialise une nouvelle instance avec la liste des règles de filtrage (notations CIDR ou adresses IP exactes).
    /// </summary>
    /// <param name="rules">Règles d'exclusion (ex: "169.254.0.0/16", "224.0.0.0/4", "255.255.255.255").</param>
    public DnsAddressExclusionEvaluator(IEnumerable<string>? rules)
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
                    _excludedNetworks.Add(network);
                }
            }
            else if (IPAddress.TryParse(trimmed, out var address))
            {
                _excludedAddresses.Add(address);
            }
        }
    }

    /// <summary>
    /// Détermine si l'adresse IP spécifiée doit être exclue de la résolution DNS inverse.
    /// </summary>
    /// <param name="address">Adresse IP à évaluer.</param>
    /// <returns>True si l'adresse est exclue, sinon False.</returns>
    public bool ShouldExclude(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (_excludedAddresses.Contains(address))
        {
            return true;
        }

        for (var i = 0; i < _excludedNetworks.Count; i++)
        {
            if (_excludedNetworks[i].Contains(address))
            {
                return true;
            }
        }

        return false;
    }
}
