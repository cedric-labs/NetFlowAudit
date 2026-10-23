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
public sealed class FlowAuditEngine(
    ISocketScanner socketScanner,
    ProcessExclusionFilter exclusionFilter,
    INetworkFlowFilter networkFlowFilter,
    ConnectionDeduplicationRegister deduplicationRegister,
    IServerHostProvider hostProvider,
    PersistentDelimitedFileWriter csvWriter,
    AuditCsvFormatter csvFormatter,
    IHostNameResolver hostNameResolver,
    TimeSpan scanInterval)
{
    private readonly IHostNameResolver _hostNameResolver = InitializeHostNameResolver(
        socketScanner,
        exclusionFilter,
        networkFlowFilter,
        deduplicationRegister,
        hostProvider,
        csvWriter,
        csvFormatter,
        hostNameResolver);

    /// <summary>
    /// Constructeur avec filtre de flux explicite et résolveur de nom d'hôte inactif par défaut.
    /// </summary>
    public FlowAuditEngine(
        ISocketScanner socketScanner,
        ProcessExclusionFilter exclusionFilter,
        INetworkFlowFilter networkFlowFilter,
        ConnectionDeduplicationRegister deduplicationRegister,
        IServerHostProvider hostProvider,
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
        IServerHostProvider hostProvider,
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
        await csvWriter.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var existingSignatures = await csvWriter.ReadExistingLinesAsync(cancellationToken).ConfigureAwait(false);
        deduplicationRegister.Hydrate(existingSignatures);

        using var timer = new PeriodicTimer(scanInterval);

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
        var activeSockets = await socketScanner.ScanSocketsAsync(cancellationToken).ConfigureAwait(false);
        var host = hostProvider.GetCurrentHostMetadata();
        var localAddresses = hostProvider.GetLocalIpAddresses();
        var now = DateTimeOffset.UtcNow;
        var newlyDiscovered = new List<NetworkAuditEntry>();
        var excludedCount = 0;

        for (var i = 0; i < activeSockets.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var socket = activeSockets[i];

            if (exclusionFilter.ShouldExclude(socket.Process) || networkFlowFilter.ShouldExclude(socket, localAddresses))
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

            var isNew = deduplicationRegister.TryRegisterOrUpdate(entry, out var registered);

            if (isNew)
            {
                var line = csvFormatter.FormatEntry(registered);
                await csvWriter.AppendLineAsync(line, cancellationToken).ConfigureAwait(false);
                newlyDiscovered.Add(registered);
            }
        }

        var result = new FlowAuditCycleResult(
            TimestampUtc: now,
            TotalSocketsScanned: activeSockets.Count,
            ExcludedSocketsCount: excludedCount,
            NewlyDiscoveredCount: newlyDiscovered.Count,
            TotalUniqueFlowsTracked: deduplicationRegister.UniqueCount,
            NewlyDiscoveredEntries: newlyDiscovered,
            ActiveSockets: activeSockets);

        ScanCycleCompleted?.Invoke(this, new FlowAuditCycleEventArgs(result));

        return newlyDiscovered.Count;
    }

    private static IHostNameResolver InitializeHostNameResolver(
        ISocketScanner socketScanner,
        ProcessExclusionFilter exclusionFilter,
        INetworkFlowFilter networkFlowFilter,
        ConnectionDeduplicationRegister deduplicationRegister,
        IServerHostProvider hostProvider,
        PersistentDelimitedFileWriter csvWriter,
        AuditCsvFormatter csvFormatter,
        IHostNameResolver hostNameResolver)
    {
        ArgumentNullException.ThrowIfNull(socketScanner);
        ArgumentNullException.ThrowIfNull(exclusionFilter);
        ArgumentNullException.ThrowIfNull(networkFlowFilter);
        ArgumentNullException.ThrowIfNull(deduplicationRegister);
        ArgumentNullException.ThrowIfNull(hostProvider);
        ArgumentNullException.ThrowIfNull(csvWriter);
        ArgumentNullException.ThrowIfNull(csvFormatter);
        ArgumentNullException.ThrowIfNull(hostNameResolver);

        hostNameResolver.HostNameResolved += (_, e) =>
        {
            deduplicationRegister.UpdateRemoteHostName(e.Address, e.HostName);
        };

        return hostNameResolver;
    }
}
