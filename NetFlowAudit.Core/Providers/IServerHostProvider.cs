using System.Net;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Core.Providers;

/// <summary>
/// Contrat d'abstraction fournissant les métadonnées de la machine hôte et de ses interfaces réseau.
/// </summary>
public interface IServerHostProvider
{
    /// <summary>
    /// Récupère les métadonnées du serveur local.
    /// </summary>
    /// <returns>Métadonnées de la machine hôte.</returns>
    ServerHostMetadata GetCurrentHostMetadata();

    /// <summary>
    /// Récupère la liste de toutes les adresses IP associées à la machine hôte.
    /// </summary>
    /// <returns>Ensemble des adresses IP locales de la machine hôte.</returns>
    IReadOnlySet<IPAddress> GetLocalIpAddresses();
}
