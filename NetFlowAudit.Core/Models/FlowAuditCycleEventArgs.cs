namespace NetFlowAudit.Core.Models;

/// <summary>
/// Événement émis lors de la complétion d'un cycle de balayage d'audit.
/// </summary>
public sealed class FlowAuditCycleEventArgs(FlowAuditCycleResult result) : EventArgs
{
    /// <summary>
    /// Résultat et métriques du cycle d'audit.
    /// </summary>
    public FlowAuditCycleResult Result { get; } = result ?? throw new ArgumentNullException(nameof(result));
}
