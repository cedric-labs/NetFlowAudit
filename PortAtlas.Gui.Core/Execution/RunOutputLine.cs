namespace PortAtlas.Gui.Core.Execution;

/// <summary>
/// One line written by the running command.
/// </summary>
/// <param name="Stream">Stream the line was read from.</param>
/// <param name="Text">Line without its terminator.</param>
public sealed record RunOutputLine(RunOutputStream Stream, string Text);
