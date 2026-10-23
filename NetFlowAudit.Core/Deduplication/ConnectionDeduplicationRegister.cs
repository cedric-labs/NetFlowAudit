using System.Collections.Concurrent;
using System.Net;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Deduplication;

/// <summary>
/// Registre thread-safe de dédoublonnage des entrées d'audit réseau.
/// </summary>
public sealed class ConnectionDeduplicationRegister
{
    private readonly ConcurrentDictionary<FlowIdentityKey, NetworkAuditEntry?> _activeFlows = new();

    /// <summary>
    /// Hydrate le registre avec des clés préexistantes (ex: rechargées depuis un CSV au démarrage).
    /// </summary>
    /// <param name="existingKeys">Liste des clés logiques textuelles existantes.</param>
    public void Hydrate(IEnumerable<string> existingKeys)
    {
        ArgumentNullException.ThrowIfNull(existingKeys);

        foreach (var key in existingKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                _activeFlows.TryAdd(new FlowIdentityKey(key), null);
            }
        }
    }

    /// <summary>
    /// Tente d'enregistrer une entrée d'audit. Retourne true si c'est la première occurrence observée.
    /// </summary>
    /// <param name="candidate">Entrée d'audit candidate.</param>
    /// <param name="registered">Entrée enregistrée ou mise à jour dans le registre.</param>
    /// <returns>True si l'entrée est nouvelle, False s'il s'agit d'une mise à jour.</returns>
    public bool TryRegisterOrUpdate(NetworkAuditEntry candidate, out NetworkAuditEntry registered)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var key = FlowIdentityKey.FromEntry(candidate);
        var isNew = false;

        var result = _activeFlows.AddOrUpdate(
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

        registered = result ?? candidate;
        return isNew;
    }

    /// <summary>
    /// Met à jour une entrée déjà enregistrée dans le registre (ex: enrichissement après résolution DNS inverse).
    /// </summary>
    /// <param name="updated">Entrée enrichie à consigner dans le registre.</param>
    public void UpdateRegisteredEntry(NetworkAuditEntry updated)
    {
        ArgumentNullException.ThrowIfNull(updated);
        var key = FlowIdentityKey.FromEntry(updated);
        _activeFlows[key] = updated;
    }

    /// <summary>
    /// Met à jour le nom d'hôte distant pour l'ensemble des flux enregistrés correspondant à une adresse IP distante donnée.
    /// </summary>
    /// <param name="remoteAddress">Adresse IP distante dont le nom d'hôte a été résolu.</param>
    /// <param name="resolvedHostName">Nom d'hôte résolu.</param>
    public void UpdateRemoteHostName(IPAddress remoteAddress, string resolvedHostName)
    {
        ArgumentNullException.ThrowIfNull(remoteAddress);
        ArgumentNullException.ThrowIfNull(resolvedHostName);

        foreach (var (key, entry) in _activeFlows)
        {
            if (entry is not null && entry.Socket.RemoteEndpoint.Address.Equals(remoteAddress) && string.IsNullOrWhiteSpace(entry.RemoteHostName))
            {
                _activeFlows[key] = entry with { RemoteHostName = resolvedHostName };
            }
        }
    }

    /// <summary>
    /// Nombre total de flux uniques répertoriés.
    /// </summary>
    public int UniqueCount => _activeFlows.Count;
}
