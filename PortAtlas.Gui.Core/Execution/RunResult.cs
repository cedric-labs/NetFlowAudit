namespace PortAtlas.Gui.Core.Execution;

/// <summary>
/// Outcome of a command that ran to its end.
/// </summary>
/// <param name="ExitCode">Exit code of the process; the CLI returns 1 for a failed command and for a parse error.</param>
public sealed record RunResult(int ExitCode);
