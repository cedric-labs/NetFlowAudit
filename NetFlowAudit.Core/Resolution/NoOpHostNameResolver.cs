using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Implémentation inactive du résolveur de nom d'hôte retournant systématiquement une chaîne vide.
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
        // Aucun traitement nécessaire
    }

    /// <inheritdoc />
    public Task<string> ResolveHostNameAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }
}
