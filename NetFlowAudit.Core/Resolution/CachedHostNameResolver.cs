using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Asynchronous non-blocking reverse DNS resolver with memory caching, subnet exclusion, and RDAP organization fallback.
/// </summary>
public sealed class CachedHostNameResolver(
    TimeSpan resolutionTimeout,
    DnsAddressExclusionEvaluator? exclusionEvaluator = null,
    PersistentDnsCacheStore? cacheStore = null,
    IIpOrganizationResolver? organizationResolver = null,
    IReadOnlyDictionary<string, string>? customHostAliases = null) : IHostNameResolver, IDisposable, IAsyncDisposable
{
    private readonly TimeSpan _resolutionTimeout = resolutionTimeout;
    private readonly DnsAddressExclusionEvaluator _exclusionEvaluator = exclusionEvaluator ?? new DnsAddressExclusionEvaluator([]);
    private readonly PersistentDnsCacheStore? _cacheStore = cacheStore;
    private readonly IIpOrganizationResolver? _organizationResolver = organizationResolver;
    private readonly ConcurrentDictionary<IPAddress, DnsCacheEntry> _cache = LoadCache(cacheStore, customHostAliases);
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
    /// Initializes a new instance with the default resolution timeout (500ms).
    /// </summary>
    public CachedHostNameResolver() : this(TimeSpan.FromMilliseconds(500))
    {
    }

    private static ConcurrentDictionary<IPAddress, DnsCacheEntry> LoadCache(PersistentDnsCacheStore? cacheStore, IReadOnlyDictionary<string, string>? customHostAliases)
    {
        var cache = new ConcurrentDictionary<IPAddress, DnsCacheEntry>();
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
        if (customHostAliases is not null)
        {
            foreach (var (ipStr, hostName) in customHostAliases)
            {
                if (IPAddress.TryParse(ipStr, out var address) && !string.IsNullOrWhiteSpace(hostName))
                {
                    cache[address] = new DnsCacheEntry
                    {
                        HostName = hostName.Trim(),
                        Source = "Custom",
                        ResolvedAtUtc = DateTimeOffset.UtcNow
                    };
                }
            }
        }

        return cache;
    }

    private void EnsureWorkerStarted()
    {
        if (Interlocked.CompareExchange(ref _workerStarted, 1, 0) == 0)
        {
            _backgroundWorker = Task.Run(() => ProcessQueueAsync(_cts.Token), _cts.Token);
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
            hostName = cached.HostName;
            return true;
        }

        hostName = string.Empty;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the full enriched cache entry for the specified IP address.
    /// </summary>
    /// <param name="address">Target IP address to query.</param>
    /// <param name="entry">Enriched cache entry or null if absent.</param>
    /// <returns>True if present in cache; otherwise false.</returns>
    public bool TryGetCachedEntry(IPAddress address, out DnsCacheEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (_cache.TryGetValue(address, out var cached))
        {
            entry = cached;
            return true;
        }

        if (IsFastTrackAddress(address, out var fastHost))
        {
            var source = IPAddress.IsLoopback(address) ? "Local" : "Excluded";
            entry = new DnsCacheEntry { HostName = fastHost, Source = source };
            return true;
        }

        entry = null;
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

        EnsureWorkerStarted();

        if (_queuedAddresses.TryAdd(address, 0))
        {
            if (!_channel.Writer.TryWrite(address))
            {
                _queuedAddresses.TryRemove(address, out _);
            }
        }
    }

    /// <inheritdoc />
    public async Task<string> ResolveHostNameAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (IsFastTrackAddress(address, out var fastHostName))
        {
            return fastHostName;
        }

        if (_cache.TryGetValue(address, out var cachedHostName))
        {
            return cachedHostName.HostName;
        }

        await _concurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(address, out var doubleCheck))
            {
                return doubleCheck.HostName;
            }

            return await ResolveAndCacheAsync(address, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    private bool IsFastTrackAddress(IPAddress address, out string hostName)
    {
        if (_cache.TryGetValue(address, out var customEntry) && string.Equals(customEntry.Source, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            hostName = customEntry.HostName;
            return true;
        }

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
            _cache.TryAdd(address, new DnsCacheEntry
            {
                HostName = string.Empty,
                Source = "Excluded",
                ResolvedAtUtc = DateTimeOffset.UtcNow
            });
            return true;
        }

        hostName = string.Empty;
        return false;
    }

    private async Task<string> ResolveAndCacheAsync(IPAddress address, CancellationToken cancellationToken)
    {
        string? dnsHostName = null;

        // 1. Attempt reverse DNS (PTR) lookup
        try
        {
            using var dnsTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            dnsTimeoutCts.CancelAfter(_resolutionTimeout);

            var entry = await Dns.GetHostEntryAsync(address.ToString(), dnsTimeoutCts.Token).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(entry.HostName))
            {
                dnsHostName = entry.HostName;
            }
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException)
        {
            // Tolerated: Remote address lacks a reverse DNS PTR record, or local DNS server is offline/unreachable
        }

        IpOwnerMetadata? ownerDetails = null;

        // 2. Fallback: If no PTR hostname was found and an organization resolver is available
        if (string.IsNullOrWhiteSpace(dnsHostName) && _organizationResolver is not null)
        {
            try
            {
                ownerDetails = await _organizationResolver.ResolveOwnerDetailsAsync(address, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or SocketException or TimeoutException or OperationCanceledException)
            {
                // Tolerated: Network-isolated environment, RDAP server unreachable, or cancellation requested
            }
        }

        string hostName;
        string source;

        if (!string.IsNullOrWhiteSpace(dnsHostName))
        {
            hostName = dnsHostName;
            source = "DNS";
        }
        else if (!string.IsNullOrWhiteSpace(ownerDetails?.Organization))
        {
            hostName = ownerDetails.Organization;
            source = "RDAP";
        }
        else
        {
            hostName = string.Empty;
            source = "None";
        }

        var cacheEntry = new DnsCacheEntry
        {
            HostName = hostName,
            Organization = ownerDetails?.Organization,
            Country = ownerDetails?.Country,
            NetworkRange = ownerDetails?.NetworkRange,
            Source = source,
            ResolvedAtUtc = DateTimeOffset.UtcNow
        };

        _cache[address] = cacheEntry;
        ScheduleCacheSave();
        return hostName;
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
            // Cooperative queue termination
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
                await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
                Interlocked.Exchange(ref _pendingSaves, 0);

                var snapshot = new Dictionary<string, DnsCacheEntry>(_cache.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in _cache)
                {
                    snapshot[kvp.Key.ToString()] = kvp.Value;
                }

                await _cacheStore.SaveAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
            }, CancellationToken.None);
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
            var snapshot = new Dictionary<string, DnsCacheEntry>(_cache.Count, StringComparer.OrdinalIgnoreCase);
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
                // Tolerated on service shutdown
            }
        }

        _cts.Dispose();
        _concurrencySemaphore.Dispose();

        if (_cacheStore is not null)
        {
            var snapshot = new Dictionary<string, DnsCacheEntry>(_cache.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _cache)
            {
                snapshot[kvp.Key.ToString()] = kvp.Value;
            }

            await _cacheStore.SaveAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
