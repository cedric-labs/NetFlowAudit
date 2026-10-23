using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Contrat d'abstraction pour la résolution de l'organisation propriétaire ou du titulaire d'une adresse IP publique.
/// </summary>
public interface IIpOrganizationResolver : IDisposable
{
    /// <summary>
    /// Tente d'identifier le nom de l'organisation ou entité propriétaire d'une adresse IP.
    /// </summary>
    /// <param name="address">Adresse IP à rechercher.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Le nom de l'organisation propriétaire, ou null si introuvable, privé ou en échec réseau.</returns>
    Task<string?> ResolveOrganizationAsync(IPAddress address, CancellationToken cancellationToken = default);
}
