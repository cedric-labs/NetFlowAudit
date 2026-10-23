using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Résolveur DNS inverse asynchrone et non-bloquant avec mise en cache mémoire et exclusion de sous-réseaux.
/// </summary>
public sealed class CachedHostNameResolver(
    TimeSpan resolutionTimeout,
    DnsAddressExclusionEvaluator? exclusionEvaluator = null,
    PersistentDnsCacheStore? cacheStore = null) : IHostNameResolver, IDisposable, IAsyncDisposable
{
    private readonly TimeSpan _resolutionTimeout = resolutionTimeout;
    private readonly DnsAddressExclusionEvaluator _exclusionEvaluator = exclusionEvaluator ?? new DnsAddressExclusionEvaluator([]);
    private readonly PersistentDnsCacheStore? _cacheStore = cacheStore;
    private readonly ConcurrentDictionary<IPAddress, string> _cache = LoadCache(cacheStore);
    private readonly ConcurrentDictionary<IPAddress, byte> _queuedAddresses = new();
    private readonly Channel<IPAddress> _channel = Channel.CreateUnbounded<IPAddress>();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _concurrencySemaphore = new(10, 10);
    private Task? _backgroundWorker;
    private int _workerStarted;
    private int _pendingSaves;

    /// <inheritdoc />
    public event EventHandler<HostNameResolvedEventArgs>? HostNameResolved;

    /// <summary>
    /// Initialise une nouvelle instance avec le délai d'expiration par défaut (500ms).
    /// </summary>
    public CachedHostNameResolver() : this(TimeSpan.FromMilliseconds(500))
    {
    }

    private static ConcurrentDictionary<IPAddress, string> LoadCache(PersistentDnsCacheStore? cacheStore)
    {
        var cache = new ConcurrentDictionary<IPAddress, string>();
        if (cacheStore is not null)
        {
            var loaded = cacheStore.Load();
            foreach (var kvp in loaded)
            {
                if (IPAddress.TryParse(kvp.Key, out var address))
                {
                    cache.TryAdd(address, kvp.Value);
                }
            }
        }
        return cache;
    }

    private void EnsureWorkerStarted()
    {
        if (Interlocked.CompareExchange(ref _workerStarted, 1, 0) == 0)
        {
            _backgroundWorker = Task.Run(() => ProcessQueueAsync(_cts.Token));
        }
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
            EnsureWorkerStarted();
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
                            var resolved = await ResolveAndCacheAsync(address, cancellationToken).ConfigureAwait(false);
                            _queuedAddresses.TryRemove(address, out _);

                            if (!string.IsNullOrWhiteSpace(resolved))
                            {
                                HostNameResolved?.Invoke(this, new HostNameResolvedEventArgs(address, resolved));
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
        _channel.Writer.TryComplete();
        _cts.Cancel();
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _cts.CancelAsync().ConfigureAwait(false);

        if (_backgroundWorker is not null)
        {
            try
            {
                await _backgroundWorker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Toléré lors de l'arrêt
            }
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

            await _cacheStore.SaveAsync(snapshot).ConfigureAwait(false);
        }
    }
}
