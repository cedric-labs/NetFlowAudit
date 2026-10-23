using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Implémentation inactive (sans opération) du résolveur d'organisation IP, utilisée en environnement isolé ou hors-ligne.
/// </summary>
public sealed class NoOpIpOrganizationResolver : IIpOrganizationResolver
{
    /// <inheritdoc />
    public Task<string?> ResolveOrganizationAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Aucun état ou ressource non gérée à libérer
    }
}
