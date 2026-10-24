using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Service contract for resolving the organization or entity owning an IP address.
/// </summary>
public interface IIpOrganizationResolver : IDisposable
{
    /// <summary>
    /// Resolves the organization or entity name associated with the specified IP address.
    /// </summary>
    /// <param name="address">Target IP address to resolve.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Organization name, or null if not found.</returns>
    Task<string?> ResolveOrganizationAsync(IPAddress address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves detailed network owner metadata (organization, country, network range, handle) associated with an IP address.
    /// </summary>
    /// <param name="address">Target IP address to resolve.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Detailed owner metadata, or null if not found.</returns>
    Task<IpOwnerMetadata?> ResolveOwnerDetailsAsync(IPAddress address, CancellationToken cancellationToken = default);
}
