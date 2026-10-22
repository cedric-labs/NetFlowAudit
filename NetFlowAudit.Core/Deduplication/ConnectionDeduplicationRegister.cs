using System.Collections.Concurrent;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Deduplication;

/// <summary>
/// Registre thread-safe de dédoublonnage des flux et connexions réseau observés.
/// </summary>
public sealed class ConnectionDeduplicationRegister
{
    private readonly ConcurrentDictionary<string, NetworkConnectionRecord> _activeFlows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initialise le registre avec un ensemble de signatures préexistantes.
    /// </summary>
    /// <param name="existingKeys">Clés de dédoublonnage déjà persistées.</param>
    public void Hydrate(IEnumerable<string> existingKeys)
    {
        ArgumentNullException.ThrowIfNull(existingKeys);

        foreach (var key in existingKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                _activeFlows.TryAdd(key, null!);
            }
        }
    }

    /// <summary>
    /// Tente d'enregistrer une connexion observée. Retourne true si c'est la toute première fois qu'elle apparaît.
    /// </summary>
    /// <param name="candidate">Connexion candidate.</param>
    /// <param name="registered">Connexion résultante enregistrée.</param>
    /// <returns>True si le flux est nouveau, False s'il s'agit d'un flux déjà connu.</returns>
    public bool TryRegisterOrUpdate(NetworkConnectionRecord candidate, out NetworkConnectionRecord registered)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var key = candidate.ComputeDeduplicationKey();

        var isNew = false;
        registered = _activeFlows.AddOrUpdate(
            key,
            addValueFactory: _ =>
            {
                isNew = true;
                return candidate;
            },
            updateValueFactory: (_, existing) =>
            {
                if (existing is null)
                {
                    // Cas d'une clé hydratée depuis un CSV au démarrage sans objet record complet
                    return candidate;
                }

                return existing with
                {
                    LastSeenUtc = candidate.LastSeenUtc,
                    ObservationCount = existing.ObservationCount + 1,
                    TcpState = candidate.TcpState
                };
            });

        return isNew;
    }

    /// <summary>
    /// Nombre total de flux uniques répertoriés dans le registre en mémoire.
    /// </summary>
    public int UniqueCount => _activeFlows.Count;
}
