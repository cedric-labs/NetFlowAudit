namespace NetFlowAudit.Cli.Dashboard;

/// <summary>
/// Selectable interactive columns for inline flow exclusion in the TUI dashboard.
/// </summary>
public enum InteractiveExclusionColumn
{
    /// <summary>
    /// Local listening or communicating port.
    /// </summary>
    Local = 0,

    /// <summary>
    /// Remote communication IP address or port.
    /// </summary>
    Remote = 1,

    /// <summary>
    /// Exact owning process name.
    /// </summary>
    Process = 2,

    /// <summary>
    /// Application target executable or payload command line.
    /// </summary>
    Target = 3
}
