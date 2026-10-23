using System.Net;
using SocketForge.Models;

namespace NetFlowAudit.Core.Filtering;

/// <summary>
/// Contrat de filtrage des connexions réseau (ports, adresses IP distantes et boucle locale).
/// </summary>
public interface INetworkFlowFilter
{
    /// <summary>
    /// Évalue si la connexion réseau doit être exclue de l'audit.
    /// </summary>
    /// <param name="connection">Connexion socket inspectée.</param>
    /// <param name="localHostAddresses">Ensemble des adresses IP locales de la machine hôte.</param>
    /// <returns>True si le flux doit être ignoré, sinon false.</returns>
    bool ShouldExclude(ActiveSocketConnection connection, IReadOnlySet<IPAddress> localHostAddresses);

    /// <summary>
    /// Ajoute dynamiquement un port local à la liste d'exclusion au cours de l'exécution.
    /// </summary>
    /// <param name="port">Numéro de port local à exclure.</param>
    void ExcludeLocalPort(int port);

    /// <summary>
    /// Ajoute dynamiquement un port distant à la liste d'exclusion au cours de l'exécution.
    /// </summary>
    /// <param name="port">Numéro de port distant à exclure.</param>
    void ExcludeRemotePort(int port);

    /// <summary>
    /// Ajoute dynamiquement une adresse IP ou notation CIDR distante à la liste d'exclusion au cours de l'exécution.
    /// </summary>
    /// <param name="ipOrCidr">Adresse IP ou notation CIDR à exclure.</param>
    void ExcludeRemoteIp(string ipOrCidr);
}
