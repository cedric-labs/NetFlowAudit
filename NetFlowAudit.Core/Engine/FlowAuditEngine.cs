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
/// Orchestrates SocketForge socket sampling, ProcessForge filtering, flow filtering, DNS resolution, and FileForge persistent storage.
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
    /// Overload initializing the engine with explicit flow filter and default no-op host name resolver.
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
    /// Compatibility overload without explicit flow filter (uses default flow filter options).
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
    /// Event raised at the end of each scan cycle with metric and discovery summary.
    /// </summary>
    public event EventHandler<FlowAuditCycleEventArgs>? ScanCycleCompleted;

    /// <summary>
    /// Executes the continuous audit loop until cancellation is requested.
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
    /// Executes a single scan, filter, deduplication, and file append cycle.
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
