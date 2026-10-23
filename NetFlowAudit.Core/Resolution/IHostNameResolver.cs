using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Contrat de service pour la résolution asynchrone des noms d'hôtes avec cache et file d'arrière-plan.
/// </summary>
public interface IHostNameResolver
{
    /// <summary>
    /// Événement déclenché lorsqu'une résolution asynchrone d'arrière-plan est finalisée.
    /// </summary>
    event EventHandler<HostNameResolvedEventArgs>? HostNameResolved;

    /// <summary>
    /// Tente d'obtenir immédiatement le nom d'hôte depuis le cache mémoire sans latence ni requête réseau.
    /// </summary>
    /// <param name="address">Adresse IP à rechercher.</param>
    /// <param name="hostName">Nom d'hôte mis en cache ou chaîne vide si aucun enregistrement PTR.</param>
    /// <returns>True si l'adresse a déjà été évaluée/mise en cache, sinon False.</returns>
    bool TryGetCachedHostName(IPAddress address, out string hostName);

    /// <summary>
    /// Enfile une adresse IP pour résolution asynchrone en arrière-plan sans bloquer l'appelant.
    /// </summary>
    /// <param name="address">Adresse IP à résoudre.</param>
    void QueueResolution(IPAddress address);

    /// <summary>
    /// Résout le nom d'hôte associé à une adresse IP de manière asynchrone.
    /// </summary>
    /// <param name="address">Adresse IP à résoudre.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Le nom d'hôte résolu, ou une chaîne vide si la résolution échoue ou n'est pas applicable.</returns>
    Task<string> ResolveHostNameAsync(IPAddress address, CancellationToken cancellationToken = default);
}
