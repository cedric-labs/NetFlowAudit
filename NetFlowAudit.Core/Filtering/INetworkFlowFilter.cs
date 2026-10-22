using System.Net;
using SocketForge.Models;

namespace NetFlowAudit.Core.Filtering;

/// <summary>
/// Contrat de filtrage des connexions réseau (ports et boucle locale).
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
}
