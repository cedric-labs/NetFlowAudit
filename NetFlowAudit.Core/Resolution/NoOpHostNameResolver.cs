using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// No-op implementation of the host name resolver returning empty strings immediately.
/// </summary>
public sealed class NoOpHostNameResolver : IHostNameResolver
{
    /// <inheritdoc />
    public event EventHandler<HostNameResolvedEventArgs>? HostNameResolved
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public bool TryGetCachedHostName(IPAddress address, out string hostName)
    {
        hostName = string.Empty;
        return true;
    }

    /// <inheritdoc />
    public void QueueResolution(IPAddress address)
    {
        // No action required
    }

    /// <inheritdoc />
    public Task<string> ResolveHostNameAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }
}
