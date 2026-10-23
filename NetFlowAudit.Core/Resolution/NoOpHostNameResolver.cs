using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Implémentation inactive du résolveur de nom d'hôte retournant systématiquement une chaîne vide.
/// </summary>
public sealed class NoOpHostNameResolver : IHostNameResolver
{
    /// <inheritdoc />
    public Task<string> ResolveHostNameAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }
}
