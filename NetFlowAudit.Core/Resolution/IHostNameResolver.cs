using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Contrat de service pour la résolution asynchrone des noms d'hôtes à partir d'adresses IP.
/// </summary>
public interface IHostNameResolver
{
    /// <summary>
    /// Résout le nom d'hôte associé à une adresse IP de manière asynchrone.
    /// </summary>
    /// <param name="address">Adresse IP à résoudre.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Le nom d'hôte résolu, ou une chaîne vide si la résolution échoue ou n'est pas applicable.</returns>
    Task<string> ResolveHostNameAsync(IPAddress address, CancellationToken cancellationToken = default);
}
