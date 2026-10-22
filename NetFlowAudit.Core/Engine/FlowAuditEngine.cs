using FileForge.IO;
using NetFlowAudit.Core.Deduplication;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Models;
using NetFlowAudit.Core.Providers;
using ProcessForge.Filtering;
using SocketForge.Contracts;

namespace NetFlowAudit.Core.Engine;

/// <summary>
/// Moteur orchestrant l'échantillonnage de SocketForge, le filtrage ProcessForge et la persistance FileForge.
/// </summary>
public sealed class FlowAuditEngine(
    ISocketScanner socketScanner,
    ProcessExclusionFilter exclusionFilter,
    ConnectionDeduplicationRegister deduplicationRegister,
    LocalServerHostProvider hostProvider,
    PersistentDelimitedFileWriter csvWriter,
    AuditCsvFormatter csvFormatter,
    TimeSpan scanInterval)
{
    private readonly ISocketScanner _socketScanner = socketScanner ?? throw new ArgumentNullException(nameof(socketScanner));
    private readonly ProcessExclusionFilter _exclusionFilter = exclusionFilter ?? throw new ArgumentNullException(nameof(exclusionFilter));
    private readonly ConnectionDeduplicationRegister _deduplicationRegister = deduplicationRegister ?? throw new ArgumentNullException(nameof(deduplicationRegister));
    private readonly LocalServerHostProvider _hostProvider = hostProvider ?? throw new ArgumentNullException(nameof(hostProvider));
    private readonly PersistentDelimitedFileWriter _csvWriter = csvWriter ?? throw new ArgumentNullException(nameof(csvWriter));
    private readonly AuditCsvFormatter _csvFormatter = csvFormatter ?? throw new ArgumentNullException(nameof(csvFormatter));
    private readonly TimeSpan _scanInterval = scanInterval;

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
        var now = DateTimeOffset.UtcNow;
        var newlyDiscovered = 0;

        for (var i = 0; i < activeSockets.Count; i++)
        {
            var socket = activeSockets[i];

            if (_exclusionFilter.ShouldExclude(socket.Process))
            {
                continue;
            }

            var entry = new NetworkAuditEntry(now, now, 1, host, socket);
            var isNew = _deduplicationRegister.TryRegisterOrUpdate(entry, out var registered);

            if (isNew)
            {
                var line = _csvFormatter.FormatEntry(registered);
                await _csvWriter.AppendLineAsync(line, cancellationToken).ConfigureAwait(false);
                newlyDiscovered++;
            }
        }

        return newlyDiscovered;
    }
}



