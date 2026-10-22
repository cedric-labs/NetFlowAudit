using System.Collections.Concurrent;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Deduplication;

/// <summary>
/// Registre thread-safe de dédoublonnage des entrées d'audit réseau.
/// </summary>
public sealed class ConnectionDeduplicationRegister
{
    private readonly ConcurrentDictionary<string, NetworkAuditEntry> _activeFlows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hydrate le registre avec des clés préexistantes (ex: rechargées depuis un CSV au démarrage).
    /// </summary>
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
    /// Tente d'enregistrer une entrée d'audit. Retourne true si c'est la première occurrence observée.
    /// </summary>
    public bool TryRegisterOrUpdate(NetworkAuditEntry candidate, out NetworkAuditEntry registered)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var key = candidate.ComputeAuditKey();
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
                    return candidate;
                }

                return existing with
                {
                    LastSeenUtc = candidate.LastSeenUtc,
                    ObservationCount = existing.ObservationCount + 1,
                    Socket = existing.Socket with { State = candidate.Socket.State }
                };
            });

        return isNew;
    }

    /// <summary>
    /// Nombre total de flux uniques répertoriés.
    /// </summary>
    public int UniqueCount => _activeFlows.Count;
}
