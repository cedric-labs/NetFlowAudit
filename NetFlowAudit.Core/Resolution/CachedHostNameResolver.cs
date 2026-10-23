using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Résolveur DNS inverse haute performance s'appuyant sur un cache mémoire, un fichier de persistance dédié,
/// l'exclusion de plages réseaux et une file de résolution asynchrone non-bloquante en tâche de fond.
/// </summary>
public sealed class CachedHostNameResolver : IHostNameResolver, IDisposable, IAsyncDisposable
{
    private readonly TimeSpan _resolutionTimeout;
    private readonly DnsAddressExclusionEvaluator _exclusionEvaluator;
    private readonly PersistentDnsCacheStore? _cacheStore;
    private readonly ConcurrentDictionary<IPAddress, string> _cache = new();
    private readonly ConcurrentDictionary<IPAddress, byte> _queuedAddresses = new();
    private readonly Channel<IPAddress> _channel = Channel.CreateUnbounded<IPAddress>();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _backgroundWorker;
    private readonly SemaphoreSlim _concurrencySemaphore = new(10, 10);
    private int _pendingSaves;

    /// <inheritdoc />
    public event EventHandler<HostNameResolvedEventArgs>? HostNameResolved;

    /// <summary>
    /// Initialise une nouvelle instance avec configuration complète.
    /// </summary>
    /// <param name="resolutionTimeout">Délai d'expiration de chaque requête DNS (par défaut 500ms).</param>
    /// <param name="exclusionEvaluator">Évaluateur optionnel des plages réseaux exclues de la résolution DNS.</param>
    /// <param name="cacheStore">Gestionnaire optionnel de persistance sur disque du cache DNS.</param>
    public CachedHostNameResolver(
        TimeSpan resolutionTimeout,
        DnsAddressExclusionEvaluator? exclusionEvaluator = null,
        PersistentDnsCacheStore? cacheStore = null)
    {
        _resolutionTimeout = resolutionTimeout;
        _exclusionEvaluator = exclusionEvaluator ?? new DnsAddressExclusionEvaluator([]);
        _cacheStore = cacheStore;

        if (_cacheStore is not null)
        {
            var loaded = _cacheStore.Load();
            foreach (var kvp in loaded)
            {
                if (IPAddress.TryParse(kvp.Key, out var address))
                {
                    _cache.TryAdd(address, kvp.Value);
                }
            }
        }

        _backgroundWorker = Task.Run(() => ProcessQueueAsync(_cts.Token));
    }

    /// <summary>
    /// Initialise une nouvelle instance avec un délai d'expiration par défaut de 500 ms.
    /// </summary>
    public CachedHostNameResolver() : this(TimeSpan.FromMilliseconds(500))
    {
    }

    /// <inheritdoc />
    public bool TryGetCachedHostName(IPAddress address, out string hostName)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (IsFastTrackAddress(address, out hostName))
        {
            return true;
        }

        if (_cache.TryGetValue(address, out var cached))
        {
            hostName = cached;
            return true;
        }

        hostName = string.Empty;
        return false;
    }

    /// <inheritdoc />
    public void QueueResolution(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (IsFastTrackAddress(address, out _))
        {
            return;
        }

        if (_cache.ContainsKey(address))
        {
            return;
        }

        if (_queuedAddresses.TryAdd(address, 0))
        {
            _channel.Writer.TryWrite(address);
        }
    }

    /// <inheritdoc />
    public async Task<string> ResolveHostNameAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (IsFastTrackAddress(address, out var fastHost))
        {
            return fastHost;
        }

        if (_cache.TryGetValue(address, out var cached))
        {
            return cached;
        }

        return await ResolveAndCacheAsync(address, cancellationToken).ConfigureAwait(false);
    }

    private bool IsFastTrackAddress(IPAddress address, out string hostName)
    {
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None))
        {
            hostName = string.Empty;
            return true;
        }

        if (IPAddress.IsLoopback(address))
        {
            hostName = "localhost";
            return true;
        }

        if (_exclusionEvaluator.ShouldExclude(address))
        {
            hostName = string.Empty;
            _cache.TryAdd(address, string.Empty);
            return true;
        }

        hostName = string.Empty;
        return false;
    }

    private async Task<string> ResolveAndCacheAsync(IPAddress address, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_resolutionTimeout);

            var entry = await Dns.GetHostEntryAsync(address.ToString(), timeoutCts.Token).ConfigureAwait(false);
            var hostName = string.IsNullOrWhiteSpace(entry.HostName) ? string.Empty : entry.HostName;

            _cache[address] = hostName;
            ScheduleCacheSave();
            return hostName;
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException)
        {
            // Toléré : L'adresse distante ne dispose pas d'enregistrement DNS PTR inverse,
            // le serveur DNS est inaccessible ou la requête a dépassé le délai imparti.
            // On enregistre une chaîne vide dans le cache pour ne pas répéter la requête inutilement (cache négatif).
            _cache[address] = string.Empty;
            ScheduleCacheSave();
            return string.Empty;
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (_channel.Reader.TryRead(out var address))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (_cache.ContainsKey(address))
                    {
                        continue;
                    }

                    await _concurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var host = await ResolveAndCacheAsync(address, cancellationToken).ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(host))
                            {
                                HostNameResolved?.Invoke(this, new HostNameResolvedEventArgs(address, host));
                            }
                        }
                        finally
                        {
                            _concurrencySemaphore.Release();
                        }
                    }, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Fin coopérative de la file
        }
    }

    private void ScheduleCacheSave()
    {
        if (_cacheStore is null)
        {
            return;
        }

        if (Interlocked.Increment(ref _pendingSaves) == 1)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(false);
                Interlocked.Exchange(ref _pendingSaves, 0);

                var snapshot = new Dictionary<string, string>(_cache.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in _cache)
                {
                    snapshot[kvp.Key.ToString()] = kvp.Value;
                }

                await _cacheStore.SaveAsync(snapshot).ConfigureAwait(false);
            });
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _backgroundWorker.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Toléré lors de l'arrêt
        }

        _cts.Dispose();
        _concurrencySemaphore.Dispose();

        if (_cacheStore is not null)
        {
            var snapshot = new Dictionary<string, string>(_cache.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _cache)
            {
                snapshot[kvp.Key.ToString()] = kvp.Value;
            }

            _cacheStore.Save(snapshot);
        }
    }
}
