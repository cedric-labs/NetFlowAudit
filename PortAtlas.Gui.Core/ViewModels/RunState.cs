namespace PortAtlas.Gui.Core.ViewModels;

/// <summary>
/// Execution state of a CLI run triggered from the GUI.
/// </summary>
public enum RunState
{
    /// <summary>No run has started yet.</summary>
    Idle,

    /// <summary>The process is currently running.</summary>
    Running,

    /// <summary>The process exited normally (regardless of exit code).</summary>
    Completed,

    /// <summary>The run was cancelled by the user.</summary>
    Cancelled,

    /// <summary>The process could not be started (for example, executable not found).</summary>
    Failed
}
