using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Contracts;

/// <summary>
/// Contrat d'acquisition des métadonnées du serveur hôte local.
/// </summary>
public interface IServerHostProvider
{
    /// <summary>
    /// Fournit les métadonnées relatives au serveur local hôte.
    /// </summary>
    /// <returns>Les informations immuables d'identification du serveur.</returns>
    ServerHostMetadata GetCurrentHostMetadata();
}
