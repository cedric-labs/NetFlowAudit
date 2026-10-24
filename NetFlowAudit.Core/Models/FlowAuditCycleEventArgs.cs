namespace NetFlowAudit.Core.Models;

/// <summary>
/// Event arguments raised upon completion of a flow auditing scan cycle.
/// </summary>
public sealed class FlowAuditCycleEventArgs(FlowAuditCycleResult result) : EventArgs
{
    /// <summary>
    /// Audit scan cycle outcome and summary metrics.
    /// </summary>
    public FlowAuditCycleResult Result { get; } = result ?? throw new ArgumentNullException(nameof(result));
}
