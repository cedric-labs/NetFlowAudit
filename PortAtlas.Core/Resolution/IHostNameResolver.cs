using System.Net;

namespace PortAtlas.Core.Resolution;

/// <summary>
/// Service contract for asynchronous reverse DNS and organization resolution with memory caching and background worker queue.
/// </summary>
public interface IHostNameResolver
{
    /// <summary>
    /// Event raised when an asynchronous background resolution completes.
    /// </summary>
    event EventHandler<HostNameResolvedEventArgs>? HostNameResolved;

    /// <summary>
    /// Attempts to immediately retrieve the cached host name from memory without network latency.
    /// </summary>
    /// <param name="address">Target IP address to query.</param>
    /// <param name="hostName">Cached host name or empty string if known to lack a PTR record.</param>
    /// <returns>True if the address has already been evaluated in cache; otherwise false.</returns>
    bool TryGetCachedHostName(IPAddress address, out string hostName);

    /// <summary>
    /// Enqueues an IP address for non-blocking asynchronous background resolution.
    /// </summary>
    /// <param name="address">Target IP address to resolve.</param>
    void QueueResolution(IPAddress address);

    /// <summary>
    /// Resolves the host name associated with the specified IP address asynchronously.
    /// </summary>
    /// <param name="address">Target IP address to resolve.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Resolved host name, or empty string if resolution failed or timed out.</returns>
    Task<string> ResolveHostNameAsync(IPAddress address, CancellationToken cancellationToken = default);
}
