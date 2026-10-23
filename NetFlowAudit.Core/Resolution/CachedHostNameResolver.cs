using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Résolveur DNS inverse avec mise en cache mémoire et gestion de délai d'expiration pour ne pas bloquer l'audit.
/// </summary>
/// <param name="resolutionTimeout">Délai maximal alloué à chaque requête de résolution DNS inverse.</param>
public sealed class CachedHostNameResolver(TimeSpan resolutionTimeout) : IHostNameResolver
{
    private readonly TimeSpan _resolutionTimeout = resolutionTimeout;
    private readonly ConcurrentDictionary<IPAddress, string> _cache = new();

    /// <summary>
    /// Initialise une nouvelle instance avec un délai d'expiration par défaut de 1 seconde.
    /// </summary>
    public CachedHostNameResolver() : this(TimeSpan.FromSeconds(1))
    {
    }

    /// <inheritdoc />
    public async Task<string> ResolveHostNameAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None))
        {
            return string.Empty;
        }

        if (IPAddress.IsLoopback(address))
        {
            return "localhost";
        }

        if (_cache.TryGetValue(address, out var cached))
        {
            return cached;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_resolutionTimeout);

            var entry = await Dns.GetHostEntryAsync(address.ToString(), timeoutCts.Token).ConfigureAwait(false);
            var hostName = string.IsNullOrWhiteSpace(entry.HostName) ? string.Empty : entry.HostName;

            _cache[address] = hostName;
            return hostName;
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException)
        {
            // Toléré : L'adresse distante ne dispose pas d'enregistrement DNS PTR inverse,
            // le serveur DNS est inaccessible ou la requête a dépassé le délai imparti.
            // On enregistre une chaîne vide dans le cache pour ne pas répéter la requête inutilement (cache négatif).
            _cache[address] = string.Empty;
            return string.Empty;
        }
    }
}
