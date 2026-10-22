using NetFlowAudit.Core.Contracts;
using NetFlowAudit.Core.Deduplication;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Filtering;

namespace NetFlowAudit.Core.Engine;

/// <summary>
/// Moteur orchestrateur d'audit de flux réseau périodique.
/// </summary>
public sealed class FlowAuditEngine(
    INetworkSocketScanner socketScanner,
    ProcessExclusionFilter exclusionFilter,
    ConnectionDeduplicationRegister deduplicationRegister,
    PersistentCsvAuditWriter csvWriter,
    TimeSpan scanInterval)
{
    private readonly INetworkSocketScanner _socketScanner = socketScanner ?? throw new ArgumentNullException(nameof(socketScanner));
    private readonly ProcessExclusionFilter _exclusionFilter = exclusionFilter ?? throw new ArgumentNullException(nameof(exclusionFilter));
    private readonly ConnectionDeduplicationRegister _deduplicationRegister = deduplicationRegister ?? throw new ArgumentNullException(nameof(deduplicationRegister));
    private readonly PersistentCsvAuditWriter _csvWriter = csvWriter ?? throw new ArgumentNullException(nameof(csvWriter));
    private readonly TimeSpan _scanInterval = scanInterval;

    /// <summary>
    /// Démarre la boucle continue d'audit de flux réseau.
    /// </summary>
    public async Task RunContinuousAuditAsync(CancellationToken cancellationToken = default)
    {
        await _csvWriter.InitializeAsync(cancellationToken).ConfigureAwait(false);

        // Hydratation du registre au démarrage pour éviter de réécrire les flux déjà connus
        var existingSignatures = await _csvWriter.LoadExistingSignaturesAsync(cancellationToken).ConfigureAwait(false);
        _deduplicationRegister.Hydrate(existingSignatures);

        using var timer = new PeriodicTimer(_scanInterval);

        // Premier cycle immédiat
        await ExecuteScanCycleAsync(cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await ExecuteScanCycleAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Exécute un cycle unique d'échantillonnage, de filtrage et de dédoublonnage.
    /// </summary>
    public async Task<int> ExecuteScanCycleAsync(CancellationToken cancellationToken = default)
    {
        var activeConnections = await _socketScanner.ScanActiveConnectionsAsync(cancellationToken).ConfigureAwait(false);
        var newlyDiscoveredCount = 0;

        for (var i = 0; i < activeConnections.Count; i++)
        {
            var connection = activeConnections[i];

            if (_exclusionFilter.ShouldExclude(connection.Process))
            {
                continue;
            }

            var isNew = _deduplicationRegister.TryRegisterOrUpdate(connection, out var registered);
            if (isNew)
            {
                await _csvWriter.AppendRecordAsync(registered, cancellationToken).ConfigureAwait(false);
                newlyDiscoveredCount++;
            }
        }

        return newlyDiscoveredCount;
    }
}
