using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// No-op implementation of the IP organization resolver returning null immediately.
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
        // No unmanaged resources to release
    }
}
