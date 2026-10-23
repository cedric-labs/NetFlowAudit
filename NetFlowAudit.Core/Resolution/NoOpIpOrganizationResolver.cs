using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Implémentation inactive (No-Op) du résolveur d'organisation IP.
/// </summary>
public sealed class NoOpIpOrganizationResolver : IIpOrganizationResolver
{
    /// <inheritdoc />
    public Task<string?> ResolveOrganizationAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    public Task<IpOwnerMetadata?> ResolveOwnerDetailsAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IpOwnerMetadata?>(null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Aucun état ou ressource à libérer
    }
}
