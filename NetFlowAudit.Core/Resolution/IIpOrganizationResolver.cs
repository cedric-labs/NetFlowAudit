using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Contrat de résolution de l'organisation ou entité détentrice d'une adresse IP.
/// </summary>
public interface IIpOrganizationResolver : IDisposable
{
    /// <summary>
    /// Résout le nom d'organisation ou entité associée à une adresse IP.
    /// </summary>
    /// <param name="address">Adresse IP à résoudre.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Nom d'organisation ou null si introuvable.</returns>
    Task<string?> ResolveOrganizationAsync(IPAddress address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Résout les métadonnées réseau détaillées (organisation, pays, plage réseau, handle) associées à une adresse IP.
    /// </summary>
    /// <param name="address">Adresse IP à résoudre.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Métadonnées détaillées ou null si introuvable.</returns>
    Task<IpOwnerMetadata?> ResolveOwnerDetailsAsync(IPAddress address, CancellationToken cancellationToken = default);
}
