using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Contracts;

/// <summary>
/// Contrat d'inventaire et de scan des flux et sockets réseau actifs sur le système d'exploitation.
/// </summary>
public interface INetworkSocketScanner
{
    /// <summary>
    /// Scanne et énumère l'ensemble des connexions réseau TCP et UDP actuellement ouvertes.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Collection en lecture seule des connexions réseau auditées.</returns>
    Task<IReadOnlyList<NetworkConnectionRecord>> ScanActiveConnectionsAsync(CancellationToken cancellationToken = default);
}
