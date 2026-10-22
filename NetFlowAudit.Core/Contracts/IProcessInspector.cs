using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Contracts;

/// <summary>
/// Contrat d'inspection et de résolution des métadonnées d'un processus exécutable.
/// </summary>
public interface IProcessInspector
{
    /// <summary>
    /// Résout les métadonnées détaillées d'un processus à partir de son identifiant numérique.
    /// </summary>
    /// <param name="processId">Identifiant unique du processus.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Les métadonnées résolues du processus.</returns>
    Task<ProcessMetadata> ResolveProcessAsync(int processId, CancellationToken cancellationToken = default);
}
