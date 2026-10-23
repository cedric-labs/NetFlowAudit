using FileForge.IO;
using NetFlowAudit.Core.Deduplication;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Filtering;
using NetFlowAudit.Core.Models;
using NetFlowAudit.Core.Providers;
using NetFlowAudit.Core.Resolution;
using ProcessForge.Filtering;
using SocketForge.Contracts;

namespace NetFlowAudit.Core.Engine;

/// <summary>
/// Moteur orchestrant l'échantillonnage de SocketForge, le filtrage ProcessForge, le filtrage de flux réseau, la résolution de noms d'hôtes et la persistance FileForge.
/// </summary>
public sealed class FlowAuditEngine
{
    private readonly ISocketScanner _socketScanner;
    private readonly ProcessExclusionFilter _exclusionFilter;
    private readonly INetworkFlowFilter _networkFlowFilter;
    private readonly ConnectionDeduplicationRegister _deduplicationRegister;
    private readonly LocalServerHostProvider _hostProvider;
    private readonly PersistentDelimitedFileWriter _csvWriter;
    private readonly AuditCsvFormatter _csvFormatter;
    private readonly IHostNameResolver _hostNameResolver;
    private readonly TimeSpan _scanInterval;

    /// <summary>
    /// Initialise une nouvelle instance complète du moteur d'audit.
    /// </summary>
    public FlowAuditEngine(
        ISocketScanner socketScanner,
        ProcessExclusionFilter exclusionFilter,
        INetworkFlowFilter networkFlowFilter,
        ConnectionDeduplicationRegister deduplicationRegister,
        LocalServerHostProvider hostProvider,
        PersistentDelimitedFileWriter csvWriter,
        AuditCsvFormatter csvFormatter,
        IHostNameResolver hostNameResolver,
        TimeSpan scanInterval)
    {
        _socketScanner = socketScanner ?? throw new ArgumentNullException(nameof(socketScanner));
        _exclusionFilter = exclusionFilter ?? throw new ArgumentNullException(nameof(exclusionFilter));
        _networkFlowFilter = networkFlowFilter ?? throw new ArgumentNullException(nameof(networkFlowFilter));
        _deduplicationRegister = deduplicationRegister ?? throw new ArgumentNullException(nameof(deduplicationRegister));
        _hostProvider = hostProvider ?? throw new ArgumentNullException(nameof(hostProvider));
        _csvWriter = csvWriter ?? throw new ArgumentNullException(nameof(csvWriter));
        _csvFormatter = csvFormatter ?? throw new ArgumentNullException(nameof(csvFormatter));
        _hostNameResolver = hostNameResolver ?? throw new ArgumentNullException(nameof(hostNameResolver));
        _scanInterval = scanInterval;

        _hostNameResolver.HostNameResolved += (_, e) =>
        {
            _deduplicationRegister.UpdateRemoteHostName(e.Address, e.HostName);
        };
    }

    /// <summary>
    /// Constructeur avec filtre de flux explicite et résolveur de nom d'hôte inactif par défaut.
    /// </summary>
    public FlowAuditEngine(
        ISocketScanner socketScanner,
        ProcessExclusionFilter exclusionFilter,
        INetworkFlowFilter networkFlowFilter,
        ConnectionDeduplicationRegister deduplicationRegister,
        LocalServerHostProvider hostProvider,
        PersistentDelimitedFileWriter csvWriter,
        AuditCsvFormatter csvFormatter,
        TimeSpan scanInterval)
        : this(
            socketScanner,
            exclusionFilter,
            networkFlowFilter,
            deduplicationRegister,
            hostProvider,
            csvWriter,
            csvFormatter,
            new NoOpHostNameResolver(),
            scanInterval)
    {
    }

    /// <summary>
    /// Constructeur de compatibilité sans filtre de flux explicite (utilise les options par défaut).
    /// </summary>
    public FlowAuditEngine(
        ISocketScanner socketScanner,
        ProcessExclusionFilter exclusionFilter,
        ConnectionDeduplicationRegister deduplicationRegister,
        LocalServerHostProvider hostProvider,
        PersistentDelimitedFileWriter csvWriter,
        AuditCsvFormatter csvFormatter,
        TimeSpan scanInterval)
        : this(
            socketScanner,
            exclusionFilter,
            new NetworkFlowFilter(new NetworkFlowFilterOptions()),
            deduplicationRegister,
            hostProvider,
            csvWriter,
            csvFormatter,
            new NoOpHostNameResolver(),
            scanInterval)
    {
    }

    /// <summary>
    /// Événement déclenché à la fin de chaque cycle de scan avec la synthèse métrique.
    /// </summary>
    public event EventHandler<FlowAuditCycleEventArgs>? ScanCycleCompleted;

    /// <summary>
    /// Exécute l'audit continu.
    /// </summary>
    public async Task RunContinuousAuditAsync(CancellationToken cancellationToken = default)
    {
        await _csvWriter.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var existingSignatures = await _csvWriter.ReadExistingLinesAsync(cancellationToken).ConfigureAwait(false);
        _deduplicationRegister.Hydrate(existingSignatures);

        using var timer = new PeriodicTimer(_scanInterval);

        await ExecuteScanCycleAsync(cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await ExecuteScanCycleAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Exécute un cycle de scan, filtrage et écriture.
    /// </summary>
    public async Task<int> ExecuteScanCycleAsync(CancellationToken cancellationToken = default)
    {
        var activeSockets = await _socketScanner.ScanSocketsAsync(cancellationToken).ConfigureAwait(false);
        var host = _hostProvider.GetCurrentHostMetadata();
        var localAddresses = _hostProvider.GetLocalIpAddresses();
        var now = DateTimeOffset.UtcNow;
        var newlyDiscovered = new List<NetworkAuditEntry>();
        var excludedCount = 0;

        for (var i = 0; i < activeSockets.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var socket = activeSockets[i];

            if (_exclusionFilter.ShouldExclude(socket.Process) || _networkFlowFilter.ShouldExclude(socket, localAddresses))
            {
                excludedCount++;
                continue;
            }

            var entry = new NetworkAuditEntry(now, now, 1, host, socket);

            if (_hostNameResolver.TryGetCachedHostName(socket.RemoteEndpoint.Address, out var cachedHost) && !string.IsNullOrWhiteSpace(cachedHost))
            {
                entry = entry with { RemoteHostName = cachedHost };
            }
            else
            {
                _hostNameResolver.QueueResolution(socket.RemoteEndpoint.Address);
            }

            var isNew = _deduplicationRegister.TryRegisterOrUpdate(entry, out var registered);

            if (isNew)
            {
                var line = _csvFormatter.FormatEntry(registered);
                await _csvWriter.AppendLineAsync(line, cancellationToken).ConfigureAwait(false);
                newlyDiscovered.Add(registered);
            }
        }

        var result = new FlowAuditCycleResult(
            TimestampUtc: now,
            TotalSocketsScanned: activeSockets.Count,
            ExcludedSocketsCount: excludedCount,
            NewlyDiscoveredCount: newlyDiscovered.Count,
            TotalUniqueFlowsTracked: _deduplicationRegister.UniqueCount,
            NewlyDiscoveredEntries: newlyDiscovered,
            ActiveSockets: activeSockets);

        ScanCycleCompleted?.Invoke(this, new FlowAuditCycleEventArgs(result));

        return newlyDiscovered.Count;
    }
}
