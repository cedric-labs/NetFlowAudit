using System.Net;
using NetFlowAudit.Core.Filtering;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Évaluateur vérifiant si une adresse IP correspond à une plage réseau ou une adresse exclue de la résolution DNS inverse.
/// </summary>
public sealed class DnsAddressExclusionEvaluator(IEnumerable<string>? rules)
{
    private readonly IpAddressRangeMatcher _matcher = new(rules);

    /// <summary>
    /// Détermine si l'adresse IP spécifiée doit être exclue de la résolution DNS inverse.
    /// </summary>
    /// <param name="address">Adresse IP à évaluer.</param>
    /// <returns>True si l'adresse est exclue, sinon False.</returns>
    public bool ShouldExclude(IPAddress address) => _matcher.IsMatch(address);
}
