namespace PortAtlas.Gui.Core.Execution;

/// <summary>
/// Stream of the child process a line of output was read from.
/// </summary>
public enum RunOutputStream
{
    /// <summary>Standard output.</summary>
    Standard,

    /// <summary>Standard error.</summary>
    Error
}
